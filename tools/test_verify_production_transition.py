from __future__ import annotations

import contextlib
import hashlib
import io
import json
import re
import tempfile
import unittest
from pathlib import Path
from typing import Any, Dict, Iterable, List
from unittest import mock

from tools import verify_production_transition as verifier


SHA_A = "a" * 64
REVISION = "b" * 40
FREEZE_ID = "demo-2026-08-12"


def _json_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def _sha256(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def _independent_partition_hash(entries: Iterable[Dict[str, Any]]) -> str:
    rows = [
        {
            "audience": entry["audience"],
            "bytes": entry["bytes"],
            "path": entry["path"],
            "sha256": entry["sha256"],
        }
        for entry in entries
    ]
    rows.sort(key=lambda row: row["path"])
    raw = json.dumps(
        rows,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return _sha256(raw)


def _coverage(title: str, status: str = "included", blocker: str | None = None) -> str:
    if blocker is None:
        blocker = "none" if status in {"included", "excluded"} else "`PT-DEC-TEST-001`"
    return (
        f"# {title}\n\n"
        "| ID | 항목 | 상태 | Blocking decision |\n"
        "|---|---|---|---|\n"
        f"| `ROW-001` | representative surface | {status} | {blocker} |\n"
    )


def _rule_document(rule_id: str) -> str:
    fields = "\n".join(f"- **{label}:** representative value" for label in verifier.RULE_FIELDS)
    return f"# Rules\n\n## `{rule_id}` — Representative rule\n\n{fields}\n"


RULE_IDS_BY_PATH = {
    "common/rules/authority-identity-and-results.md": "PT-COM-001",
    "common/rules/ordering-resync-and-versioning.md": "PT-COM-002",
    "client/rules/authority-and-projection.md": "PT-CLI-001",
    "client/rules/presentation-and-catalog.md": "PT-CLI-002",
    "game-server/rules/authority-and-state.md": "PT-SRV-001",
    "game-server/rules/time-ordering-numeric-rng.md": "PT-SRV-002",
    "game-server/rules/content-result-and-replay.md": "PT-SRV-003",
}


def _codes(report: verifier.VerificationReport) -> set[str]:
    return {diagnostic.code for diagnostic in report.diagnostics}


def _tree_bytes(root: Path) -> Dict[str, bytes]:
    return {
        path.relative_to(root).as_posix(): path.read_bytes()
        for path in sorted(root.rglob("*"))
        if path.is_file()
    }


class LivingFixture:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.transition = root / "docs" / "production-transition"
        for relative in verifier.REQUIRED_LIVING_FILES:
            path = self.transition.joinpath(*Path(relative).parts)
            path.parent.mkdir(parents=True, exist_ok=True)
            if relative.endswith(".json"):
                source = Path(__file__).resolve().parents[1] / "docs" / "production-transition" / relative
                path.write_bytes(source.read_bytes())
            elif relative == "client/demo-experience-map.md":
                path.write_text(_coverage("Client coverage", "decision-blocked"), encoding="utf-8")
            elif relative == "game-server/domain-coverage.md":
                path.write_text(_coverage("Server coverage", "decision-blocked"), encoding="utf-8")
            elif relative in RULE_IDS_BY_PATH:
                path.write_text(_rule_document(RULE_IDS_BY_PATH[relative]), encoding="utf-8")
            else:
                path.write_text(f"# {relative}\n", encoding="utf-8")


class FreezeFixture:
    def __init__(self, root: Path) -> None:
        self.freeze = root / FREEZE_ID
        self.events = root / "audit-events"
        self.freeze.mkdir()
        self.events.mkdir()
        self.files: List[Dict[str, Any]] = []
        for relative, audience in verifier.REQUIRED_EXPORT_ARTIFACTS:
            if relative == "client/demo-experience-map.md":
                text = _coverage("Client coverage")
            elif relative == "game-server/domain-coverage.md":
                text = _coverage("Server coverage")
            elif relative in RULE_IDS_BY_PATH:
                text = _rule_document(RULE_IDS_BY_PATH[relative])
            else:
                text = f"# {relative}\n"
            self._add_document(relative, audience, text)

        self.destinations = {
            "client": f"somnia-client/docs/migration-input/dreamsquad-demo/{FREEZE_ID}/",
            "game-server": f"somnia-game-server/docs/migration-input/dreamsquad-demo/{FREEZE_ID}/",
        }
        self.bundle_hashes = {
            partition: _independent_partition_hash(
                entry for entry in self.files if entry["audience"] == partition
            )
            for partition in ("common", "client", "game-server")
        }
        self.manifest: Dict[str, Any] = {
            "schema_version": "1.0",
            "freeze_id": FREEZE_ID,
            "created_at": "2026-08-12T00:01:30Z",
            "demo_revision": REVISION,
            "demo_content_sha256": SHA_A,
            "destinations": dict(self.destinations),
            "bundle_hashes": dict(self.bundle_hashes),
            "files": list(self.files),
        }
        self.write_manifest()
        self._write_events_and_receipts()

    def _add_document(self, relative: str, audience: str, text: str) -> None:
        path = self.freeze.joinpath(*Path(relative).parts)
        path.parent.mkdir(parents=True, exist_ok=True)
        raw = text.encode("utf-8")
        path.write_bytes(raw)
        self.files.append(
            {
                "path": relative,
                "audience": audience,
                "sha256": _sha256(raw),
                "bytes": len(raw),
            }
        )

    def add_document_and_refresh(self, relative: str, audience: str, text: str) -> None:
        """Add a probe document and rebuild every dependent integrity record."""

        self._add_document(relative, audience, text)
        self._refresh_integrity()

    def remove_document_and_refresh(self, relative: str) -> None:
        """Remove one payload document and rebuild every dependent integrity record."""

        self.freeze.joinpath(*Path(relative).parts).unlink()
        self.files = [entry for entry in self.files if entry["path"] != relative]
        self._refresh_integrity()

    def replace_document_and_refresh(self, relative: str, text: str) -> None:
        """Replace one payload document and rebuild every dependent integrity record."""

        path = self.freeze.joinpath(*Path(relative).parts)
        raw = text.encode("utf-8")
        path.write_bytes(raw)
        for entry in self.files:
            if entry["path"] == relative:
                entry["sha256"] = _sha256(raw)
                entry["bytes"] = len(raw)
                break
        else:
            raise AssertionError(f"missing fixture document: {relative}")
        self._refresh_integrity()

    def _refresh_integrity(self) -> None:
        self.bundle_hashes = {
            partition: _independent_partition_hash(
                entry for entry in self.files if entry["audience"] == partition
            )
            for partition in ("common", "client", "game-server")
        }
        self.manifest["bundle_hashes"] = dict(self.bundle_hashes)
        self.manifest["files"] = list(self.files)
        self.write_manifest()
        self._write_events_and_receipts()

    def write_json(self, relative: str, value: Dict[str, Any]) -> bytes:
        parts = Path(relative).parts
        if parts and parts[0] == "events":
            path = self.events.joinpath(*parts[1:])
        else:
            path = self.freeze.joinpath(*parts)
        path.parent.mkdir(parents=True, exist_ok=True)
        raw = _json_bytes(value)
        path.write_bytes(raw)
        return raw

    def read_json(self, relative: str) -> Dict[str, Any]:
        parts = Path(relative).parts
        path = self.events.joinpath(*parts[1:]) if parts and parts[0] == "events" else self.freeze.joinpath(*parts)
        return json.loads(path.read_text(encoding="utf-8"))

    def write_manifest(self) -> bytes:
        return self.write_json("manifest.json", self.manifest)

    def _event_base(self, event_id: str, event_type: str, approved_at: str) -> Dict[str, Any]:
        return {
            "schema_version": "1.0",
            "event_id": event_id,
            "event_type": event_type,
            "acting_role": "project-owner",
            "project_owner": "owner@example.invalid",
            "approved_at": approved_at,
            "approval_reference": f"approval:{event_id}",
            "demo_revision": REVISION,
            "demo_content_sha256": SHA_A,
        }

    def _assigned_entries(self, consumer: str) -> List[Dict[str, Any]]:
        return [
            entry
            for entry in self.files
            if entry["audience"] in {"common", consumer, "reference"}
        ]

    def _write_events_and_receipts(self) -> None:
        manifest_raw = (self.freeze / "manifest.json").read_bytes()
        approved = self._event_base("event-approved", "demo-approved", "2026-08-12T00:01:00Z")
        approved["approved_scope"] = ["client:ROW-001", "game-server:ROW-001"]
        self.write_json("events/1-demo-approved.json", approved)

        frozen = self._event_base("event-frozen", "demo-frozen", "2026-08-12T00:02:00Z")
        frozen.update(
            {
                "predecessor_event_id": approved["event_id"],
                "freeze_id": FREEZE_ID,
                "manifest_sha256": _sha256(manifest_raw),
                "common_sha256": self.bundle_hashes["common"],
                "client_sha256": self.bundle_hashes["client"],
                "game_server_sha256": self.bundle_hashes["game-server"],
            }
        )
        self.write_json("events/2-demo-frozen.json", frozen)

        receipt_raw: Dict[str, bytes] = {}
        for consumer in ("client", "game-server"):
            assigned = self._assigned_entries(consumer)
            receipt = {
                "schema_version": "1.0",
                "consumer": consumer,
                "freeze_id": FREEZE_ID,
                "destination": self.destinations[consumer],
                "manifest_sha256": _sha256(manifest_raw),
                "common_sha256": self.bundle_hashes["common"],
                "assigned_bundle_sha256": self.bundle_hashes[consumer],
                "file_count": 1 + len(assigned),
                "byte_count": len(manifest_raw) + sum(entry["bytes"] for entry in assigned),
                "received_at": "2026-08-12T00:03:00Z",
                "status": "verified",
                "verified_by": f"{consumer}-tech-owner",
            }
            receipt_raw[consumer] = self.write_json(f"receipts/{consumer}.json", receipt)

        completed = self._event_base(
            "event-completed",
            "transfer-completed",
            "2026-08-12T00:04:00Z",
        )
        completed.update(
            {
                "predecessor_event_id": frozen["event_id"],
                "freeze_id": FREEZE_ID,
                "client_receipt_sha256": _sha256(receipt_raw["client"]),
                "game_server_receipt_sha256": _sha256(receipt_raw["game-server"]),
                "destinations": dict(self.destinations),
            }
        )
        self.write_json("events/3-transfer-completed.json", completed)


class AuthorizationTests(unittest.TestCase):
    def test_prepare_without_authorization_skips_before_verifier(self) -> None:
        stdout = io.StringIO()
        with mock.patch.object(verifier, "verify_prepare", side_effect=AssertionError("must not run")):
            with contextlib.redirect_stdout(stdout):
                exit_code = verifier.main(["prepare", "--root", "missing-root"])
        self.assertEqual(0, exit_code)
        self.assertIn("SKIP", stdout.getvalue())

    def test_cutover_without_authorization_skips_even_without_freeze_dir(self) -> None:
        stdout = io.StringIO()
        with mock.patch.object(verifier, "verify_cutover", side_effect=AssertionError("must not run")):
            with contextlib.redirect_stdout(stdout):
                exit_code = verifier.main(["cutover", "--root", "missing-root"])
        self.assertEqual(0, exit_code)
        self.assertIn("no transition files or Git state were read", stdout.getvalue())

    def test_authorized_cutover_requires_explicit_freeze_dir(self) -> None:
        stdout = io.StringIO()
        with contextlib.redirect_stdout(stdout):
            exit_code = verifier.main(["cutover", "--project-owner-authorized"])
        self.assertEqual(2, exit_code)
        self.assertIn("--freeze-dir", stdout.getvalue())


class PrepareTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.fixture = LivingFixture(self.root)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def test_valid_living_structure_passes_and_reports_partitions(self) -> None:
        report = verifier.verify_prepare(self.root)
        self.assertTrue(report.ok, [item.format() for item in report.diagnostics])
        self.assertEqual({"common", "client", "game-server", "policy"}, set(report.partition_hashes))
        self.assertEqual(1, len(report.partitions["policy"]))
        self.assertEqual("references/transition-policy.md", report.partitions["policy"][0].path)

    def test_prepare_is_deterministic_and_read_only(self) -> None:
        before = _tree_bytes(self.root)
        first = verifier.verify_prepare(self.root)
        second = verifier.verify_prepare(self.root)
        after = _tree_bytes(self.root)
        self.assertTrue(first.ok)
        self.assertEqual(first.partition_hashes, second.partition_hashes)
        self.assertEqual(before, after)

    def test_missing_required_living_file_fails(self) -> None:
        (self.fixture.transition / "common" / "README.md").unlink()
        report = verifier.verify_prepare(self.root)
        self.assertIn("LIVING_STRUCTURE", _codes(report))

    def test_three_schema_files_must_parse_as_json_objects(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "event.schema.json"
        schema.write_text("[not-an-object]", encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_JSON", _codes(report))

    def test_schema_declares_draft_and_id(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "receipt.schema.json"
        schema.write_bytes(_json_bytes({"type": "object"}))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_JSON", _codes(report))

    def test_empty_object_shaped_schema_contract_fails(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "manifest.schema.json"
        schema.write_bytes(
            _json_bytes(
                {
                    "$schema": "https://json-schema.org/draft/2020-12/schema",
                    "$id": "https://example.invalid/empty-manifest.json",
                    "type": "object",
                    "properties": {},
                }
            )
        )
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_event_schema_phase_fields_must_match_verifier_exactly(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "event.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        approved = value["$defs"]["demoApproved"]
        approved["required"].append("freeze_id")
        approved["properties"]["freeze_id"] = {"$ref": "#/$defs/freezeId"}
        schema.write_bytes(_json_bytes(value))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_manifest_schema_reference_route_is_exact(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "manifest.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        reference = value["$defs"]["file"]["allOf"][0]["oneOf"][3]
        reference["properties"]["path"]["const"] = "references/other.md"
        schema.write_bytes(_json_bytes(value))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_manifest_schema_null_file_path_is_diagnostic_not_crash(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "manifest.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        value["$defs"]["file"]["properties"]["path"] = None
        schema.write_bytes(_json_bytes(value))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_manifest_schema_unhashable_audience_enum_is_diagnostic_not_crash(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "manifest.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        value["$defs"]["file"]["properties"]["audience"]["enum"] = [
            "common",
            [],
        ]
        schema.write_bytes(_json_bytes(value))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_receipt_schema_null_consumer_is_diagnostic_not_crash(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "receipt.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        value["properties"]["consumer"] = None
        schema.write_bytes(_json_bytes(value))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_manifest_schema_malformed_discriminator_required_is_diagnostic_not_crash(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "manifest.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        value["$defs"]["file"]["allOf"][0]["oneOf"][0]["required"] = [
            ["audience"],
            "path",
        ]
        schema.write_bytes(_json_bytes(value))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_unexpected_schema_constraints_fail_prepare(self) -> None:
        cases = (
            ("event root", "event.schema.json", lambda value: value.update({"not": {}})),
            (
                "event phase",
                "event.schema.json",
                lambda value: value["$defs"]["demoApproved"].update({"not": {}}),
            ),
            (
                "event destinations",
                "event.schema.json",
                lambda value: value["$defs"]["destinations"].update({"not": {}}),
            ),
            ("manifest root", "manifest.schema.json", lambda value: value.update({"not": {}})),
            (
                "manifest destinations",
                "manifest.schema.json",
                lambda value: value["properties"]["destinations"].update({"not": {}}),
            ),
            (
                "manifest bundle hashes",
                "manifest.schema.json",
                lambda value: value["properties"]["bundle_hashes"].update({"not": {}}),
            ),
            (
                "manifest files",
                "manifest.schema.json",
                lambda value: value["properties"]["files"].update({"not": {}}),
            ),
            (
                "manifest file",
                "manifest.schema.json",
                lambda value: value["$defs"]["file"].update({"not": {}}),
            ),
            (
                "manifest discriminator wrapper",
                "manifest.schema.json",
                lambda value: value["$defs"]["file"]["allOf"][0].update({"not": {}}),
            ),
            (
                "manifest discriminator choice",
                "manifest.schema.json",
                lambda value: value["$defs"]["file"]["allOf"][0]["oneOf"][0].update(
                    {"not": {}}
                ),
            ),
            (
                "manifest discriminator leaf",
                "manifest.schema.json",
                lambda value: value["$defs"]["file"]["allOf"][0]["oneOf"][0][
                    "properties"
                ]["path"].update({"not": {}}),
            ),
            ("receipt root", "receipt.schema.json", lambda value: value.update({"not": {}})),
            (
                "receipt consumer",
                "receipt.schema.json",
                lambda value: value["properties"]["consumer"].update({"not": {}}),
            ),
            (
                "receipt discriminator wrapper",
                "receipt.schema.json",
                lambda value: value["allOf"][0].update({"not": {}}),
            ),
            (
                "receipt discriminator choice",
                "receipt.schema.json",
                lambda value: value["allOf"][0]["oneOf"][0].update({"not": {}}),
            ),
            (
                "receipt discriminator leaf",
                "receipt.schema.json",
                lambda value: value["allOf"][0]["oneOf"][0]["properties"]["destination"].update(
                    {"not": {}}
                ),
            ),
        )
        for label, filename, mutate in cases:
            with self.subTest(label=label):
                schema = self.fixture.transition / "governance" / "schemas" / filename
                original = schema.read_bytes()
                value = json.loads(original)
                mutate(value)
                schema.write_bytes(_json_bytes(value))
                report = verifier.verify_prepare(self.root)
                self.assertIn("SCHEMA_CONTRACT", _codes(report))
                schema.write_bytes(original)

    def test_manifest_schema_path_rejects_forbidden_segments_and_c1(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "manifest.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        pattern = value["$defs"]["file"]["properties"]["path"]["pattern"]
        self.assertIsNone(re.fullmatch(pattern, "client/fixture/sample.md"))
        self.assertIsNone(re.fullmatch(pattern, "client/fixtures/sample.md"))
        self.assertIsNone(re.fullmatch(pattern, "client/FiXtUrE/sample.md"))
        self.assertIsNone(re.fullmatch(pattern, "common/a\u0085.md"))
        self.assertIsNotNone(re.fullmatch(pattern, "client/fixture-old/sample.md"))
        self.assertIsNotNone(re.fullmatch(pattern, "common/규칙.md"))

    def test_receipt_destination_base_pattern_must_match_verifier(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "receipt.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        value["properties"]["destination"]["pattern"] = "^invalid-only/$"
        schema.write_bytes(_json_bytes(value))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_shared_schema_primitives_must_match_verifier(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "event.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        value["$defs"]["gitRevision"]["pattern"] = "^[a-f0-9]{7}$"
        schema.write_bytes(_json_bytes(value))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_event_phase_revision_wiring_must_match_verifier(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "event.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        value["$defs"]["demoApproved"]["properties"]["demo_revision"] = {
            "$ref": "#/$defs/sha256"
        }
        schema.write_bytes(_json_bytes(value))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_event_approved_scope_shape_must_match_verifier(self) -> None:
        schema = self.fixture.transition / "governance" / "schemas" / "event.schema.json"
        value = json.loads(schema.read_text(encoding="utf-8"))
        value["$defs"]["demoApproved"]["properties"]["approved_scope"] = {
            "type": "string"
        }
        schema.write_bytes(_json_bytes(value))
        report = verifier.verify_prepare(self.root)
        self.assertIn("SCHEMA_CONTRACT", _codes(report))

    def test_rule_block_requires_all_eight_fields(self) -> None:
        path = self.fixture.transition / "client" / "rules" / "authority-and-projection.md"
        text = path.read_text(encoding="utf-8")
        text = text.replace("- **Demo source pointer:** representative value\n", "")
        path.write_text(text, encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("RULE_CONTRACT", _codes(report))

    def test_rule_ids_must_be_unique_across_living_rules(self) -> None:
        path = self.fixture.transition / "common" / "rules" / "ordering-resync-and-versioning.md"
        path.write_text(_rule_document("PT-COM-001"), encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("RULE_CONTRACT", _codes(report))

    def test_broken_living_markdown_link_fails(self) -> None:
        readme = self.fixture.transition / "client" / "README.md"
        readme.write_text("# Client\n\n[missing](rules/nope.md)\n", encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("MARKDOWN_LINK", _codes(report))

    def test_nonportable_local_markdown_links_fail_prepare(self) -> None:
        for target in ("C:/missing.md", "C:\\missing.md", "file:///C:/missing.md"):
            with self.subTest(target=target):
                readme = self.fixture.transition / "common" / "README.md"
                original = readme.read_text(encoding="utf-8")
                readme.write_text(original + f"\n[x]({target})\n", encoding="utf-8")
                report = verifier.verify_prepare(self.root)
                self.assertIn("MARKDOWN_LINK", _codes(report))
                readme.write_text(original, encoding="utf-8")

    def test_cross_consumer_links_fail_prepare_in_both_directions(self) -> None:
        cases = (
            ("client/README.md", "../game-server/README.md"),
            ("game-server/README.md", "../client/README.md"),
        )
        for relative, target in cases:
            with self.subTest(relative=relative):
                path = self.fixture.transition.joinpath(*Path(relative).parts)
                original = path.read_text(encoding="utf-8")
                path.write_text(original + f"\n[cross]({target})\n", encoding="utf-8")
                report = verifier.verify_prepare(self.root)
                self.assertIn("MARKDOWN_LINK_CLOSURE", _codes(report))
                path.write_text(original, encoding="utf-8")

    def test_reference_and_autolink_cross_consumer_links_fail_prepare(self) -> None:
        readme = self.fixture.transition / "client" / "README.md"
        original = readme.read_text(encoding="utf-8")
        for content in (
            "\n[server-only][server]\n\n[server]: ../game-server/README.md\n",
            "\n<../game-server/README.md>\n",
        ):
            with self.subTest(content=content):
                readme.write_text(original + content, encoding="utf-8")
                report = verifier.verify_prepare(self.root)
                self.assertIn("MARKDOWN_LINK_CLOSURE", _codes(report))
                readme.write_text(original, encoding="utf-8")

    def test_archive_markdown_links_are_not_checked(self) -> None:
        archive = self.fixture.transition / "archive" / "legacy" / "README.md"
        archive.write_text("# Archive\n\n[historical broken](../../../gone.md)\n", encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertTrue(report.ok, [item.format() for item in report.diagnostics])

    def test_invalid_client_coverage_state_fails(self) -> None:
        path = self.fixture.transition / "client" / "demo-experience-map.md"
        path.write_text(_coverage("Client", "maybe"), encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("COVERAGE", _codes(report))

    def test_invalid_server_coverage_state_fails(self) -> None:
        path = self.fixture.transition / "game-server" / "domain-coverage.md"
        path.write_text(_coverage("Server", "ready"), encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("COVERAGE", _codes(report))

    def test_all_status_tables_are_checked_not_only_the_first(self) -> None:
        path = self.fixture.transition / "client" / "demo-experience-map.md"
        path.write_text(
            _coverage("Decoy", "included") + "\n" + _coverage("Actual", "invalid"),
            encoding="utf-8",
        )
        report = verifier.verify_prepare(self.root)
        self.assertIn("COVERAGE", _codes(report))

    def test_legacy_material_cannot_remain_active(self) -> None:
        legacy = self.fixture.transition / "governance" / "registry.json"
        legacy.write_text("{}\n", encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("LEGACY_ACTIVE", _codes(report))

    def test_plural_fixtures_are_excluded_from_official_inventory(self) -> None:
        fixture = self.fixture.transition / "client" / "fixtures" / "sample.md"
        fixture.parent.mkdir(parents=True)
        fixture.write_text("# fixture\n", encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("EXPORT_INVENTORY", _codes(report))
        selected = {entry.path for entries in report.partitions.values() for entry in entries}
        self.assertNotIn("client/fixtures/sample.md", selected)

    def test_singular_fixture_is_excluded_from_official_inventory(self) -> None:
        fixture = self.fixture.transition / "client" / "fixture" / "sample.md"
        fixture.parent.mkdir(parents=True)
        fixture.write_text("# fixture\n", encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("EXPORT_INVENTORY", _codes(report))
        selected = {entry.path for entries in report.partitions.values() for entry in entries}
        self.assertNotIn("client/fixture/sample.md", selected)

    def test_c1_path_is_excluded_from_official_inventory(self) -> None:
        relative = "common/a\u0085.md"
        path = self.fixture.transition.joinpath(*Path(relative).parts)
        path.write_text("# invisible path\n", encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("EXPORT_INVENTORY", _codes(report))
        selected = {entry.path for entries in report.partitions.values() for entry in entries}
        self.assertNotIn(relative, selected)

    def test_empty_forbidden_consumer_directory_also_fails(self) -> None:
        (self.fixture.transition / "client" / "evidence").mkdir()
        report = verifier.verify_prepare(self.root)
        self.assertIn("FORBIDDEN_PATH", _codes(report))

    def test_non_markdown_consumer_artifact_fails(self) -> None:
        binary = self.fixture.transition / "common" / "rules" / "fixture.json"
        binary.write_text("{}\n", encoding="utf-8")
        report = verifier.verify_prepare(self.root)
        self.assertIn("EXPORT_INVENTORY", _codes(report))

    def test_archive_maintenance_and_governance_are_not_hashed_into_packages(self) -> None:
        report = verifier.verify_prepare(self.root)
        selected = {entry.path for entries in report.partitions.values() for entry in entries}
        self.assertFalse(any(path.startswith("archive/") for path in selected))
        self.assertFalse(any(path.startswith("maintenance/") for path in selected))
        self.assertEqual(
            {"references/transition-policy.md"},
            {path for path in selected if path.startswith("references/")},
        )


class CutoverTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.fixture = FreezeFixture(self.root)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def test_valid_freeze_passes_and_reports_hashes(self) -> None:
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertTrue(report.ok, [item.format() for item in report.diagnostics])
        self.assertEqual(self.fixture.bundle_hashes["common"], report.partition_hashes["common"])
        self.assertEqual(
            _sha256((self.fixture.freeze / "manifest.json").read_bytes()),
            report.manifest_sha256,
        )

    def test_cutover_is_read_only(self) -> None:
        before = _tree_bytes(self.root)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        after = _tree_bytes(self.root)
        self.assertTrue(report.ok)
        self.assertEqual(before, after)

    def test_manifest_must_be_valid_json(self) -> None:
        (self.fixture.freeze / "manifest.json").write_text("{", encoding="utf-8")
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MANIFEST_JSON", _codes(report))

    def test_manifest_rejects_extra_field(self) -> None:
        self.fixture.manifest["registry"] = "legacy"
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("FIELDS", _codes(report))

    def test_manifest_rejects_path_traversal(self) -> None:
        self.fixture.manifest["files"][0]["path"] = "common/../escape.md"
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MANIFEST_PATH", _codes(report))

    def test_manifest_rejects_case_colliding_duplicate(self) -> None:
        duplicate = dict(self.fixture.manifest["files"][0])
        duplicate["path"] = duplicate["path"].upper()
        self.fixture.manifest["files"].append(duplicate)
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MANIFEST_DUPLICATE", _codes(report))

    def test_manifest_rejects_audience_path_mismatch(self) -> None:
        entry = next(
            item for item in self.fixture.manifest["files"] if item["path"].startswith("common/")
        )
        entry["audience"] = "client"
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("AUDIENCE_PATH", _codes(report))

    def test_manifest_unhashable_audience_is_diagnostic_not_crash(self) -> None:
        self.fixture.manifest["files"][0]["audience"] = []
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("FIELD_VALUE", _codes(report))

    def test_manifest_control_character_path_is_diagnostic_not_crash(self) -> None:
        self.fixture.manifest["files"][0]["path"] = "common/a\x00.md"
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MANIFEST_PATH", _codes(report))

    def test_manifest_rejects_integrity_consistent_c1_path(self) -> None:
        self.fixture.add_document_and_refresh("common/a\u0085.md", "common", "# C1 path\n")
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MANIFEST_PATH", _codes(report))

    def test_manifest_rejects_additional_reference(self) -> None:
        extra_path = self.fixture.freeze / "references" / "other.md"
        extra_path.write_text("# Other\n", encoding="utf-8")
        raw = extra_path.read_bytes()
        self.fixture.manifest["files"].append(
            {
                "path": "references/other.md",
                "audience": "reference",
                "sha256": _sha256(raw),
                "bytes": len(raw),
            }
        )
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("AUDIENCE_PATH", _codes(report))

    def test_manifest_rejects_non_markdown_artifact(self) -> None:
        self.fixture.manifest["files"][0]["path"] = "common/data.json"
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MANIFEST_PATH", _codes(report))

    def test_file_hash_and_byte_count_are_verified(self) -> None:
        path = self.fixture.freeze / "common" / "README.md"
        path.write_text("# changed\n", encoding="utf-8")
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("FILE_HASH", _codes(report))
        self.assertIn("FILE_BYTES", _codes(report))

    def test_unlisted_file_fails_inventory(self) -> None:
        extra = self.fixture.freeze / "client" / "extra.md"
        extra.write_text("# extra\n", encoding="utf-8")
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MANIFEST_INVENTORY", _codes(report))

    def test_required_export_cannot_be_omitted_with_consistent_integrity(self) -> None:
        self.fixture.remove_document_and_refresh("game-server/plans/implementation-waves.md")
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("REQUIRED_EXPORT_INVENTORY", _codes(report))

    def test_uncatalogued_export_cannot_be_added_with_consistent_integrity(self) -> None:
        self.fixture.add_document_and_refresh("client/extra.md", "client", "# extra\n")
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("REQUIRED_EXPORT_INVENTORY", _codes(report))

    def test_frozen_rule_contract_is_revalidated_after_integrity_refresh(self) -> None:
        relative = "game-server/rules/authority-and-state.md"
        invalid = _rule_document(RULE_IDS_BY_PATH[relative]).replace(
            "- **Demo source pointer:** representative value\n",
            "",
        )
        self.fixture.replace_document_and_refresh(relative, invalid)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("RULE_CONTRACT", _codes(report))

    def test_frozen_markdown_links_are_revalidated_after_integrity_refresh(self) -> None:
        self.fixture.replace_document_and_refresh(
            "client/README.md",
            "# Client\n\n[missing](rules/missing.md)\n",
        )
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MARKDOWN_LINK", _codes(report))

    def test_frozen_cross_consumer_links_fail_in_both_directions(self) -> None:
        cases = (
            ("client/README.md", "../game-server/README.md"),
            ("game-server/README.md", "../client/README.md"),
        )
        for relative, target in cases:
            with self.subTest(relative=relative):
                path = self.fixture.freeze.joinpath(*Path(relative).parts)
                original = path.read_text(encoding="utf-8")
                self.fixture.replace_document_and_refresh(
                    relative,
                    original + f"\n[cross]({target})\n",
                )
                report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
                self.assertIn("MARKDOWN_LINK_CLOSURE", _codes(report))
                self.fixture.replace_document_and_refresh(relative, original)

    def test_demo_approved_scope_must_exactly_match_included_coverage(self) -> None:
        approved = self.fixture.read_json("events/1-demo-approved.json")
        approved["approved_scope"] = ["client:UNRELATED", "game-server:ROW-001"]
        self.fixture.write_json("events/1-demo-approved.json", approved)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("EVENT_SCOPE", _codes(report))

    def test_leading_pipe_optional_coverage_table_is_validated(self) -> None:
        relative = "client/demo-experience-map.md"
        path = self.fixture.freeze.joinpath(*Path(relative).parts)
        original = path.read_text(encoding="utf-8")
        hidden = (
            "\nID | 항목 | 상태 | Blocking decision\n"
            "---|---|---|---\n"
            "ROW-HIDDEN | hidden surface | decision-blocked | PT-DEC-HIDDEN\n"
        )
        self.fixture.replace_document_and_refresh(relative, original + hidden)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("COVERAGE_BLOCKED", _codes(report))

    def test_frozen_reference_and_autolink_cross_consumer_links_fail(self) -> None:
        relative = "client/README.md"
        path = self.fixture.freeze.joinpath(*Path(relative).parts)
        original = path.read_text(encoding="utf-8")
        for content in (
            "\n[server-only][server]\n\n[server]: ../game-server/README.md\n",
            "\n<../game-server/README.md>\n",
        ):
            with self.subTest(content=content):
                self.fixture.replace_document_and_refresh(relative, original + content)
                report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
                self.assertIn("MARKDOWN_LINK_CLOSURE", _codes(report))
                self.fixture.replace_document_and_refresh(relative, original)

    def test_frozen_file_uri_link_is_rejected_after_integrity_refresh(self) -> None:
        self.fixture.replace_document_and_refresh(
            "client/README.md",
            "# Client\n\n[local](file:///C:/missing.md)\n",
        )
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MARKDOWN_LINK", _codes(report))

    def test_disallowed_plural_fixtures_path_fails(self) -> None:
        self.fixture.add_document_and_refresh(
            "client/fixtures/sample.md",
            "client",
            "# sample\n",
        )
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MANIFEST_PATH", _codes(report))

    def test_disallowed_singular_fixture_path_fails(self) -> None:
        self.fixture.add_document_and_refresh(
            "client/fixture/sample.md",
            "client",
            "# sample\n",
        )
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MANIFEST_PATH", _codes(report))

    def test_bundle_hash_is_recomputed(self) -> None:
        self.fixture.manifest["bundle_hashes"]["client"] = "c" * 64
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("BUNDLE_HASH", _codes(report))

    def test_event_phase_extra_field_is_rejected(self) -> None:
        event = self.fixture.read_json("events/1-demo-approved.json")
        event["freeze_id"] = FREEZE_ID
        self.fixture.write_json("events/1-demo-approved.json", event)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("FIELDS", _codes(report))

    def test_event_predecessor_chain_is_exact(self) -> None:
        event = self.fixture.read_json("events/2-demo-frozen.json")
        event["predecessor_event_id"] = "wrong"
        self.fixture.write_json("events/2-demo-frozen.json", event)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("EVENT_SEQUENCE", _codes(report))

    def test_event_revision_and_freeze_id_match_manifest(self) -> None:
        event = self.fixture.read_json("events/3-transfer-completed.json")
        event["demo_revision"] = "c" * 40
        event["freeze_id"] = "another-freeze"
        self.fixture.write_json("events/3-transfer-completed.json", event)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("EVENT_CONSISTENCY", _codes(report))

    def test_event_timestamps_must_be_ordered(self) -> None:
        event = self.fixture.read_json("events/3-transfer-completed.json")
        event["approved_at"] = "2026-08-11T23:00:00Z"
        self.fixture.write_json("events/3-transfer-completed.json", event)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("EVENT_SEQUENCE", _codes(report))

    def test_manifest_creation_must_follow_approval_and_precede_freeze(self) -> None:
        self.fixture.manifest["created_at"] = "2026-08-11T23:00:00Z"
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("EVENT_SEQUENCE", _codes(report))

    def test_manifest_timestamp_must_use_rfc3339_lexical_form(self) -> None:
        self.fixture.manifest["created_at"] = "2026-08-12 00:01:30+00:00"
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("FIELD_VALUE", _codes(report))

    def test_receipt_time_must_follow_freeze_and_precede_completion(self) -> None:
        receipt = self.fixture.read_json("receipts/client.json")
        receipt["received_at"] = "2026-08-13T00:00:00Z"
        self.fixture.write_json("receipts/client.json", receipt)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("EVENT_SEQUENCE", _codes(report))

    def test_manifest_destination_must_use_freeze_id_suffix(self) -> None:
        self.fixture.manifest["destinations"]["client"] = (
            "somnia-client/docs/migration-input/dreamsquad-demo/wrong/"
        )
        self.fixture.write_manifest()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("DESTINATION", _codes(report))

    def test_receipt_must_match_assigned_bundle(self) -> None:
        receipt = self.fixture.read_json("receipts/client.json")
        receipt["assigned_bundle_sha256"] = "d" * 64
        self.fixture.write_json("receipts/client.json", receipt)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("RECEIPT", _codes(report))

    def test_receipt_counts_assigned_manifest_inventory(self) -> None:
        receipt = self.fixture.read_json("receipts/game-server.json")
        receipt["file_count"] += 1
        receipt["byte_count"] += 1
        self.fixture.write_json("receipts/game-server.json", receipt)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("RECEIPT", _codes(report))

    def test_transfer_event_hashes_exact_receipt_bytes(self) -> None:
        event = self.fixture.read_json("events/3-transfer-completed.json")
        event["client_receipt_sha256"] = "e" * 64
        self.fixture.write_json("events/3-transfer-completed.json", event)
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("EVENT_CONSISTENCY", _codes(report))

    def test_exactly_three_event_files_are_allowed(self) -> None:
        self.fixture.write_json("events/4-demo-refrozen.json", {"event_type": "demo-frozen"})
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("FREEZE_LAYOUT", _codes(report))

    def test_exactly_two_receipts_are_allowed(self) -> None:
        self.fixture.write_json("receipts/extra.json", {"consumer": "client"})
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("FREEZE_LAYOUT", _codes(report))

    def test_missing_manifest_file_fails(self) -> None:
        (self.fixture.freeze / "client" / "README.md").unlink()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("MANIFEST_INVENTORY", _codes(report))

    def test_decision_blocked_coverage_cannot_be_frozen(self) -> None:
        path = self.fixture.freeze / "client" / "demo-experience-map.md"
        path.write_text(_coverage("Client", "decision-blocked"), encoding="utf-8")
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("COVERAGE_BLOCKED", _codes(report))

    def test_included_coverage_cannot_retain_blocking_decision(self) -> None:
        path = self.fixture.freeze / "client" / "demo-experience-map.md"
        path.write_text(
            _coverage("Client", "included", "`PT-DEC-CLIENT-001`"),
            encoding="utf-8",
        )
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("COVERAGE_BLOCKED", _codes(report))

    def test_decoy_coverage_table_cannot_hide_blocked_rows(self) -> None:
        path = self.fixture.freeze / "client" / "demo-experience-map.md"
        path.write_text(
            _coverage("Decoy", "included") + "\n" + _coverage("Actual", "decision-blocked"),
            encoding="utf-8",
        )
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("COVERAGE_BLOCKED", _codes(report))

    def test_unexpected_top_level_freeze_entry_fails(self) -> None:
        (self.fixture.freeze / "maintenance").mkdir()
        report = verifier.verify_cutover(self.fixture.freeze, self.fixture.events)
        self.assertIn("FREEZE_LAYOUT", _codes(report))

    def test_freeze_directory_name_must_match_manifest_id(self) -> None:
        wrong = self.root / "wrong-freeze"
        self.fixture.freeze.rename(wrong)
        report = verifier.verify_cutover(wrong, self.fixture.events)
        self.assertIn("FREEZE_ID_PATH", _codes(report))

    def test_cli_accepts_relative_explicit_freeze_dir(self) -> None:
        canonical_freeze = (
            self.root
            / "docs"
            / "production-transition"
            / "freezes"
            / FREEZE_ID
        )
        canonical_events = (
            self.root
            / "docs"
            / "production-transition"
            / "governance"
            / "audit-events"
        )
        canonical_freeze.parent.mkdir(parents=True)
        canonical_events.parent.mkdir(parents=True)
        self.fixture.freeze.rename(canonical_freeze)
        self.fixture.events.rename(canonical_events)
        self.fixture.freeze = canonical_freeze
        self.fixture.events = canonical_events
        stdout = io.StringIO()
        with contextlib.redirect_stdout(stdout):
            exit_code = verifier.main(
                [
                    "cutover",
                    "--project-owner-authorized",
                    "--root",
                    str(self.root),
                    "--freeze-dir",
                    f"docs/production-transition/freezes/{FREEZE_ID}",
                    "--events-dir",
                    "docs/production-transition/governance/audit-events",
                ]
            )
        self.assertEqual(0, exit_code, stdout.getvalue())
        self.assertIn("cutover: PASS", stdout.getvalue())

    def test_official_cli_rejects_a_second_freeze_candidate(self) -> None:
        canonical_freezes = self.root / "docs" / "production-transition" / "freezes"
        canonical_events = (
            self.root
            / "docs"
            / "production-transition"
            / "governance"
            / "audit-events"
        )
        canonical_freezes.mkdir(parents=True)
        canonical_events.parent.mkdir(parents=True)
        selected = canonical_freezes / FREEZE_ID
        self.fixture.freeze.rename(selected)
        self.fixture.events.rename(canonical_events)
        (canonical_freezes / "second-freeze").mkdir()
        stdout = io.StringIO()
        with contextlib.redirect_stdout(stdout):
            exit_code = verifier.main(
                [
                    "cutover",
                    "--project-owner-authorized",
                    "--root",
                    str(self.root),
                    "--freeze-dir",
                    f"docs/production-transition/freezes/{FREEZE_ID}",
                    "--events-dir",
                    "docs/production-transition/governance/audit-events",
                ]
            )
        self.assertEqual(1, exit_code)
        self.assertIn("ONE_SHOT_LAYOUT", stdout.getvalue())


if __name__ == "__main__":
    unittest.main()
