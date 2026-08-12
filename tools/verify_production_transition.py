#!/usr/bin/env python3
"""Read-only verifier for the one-time Demo -> Production transition.

The verifier has two deliberately separate modes:

* ``prepare`` audits the living, rule-and-plan-oriented source documents and
  computes deterministic in-memory partition hashes.
* ``cutover`` audits an explicitly named immutable freeze directory after the
  three Project owner events and both consumer receipts exist.

No mode writes files or invokes Git.  Without ``--project-owner-authorized``
the CLI exits successfully before inspecting any transition path.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import posixpath
import re
import sys
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path, PurePosixPath
from typing import Any, Dict, Iterable, List, Mapping, Optional, Sequence, Set, Tuple
from urllib.parse import unquote, urlsplit


MODES = ("prepare", "cutover")
SHA256_RE = re.compile(r"^[a-f0-9]{64}$")
GIT_REVISION_RE = re.compile(r"^[a-f0-9]{40}$")
FREEZE_ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
ALLOWED_COVERAGE_STATES = {"included", "excluded", "decision-blocked"}
NO_BLOCKING_DECISION_VALUES = {"none", "없음", "-", "n/a", "na"}
DISALLOWED_EXPORT_SEGMENTS = {
    "archive",
    "maintenance",
    "fixture",
    "fixtures",
    "evidence",
}

DESTINATION_PREFIXES = {
    "client": "somnia-client/docs/migration-input/dreamsquad-demo/",
    "game-server": "somnia-game-server/docs/migration-input/dreamsquad-demo/",
}
SHA256_SCHEMA = {"type": "string", "pattern": SHA256_RE.pattern}
GIT_REVISION_SCHEMA = {"type": "string", "pattern": GIT_REVISION_RE.pattern}
FREEZE_ID_SCHEMA = {"type": "string", "pattern": FREEZE_ID_RE.pattern}
DATE_TIME_SCHEMA = {"type": "string", "format": "date-time"}
RFC3339_DATE_TIME = re.compile(
    r"^\d{4}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\d|3[01])"
    r"[Tt](?:[01]\d|2[0-3]):[0-5]\d:[0-5]\d(?:\.\d+)?"
    r"(?:[Zz]|[+-](?:[01]\d|2[0-3]):[0-5]\d)$"
)
NONEMPTY_STRING_SCHEMA = {"type": "string", "minLength": 1}
MANIFEST_DESTINATION_PATTERNS = {
    consumer: f"^{prefix}[A-Za-z0-9][A-Za-z0-9._-]*/$"
    for consumer, prefix in DESTINATION_PREFIXES.items()
}
RECEIPT_DESTINATION_SCHEMA_PATTERN = (
    r"^somnia-(client|game-server)/docs/migration-input/dreamsquad-demo/"
    r"[A-Za-z0-9][A-Za-z0-9._-]*/$"
)

REQUIRED_LIVING_FILES = (
    "README.md",
    "AGENTS.md",
    "governance/transition-policy.md",
    "governance/one-time-transition-plan.md",
    "governance/decision-register.md",
    "governance/schemas/event.schema.json",
    "governance/schemas/manifest.schema.json",
    "governance/schemas/receipt.schema.json",
    "common/README.md",
    "common/rules/authority-identity-and-results.md",
    "common/rules/ordering-resync-and-versioning.md",
    "client/README.md",
    "client/rules/authority-and-projection.md",
    "client/rules/presentation-and-catalog.md",
    "client/plans/implementation-waves.md",
    "client/plans/acceptance-gates.md",
    "client/demo-experience-map.md",
    "game-server/README.md",
    "game-server/rules/authority-and-state.md",
    "game-server/rules/time-ordering-numeric-rng.md",
    "game-server/rules/content-result-and-replay.md",
    "game-server/plans/implementation-waves.md",
    "game-server/plans/acceptance-gates.md",
    "game-server/domain-coverage.md",
    "maintenance/change-register.md",
    "archive/legacy/README.md",
)

# This versioned catalog is the independent completeness contract for both
# prepare-time selection and an immutable freeze.  The target path differs only
# for the policy reference; every consumer-facing living document is otherwise
# exported at its source-relative path.
REQUIRED_EXPORT_ARTIFACTS = tuple(
    sorted(
        [
            (relative, PurePosixPath(relative).parts[0])
            for relative in REQUIRED_LIVING_FILES
            if PurePosixPath(relative).parts[0] in {"common", "client", "game-server"}
        ]
        + [("references/transition-policy.md", "reference")]
    )
)

FORBIDDEN_ACTIVE_PATHS = (
    "shared",
    "architecture",
    "product",
    "evidence",
    "migration-dossier",
    "demo-baseline.md",
    "source-map.md",
    "governance/registry.json",
    "governance/reviews.json",
    "governance/decisions.json",
    "governance/manifest.schema.json",
    "governance/export-charter.md",
    "governance/foundation-pilot",
    "client/cards",
    "client/fixtures",
    "game-server/cards",
    "game-server/fixtures",
)

SCHEMA_FILES = (
    "governance/schemas/event.schema.json",
    "governance/schemas/manifest.schema.json",
    "governance/schemas/receipt.schema.json",
)

EVENT_BASE_KEYS = {
    "schema_version",
    "event_id",
    "event_type",
    "acting_role",
    "project_owner",
    "approved_at",
    "approval_reference",
    "demo_revision",
    "demo_content_sha256",
}

EVENT_PHASE_KEYS = {
    "demo-approved": {"approved_scope"},
    "demo-frozen": {
        "predecessor_event_id",
        "freeze_id",
        "manifest_sha256",
        "common_sha256",
        "client_sha256",
        "game_server_sha256",
    },
    "transfer-completed": {
        "predecessor_event_id",
        "freeze_id",
        "client_receipt_sha256",
        "game_server_receipt_sha256",
        "destinations",
    },
}

MANIFEST_KEYS = {
    "schema_version",
    "freeze_id",
    "created_at",
    "demo_revision",
    "demo_content_sha256",
    "destinations",
    "bundle_hashes",
    "files",
}

RECEIPT_KEYS = {
    "schema_version",
    "consumer",
    "freeze_id",
    "destination",
    "manifest_sha256",
    "common_sha256",
    "assigned_bundle_sha256",
    "file_count",
    "byte_count",
    "received_at",
    "status",
    "verified_by",
}

MANIFEST_PATH_SCHEMA_PATTERN = (
    r"^(?!.*\/(?:[Aa][Rr][Cc][Hh][Ii][Vv][Ee]|"
    r"[Mm][Aa][Ii][Nn][Tt][Ee][Nn][Aa][Nn][Cc][Ee]|"
    r"[Ff][Ii][Xx][Tt][Uu][Rr][Ee][Ss]?|"
    r"[Ee][Vv][Ii][Dd][Ee][Nn][Cc][Ee])(?:/|$))"
    r"(common|client|game-server|references)/(?!.*//)"
    r"(?!\.{1,2}(?:/|$))(?!.*\/\.{1,2}(?:/|$))"
    r"[^\\\u0000-\u001F\u007F-\u009F]+\.md$"
)

RULE_FIELDS = (
    "책임 owner",
    "Invariant",
    "허용",
    "금지",
    "Semantic input/outcome",
    "Production 제약",
    "미결 decision",
    "Demo source pointer",
)

RULE_HEADING_RE = re.compile(r"^##\s+`(?P<id>PT-(?:COM|CLI|SRV)-[0-9]{3})`(?:\s+.*)?$")
RULE_FIELD_RE = re.compile(r"^\s*-\s+\*\*(?P<label>[^*]+?):\*\*\s*(?P<value>.*)$")

MARKDOWN_LINK_RE = re.compile(
    r"!?\[[^\]]*\]\(\s*(?P<target><[^>]+>|[^)\s]+)"
    r"(?:\s+(?:\"[^\"]*\"|'[^']*'|\([^)]*\)))?\s*\)"
)
MARKDOWN_REFERENCE_DEFINITION_RE = re.compile(
    r"(?m)^\s{0,3}\[[^\]]+\]:\s*(?P<target><[^>]+>|\S+)"
)
MARKDOWN_AUTOLINK_RE = re.compile(r"<(?P<target>(?:\.{1,2}/|/|[A-Za-z]:[/\\]|file:)[^>]+)>")


@dataclass(frozen=True, order=True)
class Diagnostic:
    code: str
    path: str
    message: str

    def format(self) -> str:
        location = self.path or "<global>"
        return f"[{self.code}] {location}: {self.message}"


@dataclass(frozen=True)
class Artifact:
    path: str
    audience: str
    sha256: str
    byte_count: int

    def canonical_dict(self) -> Dict[str, Any]:
        return {
            "audience": self.audience,
            "bytes": self.byte_count,
            "path": self.path,
            "sha256": self.sha256,
        }


@dataclass
class VerificationReport:
    mode: str
    diagnostics: List[Diagnostic] = field(default_factory=list)
    partitions: Dict[str, List[Artifact]] = field(default_factory=dict)
    partition_hashes: Dict[str, str] = field(default_factory=dict)
    manifest_sha256: Optional[str] = None

    @property
    def ok(self) -> bool:
        return not self.diagnostics

    def add(self, code: str, path: str, message: str) -> None:
        self.diagnostics.append(Diagnostic(code, path, message))

    def finish(self) -> "VerificationReport":
        self.diagnostics = sorted(set(self.diagnostics))
        for entries in self.partitions.values():
            entries.sort(key=lambda item: item.path)
        return self


@dataclass(frozen=True)
class ManifestData:
    freeze_id: str
    demo_revision: str
    demo_content_sha256: str
    created_at: Optional[datetime]
    destinations: Dict[str, str]
    bundle_hashes: Dict[str, str]
    entries: List[Artifact]


def _sha256(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def _canonical_json(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def partition_hash(entries: Sequence[Artifact]) -> str:
    """Hash a partition's sorted manifest-shaped inventory."""

    rows = [entry.canonical_dict() for entry in sorted(entries, key=lambda item: item.path)]
    return _sha256(_canonical_json(rows))


def _validate_required_export_inventory(
    entries: Iterable[Artifact],
    report: VerificationReport,
    display_path: str,
) -> None:
    expected = set(REQUIRED_EXPORT_ARTIFACTS)
    actual = {(entry.path, entry.audience) for entry in entries}
    for target, audience in sorted(expected - actual):
        report.add(
            "REQUIRED_EXPORT_INVENTORY",
            target,
            f"required {audience} artifact is missing from {display_path}",
        )
    for target, audience in sorted(actual - expected):
        report.add(
            "REQUIRED_EXPORT_INVENTORY",
            target,
            f"uncatalogued {audience} artifact is present in {display_path}",
        )


def _contains_unicode_cc(value: str) -> bool:
    """Return whether a path contains a Unicode Cc control character."""

    return any(
        ord(character) <= 0x1F or 0x7F <= ord(character) <= 0x9F
        for character in value
    )


def _relative(root: Path, path: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return str(path)


def _is_within(root: Path, candidate: Path) -> bool:
    try:
        candidate.relative_to(root)
        return True
    except ValueError:
        return False


def _has_symlink_component(root: Path, candidate: Path) -> bool:
    try:
        relative = candidate.relative_to(root)
    except ValueError:
        return True
    current = root
    for part in relative.parts:
        current = current / part
        if current.is_symlink():
            return True
    return False


def _read_bytes(path: Path, report: VerificationReport, code: str = "READ") -> Optional[bytes]:
    try:
        return path.read_bytes()
    except OSError as exc:
        report.add(code, str(path), f"cannot read file: {exc}")
        return None


def _read_text(path: Path, report: VerificationReport, code: str = "READ") -> Optional[str]:
    try:
        return path.read_text(encoding="utf-8-sig")
    except (OSError, UnicodeError) as exc:
        report.add(code, str(path), f"cannot read UTF-8 text: {exc}")
        return None


def _load_json_object(
    path: Path,
    report: VerificationReport,
    code: str = "JSON",
) -> Optional[Dict[str, Any]]:
    text = _read_text(path, report, code)
    if text is None:
        return None
    try:
        value = json.loads(text)
    except json.JSONDecodeError as exc:
        report.add(code, str(path), f"invalid JSON at line {exc.lineno}, column {exc.colno}: {exc.msg}")
        return None
    if not isinstance(value, dict):
        report.add(code, str(path), "top-level JSON value must be an object")
        return None
    return value


def _exact_keys(
    value: Mapping[str, Any],
    expected: Set[str],
    report: VerificationReport,
    path: str,
    label: str,
) -> None:
    actual = set(value)
    missing = sorted(expected - actual)
    extra = sorted(actual - expected)
    if missing:
        report.add("FIELDS", path, f"{label} is missing field(s): {', '.join(missing)}")
    if extra:
        report.add("FIELDS", path, f"{label} has unexpected field(s): {', '.join(extra)}")


def _nonempty_string(
    value: Any,
    report: VerificationReport,
    path: str,
    field_name: str,
) -> Optional[str]:
    if not isinstance(value, str) or not value:
        report.add("FIELD_TYPE", path, f"{field_name} must be a non-empty string")
        return None
    return value


def _pattern_string(
    value: Any,
    pattern: re.Pattern[str],
    report: VerificationReport,
    path: str,
    field_name: str,
) -> Optional[str]:
    parsed = _nonempty_string(value, report, path, field_name)
    if parsed is not None and not pattern.fullmatch(parsed):
        report.add("FIELD_VALUE", path, f"{field_name} has an invalid value: {parsed}")
        return None
    return parsed


def _timestamp(
    value: Any,
    report: VerificationReport,
    path: str,
    field_name: str,
) -> Optional[datetime]:
    parsed = _nonempty_string(value, report, path, field_name)
    if parsed is None:
        return None
    if not RFC3339_DATE_TIME.fullmatch(parsed):
        report.add("FIELD_VALUE", path, f"{field_name} must be an RFC 3339 date-time")
        return None
    candidate = parsed[:-1] + "+00:00" if parsed.endswith("Z") else parsed
    try:
        result = datetime.fromisoformat(candidate)
    except ValueError:
        report.add("FIELD_VALUE", path, f"{field_name} must be an RFC 3339 date-time")
        return None
    if result.tzinfo is None or result.utcoffset() is None:
        report.add("FIELD_VALUE", path, f"{field_name} must include a UTC offset")
        return None
    return result


def _walk_regular_files(
    base: Path,
    report: VerificationReport,
    root_for_paths: Path,
) -> List[Path]:
    files: List[Path] = []
    if not base.is_dir() or base.is_symlink():
        report.add("STRUCTURE", _relative(root_for_paths, base), "required directory is missing or is a symlink")
        return files

    for current, dirnames, filenames in os.walk(str(base), followlinks=False):
        current_path = Path(current)
        retained_dirs: List[str] = []
        for name in sorted(dirnames):
            child = current_path / name
            if child.is_symlink():
                report.add("SYMLINK", _relative(root_for_paths, child), "symlink directories are not allowed")
            else:
                relative_parts = {
                    part.lower()
                    for part in PurePosixPath(_relative(root_for_paths, child)).parts
                }
                forbidden = sorted(relative_parts & DISALLOWED_EXPORT_SEGMENTS)
                if forbidden:
                    report.add(
                        "FORBIDDEN_PATH",
                        _relative(root_for_paths, child),
                        f"official inventory directory uses forbidden segment(s): {', '.join(forbidden)}",
                    )
                retained_dirs.append(name)
        dirnames[:] = retained_dirs
        for name in sorted(filenames):
            child = current_path / name
            if child.is_symlink():
                report.add("SYMLINK", _relative(root_for_paths, child), "symlink files are not allowed")
            elif child.is_file():
                files.append(child)
            else:
                report.add("STRUCTURE", _relative(root_for_paths, child), "inventory entry is not a regular file")
    return sorted(files)


def _validate_required_structure(transition_root: Path, report: VerificationReport) -> None:
    for relative in REQUIRED_LIVING_FILES:
        path = transition_root / PurePosixPath(relative)
        if not path.is_file() or path.is_symlink():
            report.add("LIVING_STRUCTURE", relative, "required living file is missing or is a symlink")

    for relative in FORBIDDEN_ACTIVE_PATHS:
        path = transition_root / PurePosixPath(relative)
        if os.path.lexists(str(path)):
            report.add("LEGACY_ACTIVE", relative, "legacy material must live only under archive/legacy")


def _schema_object_contract(
    value: Any,
    required: Set[str],
    report: VerificationReport,
    path: str,
    label: str,
    extra_keywords: Optional[Set[str]] = None,
) -> Optional[Dict[str, Any]]:
    if not isinstance(value, dict):
        report.add("SCHEMA_CONTRACT", path, f"{label} must be a schema object")
        return None
    _schema_exact_keywords(
        value,
        {"type", "additionalProperties", "required", "properties"}
        | (extra_keywords or set()),
        report,
        path,
        label,
    )
    if value.get("type") != "object":
        report.add("SCHEMA_CONTRACT", path, f"{label} must declare type object")
    if value.get("additionalProperties") is not False:
        report.add("SCHEMA_CONTRACT", path, f"{label} must set additionalProperties to false")

    raw_required = value.get("required")
    if (
        not isinstance(raw_required, list)
        or any(not isinstance(item, str) for item in raw_required)
        or len(raw_required) != len(set(raw_required))
        or set(raw_required) != required
    ):
        report.add(
            "SCHEMA_CONTRACT",
            path,
            f"{label} required fields must exactly match: {', '.join(sorted(required))}",
        )

    properties = value.get("properties")
    if not isinstance(properties, dict) or set(properties) != required:
        report.add(
            "SCHEMA_CONTRACT",
            path,
            f"{label} properties must exactly match: {', '.join(sorted(required))}",
        )
        return None
    malformed_property = False
    for name, definition in properties.items():
        if not isinstance(definition, dict) or not definition:
            report.add("SCHEMA_CONTRACT", path, f"{label}.{name} must have a non-empty schema")
            malformed_property = True
    return None if malformed_property else properties


def _schema_exact_keywords(
    value: Any,
    expected: Set[str],
    report: VerificationReport,
    path: str,
    label: str,
) -> None:
    """Reject extra schema keywords that could silently tighten the contract."""

    if not isinstance(value, dict) or set(value) != expected:
        report.add(
            "SCHEMA_CONTRACT",
            path,
            f"{label} schema keywords must exactly match: {', '.join(sorted(expected))}",
        )


def _schema_string_enum_matches(value: Any, expected: Set[str]) -> bool:
    """Return whether a JSON value is an exact, duplicate-free string enum."""

    return (
        isinstance(value, list)
        and all(isinstance(item, str) for item in value)
        and len(value) == len(set(value))
        and set(value) == expected
    )


def _schema_local_refs(
    value: Any,
    definitions: Mapping[str, Any],
    report: VerificationReport,
    path: str,
) -> None:
    if isinstance(value, dict):
        reference = value.get("$ref")
        if reference is not None:
            prefix = "#/$defs/"
            target = reference[len(prefix) :] if isinstance(reference, str) and reference.startswith(prefix) else None
            if target is None or not isinstance(definitions.get(target), dict) or not definitions[target]:
                report.add("SCHEMA_CONTRACT", path, f"unresolved or empty local $ref: {reference}")
        for child in value.values():
            _schema_local_refs(child, definitions, report, path)
    elif isinstance(value, list):
        for child in value:
            _schema_local_refs(child, definitions, report, path)


def _schema_fragment(
    actual: Any,
    expected: Mapping[str, Any],
    report: VerificationReport,
    path: str,
    label: str,
) -> None:
    if actual != dict(expected):
        report.add("SCHEMA_CONTRACT", path, f"{label} does not match the verifier contract")


def _schema_discriminator_mapping(
    value: Any,
    discriminator: str,
    constrained_field: str,
    expected: Mapping[str, Tuple[str, str]],
    report: VerificationReport,
    path: str,
    label: str,
) -> None:
    choices: Any = None
    exact = True
    if isinstance(value, list) and len(value) == 1 and isinstance(value[0], dict):
        if set(value[0]) != {"oneOf"}:
            exact = False
        choices = value[0].get("oneOf")
    if not isinstance(choices, list):
        report.add("SCHEMA_CONTRACT", path, f"{label} must define a oneOf discriminator mapping")
        return

    actual: Dict[str, Tuple[str, str]] = {}
    for choice in choices:
        if not isinstance(choice, dict):
            exact = False
            continue
        if set(choice) != {"required", "properties"}:
            exact = False
        required = choice.get("required")
        properties = choice.get("properties")
        if not _schema_string_enum_matches(required, {discriminator, constrained_field}):
            exact = False
            continue
        if not isinstance(properties, dict) or set(properties) != {discriminator, constrained_field}:
            exact = False
            continue
        discriminator_schema = properties.get(discriminator)
        constraint_schema = properties.get(constrained_field)
        if not isinstance(discriminator_schema, dict) or not isinstance(constraint_schema, dict):
            exact = False
            continue
        discriminator_value = discriminator_schema.get("const")
        if (
            not isinstance(discriminator_value, str)
            or discriminator_schema != {"const": discriminator_value}
        ):
            exact = False
            continue
        expected_constraint = expected.get(discriminator_value)
        if expected_constraint is None:
            exact = False
            continue
        keyword, expected_value = expected_constraint
        if constraint_schema != {keyword: expected_value}:
            exact = False
            continue
        if discriminator_value in actual:
            exact = False
            continue
        actual[discriminator_value] = expected_constraint
    if not exact or actual != dict(expected):
        report.add("SCHEMA_CONTRACT", path, f"{label} discriminator mapping does not match verifier routing")


def _validate_event_schema(value: Dict[str, Any], report: VerificationReport, path: str) -> None:
    _schema_exact_keywords(
        value,
        {"$schema", "$id", "title", "oneOf", "$defs"},
        report,
        path,
        "event root",
    )
    definitions = value.get("$defs")
    if not isinstance(definitions, dict):
        report.add("SCHEMA_CONTRACT", path, "event schema must define non-empty $defs")
        return
    expected_refs = [
        {"$ref": "#/$defs/demoApproved"},
        {"$ref": "#/$defs/demoFrozen"},
        {"$ref": "#/$defs/transferCompleted"},
    ]
    if value.get("oneOf") != expected_refs:
        report.add("SCHEMA_CONTRACT", path, "event oneOf must contain the three ordered lifecycle phases")

    phase_names = {
        "demo-approved": "demoApproved",
        "demo-frozen": "demoFrozen",
        "transfer-completed": "transferCompleted",
    }
    for definition_name, expected in {
        "sha256": SHA256_SCHEMA,
        "gitRevision": GIT_REVISION_SCHEMA,
        "eventId": NONEMPTY_STRING_SCHEMA,
        "owner": NONEMPTY_STRING_SCHEMA,
        "approvedAt": DATE_TIME_SCHEMA,
        "approvalReference": NONEMPTY_STRING_SCHEMA,
        "freezeId": FREEZE_ID_SCHEMA,
    }.items():
        _schema_fragment(
            definitions.get(definition_name),
            expected,
            report,
            path,
            f"event.$defs.{definition_name}",
        )
    for event_type, definition_name in phase_names.items():
        properties = _schema_object_contract(
            definitions.get(definition_name),
            EVENT_BASE_KEYS | EVENT_PHASE_KEYS[event_type],
            report,
            path,
            definition_name,
        )
        if properties is not None:
            expected_properties: Dict[str, Dict[str, Any]] = {
                "schema_version": {"const": "1.0"},
                "event_id": {"$ref": "#/$defs/eventId"},
                "event_type": {"const": event_type},
                "acting_role": {"const": "project-owner"},
                "project_owner": {"$ref": "#/$defs/owner"},
                "approved_at": {"$ref": "#/$defs/approvedAt"},
                "approval_reference": {"$ref": "#/$defs/approvalReference"},
                "demo_revision": {"$ref": "#/$defs/gitRevision"},
                "demo_content_sha256": {"$ref": "#/$defs/sha256"},
            }
            if event_type == "demo-approved":
                expected_properties["approved_scope"] = {
                    "type": "array",
                    "minItems": 1,
                    "uniqueItems": True,
                    "items": NONEMPTY_STRING_SCHEMA,
                }
            elif event_type == "demo-frozen":
                expected_properties.update(
                    {
                        "predecessor_event_id": {"$ref": "#/$defs/eventId"},
                        "freeze_id": {"$ref": "#/$defs/freezeId"},
                        "manifest_sha256": {"$ref": "#/$defs/sha256"},
                        "common_sha256": {"$ref": "#/$defs/sha256"},
                        "client_sha256": {"$ref": "#/$defs/sha256"},
                        "game_server_sha256": {"$ref": "#/$defs/sha256"},
                    }
                )
            else:
                expected_properties.update(
                    {
                        "predecessor_event_id": {"$ref": "#/$defs/eventId"},
                        "freeze_id": {"$ref": "#/$defs/freezeId"},
                        "client_receipt_sha256": {"$ref": "#/$defs/sha256"},
                        "game_server_receipt_sha256": {"$ref": "#/$defs/sha256"},
                        "destinations": {"$ref": "#/$defs/destinations"},
                    }
                )
            for field, expected in expected_properties.items():
                _schema_fragment(
                    properties.get(field),
                    expected,
                    report,
                    path,
                    f"event {definition_name}.{field}",
                )

    destination = definitions.get("destinations")
    destination_properties = _schema_object_contract(
        destination, {"client", "game-server"}, report, path, "destinations"
    )
    if destination_properties is not None:
        for consumer in ("client", "game-server"):
            _schema_fragment(
                destination_properties.get(consumer),
                NONEMPTY_STRING_SCHEMA,
                report,
                path,
                f"event destinations.{consumer}",
            )
    _schema_local_refs(value, definitions, report, path)


def _validate_manifest_schema(value: Dict[str, Any], report: VerificationReport, path: str) -> None:
    _schema_exact_keywords(
        value,
        {
            "$schema",
            "$id",
            "title",
            "type",
            "additionalProperties",
            "required",
            "properties",
            "$defs",
        },
        report,
        path,
        "manifest root",
    )
    properties = _schema_object_contract(
        value,
        MANIFEST_KEYS,
        report,
        path,
        "manifest",
        {"$schema", "$id", "title", "$defs"},
    )
    definitions = value.get("$defs")
    if not isinstance(definitions, dict):
        report.add("SCHEMA_CONTRACT", path, "manifest schema must define non-empty $defs")
        return
    if properties is not None:
        for field, expected in {
            "schema_version": {"const": "1.0"},
            "freeze_id": FREEZE_ID_SCHEMA,
            "created_at": DATE_TIME_SCHEMA,
            "demo_revision": GIT_REVISION_SCHEMA,
            "demo_content_sha256": {"$ref": "#/$defs/sha256"},
        }.items():
            _schema_fragment(
                properties.get(field), expected, report, path, f"manifest.{field}"
            )
        destination_properties = _schema_object_contract(
            properties.get("destinations"),
            {"client", "game-server"},
            report,
            path,
            "manifest.destinations",
        )
        if destination_properties is not None:
            for consumer in ("client", "game-server"):
                _schema_fragment(
                    destination_properties.get(consumer),
                    {"type": "string", "pattern": MANIFEST_DESTINATION_PATTERNS[consumer]},
                    report,
                    path,
                    f"manifest.destinations.{consumer}",
                )
        bundle_properties = _schema_object_contract(
            properties.get("bundle_hashes"),
            {"common", "client", "game-server"},
            report,
            path,
            "manifest.bundle_hashes",
        )
        if bundle_properties is not None:
            for partition in ("common", "client", "game-server"):
                _schema_fragment(
                    bundle_properties.get(partition),
                    {"$ref": "#/$defs/sha256"},
                    report,
                    path,
                    f"manifest.bundle_hashes.{partition}",
                )
        _schema_fragment(
            properties.get("files"),
            {"type": "array", "minItems": 1, "items": {"$ref": "#/$defs/file"}},
            report,
            path,
            "manifest.files",
        )

    file_schema = definitions.get("file")
    _schema_exact_keywords(
        file_schema,
        {"type", "additionalProperties", "required", "properties", "allOf"},
        report,
        path,
        "manifest file",
    )
    _schema_fragment(
        definitions.get("sha256"), SHA256_SCHEMA, report, path, "manifest.$defs.sha256"
    )
    file_properties = _schema_object_contract(
        file_schema,
        {"path", "audience", "sha256", "bytes"},
        report,
        path,
        "manifest file",
        {"allOf"},
    )
    if file_properties is not None:
        _schema_fragment(
            file_properties.get("path"),
            {"type": "string", "pattern": MANIFEST_PATH_SCHEMA_PATTERN},
            report,
            path,
            "manifest file.path",
        )
        _schema_fragment(
            file_properties.get("audience"),
            {"enum": ["common", "client", "game-server", "reference"]},
            report,
            path,
            "manifest file.audience",
        )
        _schema_fragment(
            file_properties.get("sha256"),
            {"$ref": "#/$defs/sha256"},
            report,
            path,
            "manifest file.sha256",
        )
        _schema_fragment(
            file_properties.get("bytes"),
            {"type": "integer", "minimum": 0},
            report,
            path,
            "manifest file.bytes",
        )
        _schema_discriminator_mapping(
            file_schema.get("allOf") if isinstance(file_schema, dict) else None,
            "audience",
            "path",
            {
                "common": ("pattern", "^common/"),
                "client": ("pattern", "^client/"),
                "game-server": ("pattern", "^game-server/"),
                "reference": ("const", "references/transition-policy.md"),
            },
            report,
            path,
            "manifest audience/path",
        )
    _schema_local_refs(value, definitions, report, path)


def _validate_receipt_schema(value: Dict[str, Any], report: VerificationReport, path: str) -> None:
    _schema_exact_keywords(
        value,
        {
            "$schema",
            "$id",
            "title",
            "type",
            "additionalProperties",
            "required",
            "properties",
            "allOf",
            "$defs",
        },
        report,
        path,
        "receipt root",
    )
    properties = _schema_object_contract(
        value,
        RECEIPT_KEYS,
        report,
        path,
        "receipt",
        {"$schema", "$id", "title", "allOf", "$defs"},
    )
    definitions = value.get("$defs")
    if not isinstance(definitions, dict):
        report.add("SCHEMA_CONTRACT", path, "receipt schema must define non-empty $defs")
        return
    if properties is not None:
        _schema_fragment(
            properties.get("consumer"),
            {"enum": ["client", "game-server"]},
            report,
            path,
            "receipt.consumer",
        )
        expected_properties = {
            "schema_version": {"const": "1.0"},
            "freeze_id": FREEZE_ID_SCHEMA,
            "destination": {
                "type": "string",
                "pattern": RECEIPT_DESTINATION_SCHEMA_PATTERN,
            },
            "manifest_sha256": {"$ref": "#/$defs/sha256"},
            "common_sha256": {"$ref": "#/$defs/sha256"},
            "assigned_bundle_sha256": {"$ref": "#/$defs/sha256"},
            "file_count": {"type": "integer", "minimum": 1},
            "byte_count": {"type": "integer", "minimum": 1},
            "received_at": DATE_TIME_SCHEMA,
            "status": {"const": "verified"},
            "verified_by": NONEMPTY_STRING_SCHEMA,
        }
        for field, expected in expected_properties.items():
            _schema_fragment(
                properties.get(field), expected, report, path, f"receipt.{field}"
            )
    _schema_fragment(
        definitions.get("sha256"), SHA256_SCHEMA, report, path, "receipt.$defs.sha256"
    )
    _schema_discriminator_mapping(
        value.get("allOf"),
        "consumer",
        "destination",
        {
            "client": ("pattern", "^somnia-client/"),
            "game-server": ("pattern", "^somnia-game-server/"),
        },
        report,
        path,
        "receipt consumer/destination",
    )
    _schema_local_refs(value, definitions, report, path)


def _validate_schema_json(transition_root: Path, report: VerificationReport) -> None:
    validators = {
        "governance/schemas/event.schema.json": _validate_event_schema,
        "governance/schemas/manifest.schema.json": _validate_manifest_schema,
        "governance/schemas/receipt.schema.json": _validate_receipt_schema,
    }
    for relative in SCHEMA_FILES:
        value = _load_json_object(transition_root / PurePosixPath(relative), report, "SCHEMA_JSON")
        if value is None:
            continue
        if value.get("$schema") != "https://json-schema.org/draft/2020-12/schema":
            report.add("SCHEMA_JSON", relative, "schema must declare JSON Schema draft 2020-12")
        if not isinstance(value.get("$id"), str) or not value.get("$id"):
            report.add("SCHEMA_JSON", relative, "schema must declare a non-empty $id")
        validators[relative](value, report, relative)


def _markdown_target(raw_target: str) -> Optional[str]:
    target = raw_target[1:-1] if raw_target.startswith("<") and raw_target.endswith(">") else raw_target
    if target.startswith("#"):
        return None
    parsed = urlsplit(target)
    if parsed.scheme.lower() == "file" or re.match(r"^[A-Za-z]:[/\\]", target):
        return target
    if parsed.scheme or parsed.netloc:
        return None
    decoded = unquote(parsed.path)
    return decoded or None


def _markdown_link_targets(text: str) -> Iterable[str]:
    """Yield inline, reference-definition, and local autolink targets."""

    for pattern in (
        MARKDOWN_LINK_RE,
        MARKDOWN_REFERENCE_DEFINITION_RE,
        MARKDOWN_AUTOLINK_RE,
    ):
        for match in pattern.finditer(text):
            yield match.group("target")


def _validate_markdown_links(root: Path, transition_root: Path, report: VerificationReport) -> None:
    if not transition_root.is_dir():
        return
    for path in sorted(transition_root.rglob("*.md")):
        relative = path.relative_to(transition_root)
        if "archive" in relative.parts or "freezes" in relative.parts:
            continue
        if path.is_symlink():
            report.add("SYMLINK", relative.as_posix(), "Markdown source may not be a symlink")
            continue
        text = _read_text(path, report, "MARKDOWN_READ")
        if text is None:
            continue
        for raw_target in _markdown_link_targets(text):
            local = _markdown_target(raw_target)
            if local is None:
                continue
            if local.startswith("/") or "\\" in local:
                report.add("MARKDOWN_LINK", relative.as_posix(), f"unsafe local link: {local}")
                continue
            candidate = (path.parent / PurePosixPath(local)).resolve(strict=False)
            repository = root.resolve(strict=False)
            if not _is_within(repository, candidate):
                report.add("MARKDOWN_LINK", relative.as_posix(), f"local link escapes repository: {local}")
            elif not candidate.exists():
                report.add("MARKDOWN_LINK", relative.as_posix(), f"local link target does not exist: {local}")


def _validate_consumer_link_closure(
    artifact_root: Path,
    partitions: Mapping[str, Sequence[Artifact]],
    report: VerificationReport,
    source_overrides: Optional[Mapping[str, Path]] = None,
) -> None:
    """Require every local link to resolve inside each consumer's delivered files."""

    overrides = source_overrides or {}
    for consumer in ("client", "game-server"):
        assigned = [
            artifact
            for partition in ("common", consumer, "policy")
            for artifact in partitions.get(partition, ())
        ]
        delivered = {"manifest.json"}
        delivered.update(artifact.path for artifact in assigned)
        for artifact in assigned:
            if not artifact.path.lower().endswith(".md"):
                continue
            source = overrides.get(
                artifact.path,
                artifact_root.joinpath(*PurePosixPath(artifact.path).parts),
            )
            text = _read_text(source, report, "MARKDOWN_READ")
            if text is None:
                continue
            for raw_target in _markdown_link_targets(text):
                local = _markdown_target(raw_target)
                if local is None:
                    continue
                if local.startswith("/") or "\\" in local:
                    continue  # The ordinary link validation reports unsafe paths.
                target = posixpath.normpath(
                    posixpath.join(posixpath.dirname(artifact.path), local)
                )
                if target not in delivered:
                    report.add(
                        "MARKDOWN_LINK_CLOSURE",
                        artifact.path,
                        f"{consumer} delivery does not contain local link target: {local}",
                    )


def _table_cells(line: str) -> List[str]:
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def _validate_coverage_file(
    path: Path,
    report: VerificationReport,
    display_path: str,
    allow_blocked: bool,
) -> Set[str]:
    text = _read_text(path, report, "COVERAGE_READ")
    if text is None:
        return set()
    lines = text.splitlines()
    rows = 0
    tables = 0
    seen_ids: Set[str] = set()
    included_ids: Set[str] = set()
    index = 0
    while index < len(lines):
        line = lines[index]
        header = [cell.strip().lower() for cell in _table_cells(line)]
        state_index: Optional[int] = None
        for candidate in ("상태", "status"):
            if candidate in header:
                state_index = header.index(candidate)
                break
        blocking_index: Optional[int] = None
        for candidate in ("blocking decision", "미결 decision", "blocking"):
            if candidate in header:
                blocking_index = header.index(candidate)
                break
        if state_index is None or index + 1 >= len(lines):
            index += 1
            continue
        separator = _table_cells(lines[index + 1])
        if len(separator) != len(header) or not all(
            re.fullmatch(r":?-{3,}:?", cell.replace(" ", "")) for cell in separator
        ):
            index += 1
            continue

        tables += 1
        cursor = index + 2
        while cursor < len(lines):
            row = lines[cursor]
            if not row.strip() or "|" not in row:
                break
            cells = _table_cells(lines[cursor])
            if state_index >= len(cells):
                report.add("COVERAGE", display_path, "coverage row is missing the status cell")
                cursor += 1
                continue
            coverage_id = cells[0].strip().strip("`") if cells else ""
            if not coverage_id:
                report.add("COVERAGE", display_path, "coverage row must have a non-empty ID")
            elif coverage_id in seen_ids:
                report.add("COVERAGE", display_path, f"duplicate coverage ID: {coverage_id}")
            else:
                seen_ids.add(coverage_id)
            state = cells[state_index].strip().strip("`").lower()
            if coverage_id and state == "included":
                included_ids.add(coverage_id)
            rows += 1
            if state not in ALLOWED_COVERAGE_STATES:
                report.add(
                    "COVERAGE",
                    display_path,
                    f"coverage status must be included, excluded, or decision-blocked: {state}",
                )
            elif not allow_blocked and state == "decision-blocked":
                report.add("COVERAGE_BLOCKED", display_path, "official freeze cannot contain decision-blocked coverage")
            elif not allow_blocked and state == "included":
                if blocking_index is None or blocking_index >= len(cells):
                    report.add(
                        "COVERAGE_BLOCKED",
                        display_path,
                        f"included coverage {coverage_id} must declare Blocking decision as none",
                    )
                else:
                    blocker = cells[blocking_index].strip().strip("`").lower()
                    if blocker not in NO_BLOCKING_DECISION_VALUES:
                        report.add(
                            "COVERAGE_BLOCKED",
                            display_path,
                            f"included coverage {coverage_id} has an unresolved blocking decision: {blocker}",
                        )
            cursor += 1
        index = cursor

    if tables == 0:
        report.add("COVERAGE", display_path, "coverage table must have a 상태 or Status column")
    elif rows == 0:
        report.add("COVERAGE", display_path, "coverage table must contain at least one row")
    return included_ids


def _validate_rule_documents(transition_root: Path, report: VerificationReport) -> None:
    expected_prefixes = {
        "common": "PT-COM-",
        "client": "PT-CLI-",
        "game-server": "PT-SRV-",
    }
    seen_ids: Dict[str, str] = {}
    for partition, expected_prefix in expected_prefixes.items():
        rules_root = transition_root / partition / "rules"
        for path in sorted(rules_root.rglob("*.md")):
            display_path = path.relative_to(transition_root).as_posix()
            text = _read_text(path, report, "RULE_CONTRACT")
            if text is None:
                continue
            blocks: List[Tuple[str, Dict[str, List[str]]]] = []
            current_id: Optional[str] = None
            current_fields: Dict[str, List[str]] = {}
            for line in text.splitlines():
                if line.startswith("## "):
                    if current_id is not None:
                        blocks.append((current_id, current_fields))
                    current_id = None
                    current_fields = {}
                    match = RULE_HEADING_RE.fullmatch(line)
                    if match is None:
                        report.add("RULE_CONTRACT", display_path, f"invalid Rule ID heading: {line}")
                        continue
                    current_id = match.group("id")
                    if not current_id.startswith(expected_prefix):
                        report.add(
                            "RULE_CONTRACT",
                            display_path,
                            f"{current_id} does not match the {partition} rule namespace",
                        )
                    previous = seen_ids.get(current_id)
                    if previous is not None:
                        report.add(
                            "RULE_CONTRACT",
                            display_path,
                            f"duplicate Rule ID {current_id}; first declared in {previous}",
                        )
                    else:
                        seen_ids[current_id] = display_path
                    continue

                field_match = RULE_FIELD_RE.fullmatch(line)
                if field_match is not None and current_id is not None:
                    label = field_match.group("label")
                    value = field_match.group("value").strip()
                    if label not in RULE_FIELDS:
                        report.add("RULE_CONTRACT", display_path, f"{current_id} has unsupported field: {label}")
                    else:
                        current_fields.setdefault(label, []).append(value)

            if current_id is not None:
                blocks.append((current_id, current_fields))
            if not blocks:
                report.add("RULE_CONTRACT", display_path, "rules document must contain at least one Rule ID block")
                continue

            for rule_id, fields in blocks:
                for label in RULE_FIELDS:
                    values = fields.get(label, [])
                    if not values:
                        report.add("RULE_CONTRACT", display_path, f"{rule_id} is missing field: {label}")
                    elif len(values) > 1:
                        report.add("RULE_CONTRACT", display_path, f"{rule_id} repeats field: {label}")
                    elif not values[0]:
                        report.add("RULE_CONTRACT", display_path, f"{rule_id} has an empty field: {label}")


def _source_artifact(path: Path, target_path: str, audience: str, report: VerificationReport) -> Optional[Artifact]:
    if path.is_symlink() or not path.is_file():
        report.add("SYMLINK", str(path), "official source artifact must be a regular non-symlink file")
        return None
    raw = _read_bytes(path, report)
    if raw is None:
        return None
    return Artifact(target_path, audience, _sha256(raw), len(raw))


def _build_prepare_inventory(transition_root: Path, report: VerificationReport) -> None:
    partitions: Dict[str, List[Artifact]] = {
        "common": [],
        "client": [],
        "game-server": [],
        "policy": [],
    }
    for partition in ("common", "client", "game-server"):
        base = transition_root / partition
        for path in _walk_regular_files(base, report, transition_root):
            relative = path.relative_to(transition_root).as_posix()
            segments = {segment.lower() for segment in PurePosixPath(relative).parts}
            forbidden = sorted(segments & DISALLOWED_EXPORT_SEGMENTS)
            if forbidden:
                report.add("EXPORT_INVENTORY", relative, f"official inventory contains forbidden segment(s): {', '.join(forbidden)}")
                continue
            if _contains_unicode_cc(relative):
                report.add(
                    "EXPORT_INVENTORY",
                    relative,
                    "official inventory path may not contain Unicode Cc control characters",
                )
                continue
            if path.suffix.lower() != ".md":
                report.add("EXPORT_INVENTORY", relative, "consumer inventory may contain Markdown documents only")
            artifact = _source_artifact(path, relative, partition, report)
            if artifact is not None:
                partitions[partition].append(artifact)

    policy = transition_root / "governance" / "transition-policy.md"
    artifact = _source_artifact(policy, "references/transition-policy.md", "reference", report)
    if artifact is not None:
        partitions["policy"].append(artifact)

    for partition in ("common", "client", "game-server", "policy"):
        if not partitions[partition]:
            report.add("EXPORT_INVENTORY", partition, "official partition must contain at least one file")
        report.partitions[partition] = partitions[partition]
        report.partition_hashes[partition] = partition_hash(partitions[partition])

    _validate_required_export_inventory(
        (entry for entries in partitions.values() for entry in entries),
        report,
        "living export inventory",
    )

    selected = [entry.path for entries in partitions.values() for entry in entries]
    if len(selected) != len(set(selected)):
        report.add("EXPORT_INVENTORY", "<inventory>", "official target paths must be unique")
    if len(selected) != len({path.casefold() for path in selected}):
        report.add("EXPORT_INVENTORY", "<inventory>", "official target paths must not collide by case")
    for target in selected:
        first = PurePosixPath(target).parts[0]
        if first not in {"common", "client", "game-server", "references"}:
            report.add("EXPORT_INVENTORY", target, "official inventory has an unsupported partition")


def verify_prepare(root: Path) -> VerificationReport:
    """Audit living transition documents without mutating the repository."""

    root = root.resolve(strict=False)
    report = VerificationReport("prepare")
    transition_root = root / "docs" / "production-transition"
    if not transition_root.is_dir() or transition_root.is_symlink():
        report.add("LIVING_STRUCTURE", "docs/production-transition", "transition root is missing or is a symlink")
        return report.finish()

    _validate_required_structure(transition_root, report)
    _validate_schema_json(transition_root, report)
    _validate_markdown_links(root, transition_root, report)
    _validate_rule_documents(transition_root, report)
    _validate_coverage_file(
        transition_root / "client" / "demo-experience-map.md",
        report,
        "client/demo-experience-map.md",
        allow_blocked=True,
    )
    _validate_coverage_file(
        transition_root / "game-server" / "domain-coverage.md",
        report,
        "game-server/domain-coverage.md",
        allow_blocked=True,
    )
    _build_prepare_inventory(transition_root, report)
    _validate_consumer_link_closure(
        transition_root,
        report.partitions,
        report,
        {
            "references/transition-policy.md": transition_root
            / "governance"
            / "transition-policy.md"
        },
    )
    return report.finish()


def _canonical_manifest_path(raw: Any, report: VerificationReport, display_path: str) -> Optional[str]:
    path = _nonempty_string(raw, report, display_path, "path")
    if path is None:
        return None
    if _contains_unicode_cc(path):
        report.add("MANIFEST_PATH", display_path, "path may not contain Unicode Cc control characters")
        return None
    candidate = PurePosixPath(path)
    if (
        candidate.is_absolute()
        or "\\" in path
        or str(candidate) != path
        or any(part in {"", ".", ".."} for part in candidate.parts)
    ):
        report.add("MANIFEST_PATH", display_path, f"path must be canonical, relative POSIX: {path}")
        return None
    forbidden = sorted({part.lower() for part in candidate.parts} & DISALLOWED_EXPORT_SEGMENTS)
    if forbidden:
        report.add("MANIFEST_PATH", display_path, f"path contains forbidden segment(s): {', '.join(forbidden)}")
        return None
    return path


def _validate_manifest(value: Dict[str, Any], report: VerificationReport, path: str) -> Optional[ManifestData]:
    _exact_keys(value, MANIFEST_KEYS, report, path, "manifest")
    if value.get("schema_version") != "1.0":
        report.add("FIELD_VALUE", path, "manifest schema_version must be 1.0")
    freeze_id = _pattern_string(value.get("freeze_id"), FREEZE_ID_RE, report, path, "freeze_id")
    demo_revision = _pattern_string(value.get("demo_revision"), GIT_REVISION_RE, report, path, "demo_revision")
    demo_hash = _pattern_string(value.get("demo_content_sha256"), SHA256_RE, report, path, "demo_content_sha256")
    created_at = _timestamp(value.get("created_at"), report, path, "created_at")

    destinations: Dict[str, str] = {}
    raw_destinations = value.get("destinations")
    if not isinstance(raw_destinations, dict):
        report.add("FIELD_TYPE", path, "destinations must be an object")
    else:
        _exact_keys(raw_destinations, {"client", "game-server"}, report, path, "destinations")
        for consumer in ("client", "game-server"):
            parsed = _nonempty_string(raw_destinations.get(consumer), report, path, f"destinations.{consumer}")
            if parsed is not None:
                destinations[consumer] = parsed
                if freeze_id is not None:
                    expected_destination = f"{DESTINATION_PREFIXES[consumer]}{freeze_id}/"
                    if parsed != expected_destination:
                        report.add(
                            "DESTINATION",
                            path,
                            f"{consumer} destination must equal {expected_destination}",
                        )

    bundle_hashes: Dict[str, str] = {}
    raw_hashes = value.get("bundle_hashes")
    if not isinstance(raw_hashes, dict):
        report.add("FIELD_TYPE", path, "bundle_hashes must be an object")
    else:
        expected_hashes = {"common", "client", "game-server"}
        _exact_keys(raw_hashes, expected_hashes, report, path, "bundle_hashes")
        for partition in sorted(expected_hashes):
            parsed = _pattern_string(raw_hashes.get(partition), SHA256_RE, report, path, f"bundle_hashes.{partition}")
            if parsed is not None:
                bundle_hashes[partition] = parsed

    entries: List[Artifact] = []
    raw_files = value.get("files")
    seen: Set[str] = set()
    seen_folded: Set[str] = set()
    if not isinstance(raw_files, list) or not raw_files:
        report.add("FIELD_TYPE", path, "files must be a non-empty array")
    else:
        for index, raw_entry in enumerate(raw_files):
            entry_path = f"{path}#files[{index}]"
            if not isinstance(raw_entry, dict):
                report.add("FIELD_TYPE", entry_path, "file entry must be an object")
                continue
            _exact_keys(raw_entry, {"path", "audience", "sha256", "bytes"}, report, entry_path, "file entry")
            target = _canonical_manifest_path(raw_entry.get("path"), report, entry_path)
            audience = raw_entry.get("audience")
            if not isinstance(audience, str) or audience not in {
                "common",
                "client",
                "game-server",
                "reference",
            }:
                report.add("FIELD_VALUE", entry_path, f"invalid audience: {audience}")
                audience = None
            digest = _pattern_string(raw_entry.get("sha256"), SHA256_RE, report, entry_path, "sha256")
            byte_count = raw_entry.get("bytes")
            if isinstance(byte_count, bool) or not isinstance(byte_count, int) or byte_count < 0:
                report.add("FIELD_TYPE", entry_path, "bytes must be a non-negative integer")
                byte_count = None
            if target is not None:
                folded = target.casefold()
                if target in seen or folded in seen_folded:
                    report.add("MANIFEST_DUPLICATE", entry_path, f"duplicate or case-colliding path: {target}")
                seen.add(target)
                seen_folded.add(folded)
                if PurePosixPath(target).suffix.lower() != ".md":
                    report.add("MANIFEST_PATH", entry_path, "official freeze artifacts must be Markdown documents")
            if target is not None and audience is not None:
                expected_prefix = "references/" if audience == "reference" else f"{audience}/"
                if not target.startswith(expected_prefix):
                    report.add("AUDIENCE_PATH", entry_path, f"{audience} entry must start with {expected_prefix}")
                if audience == "reference" and target != "references/transition-policy.md":
                    report.add("AUDIENCE_PATH", entry_path, "the only reference artifact is references/transition-policy.md")
            if target is not None and audience is not None and digest is not None and byte_count is not None:
                entries.append(Artifact(target, audience, digest, byte_count))

    for audience in ("common", "client", "game-server", "reference"):
        if not any(entry.audience == audience for entry in entries):
            report.add("MANIFEST_INVENTORY", path, f"manifest must contain at least one {audience} artifact")

    if freeze_id is None or demo_revision is None or demo_hash is None:
        return None
    return ManifestData(
        freeze_id,
        demo_revision,
        demo_hash,
        created_at,
        destinations,
        bundle_hashes,
        entries,
    )


def _validate_exact_directory_files(
    directory: Path,
    expected: Set[str],
    report: VerificationReport,
    freeze_dir: Path,
) -> None:
    if not directory.is_dir() or directory.is_symlink():
        report.add("FREEZE_LAYOUT", _relative(freeze_dir, directory), "required directory is missing or is a symlink")
        return
    actual: Set[str] = set()
    for child in directory.iterdir():
        if child.is_symlink() or not child.is_file():
            report.add("FREEZE_LAYOUT", _relative(freeze_dir, child), "only regular expected files are allowed")
        else:
            actual.add(child.name)
    missing = sorted(expected - actual)
    extra = sorted(actual - expected)
    if missing:
        report.add("FREEZE_LAYOUT", _relative(freeze_dir, directory), f"missing file(s): {', '.join(missing)}")
    if extra:
        report.add("FREEZE_LAYOUT", _relative(freeze_dir, directory), f"unexpected file(s): {', '.join(extra)}")


def _validate_freeze_layout(freeze_dir: Path, report: VerificationReport) -> None:
    if not freeze_dir.is_dir() or freeze_dir.is_symlink():
        report.add("FREEZE_LAYOUT", str(freeze_dir), "freeze directory is missing or is a symlink")
        return
    allowed = {"manifest.json", "common", "client", "game-server", "references", "receipts"}
    actual = {child.name for child in freeze_dir.iterdir()}
    for name in sorted(allowed - actual):
        report.add("FREEZE_LAYOUT", name, "required freeze entry is missing")
    for name in sorted(actual - allowed):
        report.add("FREEZE_LAYOUT", name, "unexpected top-level freeze entry")
    manifest = freeze_dir / "manifest.json"
    if manifest.exists() and (manifest.is_symlink() or not manifest.is_file()):
        report.add("FREEZE_LAYOUT", "manifest.json", "manifest must be a regular file")
    for name in ("common", "client", "game-server", "references"):
        path = freeze_dir / name
        if path.exists() and (path.is_symlink() or not path.is_dir()):
            report.add("FREEZE_LAYOUT", name, "partition must be a real directory")
    _validate_exact_directory_files(
        freeze_dir / "receipts",
        {"client.json", "game-server.json"},
        report,
        freeze_dir,
    )


def _actual_freeze_inventory(freeze_dir: Path, report: VerificationReport) -> Set[str]:
    actual: Set[str] = set()
    for partition in ("common", "client", "game-server", "references"):
        base = freeze_dir / partition
        for path in _walk_regular_files(base, report, freeze_dir):
            relative = path.relative_to(freeze_dir).as_posix()
            segments = {segment.lower() for segment in PurePosixPath(relative).parts}
            forbidden = sorted(segments & DISALLOWED_EXPORT_SEGMENTS)
            if forbidden:
                report.add("MANIFEST_PATH", relative, f"freeze contains forbidden segment(s): {', '.join(forbidden)}")
            if _contains_unicode_cc(relative):
                report.add(
                    "MANIFEST_PATH",
                    relative,
                    "freeze path may not contain Unicode Cc control characters",
                )
            actual.add(relative)
    return actual


def _validate_manifest_files(
    freeze_dir: Path,
    manifest: ManifestData,
    report: VerificationReport,
) -> Dict[str, List[Artifact]]:
    expected = {entry.path for entry in manifest.entries}
    actual = _actual_freeze_inventory(freeze_dir, report)
    for missing in sorted(expected - actual):
        report.add("MANIFEST_INVENTORY", missing, "manifest file is missing from freeze")
    for extra in sorted(actual - expected):
        report.add("MANIFEST_INVENTORY", extra, "freeze file is not listed in manifest")

    partitions: Dict[str, List[Artifact]] = {
        "common": [],
        "client": [],
        "game-server": [],
        "policy": [],
    }
    freeze_root = freeze_dir.resolve(strict=False)
    for entry in manifest.entries:
        path = freeze_dir.joinpath(*PurePosixPath(entry.path).parts)
        resolved = path.resolve(strict=False)
        if not _is_within(freeze_root, resolved):
            report.add("MANIFEST_PATH", entry.path, "resolved file escapes freeze directory")
            continue
        if _has_symlink_component(freeze_dir, path) or not path.is_file():
            report.add("MANIFEST_FILE", entry.path, "manifest entry must resolve to a regular non-symlink file")
            continue
        raw = _read_bytes(path, report, "MANIFEST_FILE")
        if raw is None:
            continue
        actual_digest = _sha256(raw)
        if actual_digest != entry.sha256:
            report.add("FILE_HASH", entry.path, f"expected {entry.sha256}, got {actual_digest}")
        if len(raw) != entry.byte_count:
            report.add("FILE_BYTES", entry.path, f"expected {entry.byte_count}, got {len(raw)}")
        actual_entry = Artifact(entry.path, entry.audience, actual_digest, len(raw))
        key = "policy" if entry.audience == "reference" else entry.audience
        partitions[key].append(actual_entry)

    for key in ("common", "client", "game-server"):
        computed = partition_hash(partitions[key])
        report.partition_hashes[key] = computed
        expected_hash = manifest.bundle_hashes.get(key)
        if expected_hash is not None and computed != expected_hash:
            report.add("BUNDLE_HASH", key, f"expected {expected_hash}, got {computed}")
    report.partition_hashes["policy"] = partition_hash(partitions["policy"])
    report.partitions = partitions
    return partitions


def _validate_event(
    value: Dict[str, Any],
    expected_type: str,
    report: VerificationReport,
    path: str,
) -> Optional[datetime]:
    _exact_keys(value, EVENT_BASE_KEYS | EVENT_PHASE_KEYS[expected_type], report, path, expected_type)
    if value.get("schema_version") != "1.0":
        report.add("FIELD_VALUE", path, "event schema_version must be 1.0")
    if value.get("event_type") != expected_type:
        report.add("EVENT_SEQUENCE", path, f"event_type must be {expected_type}")
    if value.get("acting_role") != "project-owner":
        report.add("FIELD_VALUE", path, "acting_role must be project-owner")
    for key in ("event_id", "project_owner", "approval_reference"):
        _nonempty_string(value.get(key), report, path, key)
    _pattern_string(value.get("demo_revision"), GIT_REVISION_RE, report, path, "demo_revision")
    _pattern_string(value.get("demo_content_sha256"), SHA256_RE, report, path, "demo_content_sha256")
    timestamp = _timestamp(value.get("approved_at"), report, path, "approved_at")

    if expected_type == "demo-approved":
        scope = value.get("approved_scope")
        if (
            not isinstance(scope, list)
            or not scope
            or any(not isinstance(item, str) or not item for item in scope)
            or len(scope) != len(set(scope))
        ):
            report.add("FIELD_TYPE", path, "approved_scope must be a non-empty unique string array")
    elif expected_type == "demo-frozen":
        _nonempty_string(value.get("predecessor_event_id"), report, path, "predecessor_event_id")
        _pattern_string(value.get("freeze_id"), FREEZE_ID_RE, report, path, "freeze_id")
        for key in ("manifest_sha256", "common_sha256", "client_sha256", "game_server_sha256"):
            _pattern_string(value.get(key), SHA256_RE, report, path, key)
    else:
        _nonempty_string(value.get("predecessor_event_id"), report, path, "predecessor_event_id")
        _pattern_string(value.get("freeze_id"), FREEZE_ID_RE, report, path, "freeze_id")
        for key in ("client_receipt_sha256", "game_server_receipt_sha256"):
            _pattern_string(value.get(key), SHA256_RE, report, path, key)
        destinations = value.get("destinations")
        if not isinstance(destinations, dict):
            report.add("FIELD_TYPE", path, "destinations must be an object")
        else:
            _exact_keys(destinations, {"client", "game-server"}, report, path, "destinations")
            for consumer in ("client", "game-server"):
                _nonempty_string(destinations.get(consumer), report, path, f"destinations.{consumer}")
    return timestamp


def _validate_receipt(
    value: Dict[str, Any],
    consumer: str,
    report: VerificationReport,
    path: str,
) -> Optional[datetime]:
    _exact_keys(value, RECEIPT_KEYS, report, path, "receipt")
    if value.get("schema_version") != "1.0":
        report.add("FIELD_VALUE", path, "receipt schema_version must be 1.0")
    if value.get("consumer") != consumer:
        report.add("RECEIPT", path, f"consumer must be {consumer}")
    _pattern_string(value.get("freeze_id"), FREEZE_ID_RE, report, path, "freeze_id")
    _nonempty_string(value.get("destination"), report, path, "destination")
    for key in ("manifest_sha256", "common_sha256", "assigned_bundle_sha256"):
        _pattern_string(value.get(key), SHA256_RE, report, path, key)
    for key in ("file_count", "byte_count"):
        number = value.get(key)
        if isinstance(number, bool) or not isinstance(number, int) or number < 1:
            report.add("FIELD_TYPE", path, f"{key} must be a positive integer")
    received_at = _timestamp(value.get("received_at"), report, path, "received_at")
    if value.get("status") != "verified":
        report.add("FIELD_VALUE", path, "receipt status must be verified")
    _nonempty_string(value.get("verified_by"), report, path, "verified_by")
    return received_at


def _same_field(
    documents: Sequence[Tuple[str, Mapping[str, Any]]],
    field_name: str,
    expected: Any,
    report: VerificationReport,
) -> None:
    for path, document in documents:
        if document.get(field_name) != expected:
            report.add("EVENT_CONSISTENCY", path, f"{field_name} must equal {expected}")


def _validate_cutover_documents(
    freeze_dir: Path,
    events_dir: Path,
    manifest: ManifestData,
    manifest_raw: bytes,
    expected_approved_scope: Set[str],
    report: VerificationReport,
) -> None:
    event_paths = {
        "demo-approved": events_dir / "1-demo-approved.json",
        "demo-frozen": events_dir / "2-demo-frozen.json",
        "transfer-completed": events_dir / "3-transfer-completed.json",
    }
    events: Dict[str, Dict[str, Any]] = {}
    timestamps: List[Tuple[str, datetime]] = []
    _validate_exact_directory_files(
        events_dir,
        {"1-demo-approved.json", "2-demo-frozen.json", "3-transfer-completed.json"},
        report,
        events_dir,
    )
    for event_type, path in event_paths.items():
        if _has_symlink_component(events_dir, path) or not path.is_file():
            continue
        value = _load_json_object(path, report, "EVENT_JSON")
        if value is None:
            continue
        events[event_type] = value
        parsed_time = _validate_event(value, event_type, report, _relative(events_dir, path))
        if parsed_time is not None:
            timestamps.append((event_type, parsed_time))

    receipt_paths = {
        "client": freeze_dir / "receipts" / "client.json",
        "game-server": freeze_dir / "receipts" / "game-server.json",
    }
    receipts: Dict[str, Dict[str, Any]] = {}
    receipt_raw: Dict[str, bytes] = {}
    receipt_times: Dict[str, datetime] = {}
    for consumer, path in receipt_paths.items():
        if _has_symlink_component(freeze_dir, path) or not path.is_file():
            continue
        raw = _read_bytes(path, report, "RECEIPT_READ")
        value = _load_json_object(path, report, "RECEIPT_JSON")
        if raw is not None:
            receipt_raw[consumer] = raw
        if value is not None:
            receipts[consumer] = value
            received_at = _validate_receipt(value, consumer, report, _relative(freeze_dir, path))
            if received_at is not None:
                receipt_times[consumer] = received_at

    event_documents = [
        (f"events/{path.name}", value)
        for key, path in event_paths.items()
        if (value := events.get(key)) is not None
    ]
    _same_field(event_documents, "demo_revision", manifest.demo_revision, report)
    _same_field(event_documents, "demo_content_sha256", manifest.demo_content_sha256, report)

    approved = events.get("demo-approved")
    frozen = events.get("demo-frozen")
    completed = events.get("transfer-completed")
    if approved is not None:
        raw_scope = approved.get("approved_scope")
        if (
            isinstance(raw_scope, list)
            and raw_scope
            and all(isinstance(item, str) and item for item in raw_scope)
            and len(raw_scope) == len(set(raw_scope))
            and set(raw_scope) != expected_approved_scope
        ):
            report.add(
                "EVENT_SCOPE",
                "events/1-demo-approved.json",
                "approved_scope must exactly match included Client and Game Server coverage IDs",
            )
    if approved is not None and frozen is not None:
        if frozen.get("predecessor_event_id") != approved.get("event_id"):
            report.add("EVENT_SEQUENCE", "events/2-demo-frozen.json", "predecessor must reference demo-approved event_id")
    if frozen is not None and completed is not None:
        if completed.get("predecessor_event_id") != frozen.get("event_id"):
            report.add("EVENT_SEQUENCE", "events/3-transfer-completed.json", "predecessor must reference demo-frozen event_id")
    event_ids = [event.get("event_id") for event in events.values() if isinstance(event.get("event_id"), str)]
    if len(event_ids) != len(set(event_ids)):
        report.add("EVENT_SEQUENCE", "events", "event_id values must be unique")

    if len(timestamps) == 3:
        parsed_by_name = dict(timestamps)
        if not (
            parsed_by_name["demo-approved"]
            <= parsed_by_name["demo-frozen"]
            <= parsed_by_name["transfer-completed"]
        ):
            report.add("EVENT_SEQUENCE", "events", "event timestamps must be non-decreasing")
        if manifest.created_at is not None and not (
            parsed_by_name["demo-approved"]
            <= manifest.created_at
            <= parsed_by_name["demo-frozen"]
        ):
            report.add(
                "EVENT_SEQUENCE",
                "manifest.json",
                "manifest created_at must be between demo-approved and demo-frozen",
            )
        for consumer, received_at in receipt_times.items():
            if not (
                parsed_by_name["demo-frozen"]
                <= received_at
                <= parsed_by_name["transfer-completed"]
            ):
                report.add(
                    "EVENT_SEQUENCE",
                    f"receipts/{consumer}.json",
                    "received_at must be between demo-frozen and transfer-completed",
                )

    manifest_digest = _sha256(manifest_raw)
    if frozen is not None:
        if frozen.get("freeze_id") != manifest.freeze_id:
            report.add("EVENT_CONSISTENCY", "events/2-demo-frozen.json", "freeze_id must match manifest")
        expected_fields = {
            "manifest_sha256": manifest_digest,
            "common_sha256": manifest.bundle_hashes.get("common"),
            "client_sha256": manifest.bundle_hashes.get("client"),
            "game_server_sha256": manifest.bundle_hashes.get("game-server"),
        }
        for key, expected in expected_fields.items():
            if frozen.get(key) != expected:
                report.add("EVENT_CONSISTENCY", "events/2-demo-frozen.json", f"{key} must match manifest")
    if completed is not None:
        if completed.get("freeze_id") != manifest.freeze_id:
            report.add("EVENT_CONSISTENCY", "events/3-transfer-completed.json", "freeze_id must match manifest")
        if completed.get("destinations") != manifest.destinations:
            report.add("EVENT_CONSISTENCY", "events/3-transfer-completed.json", "destinations must match manifest")
        for consumer, field_name in (
            ("client", "client_receipt_sha256"),
            ("game-server", "game_server_receipt_sha256"),
        ):
            raw = receipt_raw.get(consumer)
            if raw is not None and completed.get(field_name) != _sha256(raw):
                report.add("EVENT_CONSISTENCY", "events/3-transfer-completed.json", f"{field_name} must match receipt bytes")

    for consumer, receipt in receipts.items():
        receipt_path = f"receipts/{consumer}.json"
        expected_destination = manifest.destinations.get(consumer)
        if receipt.get("freeze_id") != manifest.freeze_id:
            report.add("RECEIPT", receipt_path, "freeze_id must match manifest")
        if receipt.get("destination") != expected_destination:
            report.add("RECEIPT", receipt_path, "destination must match manifest")
        if isinstance(receipt.get("destination"), str):
            suffix = f"/{manifest.freeze_id}/"
            if not receipt["destination"].endswith(suffix):
                report.add("DESTINATION", receipt_path, f"destination must end with {suffix}")
        if receipt.get("manifest_sha256") != manifest_digest:
            report.add("RECEIPT", receipt_path, "manifest_sha256 must match manifest bytes")
        if receipt.get("common_sha256") != manifest.bundle_hashes.get("common"):
            report.add("RECEIPT", receipt_path, "common_sha256 must match manifest common bundle")
        if receipt.get("assigned_bundle_sha256") != manifest.bundle_hashes.get(consumer):
            report.add("RECEIPT", receipt_path, "assigned_bundle_sha256 must match assigned manifest bundle")
        assigned = [
            entry
            for entry in manifest.entries
            if entry.audience in {"common", consumer, "reference"}
        ]
        expected_file_count = 1 + len(assigned)  # root manifest + delivered partitions/policy
        if receipt.get("file_count") != expected_file_count:
            report.add(
                "RECEIPT",
                receipt_path,
                f"file_count must equal target delivery inventory ({expected_file_count})",
            )
        expected_bytes = len(manifest_raw) + sum(entry.byte_count for entry in assigned)
        if receipt.get("byte_count") != expected_bytes:
            report.add("RECEIPT", receipt_path, f"byte_count must equal target delivery bytes ({expected_bytes})")


def verify_cutover(freeze_dir: Path, events_dir: Optional[Path] = None) -> VerificationReport:
    """Audit one explicit immutable freeze directory without writing to it."""

    report = VerificationReport("cutover")
    if events_dir is None:
        report.add("EVENTS_DIR", "<global>", "cutover requires an explicit events audit directory")
        return report.finish()
    if freeze_dir.is_symlink():
        report.add("FREEZE_LAYOUT", str(freeze_dir), "freeze directory may not be a symlink")
        return report.finish()
    freeze_dir = freeze_dir.resolve(strict=False)
    if events_dir.is_symlink():
        report.add("EVENTS_DIR", str(events_dir), "events audit directory may not be a symlink")
        return report.finish()
    events_dir = events_dir.resolve(strict=False)
    _validate_freeze_layout(freeze_dir, report)
    manifest_path = freeze_dir / "manifest.json"
    if manifest_path.is_symlink() or not manifest_path.is_file():
        return report.finish()
    manifest_raw = _read_bytes(manifest_path, report, "MANIFEST_READ")
    manifest_json = _load_json_object(manifest_path, report, "MANIFEST_JSON")
    if manifest_raw is None or manifest_json is None:
        return report.finish()
    report.manifest_sha256 = _sha256(manifest_raw)
    manifest = _validate_manifest(manifest_json, report, "manifest.json")
    if manifest is None:
        return report.finish()
    if freeze_dir.name != manifest.freeze_id:
        report.add(
            "FREEZE_ID_PATH",
            str(freeze_dir),
            f"freeze directory basename must equal manifest freeze_id ({manifest.freeze_id})",
        )

    _validate_required_export_inventory(manifest.entries, report, "freeze manifest")
    partitions = _validate_manifest_files(freeze_dir, manifest, report)
    _validate_markdown_links(freeze_dir, freeze_dir, report)
    _validate_consumer_link_closure(freeze_dir, partitions, report)
    _validate_rule_documents(freeze_dir, report)
    client_scope = _validate_coverage_file(
        freeze_dir / "client" / "demo-experience-map.md",
        report,
        "client/demo-experience-map.md",
        allow_blocked=False,
    )
    server_scope = _validate_coverage_file(
        freeze_dir / "game-server" / "domain-coverage.md",
        report,
        "game-server/domain-coverage.md",
        allow_blocked=False,
    )
    expected_approved_scope = {f"client:{coverage_id}" for coverage_id in client_scope}
    expected_approved_scope.update(
        f"game-server:{coverage_id}" for coverage_id in server_scope
    )
    _validate_cutover_documents(
        freeze_dir,
        events_dir,
        manifest,
        manifest_raw,
        expected_approved_scope,
        report,
    )
    return report.finish()


def verify(
    root: Path,
    mode: str = "prepare",
    freeze_dir: Optional[Path] = None,
    events_dir: Optional[Path] = None,
) -> VerificationReport:
    if mode == "prepare":
        return verify_prepare(root)
    if mode == "cutover":
        if freeze_dir is None:
            report = VerificationReport("cutover")
            report.add("FREEZE_DIR", "<global>", "cutover requires an explicit freeze directory")
            return report.finish()
        resolved = freeze_dir if freeze_dir.is_absolute() else root / freeze_dir
        if events_dir is None:
            report = VerificationReport("cutover")
            report.add("EVENTS_DIR", "<global>", "cutover requires an explicit events audit directory")
            return report.finish()
        resolved_events = events_dir if events_dir.is_absolute() else root / events_dir
        return verify_cutover(resolved, resolved_events)
    raise ValueError(f"unsupported mode: {mode}")


def _validate_official_cutover_paths(
    root: Path,
    freeze_dir: Path,
    events_dir: Path,
) -> VerificationReport:
    """Bind an authorized CLI cutover to the repository's single official roots."""

    report = VerificationReport("cutover")
    repository = root.resolve(strict=False)
    transition_root = repository / "docs" / "production-transition"
    canonical_freezes = transition_root / "freezes"
    canonical_events = transition_root / "governance" / "audit-events"
    selected_freeze = (
        freeze_dir if freeze_dir.is_absolute() else repository / freeze_dir
    ).resolve(strict=False)
    selected_events = (
        events_dir if events_dir.is_absolute() else repository / events_dir
    ).resolve(strict=False)

    if selected_events != canonical_events.resolve(strict=False):
        report.add(
            "ONE_SHOT_LAYOUT",
            str(events_dir),
            "official events must use docs/production-transition/governance/audit-events",
        )
    if selected_freeze.parent != canonical_freezes.resolve(strict=False):
        report.add(
            "ONE_SHOT_LAYOUT",
            str(freeze_dir),
            "official freeze must be an immediate child of docs/production-transition/freezes",
        )

    freeze_entries: List[Path] = []
    try:
        if canonical_freezes.is_dir() and not canonical_freezes.is_symlink():
            freeze_entries = sorted(canonical_freezes.iterdir())
    except OSError as exc:
        report.add("ONE_SHOT_LAYOUT", str(canonical_freezes), f"cannot inspect official freezes: {exc}")
    if len(freeze_entries) != 1 or freeze_entries[0].resolve(strict=False) != selected_freeze:
        report.add(
            "ONE_SHOT_LAYOUT",
            str(canonical_freezes),
            "official freezes root must contain exactly the selected freeze and no second candidate",
        )

    governance = transition_root / "governance"
    try:
        event_roots = [
            path
            for path in governance.iterdir()
            if path.name.lower().startswith("audit-events")
        ] if governance.is_dir() else []
    except OSError as exc:
        report.add("ONE_SHOT_LAYOUT", str(governance), f"cannot inspect event roots: {exc}")
        event_roots = []
    if (
        len(event_roots) != 1
        or event_roots[0].resolve(strict=False) != canonical_events.resolve(strict=False)
    ):
        report.add(
            "ONE_SHOT_LAYOUT",
            str(governance),
            "governance must contain exactly one canonical audit-events root",
        )
    return report.finish()


def _print_report(report: VerificationReport) -> None:
    result = "PASS" if report.ok else "FAIL"
    print(f"Production transition {report.mode}: {result}")
    for diagnostic in report.diagnostics:
        print(diagnostic.format())
    for partition in ("common", "client", "game-server", "policy"):
        if partition in report.partition_hashes:
            count = len(report.partitions.get(partition, []))
            print(f"partition={partition} files={count} sha256={report.partition_hashes[partition]}")
    if report.manifest_sha256 is not None:
        print(f"manifest_sha256={report.manifest_sha256}")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Read-only audit for the owner-gated one-time production transition."
    )
    parser.add_argument("mode", choices=MODES, help="living preparation audit or immutable freeze audit")
    parser.add_argument(
        "--project-owner-authorized",
        action="store_true",
        help="confirm that the current Project owner request explicitly authorizes this audit",
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="repository root for prepare, and base for a relative --freeze-dir",
    )
    parser.add_argument(
        "--freeze-dir",
        type=Path,
        help="explicit freeze directory to audit in cutover mode",
    )
    parser.add_argument(
        "--events-dir",
        type=Path,
        help="explicit external audit directory containing the three Project owner events",
    )
    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = build_parser().parse_args(argv)
    if not args.project_owner_authorized:
        print(
            "Production transition verification: SKIP "
            "(--project-owner-authorized was not supplied; no transition files or Git state were read)"
        )
        return 0
    if args.mode == "cutover" and (args.freeze_dir is None or args.events_dir is None):
        print("Production transition cutover: FAIL")
        print("[CUTOVER_INPUT] <global>: cutover requires explicit --freeze-dir and --events-dir")
        return 2

    if args.mode == "cutover":
        assert args.freeze_dir is not None and args.events_dir is not None
        path_report = _validate_official_cutover_paths(
            args.root,
            args.freeze_dir,
            args.events_dir,
        )
        if not path_report.ok:
            _print_report(path_report)
            return 1

    report = verify(args.root, args.mode, args.freeze_dir, args.events_dir)
    _print_report(report)
    return 0 if report.ok else 1


if __name__ == "__main__":
    sys.exit(main())
