from __future__ import annotations

import contextlib
import hashlib
import io
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence
from unittest import mock

from tools import verify_production_transition as verifier


SOURCE_COMMIT = "a" * 40
DOCUMENT_REVISION = "b" * 64


class TransitionFixture:
    """Small, self-contained four-partition transition fixture."""

    def __init__(self, root: Path) -> None:
        self.root = root
        self.governance = root / "docs" / "production-transition" / "governance"
        self.registry: Dict[str, Any] = {
            "schema_version": "1.0",
            "transition_state": "cutover_candidate",
            "candidate_source_commit": SOURCE_COMMIT,
            "destinations": dict(verifier.DESTINATION_TEMPLATES),
            "records": [],
        }
        self.reviews: Dict[str, Any] = {
            "schema_version": "1.0",
            "reviews": [],
            "legacy_reviews": [],
        }
        self.decisions: Dict[str, Any] = {
            "schema_version": "1.0",
            "decisions": [],
        }
        self._seed_valid_records()

    def write_source(self, relative: str, text: str) -> str:
        path = self.root / Path(*relative.split("/"))
        path.parent.mkdir(parents=True, exist_ok=True)
        raw = text.encode("utf-8")
        path.write_bytes(raw)
        return hashlib.sha256(raw).hexdigest()

    def record(
        self,
        record_id: str,
        package: str,
        source_path: str,
        target_path: str,
        area: str,
        reviewers: Sequence[str],
        depends_on: Optional[Sequence[str]] = None,
        references: Optional[Sequence[str]] = None,
        consumer: Optional[Sequence[str]] = None,
        watch_paths: Optional[Sequence[str]] = None,
        text: Optional[str] = None,
    ) -> Dict[str, Any]:
        digest = self.write_source(source_path, text or f"# {record_id}\n")
        return {
            "id": record_id,
            "package": package,
            "source_path": source_path,
            "target_path": target_path,
            "owner": f"{record_id.lower()}-author",
            "consumer": list(consumer or [package]),
            "required_reviewers": list(reviewers),
            "as_of_commit": SOURCE_COMMIT,
            "document_revision": DOCUMENT_REVISION,
            "watch_paths": list(watch_paths or [f"watched/{record_id}/**"]),
            "freshness": "current",
            "review_status": "reviewed",
            "disposition": "include",
            "completeness": "complete",
            "readiness": "ready",
            "depends_on": list(depends_on or []),
            "blocking_decisions": [],
            "areas": [area],
            "references": list(references or []),
            "sha256": digest,
            "implementation_wave": "foundation",
            "execution_stage": "demo-pre-freeze",
            "cutover_blocking": True,
        }

    def approve(self, record: Dict[str, Any], area: str, role: str) -> None:
        self.reviews["reviews"].append(
            {
                "area_id": area,
                "card_id": record["id"],
                "document_revision": record["document_revision"],
                "source_commit": record["as_of_commit"],
                "reviewer_role": role,
                "reviewed_by": f"{role}-reviewer",
                "outcome": "approved",
                "approval": True,
            }
        )

    def decision(
        self,
        decision_id: str,
        status: str,
        affected_records: Sequence[str],
        blocks_cutover: bool = True,
        decision_text: Optional[str] = None,
    ) -> Dict[str, Any]:
        return {
            "id": decision_id,
            "status": status,
            "owner": "product-owner",
            "decision": decision_text,
            "blocks_cutover": blocks_cutover,
            "affected_records": list(affected_records),
            "as_of_commit": SOURCE_COMMIT,
        }

    def add_record(self, record: Dict[str, Any]) -> None:
        self.registry["records"].append(record)
        for area in record["areas"]:
            for role in record["required_reviewers"]:
                self.approve(record, area, role)

    def _seed_valid_records(self) -> None:
        shared = self.record(
            "SHARED-001",
            "shared",
            "docs/production-transition/shared/unit-lifecycle.md",
            "shared/unit-lifecycle.md",
            "SHARED-AREA-001",
            ["client-owner", "server-owner"],
            consumer=["client", "game-server"],
            watch_paths=["Assets/Game/**"],
        )
        client = self.record(
            "CLIENT-001",
            "client",
            "docs/production-transition/client/unit-lifecycle.md",
            "client/unit-lifecycle.md",
            "CLIENT-AREA-001",
            ["client-owner"],
            depends_on=["SHARED-001"],
            references=["SHARED-001"],
            consumer=["client"],
        )
        server = self.record(
            "SERVER-001",
            "game-server",
            "docs/production-transition/game-server/unit-lifecycle.md",
            "game-server/unit-lifecycle.md",
            "SERVER-AREA-001",
            ["server-owner"],
            depends_on=["SHARED-001"],
            references=["SHARED-001"],
            consumer=["game-server"],
        )
        references = self.record(
            "GOV-001",
            "references",
            "docs/production-transition/governance/policy.md",
            "references/policy.md",
            "GOV-AREA-001",
            ["transition-reviewer"],
            references=["SHARED-001"],
            consumer=["client", "game-server"],
        )
        for record in (shared, client, server, references):
            self.add_record(record)

    def find(self, record_id: str) -> Dict[str, Any]:
        return next(record for record in self.registry["records"] if record["id"] == record_id)

    def persist(self) -> None:
        self.governance.mkdir(parents=True, exist_ok=True)
        (self.governance / "registry.json").write_text(
            json.dumps(self.registry, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        (self.governance / "reviews.json").write_text(
            json.dumps(self.reviews, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        (self.governance / "decisions.json").write_text(
            json.dumps(self.decisions, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    def commit_blobs(self) -> Dict[str, bytes]:
        result: Dict[str, bytes] = {}
        for record in self.registry["records"]:
            path = self.root / Path(*record["source_path"].split("/"))
            if path.is_file():
                result[record["source_path"]] = path.read_bytes()
        return result

    def verify(
        self,
        mode: str,
        changes: Optional[Sequence[str]] = None,
        **kwargs: Any,
    ) -> verifier.VerificationReport:
        self.persist()
        kwargs.setdefault(
            "git_blobs_by_commit",
            {SOURCE_COMMIT: self.commit_blobs()},
        )
        return verifier.verify_transition(
            root=self.root,
            mode=mode,
            changed_paths_by_commit={SOURCE_COMMIT: list(changes or [])},
            **kwargs,
        )


class VerifyProductionTransitionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.fixture = TransitionFixture(self.root)

    @staticmethod
    def codes(report: verifier.VerificationReport) -> set[str]:
        return {item.code for item in report.diagnostics}

    def test_valid_cutover_and_target_separation(self) -> None:
        report = self.fixture.verify("cutover")

        self.assertTrue(report.ok, [item.as_dict() for item in report.errors])
        self.assertIn("shared/unit-lifecycle.md", report.target_inventories["client"])
        self.assertIn("shared/unit-lifecycle.md", report.target_inventories["game-server"])
        self.assertIn("client/unit-lifecycle.md", report.target_inventories["client"])
        self.assertNotIn("client/unit-lifecycle.md", report.target_inventories["game-server"])
        self.assertIn("game-server/unit-lifecycle.md", report.target_inventories["game-server"])
        self.assertNotIn("game-server/unit-lifecycle.md", report.target_inventories["client"])
        self.assertIn("references/policy.md", report.target_inventories["client"])
        self.assertIn("references/policy.md", report.target_inventories["game-server"])
        self.assertEqual(
            report.shared_hash,
            report.package_hashes["shared"],
        )

    def test_dry_run_manifest_matches_official_shape_and_hashes(self) -> None:
        freeze_id = "FREEZE-001"
        transition_id = "TRANSITION-001"
        report = self.fixture.verify(
            "cutover",
            dry_run_freeze_id=freeze_id,
            dry_run_transition_id=transition_id,
        )

        self.assertTrue(report.ok, [item.as_dict() for item in report.errors])
        manifest = report.dry_run_manifest
        self.assertEqual(
            {
                "schema_version",
                "freeze_id",
                "transition_id",
                "source_commit",
                "created_at",
                "packages",
                "destinations",
                "governance_attestation",
                "aggregate_sha256",
            },
            set(manifest),
        )
        self.assertEqual(
            {"shared", "client", "game-server", "references"},
            set(manifest["packages"]),
        )
        self.assertEqual(SOURCE_COMMIT, manifest["source_commit"])
        self.assertEqual(verifier.DRY_RUN_CREATED_AT, manifest["created_at"])
        self.assertEqual(freeze_id, manifest["freeze_id"])
        self.assertEqual(transition_id, manifest["transition_id"])
        self.assertEqual(
            f"somnia-client/docs/migration-input/dreamsquad-demo/{freeze_id}/",
            manifest["destinations"]["client"],
        )
        self.assertEqual(
            f"somnia-game-server/docs/migration-input/dreamsquad-demo/{freeze_id}/",
            manifest["destinations"]["game-server"],
        )
        basis = {key: value for key, value in manifest.items() if key != "aggregate_sha256"}
        aggregate = hashlib.sha256(
            json.dumps(
                basis,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        self.assertEqual(aggregate, manifest["aggregate_sha256"])
        payload_hash = hashlib.sha256(
            json.dumps(
                manifest,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        self.assertEqual(payload_hash, report.manifest_sha256)

    def test_governance_attestation_and_package_file_shapes_are_exact(self) -> None:
        report = self.fixture.verify("cutover")

        self.assertTrue(report.ok, [item.as_dict() for item in report.errors])
        attestation = report.dry_run_manifest["governance_attestation"]
        self.assertEqual({"records", "reviews", "decisions"}, set(attestation))
        self.assertEqual(4, len(attestation["records"]))
        record_keys = {
            "record_id",
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
            "implementation_wave",
            "execution_stage",
            "depends_on",
            "references",
            "areas",
            "blocking_decisions",
            "cutover_blocking",
        }
        for row in attestation["records"]:
            self.assertEqual(record_keys, set(row))
        review_keys = {
            "area_id",
            "card_id",
            "document_revision",
            "source_commit",
            "reviewer_role",
            "reviewed_by",
            "outcome",
            "approval",
        }
        self.assertTrue(attestation["reviews"])
        for row in attestation["reviews"]:
            self.assertEqual(review_keys, set(row))
        self.assertEqual([], attestation["decisions"])

        file_record_ids = set()
        for package in report.dry_run_manifest["packages"].values():
            for file_row in package["files"]:
                self.assertEqual({"record_id", "path", "size", "sha256"}, set(file_row))
                file_record_ids.add(file_row["record_id"])
        self.assertEqual(
            {row["record_id"] for row in attestation["records"]},
            file_record_ids,
        )

    def test_candidate_source_commit_is_required_and_exact_full_sha(self) -> None:
        del self.fixture.registry["candidate_source_commit"]
        missing = self.fixture.verify("prepare")
        self.assertFalse(missing.ok)
        self.assertIn("REGISTRY_MISSING_FIELDS", self.codes(missing))
        self.assertIn("CANDIDATE_SOURCE_COMMIT", self.codes(missing))

        self.fixture.registry["candidate_source_commit"] = "c" * 64
        invalid = self.fixture.verify("prepare")
        self.assertFalse(invalid.ok)
        self.assertIn("CANDIDATE_SOURCE_COMMIT", self.codes(invalid))

    def test_cutover_rejects_nonexistent_or_non_commit_candidate(self) -> None:
        with mock.patch.object(
            verifier,
            "_git_object_type",
            return_value=(None, "object does not exist"),
        ):
            nonexistent = self.fixture.verify("cutover", git_blobs_by_commit=None)
        self.assertFalse(nonexistent.ok)
        self.assertIn("CUTOVER_CANDIDATE_COMMIT_INVALID", self.codes(nonexistent))

        with mock.patch.object(
            verifier,
            "_git_object_type",
            return_value=("blob", None),
        ):
            non_commit = self.fixture.verify("cutover", git_blobs_by_commit=None)
        self.assertFalse(non_commit.ok)
        self.assertIn("CUTOVER_CANDIDATE_COMMIT_INVALID", self.codes(non_commit))

    def test_cutover_rejects_selected_source_missing_from_commit(self) -> None:
        blobs = self.fixture.commit_blobs()
        client = self.fixture.find("CLIENT-001")
        del blobs[client["source_path"]]

        report = self.fixture.verify(
            "cutover",
            git_blobs_by_commit={SOURCE_COMMIT: blobs},
        )

        self.assertFalse(report.ok)
        self.assertIn("CUTOVER_SOURCE_BLOB_MISSING", self.codes(report))
        self.assertNotIn("CUTOVER_SOURCE_BLOB_MISMATCH", self.codes(report))

    def test_cutover_rejects_selected_source_byte_mismatch(self) -> None:
        blobs = self.fixture.commit_blobs()
        client = self.fixture.find("CLIENT-001")
        blobs[client["source_path"]] = b"different committed bytes\n"

        report = self.fixture.verify(
            "cutover",
            git_blobs_by_commit={SOURCE_COMMIT: blobs},
        )

        self.assertFalse(report.ok)
        self.assertIn("CUTOVER_SOURCE_BLOB_MISMATCH", self.codes(report))
        self.assertNotIn("CUTOVER_SOURCE_BLOB_MISSING", self.codes(report))

    def test_nested_transition_attribute_preserves_commit_blob_bytes_with_autocrlf(self) -> None:
        repository = self.root / "autocrlf-repository"
        source_relative = "docs/production-transition/shared/nested/source.md"
        binary_relative = "docs/production-transition/shared/nested/fixture.bin"
        source = repository / Path(*source_relative.split("/"))
        binary = repository / Path(*binary_relative.split("/"))
        attributes = repository / "docs" / "production-transition" / ".gitattributes"
        source.parent.mkdir(parents=True)
        attributes.parent.mkdir(parents=True, exist_ok=True)
        attributes.write_bytes(b"* text=auto eol=lf\n** text=auto eol=lf\n")
        expected = b"# Selected transition source\n\nLF-only payload.\n"
        expected_binary = b"\x00binary-with-crlf-like-bytes\r\npayload\r\n\xff"
        source.write_bytes(expected)
        binary.write_bytes(expected_binary)

        def git(*arguments: str) -> subprocess.CompletedProcess[bytes]:
            return subprocess.run(
                ["git", *arguments],
                cwd=str(repository),
                check=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )

        git("init")
        git("config", "user.name", "Transition Test")
        git("config", "user.email", "transition-test@example.invalid")
        git("config", "core.autocrlf", "true")
        git(
            "add",
            "--",
            "docs/production-transition/.gitattributes",
            source_relative,
            binary_relative,
        )
        git("commit", "-m", "Add selected transition source")
        candidate_commit = git("rev-parse", "HEAD").stdout.decode("ascii").strip()

        source.unlink()
        binary.unlink()
        git("checkout", "--", source_relative, binary_relative)
        eol_report = git("ls-files", "--eol", "--", source_relative).stdout.decode(
            "utf-8", errors="replace"
        )
        self.assertIn("i/lf", eol_report)
        self.assertIn("w/lf", eol_report)
        self.assertIn("attr/text=auto eol=lf", eol_report)
        binary_eol_report = git("ls-files", "--eol", "--", binary_relative).stdout.decode(
            "utf-8", errors="replace"
        )
        self.assertIn("i/-text", binary_eol_report)
        self.assertIn("w/-text", binary_eol_report)
        self.assertIn("attr/text=auto eol=lf", binary_eol_report)

        object_type, type_error = verifier._git_object_type(repository, candidate_commit)
        committed, blob_error = verifier._git_blob_at_commit(
            repository,
            candidate_commit,
            source_relative,
        )
        committed_binary, binary_blob_error = verifier._git_blob_at_commit(
            repository,
            candidate_commit,
            binary_relative,
        )
        self.assertIsNone(type_error)
        self.assertEqual("commit", object_type)
        self.assertIsNone(blob_error)
        self.assertIsNone(binary_blob_error)
        self.assertEqual(expected, source.read_bytes())
        self.assertEqual(committed, source.read_bytes())
        self.assertEqual(expected_binary, binary.read_bytes())
        self.assertEqual(committed_binary, binary.read_bytes())

    def test_destination_typo_is_rejected(self) -> None:
        self.fixture.registry["destinations"]["client"] = (
            "somnia-clinet/docs/migration-input/dreamsquad-demo/<freeze-id>/"
        )

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("DESTINATION_TEMPLATE", self.codes(report))
        self.assertIn("DESTINATION_EXPANSION_MISMATCH", self.codes(report))

    def test_destination_swap_is_rejected(self) -> None:
        destinations = self.fixture.registry["destinations"]
        destinations["client"], destinations["game-server"] = (
            destinations["game-server"],
            destinations["client"],
        )

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("DESTINATION_TEMPLATE", self.codes(report))
        self.assertIn("DESTINATION_EXPANSION_MISMATCH", self.codes(report))

    def test_destination_freeze_id_expansion_mismatch_is_rejected(self) -> None:
        self.fixture.registry["destinations"]["client"] = (
            "somnia-client/docs/migration-input/dreamsquad-demo/OTHER-FREEZE/"
        )

        report = self.fixture.verify("prepare", dry_run_freeze_id="FREEZE-001")

        self.assertFalse(report.ok)
        self.assertIn("DESTINATION_TEMPLATE", self.codes(report))
        self.assertIn("DESTINATION_EXPANSION_MISMATCH", self.codes(report))

    def test_prepare_allows_explicit_incomplete_stale_and_blocked(self) -> None:
        record = self.fixture.find("SHARED-001")
        record.update(
            {
                "disposition": "candidate",
                "completeness": "partial",
                "freshness": "stale",
                "review_status": "draft",
                "readiness": "blocked",
                "cutover_blocking": False,
            }
        )
        self.fixture.reviews["reviews"] = [
            review for review in self.fixture.reviews["reviews"] if review["card_id"] != "SHARED-001"
        ]

        report = self.fixture.verify("prepare")

        self.assertTrue(report.ok, [item.as_dict() for item in report.errors])
        self.assertIn("PREPARE_GATE_GAP", self.codes(report))
        self.assertIn("AREA_REVIEW_MISSING", self.codes(report))

    def test_cutover_rejects_non_ready_and_unlocked_scope(self) -> None:
        record = self.fixture.find("CLIENT-001")
        record.update(
            {
                "disposition": "candidate",
                "completeness": "partial",
                "review_status": "draft",
                "readiness": "blocked",
                "cutover_blocking": False,
            }
        )
        references = self.fixture.find("GOV-001")
        references["consumer"] = ["client"]
        references["references"].append("CLIENT-001")

        report = self.fixture.verify("cutover")

        self.assertFalse(report.ok)
        self.assertIn("CUTOVER_SCOPE_UNLOCKED", self.codes(report))
        self.assertIn("CUTOVER_CLOSURE", self.codes(report))

    def test_missing_required_registry_field_fails(self) -> None:
        del self.fixture.find("CLIENT-001")["consumer"]

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("RECORD_MISSING_FIELDS", self.codes(report))

    def test_watch_path_change_invalidates_current_record(self) -> None:
        report = self.fixture.verify("prepare", changes=["Assets/Game/Unit.cs"])

        self.assertFalse(report.ok)
        self.assertIn("WATCH_PATH_STALE", self.codes(report))

    def test_unresolved_blocker_fails_cutover_but_is_reported_in_prepare(self) -> None:
        self.fixture.decisions["decisions"].append(
            self.fixture.decision("DEC-001", "open", ["SHARED-001"])
        )
        shared = self.fixture.find("SHARED-001")
        shared["blocking_decisions"] = ["DEC-001"]
        shared["readiness"] = "blocked"

        prepare = self.fixture.verify("prepare")
        cutover = self.fixture.verify("cutover")

        self.assertTrue(prepare.ok)
        self.assertIn("UNRESOLVED_BLOCKER", self.codes(prepare))
        self.assertFalse(cutover.ok)
        self.assertIn("GLOBAL_CUTOVER_BLOCKER", self.codes(cutover))
        self.assertIn("CUTOVER_GATE", self.codes(cutover))

    def test_irrelevant_legacy_review_and_decision_are_not_attested(self) -> None:
        legacy_review = dict(self.fixture.reviews["reviews"][0])
        legacy_review["reviewed_by"] = "legacy-reviewer"
        self.fixture.reviews["legacy_reviews"].append(legacy_review)
        self.fixture.decisions["decisions"].append(
            self.fixture.decision(
                "DEC-IRRELEVANT",
                "decided",
                [],
                blocks_cutover=False,
                decision_text="Not referenced by the selected record set",
            )
        )

        report = self.fixture.verify("cutover")

        self.assertTrue(report.ok, [item.as_dict() for item in report.errors])
        attestation = report.dry_run_manifest["governance_attestation"]
        self.assertFalse(
            any(row["reviewed_by"] == "legacy-reviewer" for row in attestation["reviews"])
        )
        self.assertFalse(
            any(row["id"] == "DEC-IRRELEVANT" for row in attestation["decisions"])
        )

    def test_relevant_approved_review_and_decided_decision_are_attested(self) -> None:
        client = self.fixture.find("CLIENT-001")
        client["blocking_decisions"] = ["DEC-CLIENT-001"]
        decision = self.fixture.decision(
            "DEC-CLIENT-001",
            "decided",
            ["CLIENT-001"],
            decision_text="Use confirmed lifecycle projection",
        )
        self.fixture.decisions["decisions"].append(decision)

        report = self.fixture.verify("cutover")

        self.assertTrue(report.ok, [item.as_dict() for item in report.errors])
        attestation = report.dry_run_manifest["governance_attestation"]
        approved_review = next(
            row for row in attestation["reviews"] if row["card_id"] == "CLIENT-001"
        )
        self.assertTrue(approved_review["approval"])
        self.assertEqual("approved", approved_review["outcome"])
        self.assertEqual(
            {
                "id": "DEC-CLIENT-001",
                "status": "decided",
                "owner": "product-owner",
                "decision": "Use confirmed lifecycle projection",
                "blocks_cutover": True,
                "affected_records": ["CLIENT-001"],
                "as_of_commit": SOURCE_COMMIT,
            },
            attestation["decisions"][0],
        )

    def test_decision_affected_record_requires_record_back_reference(self) -> None:
        self.fixture.decisions["decisions"].append(
            self.fixture.decision(
                "DEC-ONE-WAY",
                "decided",
                ["CLIENT-001"],
                decision_text="One-way decision link",
            )
        )
        self.fixture.decisions["decisions"].append(
            self.fixture.decision(
                "DEC-UNKNOWN",
                "decided",
                ["MISSING-RECORD"],
                decision_text="Unknown affected record",
            )
        )

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("DECISION_RECORD_LINK_MISMATCH", self.codes(report))
        self.assertIn("DECISION_AFFECTED_RECORD_UNKNOWN", self.codes(report))

    def test_record_blocking_decision_requires_decision_affected_record(self) -> None:
        client = self.fixture.find("CLIENT-001")
        client["blocking_decisions"] = ["DEC-ONE-WAY"]
        self.fixture.decisions["decisions"].append(
            self.fixture.decision(
                "DEC-ONE-WAY",
                "decided",
                [],
                decision_text="Missing affected record link",
            )
        )

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("DECISION_RECORD_LINK_MISMATCH", self.codes(report))

    def test_each_area_requires_exact_review(self) -> None:
        shared = self.fixture.find("SHARED-001")
        shared["areas"].append("SHARED-AREA-002")

        report = self.fixture.verify("cutover")

        self.assertFalse(report.ok)
        self.assertIn("AREA_REVIEW_MISSING", self.codes(report))

    def test_old_document_revision_review_does_not_approve(self) -> None:
        for review in self.fixture.reviews["reviews"]:
            if review["card_id"] == "CLIENT-001":
                review["document_revision"] = "c" * 64

        report = self.fixture.verify("cutover")

        self.assertFalse(report.ok)
        self.assertIn("AREA_REVIEW_MISSING", self.codes(report))

    def test_record_owner_cannot_approve_own_revision(self) -> None:
        shared = self.fixture.find("SHARED-001")
        review = next(
            row
            for row in self.fixture.reviews["reviews"]
            if row["card_id"] == "SHARED-001"
        )
        review["reviewed_by"] = shared["owner"]

        report = self.fixture.verify("cutover")

        self.assertFalse(report.ok)
        self.assertIn("REVIEW_SELF_APPROVAL", self.codes(report))

    def test_unknown_dependency_and_reference_fail_closure(self) -> None:
        client = self.fixture.find("CLIENT-001")
        client["depends_on"].append("MISSING-DEP")
        client["references"].append("MISSING-REF")

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("CLOSURE_UNKNOWN_RECORD", self.codes(report))

    def test_shared_link_to_server_only_artifact_fails_client_snapshot(self) -> None:
        shared = self.fixture.find("SHARED-001")
        shared["sha256"] = self.fixture.write_source(
            shared["source_path"],
            "[server lifecycle](../game-server/unit-lifecycle.md)\n",
        )

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        failures = [
            item
            for item in report.errors
            if item.code == "TARGET_LINK_CLOSURE" and item.record_id == "SHARED-001"
        ]
        self.assertTrue(failures)
        self.assertTrue(any("client snapshot" in item.message for item in failures))
        self.assertFalse(any("game-server snapshot" in item.message for item in failures))

    def test_relocated_charter_source_relative_link_fails_target_resolution(self) -> None:
        charter = self.fixture.find("GOV-001")
        charter["target_path"] = "references/governance/transition-charter.md"
        charter["sha256"] = self.fixture.write_source(
            charter["source_path"],
            "[shared lifecycle](../shared/unit-lifecycle.md)\n",
        )

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("TARGET_LINK_CLOSURE", self.codes(report))
        self.assertTrue(
            any(
                "references/shared/unit-lifecycle.md" in item.message
                for item in report.errors
                if item.code == "TARGET_LINK_CLOSURE"
            )
        )

    def test_dependency_missing_from_record_consumer_snapshot_fails(self) -> None:
        references = self.fixture.find("GOV-001")
        references["consumer"] = ["client"]
        references["depends_on"] = ["SERVER-001"]

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        failures = [
            item
            for item in report.errors
            if item.code == "TARGET_DEPENDENCY_CLOSURE" and item.record_id == "GOV-001"
        ]
        self.assertTrue(failures)
        self.assertTrue(any("client snapshot" in item.message for item in failures))

    def test_valid_reference_partition_link_with_anchor_passes(self) -> None:
        references = self.fixture.find("GOV-001")
        references["sha256"] = self.fixture.write_source(
            references["source_path"],
            "[shared lifecycle](../shared/unit-lifecycle.md#confirmed-state)\n",
        )

        report = self.fixture.verify("prepare")

        self.assertTrue(report.ok, [item.as_dict() for item in report.errors])
        self.assertNotIn("TARGET_LINK_CLOSURE", self.codes(report))
        self.assertNotIn("TARGET_DEPENDENCY_CLOSURE", self.codes(report))

    def test_cross_package_dependency_is_rejected(self) -> None:
        self.fixture.find("CLIENT-001")["depends_on"].append("SERVER-001")

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("PACKAGE_BOUNDARY", self.codes(report))

    def test_dependency_cycle_is_rejected(self) -> None:
        # References may depend on any partition; make the shared dependency
        # point back through references to form a structural cycle.
        self.fixture.find("GOV-001")["depends_on"] = ["SHARED-001"]
        self.fixture.find("SHARED-001")["depends_on"] = ["GOV-001"]

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("DEPENDENCY_CYCLE", self.codes(report))

    def test_target_path_traversal_is_rejected(self) -> None:
        self.fixture.find("CLIENT-001")["target_path"] = "../escape.md"

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("PATH_ESCAPE", self.codes(report))

    def test_target_path_casefold_duplicate_is_rejected(self) -> None:
        duplicate = self.fixture.record(
            "CLIENT-002",
            "client",
            "docs/production-transition/client/second.md",
            "client/UNIT-LIFECYCLE.md",
            "CLIENT-AREA-002",
            ["client-owner"],
            consumer=["client"],
        )
        self.fixture.add_record(duplicate)

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("TARGET_PATH_DUPLICATE", self.codes(report))

    def test_same_source_cannot_be_client_and_server_exclusive(self) -> None:
        client = self.fixture.find("CLIENT-001")
        server = self.fixture.find("SERVER-001")
        server["source_path"] = client["source_path"]
        server["sha256"] = client["sha256"]

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("SOURCE_PATH_DUPLICATE", self.codes(report))

    def test_shared_consumer_mismatch_changes_list_and_hash(self) -> None:
        self.fixture.find("SHARED-001")["consumer"] = ["client"]

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("SHARED_INVENTORY_MISMATCH", self.codes(report))
        self.assertIn("SHARED_HASH_MISMATCH", self.codes(report))
        self.assertIn("PACKAGE_CONSUMER_MISMATCH", self.codes(report))

    def test_sha256_mismatch_is_rejected(self) -> None:
        self.fixture.find("SERVER-001")["sha256"] = "0" * 64

        report = self.fixture.verify("prepare")

        self.assertFalse(report.ok)
        self.assertIn("SHA256_MISMATCH", self.codes(report))

    def test_manifest_and_package_hashes_are_order_independent(self) -> None:
        shared = self.fixture.find("SHARED-001")
        client = self.fixture.find("CLIENT-001")
        server = self.fixture.find("SERVER-001")
        shared["blocking_decisions"] = ["DEC-B", "DEC-A"]
        client["blocking_decisions"] = ["DEC-A"]
        server["blocking_decisions"] = ["DEC-B"]
        self.fixture.decisions["decisions"].extend(
            [
                self.fixture.decision(
                    "DEC-B",
                    "decided",
                    ["SERVER-001", "SHARED-001"],
                    decision_text="Decision B",
                ),
                self.fixture.decision(
                    "DEC-A",
                    "decided",
                    ["CLIENT-001", "SHARED-001"],
                    decision_text="Decision A",
                ),
            ]
        )
        first = self.fixture.verify("cutover")
        self.fixture.registry["records"].reverse()
        self.fixture.reviews["reviews"].reverse()
        self.fixture.decisions["decisions"].reverse()
        shared["blocking_decisions"].reverse()
        for decision in self.fixture.decisions["decisions"]:
            decision["affected_records"].reverse()
        second = self.fixture.verify("cutover")

        self.assertTrue(first.ok)
        self.assertTrue(second.ok)
        self.assertEqual(first.manifest_sha256, second.manifest_sha256)
        self.assertEqual(first.package_hashes, second.package_hashes)
        self.assertEqual(first.target_inventories, second.target_inventories)
        self.assertEqual(first.dry_run_manifest, second.dry_run_manifest)

    def test_cutover_requires_candidate_state(self) -> None:
        self.fixture.registry["transition_state"] = "preparing"

        report = self.fixture.verify("cutover")

        self.assertFalse(report.ok)
        self.assertIn("CUTOVER_STATE", self.codes(report))

    def test_dormant_prepare_runs_structural_verification(self) -> None:
        self.fixture.registry["transition_state"] = "dormant"

        report = self.fixture.verify("prepare")

        self.assertTrue(report.ok, [item.as_dict() for item in report.errors])
        self.assertEqual("dormant", report.transition_state)
        self.assertNotIn("TRANSITION_STATE", self.codes(report))

    def test_authorized_cli_runs_dormant_prepare_verification(self) -> None:
        self.fixture.registry["transition_state"] = "dormant"
        self.fixture.persist()
        stdout = io.StringIO()
        with mock.patch.object(verifier, "_git_changed_paths", return_value=([], None)):
            with contextlib.redirect_stdout(stdout):
                exit_code = verifier.main(
                    [
                        "prepare",
                        "--root",
                        str(self.root),
                        "--project-owner-authorized",
                        "--json",
                    ]
                )

        output = json.loads(stdout.getvalue())
        self.assertEqual(0, exit_code, output)
        self.assertTrue(output["ok"])
        self.assertEqual("prepare", output["mode"])
        self.assertEqual("dormant", output["transition_state"])

    def test_dormant_cutover_is_rejected(self) -> None:
        self.fixture.registry["transition_state"] = "dormant"

        report = self.fixture.verify("cutover")

        self.assertFalse(report.ok)
        self.assertEqual("dormant", report.transition_state)
        self.assertIn("CUTOVER_STATE", self.codes(report))

    def test_unauthorized_cli_skips_without_entering_verification(self) -> None:
        empty_root = self.root / "empty-repository"
        stdout = io.StringIO()
        with contextlib.ExitStack() as stack:
            verify_transition = stack.enter_context(
                mock.patch.object(verifier, "verify_transition")
            )
            resolve_cli_path = stack.enter_context(
                mock.patch.object(verifier, "_resolve_cli_path")
            )
            load_json = stack.enter_context(mock.patch.object(verifier, "_load_json"))
            git_changed_paths = stack.enter_context(
                mock.patch.object(verifier, "_git_changed_paths")
            )
            stack.enter_context(contextlib.redirect_stdout(stdout))
            exit_code = verifier.main(
                ["prepare", "--root", str(empty_root), "--json"]
            )

        output = json.loads(stdout.getvalue())
        self.assertEqual(0, exit_code)
        self.assertEqual("SKIP", output["result"])
        self.assertEqual("prepare", output["mode"])
        self.assertIn("project owner", output["reason"])
        verify_transition.assert_not_called()
        resolve_cli_path.assert_not_called()
        load_json.assert_not_called()
        git_changed_paths.assert_not_called()
        self.assertFalse(empty_root.exists())

    def test_read_only_cli_does_not_create_freeze_or_target_directories(self) -> None:
        self.fixture.persist()
        stdout = io.StringIO()
        with mock.patch.object(verifier, "_git_changed_paths", return_value=([], None)):
            with mock.patch.object(verifier, "_git_object_type", return_value=("commit", None)):
                with mock.patch.object(
                    verifier,
                    "_git_blob_at_commit",
                    side_effect=lambda root, _commit, path: (
                        (root / Path(*path.split("/"))).read_bytes(),
                        None,
                    ),
                ):
                    with contextlib.redirect_stdout(stdout):
                        exit_code = verifier.main(
                            [
                                "cutover",
                                "--root",
                                str(self.root),
                                "--project-owner-authorized",
                                "--json",
                            ]
                        )

        self.assertEqual(0, exit_code, stdout.getvalue())
        self.assertFalse((self.root / "docs" / "production-transition" / "freezes").exists())
        self.assertFalse((self.root / "docs" / "migration-input").exists())


if __name__ == "__main__":
    unittest.main()
