#!/usr/bin/env python3
"""Read-only verifier for the Dreamsquad production-transition dossier.

The verifier has two modes:

* ``prepare`` validates structure, provenance, hashes, paths, freshness claims,
  and package graph integrity while allowing explicitly incomplete/stale/blocked
  preparation records.
* ``cutover`` applies the strict one-time cutover gate to every included record.

It never writes a manifest, creates a freeze, or touches either production
repository.  The deterministic manifest and package digests returned here are
an in-memory dry-run only.
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Dict, Iterable, List, Mapping, Optional, Sequence, Set, Tuple


SCHEMA_VERSIONS = {1, "1", "1.0"}
MODES = {"prepare", "cutover"}
TRANSITION_STATES = {
    "preparing",
    "cutover_candidate",
    "cutover_in_progress",
    "cutover_complete",
}
PACKAGE_ORDER = ("shared", "client", "game-server", "references")
PACKAGES = set(PACKAGE_ORDER)
DESTINATION_TEMPLATES = {
    "client": "somnia-client/docs/migration-input/dreamsquad-demo/<freeze-id>/",
    "game-server": "somnia-game-server/docs/migration-input/dreamsquad-demo/<freeze-id>/",
}
DRY_RUN_FREEZE_ID = "DRY-RUN-FREEZE"
DRY_RUN_TRANSITION_ID = "DRY-RUN-TRANSITION"
DRY_RUN_CREATED_AT = "1970-01-01T00:00:00Z"
FRESHNESS_VALUES = {"current", "stale", "historical"}
REVIEW_STATUS_VALUES = {"draft", "review_requested", "reviewed", "historical", "stale"}
DISPOSITION_VALUES = {"candidate", "include", "defer", "exclude", "reference"}
COMPLETENESS_VALUES = {"none", "partial", "complete"}
READINESS_VALUES = {"blocked", "provisional", "ready"}
EXECUTION_STAGES = {
    "demo-pre-freeze",
    "production-client-wave",
    "production-server-wave",
    "production-release",
}
DECISION_STATUSES = {
    "open",
    "proposed",
    "deferred",
    "provisional",
    "conditional",
    "decided",
}
REVIEW_OUTCOMES = {"approved", "changes_requested", "deferred", "rejected"}

REQUIRED_RECORD_FIELDS = {
    "id",
    "package",
    "source_path",
    "target_path",
    "owner",
    "consumer",
    "required_reviewers",
    "as_of_commit",
    "document_revision",
    "watch_paths",
    "freshness",
    "review_status",
    "disposition",
    "completeness",
    "readiness",
    "depends_on",
    "blocking_decisions",
    "areas",
    "references",
    "sha256",
    "implementation_wave",
    "execution_stage",
    "cutover_blocking",
}

REQUIRED_REVIEW_FIELDS = {
    "area_id",
    "card_id",
    "document_revision",
    "source_commit",
    "reviewer_role",
    "reviewed_by",
    "outcome",
    "approval",
}

REQUIRED_DECISION_FIELDS = {
    "id",
    "status",
    "owner",
    "decision",
    "blocks_cutover",
    "affected_records",
    "as_of_commit",
}
FULL_OBJECT_ID = re.compile(r"^[0-9a-f]{40}(?:[0-9a-f]{24})?$")
FULL_COMMIT_ID = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
MANIFEST_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")


@dataclass(frozen=True)
class Diagnostic:
    severity: str
    code: str
    message: str
    record_id: Optional[str] = None

    def sort_key(self) -> Tuple[str, str, str, str]:
        severity_rank = "0" if self.severity == "error" else "1"
        return (severity_rank, self.code, self.record_id or "", self.message)

    def as_dict(self) -> Dict[str, Any]:
        value: Dict[str, Any] = {
            "severity": self.severity,
            "code": self.code,
            "message": self.message,
        }
        if self.record_id is not None:
            value["record_id"] = self.record_id
        return value


@dataclass(frozen=True)
class Record:
    record_id: str
    package: str
    source_path: str
    target_path: str
    owner: str
    consumer: Tuple[str, ...]
    required_reviewers: Tuple[str, ...]
    as_of_commit: str
    document_revision: str
    watch_paths: Tuple[str, ...]
    freshness: str
    review_status: str
    disposition: str
    completeness: str
    readiness: str
    depends_on: Tuple[str, ...]
    blocking_decisions: Tuple[str, ...]
    areas: Tuple[str, ...]
    references: Tuple[str, ...]
    expected_sha256: str
    implementation_wave: str
    execution_stage: str
    cutover_blocking: bool


@dataclass(frozen=True)
class Artifact:
    record_id: str
    package: str
    source_path: str
    target_path: str
    sha256: str
    size: int
    as_of_commit: str
    document_revision: str
    depends_on: Tuple[str, ...]
    references: Tuple[str, ...]
    content: bytes

    def manifest_dict(self) -> Dict[str, Any]:
        return {
            "id": self.record_id,
            "package": self.package,
            "source_path": self.source_path,
            "target_path": self.target_path,
            "size": self.size,
            "sha256": self.sha256,
            "as_of_commit": self.as_of_commit,
            "document_revision": self.document_revision,
            "depends_on": list(self.depends_on),
            "references": list(self.references),
        }

    def package_file_dict(self) -> Dict[str, Any]:
        return {
            "record_id": self.record_id,
            "path": self.target_path,
            "size": self.size,
            "sha256": self.sha256,
        }


@dataclass
class VerificationReport:
    mode: str
    transition_state: str
    diagnostics: List[Diagnostic]
    package_entries: Dict[str, List[Artifact]]
    package_hashes: Dict[str, str]
    target_inventories: Dict[str, List[str]]
    shared_inventory: List[str]
    shared_hash: str
    dry_run_manifest: Dict[str, Any]
    manifest_sha256: str

    @property
    def errors(self) -> List[Diagnostic]:
        return [item for item in self.diagnostics if item.severity == "error"]

    @property
    def warnings(self) -> List[Diagnostic]:
        return [item for item in self.diagnostics if item.severity == "warning"]

    @property
    def ok(self) -> bool:
        return not self.errors

    def as_dict(self) -> Dict[str, Any]:
        return {
            "ok": self.ok,
            "mode": self.mode,
            "transition_state": self.transition_state,
            "diagnostics": [item.as_dict() for item in sorted(self.diagnostics, key=Diagnostic.sort_key)],
            "packages": {
                package: {
                    "files": [entry.package_file_dict() for entry in entries],
                    "aggregate_sha256": self.package_hashes[package],
                }
                for package, entries in sorted(self.package_entries.items())
            },
            "targets": {
                name: list(paths)
                for name, paths in sorted(self.target_inventories.items())
            },
            "shared_inventory": list(self.shared_inventory),
            "shared_sha256": self.shared_hash,
            "dry_run_manifest": self.dry_run_manifest,
            "manifest_sha256": self.manifest_sha256,
        }


def _canonical_json(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _add(
    diagnostics: List[Diagnostic],
    severity: str,
    code: str,
    message: str,
    record_id: Optional[str] = None,
) -> None:
    diagnostics.append(Diagnostic(severity, code, message, record_id))


def _load_json(path: Path, label: str, diagnostics: List[Diagnostic]) -> Optional[Dict[str, Any]]:
    try:
        raw = path.read_text(encoding="utf-8")
    except OSError as exc:
        _add(diagnostics, "error", "JSON_READ_FAILED", f"Cannot read {label} {path}: {exc}")
        return None
    try:
        value = json.loads(raw)
    except json.JSONDecodeError as exc:
        _add(diagnostics, "error", "JSON_INVALID", f"Invalid JSON in {label} {path}: {exc}")
        return None
    if not isinstance(value, dict):
        _add(diagnostics, "error", "JSON_ROOT_TYPE", f"{label} root must be an object: {path}")
        return None
    return value


def _valid_schema_version(value: Any) -> bool:
    return value in SCHEMA_VERSIONS


def _validate_registry_metadata(
    registry: Mapping[str, Any],
    diagnostics: List[Diagnostic],
) -> Tuple[str, Dict[str, str]]:
    required = {"candidate_source_commit", "destinations"}
    missing = sorted(required - set(registry))
    if missing:
        _add(
            diagnostics,
            "error",
            "REGISTRY_MISSING_FIELDS",
            "Missing required registry fields: " + ", ".join(missing),
        )

    raw_commit = registry.get("candidate_source_commit")
    if not isinstance(raw_commit, str) or not FULL_COMMIT_ID.fullmatch(raw_commit):
        _add(
            diagnostics,
            "error",
            "CANDIDATE_SOURCE_COMMIT",
            "candidate_source_commit must be exactly 40 lowercase hexadecimal characters",
        )
        candidate_source_commit = "0" * 40
    else:
        candidate_source_commit = raw_commit

    raw_destinations = registry.get("destinations")
    if not isinstance(raw_destinations, dict):
        _add(
            diagnostics,
            "error",
            "DESTINATIONS_TYPE",
            "destinations must be an object",
        )
        return candidate_source_commit, dict(DESTINATION_TEMPLATES)

    actual_keys = set(raw_destinations)
    expected_keys = set(DESTINATION_TEMPLATES)
    if actual_keys != expected_keys:
        missing_keys = sorted(expected_keys - actual_keys)
        extra_keys = sorted(actual_keys - expected_keys)
        details: List[str] = []
        if missing_keys:
            details.append("missing=" + ",".join(missing_keys))
        if extra_keys:
            details.append("extra=" + ",".join(extra_keys))
        _add(
            diagnostics,
            "error",
            "DESTINATION_KEYS",
            "destinations must contain exactly client and game-server"
            + (": " + "; ".join(details) if details else ""),
        )

    templates: Dict[str, str] = {}
    for target, expected in DESTINATION_TEMPLATES.items():
        value = raw_destinations.get(target)
        if not isinstance(value, str):
            _add(
                diagnostics,
                "error",
                "DESTINATION_TEMPLATE",
                f"destinations.{target} must equal {expected!r}",
            )
            templates[target] = expected
            continue
        templates[target] = value
        if value != expected:
            _add(
                diagnostics,
                "error",
                "DESTINATION_TEMPLATE",
                f"destinations.{target} must equal {expected!r}; got {value!r}",
            )
    return candidate_source_commit, templates


def _validate_manifest_id(
    value: Any,
    field: str,
    fallback: str,
    diagnostics: List[Diagnostic],
) -> str:
    if not isinstance(value, str) or not MANIFEST_ID.fullmatch(value):
        _add(
            diagnostics,
            "error",
            "DRY_RUN_ID",
            f"{field} must match {MANIFEST_ID.pattern}",
        )
        return fallback
    return value


def _expand_destinations(
    templates: Mapping[str, str],
    freeze_id: str,
    diagnostics: List[Diagnostic],
) -> Dict[str, str]:
    expanded: Dict[str, str] = {}
    for target, expected_template in DESTINATION_TEMPLATES.items():
        template = templates.get(target, expected_template)
        if template.count("<freeze-id>") != 1:
            _add(
                diagnostics,
                "error",
                "DESTINATION_EXPANSION_MISMATCH",
                f"destinations.{target} must contain <freeze-id> exactly once",
            )
        actual = template.replace("<freeze-id>", freeze_id)
        expected = expected_template.replace("<freeze-id>", freeze_id)
        if actual != expected or "<freeze-id>" in actual:
            _add(
                diagnostics,
                "error",
                "DESTINATION_EXPANSION_MISMATCH",
                f"destinations.{target} did not expand to {expected!r}; got {actual!r}",
            )
        expanded[target] = actual
    return expanded


def _parse_repo_path(
    raw: Any,
    field: str,
    diagnostics: List[Diagnostic],
    record_id: str,
) -> Optional[str]:
    if not isinstance(raw, str) or not raw:
        _add(diagnostics, "error", "PATH_TYPE", f"{field} must be a non-empty string", record_id)
        return None
    if "\\" in raw:
        _add(diagnostics, "error", "PATH_NOT_POSIX", f"{field} must use '/' separators: {raw}", record_id)
        return None
    path = PurePosixPath(raw)
    if path.is_absolute() or any(part in {"", ".", ".."} for part in path.parts):
        _add(diagnostics, "error", "PATH_ESCAPE", f"{field} must be a contained relative path: {raw}", record_id)
        return None
    if ":" in path.parts[0] or path.as_posix() != raw:
        _add(diagnostics, "error", "PATH_NON_CANONICAL", f"{field} is not canonical: {raw}", record_id)
        return None
    return path.as_posix()


def _parse_string(
    value: Any,
    field: str,
    diagnostics: List[Diagnostic],
    record_id: str,
) -> Optional[str]:
    if not isinstance(value, str) or not value.strip():
        _add(diagnostics, "error", "FIELD_TYPE", f"{field} must be a non-empty string", record_id)
        return None
    return value


def _parse_string_list(
    value: Any,
    field: str,
    diagnostics: List[Diagnostic],
    record_id: str,
    allow_empty: bool,
) -> Optional[Tuple[str, ...]]:
    if not isinstance(value, list) or any(not isinstance(item, str) or not item for item in value):
        _add(diagnostics, "error", "FIELD_TYPE", f"{field} must be an array of non-empty strings", record_id)
        return None
    if not allow_empty and not value:
        _add(diagnostics, "error", "FIELD_EMPTY", f"{field} must not be empty", record_id)
        return None
    if len(set(value)) != len(value):
        _add(diagnostics, "error", "FIELD_DUPLICATE", f"{field} contains duplicate values", record_id)
        return None
    return tuple(value)


def _parse_record(value: Any, diagnostics: List[Diagnostic], index: int) -> Optional[Record]:
    placeholder = f"records[{index}]"
    if not isinstance(value, dict):
        _add(diagnostics, "error", "RECORD_TYPE", f"{placeholder} must be an object")
        return None
    missing = sorted(REQUIRED_RECORD_FIELDS - set(value))
    record_id = value.get("id") if isinstance(value.get("id"), str) else placeholder
    if missing:
        _add(
            diagnostics,
            "error",
            "RECORD_MISSING_FIELDS",
            "Missing required fields: " + ", ".join(missing),
            record_id,
        )
        return None

    parsed_id = _parse_string(value["id"], "id", diagnostics, record_id)
    package = _parse_string(value["package"], "package", diagnostics, record_id)
    source_path = _parse_repo_path(value["source_path"], "source_path", diagnostics, record_id)
    target_path = _parse_repo_path(value["target_path"], "target_path", diagnostics, record_id)
    owner = _parse_string(value["owner"], "owner", diagnostics, record_id)
    consumer = _parse_string_list(value["consumer"], "consumer", diagnostics, record_id, False)
    reviewers = _parse_string_list(
        value["required_reviewers"], "required_reviewers", diagnostics, record_id, False
    )
    watch_paths = _parse_string_list(value["watch_paths"], "watch_paths", diagnostics, record_id, False)
    depends_on = _parse_string_list(value["depends_on"], "depends_on", diagnostics, record_id, True)
    blockers = _parse_string_list(
        value["blocking_decisions"], "blocking_decisions", diagnostics, record_id, True
    )
    areas = _parse_string_list(value["areas"], "areas", diagnostics, record_id, False)
    references = _parse_string_list(value["references"], "references", diagnostics, record_id, True)
    as_of_commit = _parse_string(value["as_of_commit"], "as_of_commit", diagnostics, record_id)
    document_revision = _parse_string(
        value["document_revision"], "document_revision", diagnostics, record_id
    )
    implementation_wave = _parse_string(
        value["implementation_wave"], "implementation_wave", diagnostics, record_id
    )

    if package is not None and package not in PACKAGES:
        _add(diagnostics, "error", "PACKAGE_VALUE", f"Unknown package: {package}", record_id)
    if package is not None and target_path is not None:
        required_prefix = {
            "references": "references/",
            "shared": "shared/",
            "client": "client/",
            "game-server": "game-server/",
        }.get(package)
        if required_prefix is not None and not target_path.startswith(required_prefix):
            _add(
                diagnostics,
                "error",
                "TARGET_PACKAGE_PREFIX",
                f"{package} target_path must start with {required_prefix}: {target_path}",
                record_id,
            )
    for field, allowed in (
        ("freshness", FRESHNESS_VALUES),
        ("review_status", REVIEW_STATUS_VALUES),
        ("disposition", DISPOSITION_VALUES),
        ("completeness", COMPLETENESS_VALUES),
        ("readiness", READINESS_VALUES),
        ("execution_stage", EXECUTION_STAGES),
    ):
        if value[field] not in allowed:
            _add(diagnostics, "error", "ENUM_VALUE", f"Invalid {field}: {value[field]!r}", record_id)
    if as_of_commit is not None and not FULL_OBJECT_ID.fullmatch(as_of_commit):
        _add(diagnostics, "error", "OBJECT_ID", "as_of_commit must be a full lowercase Git object ID", record_id)
    if document_revision is not None and not SHA256.fullmatch(document_revision):
        _add(
            diagnostics,
            "error",
            "SHA256_FORMAT",
            "document_revision must be 64 lowercase hex characters",
            record_id,
        )
    expected_hash = value["sha256"]
    if not isinstance(expected_hash, str) or not SHA256.fullmatch(expected_hash):
        _add(diagnostics, "error", "SHA256_FORMAT", "sha256 must be 64 lowercase hex characters", record_id)
    if not isinstance(value["cutover_blocking"], bool):
        _add(diagnostics, "error", "FIELD_TYPE", "cutover_blocking must be boolean", record_id)

    required_values = (
        parsed_id,
        package,
        source_path,
        target_path,
        owner,
        consumer,
        reviewers,
        watch_paths,
        depends_on,
        blockers,
        areas,
        references,
        as_of_commit,
        document_revision,
        implementation_wave,
    )
    if any(item is None for item in required_values):
        return None
    if package not in PACKAGES or value["freshness"] not in FRESHNESS_VALUES:
        return None
    if value["review_status"] not in REVIEW_STATUS_VALUES or value["disposition"] not in DISPOSITION_VALUES:
        return None
    if value["completeness"] not in COMPLETENESS_VALUES or value["readiness"] not in READINESS_VALUES:
        return None
    if value["execution_stage"] not in EXECUTION_STAGES:
        return None
    if not FULL_OBJECT_ID.fullmatch(as_of_commit) or not SHA256.fullmatch(document_revision):
        return None
    if not isinstance(expected_hash, str) or not SHA256.fullmatch(expected_hash):
        return None
    if not isinstance(value["cutover_blocking"], bool):
        return None

    return Record(
        record_id=parsed_id,
        package=package,
        source_path=source_path,
        target_path=target_path,
        owner=owner,
        consumer=consumer,
        required_reviewers=reviewers,
        as_of_commit=as_of_commit,
        document_revision=document_revision,
        watch_paths=watch_paths,
        freshness=value["freshness"],
        review_status=value["review_status"],
        disposition=value["disposition"],
        completeness=value["completeness"],
        readiness=value["readiness"],
        depends_on=depends_on,
        blocking_decisions=blockers,
        areas=areas,
        references=references,
        expected_sha256=expected_hash,
        implementation_wave=implementation_wave,
        execution_stage=value["execution_stage"],
        cutover_blocking=value["cutover_blocking"],
    )


def _is_contained_file(root: Path, relative: str) -> Tuple[Optional[Path], Optional[str]]:
    candidate = root.joinpath(*PurePosixPath(relative).parts)
    cursor = root
    for part in PurePosixPath(relative).parts:
        cursor = cursor / part
        if cursor.is_symlink():
            return None, f"symlink is not allowed in source path: {relative}"
    try:
        resolved = candidate.resolve(strict=True)
        resolved.relative_to(root.resolve(strict=True))
    except (OSError, ValueError) as exc:
        return None, f"source path is missing or escapes repository: {relative} ({exc})"
    if not resolved.is_file():
        return None, f"source path is not a regular file: {relative}"
    return resolved, None


def _path_matches_watch(path: str, watch: str) -> bool:
    normalized_path = PurePosixPath(path).as_posix()
    normalized_watch = watch.replace("\\", "/")
    if normalized_watch.endswith("/"):
        return normalized_path.startswith(normalized_watch)
    if not any(character in normalized_watch for character in "*?["):
        return normalized_path == normalized_watch or normalized_path.startswith(normalized_watch + "/")
    return fnmatch.fnmatchcase(normalized_path, normalized_watch) or PurePosixPath(normalized_path).match(
        normalized_watch
    )


def _git_changed_paths(root: Path, commit: str) -> Tuple[Optional[List[str]], Optional[str]]:
    diff_command = [
        "git",
        "diff",
        "--name-only",
        "--no-renames",
        commit,
        "--",
    ]
    completed = subprocess.run(
        diff_command,
        cwd=str(root),
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
    )
    if completed.returncode != 0:
        detail = completed.stderr.strip() or f"git exited {completed.returncode}"
        return None, detail
    untracked = subprocess.run(
        ["git", "ls-files", "--others", "--exclude-standard"],
        cwd=str(root),
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
    )
    if untracked.returncode != 0:
        detail = untracked.stderr.strip() or f"git exited {untracked.returncode}"
        return None, detail
    paths = {
        line.strip().replace("\\", "/")
        for output in (completed.stdout, untracked.stdout)
        for line in output.splitlines()
        if line.strip()
    }
    return sorted(paths), None


def _git_object_type(root: Path, object_id: str) -> Tuple[Optional[str], Optional[str]]:
    completed = subprocess.run(
        ["git", "cat-file", "-t", object_id],
        cwd=str(root),
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if completed.returncode != 0:
        detail = completed.stderr.decode("utf-8", errors="replace").strip()
        return None, detail or f"git cat-file exited {completed.returncode}"
    return completed.stdout.decode("ascii", errors="replace").strip(), None


def _git_blob_at_commit(
    root: Path,
    commit: str,
    source_path: str,
) -> Tuple[Optional[bytes], Optional[str]]:
    listing = subprocess.run(
        ["git", "ls-tree", "-z", "--full-tree", commit, "--", source_path],
        cwd=str(root),
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if listing.returncode != 0:
        detail = listing.stderr.decode("utf-8", errors="replace").strip()
        return None, detail or f"git ls-tree exited {listing.returncode}"

    expected_path = source_path.encode("utf-8")
    blob_id: Optional[str] = None
    for row in listing.stdout.split(b"\0"):
        if not row or b"\t" not in row:
            continue
        metadata, raw_path = row.split(b"\t", 1)
        fields = metadata.split(b" ")
        if raw_path != expected_path or len(fields) != 3 or fields[1] != b"blob":
            continue
        try:
            blob_id = fields[2].decode("ascii")
        except UnicodeDecodeError:
            continue
        break
    if blob_id is None:
        return None, "path is not a tracked blob at candidate_source_commit"

    blob = subprocess.run(
        ["git", "cat-file", "blob", blob_id],
        cwd=str(root),
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if blob.returncode != 0:
        detail = blob.stderr.decode("utf-8", errors="replace").strip()
        return None, detail or f"git cat-file blob exited {blob.returncode}"
    return blob.stdout, None


def _validate_cutover_git_provenance(
    root: Path,
    candidate_source_commit: str,
    records: Mapping[str, Record],
    source_artifacts: Mapping[str, Artifact],
    diagnostics: List[Diagnostic],
    git_blobs_by_commit: Optional[Mapping[str, Optional[Mapping[str, bytes]]]],
) -> None:
    commit_blobs: Optional[Mapping[str, bytes]]
    if git_blobs_by_commit is not None:
        commit_blobs = git_blobs_by_commit.get(candidate_source_commit)
        if commit_blobs is None:
            _add(
                diagnostics,
                "error",
                "CUTOVER_CANDIDATE_COMMIT_INVALID",
                f"candidate_source_commit is not an injected commit object: {candidate_source_commit}",
            )
            return
    else:
        object_type, error = _git_object_type(root, candidate_source_commit)
        if error is not None or object_type != "commit":
            detail = error or f"object type is {object_type!r}, not 'commit'"
            _add(
                diagnostics,
                "error",
                "CUTOVER_CANDIDATE_COMMIT_INVALID",
                f"candidate_source_commit is not a Git commit object: {candidate_source_commit} ({detail})",
            )
            return
        commit_blobs = None

    selected_ids = {
        record_id
        for record_id, record in records.items()
        if record.disposition == "include"
    }
    for record_id in sorted(selected_ids):
        artifact = source_artifacts.get(record_id)
        if artifact is None:
            continue
        if git_blobs_by_commit is not None:
            assert commit_blobs is not None
            if artifact.source_path not in commit_blobs:
                committed_bytes = None
                error = "path is not present in the injected commit blob map"
            else:
                committed_bytes = commit_blobs[artifact.source_path]
                error = None
        else:
            committed_bytes, error = _git_blob_at_commit(
                root,
                candidate_source_commit,
                artifact.source_path,
            )
        if error is not None or committed_bytes is None:
            _add(
                diagnostics,
                "error",
                "CUTOVER_SOURCE_BLOB_MISSING",
                f"Selected source is not a tracked blob at candidate_source_commit: {artifact.source_path}"
                + (f" ({error})" if error else ""),
                record_id,
            )
        elif committed_bytes != artifact.content:
            _add(
                diagnostics,
                "error",
                "CUTOVER_SOURCE_BLOB_MISMATCH",
                f"Working-tree bytes differ from candidate commit blob: {artifact.source_path}",
                record_id,
            )


def _parse_decisions(value: Optional[Dict[str, Any]], diagnostics: List[Diagnostic]) -> Dict[str, Dict[str, Any]]:
    if value is None:
        return {}
    if not _valid_schema_version(value.get("schema_version")):
        _add(diagnostics, "error", "SCHEMA_VERSION", "decisions.json schema_version must be 1 or 1.0")
    rows = value.get("decisions")
    if not isinstance(rows, list):
        _add(diagnostics, "error", "DECISIONS_TYPE", "decisions must be an array")
        return {}
    result: Dict[str, Dict[str, Any]] = {}
    for index, row in enumerate(rows):
        if not isinstance(row, dict):
            _add(diagnostics, "error", "DECISION_TYPE", f"decisions[{index}] must be an object")
            continue
        missing = sorted(REQUIRED_DECISION_FIELDS - set(row))
        decision_id = row.get("id") if isinstance(row.get("id"), str) else f"decisions[{index}]"
        if missing:
            _add(
                diagnostics,
                "error",
                "DECISION_MISSING_FIELDS",
                "Missing required fields: " + ", ".join(missing),
                decision_id,
            )
            continue
        if not isinstance(row["id"], str) or not row["id"]:
            _add(diagnostics, "error", "DECISION_ID", "Decision id must be a non-empty string", decision_id)
            continue
        if row["id"] in result:
            _add(diagnostics, "error", "DECISION_DUPLICATE", f"Duplicate decision id: {row['id']}", row["id"])
            continue
        if row["status"] not in DECISION_STATUSES:
            _add(diagnostics, "error", "DECISION_STATUS", f"Invalid decision status: {row['status']!r}", row["id"])
            continue
        if not isinstance(row["owner"], str) or not row["owner"]:
            _add(diagnostics, "error", "DECISION_OWNER", "Decision owner must be a non-empty string", row["id"])
            continue
        if row["decision"] is not None and (
            not isinstance(row["decision"], str) or not row["decision"]
        ):
            _add(
                diagnostics,
                "error",
                "DECISION_VALUE",
                "decision must be null or a non-empty string",
                row["id"],
            )
            continue
        if row["status"] == "decided" and row["decision"] is None:
            _add(
                diagnostics,
                "error",
                "DECISION_VALUE",
                "status=decided requires a non-empty decision",
                row["id"],
            )
            continue
        if not isinstance(row["blocks_cutover"], bool):
            _add(diagnostics, "error", "FIELD_TYPE", "blocks_cutover must be boolean", row["id"])
            continue
        affected_records = row["affected_records"]
        if (
            not isinstance(affected_records, list)
            or any(not isinstance(item, str) or not item for item in affected_records)
            or len(set(affected_records)) != len(affected_records)
        ):
            _add(
                diagnostics,
                "error",
                "DECISION_AFFECTED_RECORDS",
                "affected_records must be an array of unique non-empty record IDs",
                row["id"],
            )
            continue
        if not isinstance(row["as_of_commit"], str) or not FULL_OBJECT_ID.fullmatch(
            row["as_of_commit"]
        ):
            _add(
                diagnostics,
                "error",
                "OBJECT_ID",
                "Decision as_of_commit must be a full lowercase Git object ID",
                row["id"],
            )
            continue
        result[row["id"]] = row
    return result


def _parse_reviews(
    value: Optional[Dict[str, Any]],
    diagnostics: List[Diagnostic],
) -> List[Dict[str, Any]]:
    if value is None:
        return []
    if not _valid_schema_version(value.get("schema_version")):
        _add(diagnostics, "error", "SCHEMA_VERSION", "reviews.json schema_version must be 1 or 1.0")
    rows = value.get("reviews")
    legacy = value.get("legacy_reviews")
    if not isinstance(rows, list):
        _add(diagnostics, "error", "REVIEWS_TYPE", "reviews must be an array")
        return []
    if not isinstance(legacy, list):
        _add(diagnostics, "error", "LEGACY_REVIEWS_TYPE", "legacy_reviews must be an array")
    result: List[Dict[str, Any]] = []
    seen: Set[Tuple[str, str, str, str, str, str]] = set()
    for index, row in enumerate(rows):
        if not isinstance(row, dict):
            _add(diagnostics, "error", "REVIEW_TYPE", f"reviews[{index}] must be an object")
            continue
        missing = sorted(REQUIRED_REVIEW_FIELDS - set(row))
        card_id = row.get("card_id") if isinstance(row.get("card_id"), str) else f"reviews[{index}]"
        if missing:
            _add(
                diagnostics,
                "error",
                "REVIEW_MISSING_FIELDS",
                "Missing required fields: " + ", ".join(missing),
                card_id,
            )
            continue
        string_fields = REQUIRED_REVIEW_FIELDS - {"approval"}
        if any(not isinstance(row[field], str) or not row[field] for field in string_fields):
            _add(diagnostics, "error", "REVIEW_FIELD_TYPE", "Review string fields must be non-empty", card_id)
            continue
        if not SHA256.fullmatch(row["document_revision"]) or not FULL_OBJECT_ID.fullmatch(
            row["source_commit"]
        ):
            _add(
                diagnostics,
                "error",
                "OBJECT_ID",
                "Review document_revision must be SHA-256 and source_commit a full Git object ID",
                card_id,
            )
            continue
        if row["outcome"] not in REVIEW_OUTCOMES:
            _add(diagnostics, "error", "REVIEW_OUTCOME", f"Invalid review outcome: {row['outcome']!r}", card_id)
            continue
        if not isinstance(row["approval"], bool):
            _add(diagnostics, "error", "REVIEW_APPROVAL_TYPE", "approval must be boolean", card_id)
            continue
        if row["approval"] != (row["outcome"] == "approved"):
            _add(
                diagnostics,
                "error",
                "REVIEW_APPROVAL_CONFLICT",
                "approval=true is valid only with outcome=approved, and approved requires approval=true",
                card_id,
            )
            continue
        key = (
            row["area_id"],
            row["card_id"],
            row["document_revision"],
            row["source_commit"],
            row["reviewer_role"],
            row["reviewed_by"],
        )
        if key in seen:
            _add(diagnostics, "error", "REVIEW_DUPLICATE", f"Duplicate review tuple: {key}", card_id)
            continue
        seen.add(key)
        result.append(row)
    return result


def _validate_decision_record_links(
    records: Mapping[str, Record],
    decisions: Mapping[str, Mapping[str, Any]],
    diagnostics: List[Diagnostic],
) -> None:
    for decision_id, decision in decisions.items():
        for record_id in decision["affected_records"]:
            record = records.get(record_id)
            if record is None:
                _add(
                    diagnostics,
                    "error",
                    "DECISION_AFFECTED_RECORD_UNKNOWN",
                    f"Decision {decision_id} names unknown affected record {record_id}",
                    decision_id,
                )
            elif decision_id not in record.blocking_decisions:
                _add(
                    diagnostics,
                    "error",
                    "DECISION_RECORD_LINK_MISMATCH",
                    f"Decision {decision_id} names {record_id}, but the record does not name the decision in blocking_decisions",
                    record_id,
                )

    for record in records.values():
        for decision_id in record.blocking_decisions:
            decision = decisions.get(decision_id)
            if decision is not None and record.record_id not in decision["affected_records"]:
                _add(
                    diagnostics,
                    "error",
                    "DECISION_RECORD_LINK_MISMATCH",
                    f"Record names decision {decision_id}, but the decision does not include the record in affected_records",
                    record.record_id,
                )


def _dependency_cycles(records: Mapping[str, Record]) -> List[List[str]]:
    state: Dict[str, int] = {}
    stack: List[str] = []
    cycles: List[List[str]] = []

    def visit(record_id: str) -> None:
        current = state.get(record_id, 0)
        if current == 2:
            return
        if current == 1:
            if record_id in stack:
                start = stack.index(record_id)
                cycles.append(stack[start:] + [record_id])
            return
        state[record_id] = 1
        stack.append(record_id)
        record = records[record_id]
        for dependency in record.depends_on:
            if dependency in records:
                visit(dependency)
        stack.pop()
        state[record_id] = 2

    for key in sorted(records):
        visit(key)
    return cycles


def _package_edge_allowed(source: str, target: str) -> bool:
    if source == "references":
        return True
    if source == "shared":
        return target in {"shared", "references"}
    if source == "client":
        return target in {"client", "shared", "references"}
    if source == "game-server":
        return target in {"game-server", "shared", "references"}
    return False


def _package_hash(entries: Sequence[Artifact]) -> str:
    files = [entry.package_file_dict() for entry in entries]
    return _sha256_bytes(_canonical_json(files))


def _consumer_has_target(record: Record, target: str) -> bool:
    normalized = {
        value.strip().casefold().replace("_", "-").replace(" ", "-")
        for value in record.consumer
    }
    if target == "client":
        return bool(normalized & {"client", "somnia-client"})
    if target == "game-server":
        return bool(normalized & {"server", "game-server", "somnia-game-server"})
    return False


def _markdown_without_code(text: str) -> str:
    visible: List[str] = []
    fence_character: Optional[str] = None
    fence_length = 0
    for line in text.splitlines(keepends=True):
        fence = re.match(r"^[ \t]{0,3}(`{3,}|~{3,})", line)
        if fence_character is not None:
            if (
                fence is not None
                and fence.group(1)[0] == fence_character
                and len(fence.group(1)) >= fence_length
            ):
                fence_character = None
                fence_length = 0
            continue
        if fence is not None:
            fence_character = fence.group(1)[0]
            fence_length = len(fence.group(1))
            continue
        visible.append(re.sub(r"`+[^`\n]*`+", "", line))
    return re.sub(r"<!--.*?-->", "", "".join(visible), flags=re.DOTALL)


def _markdown_destination(raw: str) -> Optional[str]:
    value = raw.strip()
    if not value:
        return None
    if value.startswith("<"):
        closing = value.find(">", 1)
        if closing < 0:
            return None
        return value[1:closing].strip() or None
    return value.split(None, 1)[0]


def _markdown_link_destinations(content: bytes) -> List[str]:
    text = _markdown_without_code(content.decode("utf-8", errors="replace"))
    destinations: List[str] = []
    for match in re.finditer(r"!?\[[^\]\n]*\]\(([^)\n]*)\)", text):
        destination = _markdown_destination(match.group(1))
        if destination is not None:
            destinations.append(destination)

    definitions: Dict[str, str] = {}
    for match in re.finditer(
        r"(?m)^[ \t]{0,3}\[([^\]\n]+)\]:[ \t]*(<[^>\n]+>|\S+)",
        text,
    ):
        destination = _markdown_destination(match.group(2))
        if destination is not None:
            definitions[match.group(1).strip().casefold()] = destination
    for match in re.finditer(r"!?\[([^\]\n]+)\]\[([^\]\n]*)\]", text):
        label = (match.group(2) or match.group(1)).strip().casefold()
        destination = definitions.get(label)
        if destination is not None:
            destinations.append(destination)
    return sorted(set(destinations))


def _local_document_link(destination: str) -> Optional[str]:
    value = destination.strip()
    if not value or value.startswith(("#", "/", "\\")):
        return None
    if re.match(r"^[A-Za-z][A-Za-z0-9+.-]*:", value):
        return None
    if any(token in value for token in ("<", ">", "{", "}", "$(", "${")):
        return None
    path = value.split("#", 1)[0].split("?", 1)[0]
    if not path or PurePosixPath(path).suffix.casefold() not in {".md", ".json"}:
        return None
    return path


def _resolve_target_link(source_target_path: str, link_path: str) -> Optional[str]:
    if "\\" in link_path:
        return None
    parts = list(PurePosixPath(source_target_path).parent.parts)
    for part in PurePosixPath(link_path).parts:
        if part in {"", "."}:
            continue
        if part == "..":
            if not parts:
                return None
            parts.pop()
            continue
        parts.append(part)
    return "/".join(parts) if parts else None


def _validate_target_snapshot_closure(
    records: Mapping[str, Record],
    source_artifacts: Mapping[str, Artifact],
    selected_ids: Set[str],
    target_inventories: Mapping[str, Sequence[str]],
    diagnostics: List[Diagnostic],
) -> None:
    inventory_sets = {
        target: set(paths)
        for target, paths in target_inventories.items()
    }
    for record_id in sorted(selected_ids):
        record = records[record_id]
        consumption_targets = [
            target
            for target in ("client", "game-server")
            if _consumer_has_target(record, target)
        ]
        for field, dependency_ids in (
            ("depends_on", record.depends_on),
            ("references", record.references),
        ):
            for dependency_id in dependency_ids:
                dependency = records.get(dependency_id)
                dependency_artifact = source_artifacts.get(dependency_id)
                for target in consumption_targets:
                    if (
                        dependency is None
                        or dependency_id not in selected_ids
                        or dependency_artifact is None
                        or dependency_artifact.target_path not in inventory_sets[target]
                    ):
                        _add(
                            diagnostics,
                            "error",
                            "TARGET_DEPENDENCY_CLOSURE",
                            f"{field} target {dependency_id} is not selected and present in the {target} snapshot",
                            record_id,
                        )

        artifact = source_artifacts.get(record_id)
        if artifact is None or PurePosixPath(artifact.target_path).suffix.casefold() != ".md":
            continue
        destinations = _markdown_link_destinations(artifact.content)
        if not destinations:
            continue
        for target in ("client", "game-server"):
            if artifact.target_path not in inventory_sets[target]:
                continue
            for destination in destinations:
                local_path = _local_document_link(destination)
                if local_path is None:
                    continue
                resolved = _resolve_target_link(artifact.target_path, local_path)
                if resolved is None or resolved not in inventory_sets[target]:
                    _add(
                        diagnostics,
                        "error",
                        "TARGET_LINK_CLOSURE",
                        f"Markdown link {destination!r} resolves to {resolved!r}, which is absent from the {target} snapshot",
                        record_id,
                    )


def _governance_attestation(
    records: Mapping[str, Record],
    selected_ids: Set[str],
    reviews: Sequence[Mapping[str, Any]],
    decisions: Mapping[str, Mapping[str, Any]],
) -> Dict[str, List[Dict[str, Any]]]:
    attested_records = [
        {
            "record_id": record.record_id,
            "package": record.package,
            "source_path": record.source_path,
            "target_path": record.target_path,
            "owner": record.owner,
            "consumer": sorted(record.consumer),
            "required_reviewers": sorted(record.required_reviewers),
            "as_of_commit": record.as_of_commit,
            "document_revision": record.document_revision,
            "watch_paths": sorted(record.watch_paths),
            "freshness": record.freshness,
            "review_status": record.review_status,
            "disposition": record.disposition,
            "completeness": record.completeness,
            "readiness": record.readiness,
            "implementation_wave": record.implementation_wave,
            "execution_stage": record.execution_stage,
            "depends_on": sorted(record.depends_on),
            "references": sorted(record.references),
            "areas": sorted(record.areas),
            "blocking_decisions": sorted(record.blocking_decisions),
            "cutover_blocking": record.cutover_blocking,
        }
        for record in (records[record_id] for record_id in sorted(selected_ids))
    ]

    attested_reviews: List[Dict[str, Any]] = []
    for review in reviews:
        card_id = review["card_id"]
        if card_id not in selected_ids:
            continue
        record = records[card_id]
        if (
            review["document_revision"] != record.document_revision
            or review["source_commit"] != record.as_of_commit
            or review["area_id"] not in record.areas
            or review["reviewer_role"] not in record.required_reviewers
        ):
            continue
        attested_reviews.append(
            {
                "area_id": review["area_id"],
                "card_id": card_id,
                "document_revision": review["document_revision"],
                "source_commit": review["source_commit"],
                "reviewer_role": review["reviewer_role"],
                "reviewed_by": review["reviewed_by"],
                "outcome": review["outcome"],
                "approval": review["approval"],
            }
        )
    attested_reviews.sort(
        key=lambda row: (
            row["card_id"],
            row["area_id"],
            row["document_revision"],
            row["source_commit"],
            row["reviewer_role"],
            row["reviewed_by"],
            row["outcome"],
            row["approval"],
        )
    )

    decision_ids = sorted(
        {
            decision_id
            for record_id in selected_ids
            for decision_id in records[record_id].blocking_decisions
        }
    )
    attested_decisions = [
        {
            "id": row["id"],
            "status": row["status"],
            "owner": row["owner"],
            "decision": row["decision"],
            "blocks_cutover": row["blocks_cutover"],
            "affected_records": sorted(row["affected_records"]),
            "as_of_commit": row["as_of_commit"],
        }
        for decision_id in decision_ids
        if (row := decisions.get(decision_id)) is not None
    ]
    return {
        "records": attested_records,
        "reviews": attested_reviews,
        "decisions": attested_decisions,
    }


def _build_report_artifacts(
    records: Mapping[str, Record],
    source_artifacts: Mapping[str, Artifact],
    reviews: Sequence[Mapping[str, Any]],
    decisions: Mapping[str, Mapping[str, Any]],
    mode: str,
    transition_state: str,
    diagnostics: List[Diagnostic],
    candidate_source_commit: str,
    destination_templates: Mapping[str, str],
    dry_run_freeze_id: str,
    dry_run_transition_id: str,
) -> VerificationReport:
    if mode == "cutover":
        selected_ids = {key for key, record in records.items() if record.disposition == "include"}
    else:
        selected_ids = {
            key
            for key, record in records.items()
            if record.disposition in {"candidate", "include"}
        }

    package_entries: Dict[str, List[Artifact]] = {package: [] for package in PACKAGE_ORDER}
    for record_id in sorted(selected_ids):
        artifact = source_artifacts.get(record_id)
        if artifact is not None:
            package_entries[artifact.package].append(artifact)
    for entries in package_entries.values():
        entries.sort(key=lambda item: (item.target_path.casefold(), item.target_path, item.record_id))

    package_hashes = {package: _package_hash(package_entries[package]) for package in PACKAGE_ORDER}
    canonical_shared_entries = package_entries["shared"]
    client_shared_entries = [
        entry
        for entry in canonical_shared_entries
        if _consumer_has_target(records[entry.record_id], "client")
    ]
    server_shared_entries = [
        entry
        for entry in canonical_shared_entries
        if _consumer_has_target(records[entry.record_id], "game-server")
    ]
    client_reference_entries = [
        entry
        for entry in package_entries["references"]
        if _consumer_has_target(records[entry.record_id], "client")
    ]
    server_reference_entries = [
        entry
        for entry in package_entries["references"]
        if _consumer_has_target(records[entry.record_id], "game-server")
    ]
    shared_inventory = [entry.target_path for entry in canonical_shared_entries]
    client_shared_inventory = [entry.target_path for entry in client_shared_entries]
    server_shared_inventory = [entry.target_path for entry in server_shared_entries]
    client_only = [entry.target_path for entry in package_entries["client"]]
    server_only = [entry.target_path for entry in package_entries["game-server"]]
    target_inventories = {
        "client": sorted(
            client_shared_inventory
            + client_only
            + [entry.target_path for entry in client_reference_entries]
        ),
        "game-server": sorted(
            server_shared_inventory
            + server_only
            + [entry.target_path for entry in server_reference_entries]
        ),
    }

    if set(client_only) & set(server_only):
        _add(
            diagnostics,
            "error",
            "TARGET_EXCLUSIVE_OVERLAP",
            "Client-only and Game Server-only target inventories overlap",
        )
    client_shared = [path for path in target_inventories["client"] if path.startswith("shared/")]
    server_shared = [path for path in target_inventories["game-server"] if path.startswith("shared/")]
    if client_shared != server_shared:
        _add(diagnostics, "error", "SHARED_INVENTORY_MISMATCH", "Shared target file lists differ")
    client_shared_hash = _package_hash(client_shared_entries)
    server_shared_hash = _package_hash(server_shared_entries)
    if client_shared_hash != server_shared_hash:
        _add(diagnostics, "error", "SHARED_HASH_MISMATCH", "Shared target hashes differ")

    for record in records.values():
        if record.record_id not in selected_ids:
            continue
        if record.package == "client":
            if not _consumer_has_target(record, "client") or _consumer_has_target(record, "game-server"):
                _add(
                    diagnostics,
                    "error",
                    "PACKAGE_CONSUMER_MISMATCH",
                    "Client-only record must name Client, and not Game Server, as a consumer",
                    record.record_id,
                )
        elif record.package == "game-server":
            if not _consumer_has_target(record, "game-server") or _consumer_has_target(record, "client"):
                _add(
                    diagnostics,
                    "error",
                    "PACKAGE_CONSUMER_MISMATCH",
                    "Game Server-only record must name Game Server, and not Client, as a consumer",
                    record.record_id,
                )
        elif record.package == "shared":
            if not _consumer_has_target(record, "client") or not _consumer_has_target(record, "game-server"):
                _add(
                    diagnostics,
                    "error",
                    "PACKAGE_CONSUMER_MISMATCH",
                    "Shared record must name both Client and Game Server as consumers",
                    record.record_id,
                )
        elif record.package == "references":
            if not _consumer_has_target(record, "client") and not _consumer_has_target(
                record, "game-server"
            ):
                _add(
                    diagnostics,
                    "error",
                    "PACKAGE_CONSUMER_MISMATCH",
                    "References record must name Client or Game Server as a consumer",
                    record.record_id,
                )

    _validate_target_snapshot_closure(
        records,
        source_artifacts,
        selected_ids,
        target_inventories,
        diagnostics,
    )

    manifest_packages = {
        package: {
            "files": [entry.package_file_dict() for entry in package_entries[package]],
            "aggregate_sha256": package_hashes[package],
        }
        for package in PACKAGE_ORDER
    }
    expanded_destinations = _expand_destinations(
        destination_templates,
        dry_run_freeze_id,
        diagnostics,
    )
    manifest_basis = {
        "schema_version": "1.0",
        "freeze_id": dry_run_freeze_id,
        "transition_id": dry_run_transition_id,
        "source_commit": candidate_source_commit,
        "created_at": DRY_RUN_CREATED_AT,
        "packages": manifest_packages,
        "destinations": expanded_destinations,
        "governance_attestation": _governance_attestation(
            records,
            selected_ids,
            reviews,
            decisions,
        ),
    }
    dry_run_manifest = dict(manifest_basis)
    dry_run_manifest["aggregate_sha256"] = _sha256_bytes(_canonical_json(manifest_basis))
    manifest_sha256 = _sha256_bytes(_canonical_json(dry_run_manifest))
    return VerificationReport(
        mode=mode,
        transition_state=transition_state,
        diagnostics=diagnostics,
        package_entries=package_entries,
        package_hashes=package_hashes,
        target_inventories=target_inventories,
        shared_inventory=shared_inventory,
        shared_hash=package_hashes["shared"],
        dry_run_manifest=dry_run_manifest,
        manifest_sha256=manifest_sha256,
    )


def verify_transition(
    root: Path,
    registry_path: Optional[Path] = None,
    reviews_path: Optional[Path] = None,
    decisions_path: Optional[Path] = None,
    mode: str = "prepare",
    changed_paths_by_commit: Optional[Mapping[str, Sequence[str]]] = None,
    git_blobs_by_commit: Optional[
        Mapping[str, Optional[Mapping[str, bytes]]]
    ] = None,
    dry_run_freeze_id: str = DRY_RUN_FREEZE_ID,
    dry_run_transition_id: str = DRY_RUN_TRANSITION_ID,
) -> VerificationReport:
    """Verify transition governance data without writing any file.

    ``changed_paths_by_commit`` is an injection seam for deterministic tests. If
    omitted, tracked and untracked working-tree paths changed since each
    ``as_of_commit`` are read from Git without modifying repository state.
    ``git_blobs_by_commit`` is the corresponding cutover-only test seam: a
    missing commit maps to an invalid object, and a missing path maps to an
    untracked/missing candidate blob. Production CLI use leaves it unset and
    reads object types and raw blobs through read-only Git commands.
    """

    diagnostics: List[Diagnostic] = []
    if mode not in MODES:
        raise ValueError(f"Unsupported mode: {mode}")
    dry_run_freeze_id = _validate_manifest_id(
        dry_run_freeze_id,
        "dry_run_freeze_id",
        DRY_RUN_FREEZE_ID,
        diagnostics,
    )
    dry_run_transition_id = _validate_manifest_id(
        dry_run_transition_id,
        "dry_run_transition_id",
        DRY_RUN_TRANSITION_ID,
        diagnostics,
    )
    root = root.resolve(strict=True)
    governance = root / "docs" / "production-transition" / "governance"
    registry_file = (registry_path or governance / "registry.json")
    reviews_file = (reviews_path or governance / "reviews.json")
    decisions_file = (decisions_path or governance / "decisions.json")
    if not registry_file.is_absolute():
        registry_file = root / registry_file
    if not reviews_file.is_absolute():
        reviews_file = root / reviews_file
    if not decisions_file.is_absolute():
        decisions_file = root / decisions_file

    registry_json = _load_json(registry_file, "registry", diagnostics)
    reviews_json = _load_json(reviews_file, "reviews", diagnostics)
    decisions_json = _load_json(decisions_file, "decisions", diagnostics)
    if registry_json is None:
        return _build_report_artifacts(
            {},
            {},
            [],
            {},
            mode,
            "unknown",
            diagnostics,
            "0" * 40,
            DESTINATION_TEMPLATES,
            dry_run_freeze_id,
            dry_run_transition_id,
        )

    if not _valid_schema_version(registry_json.get("schema_version")):
        _add(diagnostics, "error", "SCHEMA_VERSION", "registry.json schema_version must be 1 or 1.0")
    candidate_source_commit, destination_templates = _validate_registry_metadata(
        registry_json,
        diagnostics,
    )
    transition_state = registry_json.get("transition_state")
    if transition_state not in TRANSITION_STATES:
        _add(
            diagnostics,
            "error",
            "TRANSITION_STATE",
            f"Invalid transition_state: {transition_state!r}",
        )
        transition_state = str(transition_state or "unknown")
    if mode == "cutover" and transition_state != "cutover_candidate":
        _add(
            diagnostics,
            "error",
            "CUTOVER_STATE",
            "cutover mode is read-only preflight and requires transition_state=cutover_candidate",
        )

    raw_records = registry_json.get("records")
    if not isinstance(raw_records, list):
        _add(diagnostics, "error", "RECORDS_TYPE", "registry records must be an array")
        raw_records = []

    records: Dict[str, Record] = {}
    for index, raw_record in enumerate(raw_records):
        record = _parse_record(raw_record, diagnostics, index)
        if record is None:
            continue
        if record.record_id in records:
            _add(diagnostics, "error", "RECORD_DUPLICATE", f"Duplicate record id: {record.record_id}", record.record_id)
            continue
        records[record.record_id] = record

    source_keys: Dict[str, str] = {}
    target_keys: Dict[str, str] = {}
    source_artifacts: Dict[str, Artifact] = {}
    for record in records.values():
        source_key = record.source_path.casefold()
        if source_key in source_keys:
            _add(
                diagnostics,
                "error",
                "SOURCE_PATH_DUPLICATE",
                f"source_path also belongs to {source_keys[source_key]}: {record.source_path}",
                record.record_id,
            )
        else:
            source_keys[source_key] = record.record_id
        target_key = record.target_path.casefold()
        if target_key in target_keys:
            _add(
                diagnostics,
                "error",
                "TARGET_PATH_DUPLICATE",
                f"target_path also belongs to {target_keys[target_key]}: {record.target_path}",
                record.record_id,
            )
        else:
            target_keys[target_key] = record.record_id

        source_file, source_error = _is_contained_file(root, record.source_path)
        if source_error is not None:
            _add(diagnostics, "error", "SOURCE_PATH_INVALID", source_error, record.record_id)
            continue
        assert source_file is not None
        raw = source_file.read_bytes()
        actual_hash = _sha256_bytes(raw)
        if actual_hash != record.expected_sha256:
            _add(
                diagnostics,
                "error",
                "SHA256_MISMATCH",
                f"expected {record.expected_sha256}, got {actual_hash}",
                record.record_id,
            )
        source_artifacts[record.record_id] = Artifact(
            record_id=record.record_id,
            package=record.package,
            source_path=record.source_path,
            target_path=record.target_path,
            sha256=actual_hash,
            size=len(raw),
            as_of_commit=record.as_of_commit,
            document_revision=record.document_revision,
            depends_on=tuple(sorted(record.depends_on)),
            references=tuple(sorted(record.references)),
            content=raw,
        )

    if mode == "cutover":
        _validate_cutover_git_provenance(
            root,
            candidate_source_commit,
            records,
            source_artifacts,
            diagnostics,
            git_blobs_by_commit,
        )

    decisions = _parse_decisions(decisions_json, diagnostics)
    reviews = _parse_reviews(reviews_json, diagnostics)
    _validate_decision_record_links(records, decisions, diagnostics)
    approval_keys = {
        (
            review["area_id"],
            review["card_id"],
            review["document_revision"],
            review["source_commit"],
            review["reviewer_role"],
        )
        for review in reviews
        if review["approval"]
    }
    for review in reviews:
        record = records.get(review["card_id"])
        if record is None:
            _add(
                diagnostics,
                "error",
                "REVIEW_UNKNOWN_CARD",
                f"Review references unknown card: {review['card_id']}",
                review["card_id"],
            )
            continue
        if review["area_id"] not in record.areas:
            _add(
                diagnostics,
                "error",
                "REVIEW_UNKNOWN_AREA",
                f"Review area {review['area_id']} is not declared by the card",
                record.record_id,
            )
        if review["approval"] and review["reviewed_by"] == record.owner:
            _add(
                diagnostics,
                "error",
                "REVIEW_SELF_APPROVAL",
                "Record owner cannot approve the same document revision",
                record.record_id,
            )

    changed_cache: Dict[str, Optional[List[str]]] = {}
    for record in records.values():
        changed: Optional[List[str]]
        if changed_paths_by_commit is not None:
            if record.as_of_commit not in changed_paths_by_commit:
                _add(
                    diagnostics,
                    "error",
                    "CHANGED_PATHS_MISSING",
                    f"No changed-path fixture for {record.as_of_commit}",
                    record.record_id,
                )
                changed = None
            else:
                changed = sorted(
                    {path.replace("\\", "/") for path in changed_paths_by_commit[record.as_of_commit]}
                )
        elif record.as_of_commit in changed_cache:
            changed = changed_cache[record.as_of_commit]
        else:
            changed, error = _git_changed_paths(root, record.as_of_commit)
            if error is not None:
                _add(
                    diagnostics,
                    "error",
                    "GIT_DIFF_FAILED",
                    f"Cannot determine changed paths since {record.as_of_commit}: {error}",
                    record.record_id,
                )
            changed_cache[record.as_of_commit] = changed
        if changed is not None:
            watched_changes = sorted(
                path
                for path in changed
                if any(_path_matches_watch(path, watch) for watch in record.watch_paths)
            )
            if watched_changes and record.freshness == "current":
                summary = ", ".join(watched_changes[:5])
                if len(watched_changes) > 5:
                    summary += f" (+{len(watched_changes) - 5} more)"
                _add(
                    diagnostics,
                    "error",
                    "WATCH_PATH_STALE",
                    f"Record claims current but watched paths changed: {summary}",
                    record.record_id,
                )

    for record in records.values():
        resolved_edges: List[Tuple[str, str, Record]] = []
        for field, target_ids in (("depends_on", record.depends_on), ("references", record.references)):
            for target_id in target_ids:
                target = records.get(target_id)
                if target is None:
                    _add(
                        diagnostics,
                        "error",
                        "CLOSURE_UNKNOWN_RECORD",
                        f"{field} references unknown record: {target_id}",
                        record.record_id,
                    )
                else:
                    resolved_edges.append((field, target_id, target))

        for field, target_label, target in resolved_edges:
            if not _package_edge_allowed(record.package, target.package):
                _add(
                    diagnostics,
                    "error",
                    "PACKAGE_BOUNDARY",
                    f"{record.package} record cannot {field} {target.package} record {target_label}",
                    record.record_id,
                )
            if mode == "cutover" and record.disposition == "include" and target.disposition != "include":
                _add(
                    diagnostics,
                    "error",
                    "CUTOVER_CLOSURE",
                    f"Included record {field} target is not included: {target_label} ({target.disposition})",
                    record.record_id,
                )
            elif mode == "prepare" and record.disposition != "exclude" and target.disposition == "exclude":
                _add(
                    diagnostics,
                    "warning",
                    "PREPARE_CLOSURE_GAP",
                    f"Active preparation record {field} target is excluded: {target_label}",
                    record.record_id,
                )

        for decision_id in record.blocking_decisions:
            decision = decisions.get(decision_id)
            if decision is None:
                _add(
                    diagnostics,
                    "error",
                    "DECISION_UNKNOWN",
                    f"blocking_decisions references unknown decision: {decision_id}",
                    record.record_id,
                )
                continue
            if decision["status"] != "decided":
                severity = "error" if mode == "cutover" and record.disposition == "include" else "warning"
                _add(
                    diagnostics,
                    severity,
                    "UNRESOLVED_BLOCKER",
                    f"Decision {decision_id} is {decision['status']}, not decided",
                    record.record_id,
                )
                if record.readiness == "ready":
                    _add(
                        diagnostics,
                        "error",
                        "READY_WITH_BLOCKER",
                        f"Record is ready while decision {decision_id} is unresolved",
                        record.record_id,
                    )

        missing_approvals = [
            (area, role)
            for area in record.areas
            for role in record.required_reviewers
            if (
                area,
                record.record_id,
                record.document_revision,
                record.as_of_commit,
                role,
            )
            not in approval_keys
        ]
        if missing_approvals:
            summary = ", ".join(f"{area}/{role}" for area, role in missing_approvals)
            strict_review = record.review_status == "reviewed" or (
                mode == "cutover" and record.disposition == "include"
            )
            _add(
                diagnostics,
                "error" if strict_review else "warning",
                "AREA_REVIEW_MISSING",
                f"Missing exact area/reviewer approvals: {summary}",
                record.record_id,
            )
        elif record.review_status != "reviewed" and record.disposition == "include":
            _add(
                diagnostics,
                "error" if mode == "cutover" else "warning",
                "REVIEW_STATUS_MISMATCH",
                "Exact approvals exist but review_status is not reviewed",
                record.record_id,
            )

        ready_invariants = (
            record.completeness == "complete"
            and record.freshness == "current"
            and record.review_status == "reviewed"
        )
        if record.readiness == "ready" and not ready_invariants:
            _add(
                diagnostics,
                "error",
                "READY_INCONSISTENT",
                "readiness=ready requires complete/current/reviewed",
                record.record_id,
            )

        if mode == "cutover" and record.disposition == "include":
            failures = []
            if record.completeness != "complete":
                failures.append(f"completeness={record.completeness}")
            if record.freshness != "current":
                failures.append(f"freshness={record.freshness}")
            if record.review_status != "reviewed":
                failures.append(f"review_status={record.review_status}")
            if record.readiness != "ready":
                failures.append(f"readiness={record.readiness}")
            if failures:
                _add(
                    diagnostics,
                    "error",
                    "CUTOVER_GATE",
                    "Included record fails strict gate: " + ", ".join(failures),
                    record.record_id,
                )
        elif mode == "prepare" and record.disposition in {"candidate", "include"}:
            gaps = []
            if record.completeness != "complete":
                gaps.append(record.completeness)
            if record.freshness != "current":
                gaps.append(record.freshness)
            if record.review_status != "reviewed":
                gaps.append(record.review_status)
            if record.readiness != "ready":
                gaps.append(record.readiness)
            if gaps:
                _add(
                    diagnostics,
                    "warning",
                    "PREPARE_GATE_GAP",
                    "Preparation record is not cutover-ready: " + ", ".join(gaps),
                    record.record_id,
                )

    for cycle in _dependency_cycles(records):
        _add(diagnostics, "error", "DEPENDENCY_CYCLE", " -> ".join(cycle), cycle[0])

    for decision in decisions.values():
        if decision["blocks_cutover"] and decision["status"] != "decided":
            _add(
                diagnostics,
                "error" if mode == "cutover" else "warning",
                "GLOBAL_CUTOVER_BLOCKER",
                f"Decision {decision['id']} blocks cutover with status {decision['status']}",
                decision["id"],
            )

    if mode == "cutover":
        for record in records.values():
            if record.disposition in {"candidate", "defer"}:
                _add(
                    diagnostics,
                    "error",
                    "CUTOVER_SCOPE_UNLOCKED",
                    f"Cutover scope still contains disposition={record.disposition}",
                    record.record_id,
                )
            if record.cutover_blocking and record.disposition != "include":
                _add(
                    diagnostics,
                    "error",
                    "CUTOVER_BLOCKING_RECORD_EXCLUDED",
                    "cutover_blocking record must be included before cutover",
                    record.record_id,
                )

    # A source document cannot be classified as both Client-only and Server-only.
    # Global source-path uniqueness above enforces this mechanically. Check the
    # resulting record-ID inventories as a second, explicit invariant.
    client_ids = {record.record_id for record in records.values() if record.package == "client"}
    server_ids = {record.record_id for record in records.values() if record.package == "game-server"}
    if client_ids & server_ids:
        _add(diagnostics, "error", "TARGET_EXCLUSIVE_OVERLAP", "Client and Server record IDs overlap")

    return _build_report_artifacts(
        records,
        source_artifacts,
        reviews,
        decisions,
        mode,
        transition_state,
        diagnostics,
        candidate_source_commit,
        destination_templates,
        dry_run_freeze_id,
        dry_run_transition_id,
    )


def _resolve_cli_path(root: Path, value: Optional[str], default: Path) -> Path:
    if value is None:
        return default
    path = Path(value)
    return path if path.is_absolute() else root / path


def _print_text(report: VerificationReport) -> None:
    for item in sorted(report.diagnostics, key=Diagnostic.sort_key):
        subject = f" [{item.record_id}]" if item.record_id else ""
        print(f"[{item.severity.upper()}] {item.code}{subject}: {item.message}")
    print(f"mode={report.mode}")
    print(f"transition_state={report.transition_state}")
    for package in sorted(report.package_entries):
        print(
            f"package={package} files={len(report.package_entries[package])} "
            f"sha256={report.package_hashes[package]}"
        )
    print(f"shared_sha256={report.shared_hash}")
    print(f"manifest_sha256={report.manifest_sha256}")
    print(f"result={'PASS' if report.ok else 'FAIL'} errors={len(report.errors)} warnings={len(report.warnings)}")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("mode", choices=sorted(MODES), help="Preparation audit or strict cutover preflight")
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parent.parent,
        help="Demo repository root (default: script parent repository)",
    )
    parser.add_argument("--registry", help="Registry JSON path, relative to --root unless absolute")
    parser.add_argument("--reviews", help="Reviews JSON path, relative to --root unless absolute")
    parser.add_argument("--decisions", help="Decisions JSON path, relative to --root unless absolute")
    parser.add_argument(
        "--dry-run-freeze-id",
        default=DRY_RUN_FREEZE_ID,
        help="Deterministic freeze ID used only in the in-memory dry-run manifest",
    )
    parser.add_argument(
        "--dry-run-transition-id",
        default=DRY_RUN_TRANSITION_ID,
        help="Deterministic transition ID used only in the in-memory dry-run manifest",
    )
    parser.add_argument("--json", action="store_true", help="Print deterministic JSON report")
    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    root = args.root.resolve()
    governance = root / "docs" / "production-transition" / "governance"
    report = verify_transition(
        root=root,
        registry_path=_resolve_cli_path(root, args.registry, governance / "registry.json"),
        reviews_path=_resolve_cli_path(root, args.reviews, governance / "reviews.json"),
        decisions_path=_resolve_cli_path(root, args.decisions, governance / "decisions.json"),
        mode=args.mode,
        dry_run_freeze_id=args.dry_run_freeze_id,
        dry_run_transition_id=args.dry_run_transition_id,
    )
    if args.json:
        print(json.dumps(report.as_dict(), ensure_ascii=False, sort_keys=True, indent=2))
    else:
        _print_text(report)
    return 0 if report.ok else 1


if __name__ == "__main__":
    sys.exit(main())
