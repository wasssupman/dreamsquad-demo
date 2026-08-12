from __future__ import annotations

import contextlib
import io
import tempfile
import unittest
from pathlib import Path
from typing import Dict

from tools import verify_demo_transition_firewall as firewall


ROOT_README = """# Demo repository

The Demo source of truth is the active Demo spec and implementation.
`docs/production-transition/` is dormant downstream material, owner-gated by the
Project owner, and is not an implementation basis for Demo work. Default reading
excludes production-transition material.
"""

PRD = """# PRD

The current Demo goals and active specs are the source of truth.
`docs/production-transition/` is owner-gated dormant downstream material and is
not a current implementation baseline.
"""

TRD = """# TRD

This is the Demo technical source of truth.
`docs/production-transition/` is owner-gated dormant downstream material and cannot
be used as an architecture or implementation basis.
"""

CLAUDE = """# Project instructions

The Demo is the only upstream source of truth for design, implementation, and validation.
`docs/production-transition/` is owner-gated dormant downstream material and must not be
used as a Demo implementation basis without an explicit current Project owner request.
"""

CATCHUP = """# Catchup

`docs/production-transition/**` is owner-gated dormant downstream material.
Unless the current user request explicitly activates it, exclude transition-only
commits from active-spec and next-work inference and ignore the subtree.
"""

SPEC_INDEX = """# Spec index
"""


class FirewallFixture:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.write("README.md", ROOT_README)
        self.write("CLAUDE.md", CLAUDE)
        self.write("docs/PRD.md", PRD)
        self.write("docs/TRD.md", TRD)
        self.write(".codex/skills/catchup/SKILL.md", CATCHUP)
        self.write("docs/spec/README.md", SPEC_INDEX)
        self.write("docs/spec/live-feature/README.md", "# Live Demo feature\n")
        self.write("docs/plans/current-design.md", "# Current Demo design\n")
        self.write("Assets/keep.txt", "Demo asset\n")
        self.write("Packages/manifest.json", "{}\n")
        self.write("ProjectSettings/ProjectSettings.asset", "DemoSettings: 1\n")

        # These are the only accepted places outside the transition subtree for
        # the verifier's name. They are not automatic Demo validation entrypoints.
        self.write(
            "tools/verify_production_transition.py",
            "# owner-gated standalone verifier\n",
        )
        self.write(
            "tools/test_verify_production_transition.py",
            "from tools import verify_production_transition\n",
        )
        self.write(
            "tools/README.md",
            "Run verify_production_transition only with owner authorization.\n",
        )
        self.write(
            "docs/production-transition/governance/check.sh",
            "python tools/verify_production_transition.py prepare\n",
        )

    def write(self, relative: str, text: str) -> Path:
        path = self.root / Path(relative)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def snapshot(self) -> Dict[str, bytes]:
        return {
            path.relative_to(self.root).as_posix(): path.read_bytes()
            for path in sorted(self.root.rglob("*"))
            if path.is_file()
        }


class DemoTransitionFirewallTests(unittest.TestCase):
    def test_valid_repository_passes_without_writing_files(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            before = fixture.snapshot()

            violations = firewall.verify(fixture.root)

            self.assertEqual([], violations)
            self.assertEqual(before, fixture.snapshot())

    def test_demo_runtime_changes_do_not_require_transition_updates(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            transition_note = fixture.write(
                "docs/production-transition/stale-note.md",
                "# Dormant transition snapshot\n\nIntentionally stale.\n",
            )
            transition_before = transition_note.read_bytes()

            fixture.write("Assets/Battle/DemoChange.cs", "// Demo battle change\n")
            fixture.write("Assets/Bridge/DemoChange.cs", "// Demo bridge change\n")
            fixture.write(
                "Assets/Presentation/DemoChange.cs",
                "// Demo presentation change\n",
            )

            self.assertEqual([], firewall.verify(fixture.root))
            self.assertEqual(transition_before, transition_note.read_bytes())

    def test_cli_reports_pass_and_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                result = firewall.main(["--root", str(fixture.root)])
            self.assertEqual(0, result)
            self.assertIn("PASS", output.getvalue())

            fixture.write(
                "Assets/RuntimeConfig.txt",
                "docs/production-transition/quarantine/config\n",
            )
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                result = firewall.main(["--root", str(fixture.root)])
            self.assertEqual(1, result)
            self.assertIn("runtime-transition-reference", output.getvalue())

    def test_legacy_transition_specs_are_rejected(self) -> None:
        for relative in firewall.LEGACY_ACTIVE_PATHS:
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                legacy_path = relative if Path(relative).suffix else f"{relative}/README.md"
                fixture.write(legacy_path, "# Legacy active transition material\n")

                violations = firewall.verify(fixture.root)

                self.assertTrue(
                    any(
                        item.rule == "legacy-active-path" and item.path == relative
                        for item in violations
                    )
                )

    def test_only_governance_old_path_is_a_forbidden_legacy_active_path(self) -> None:
        self.assertEqual(
            ("docs/spec/production-transition-governance",),
            firewall.LEGACY_ACTIVE_PATHS,
        )

    def test_active_spec_cannot_use_transition_as_implementation_source(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            fixture.write(
                "docs/spec/live-feature/0_contract.md",
                "Implementation must follow "
                "[transition architecture](../../production-transition/architecture/README.md).\n",
            )

            violations = firewall.verify(fixture.root)

            self.assertTrue(
                any(item.rule == "active-doc-transition-authority" for item in violations)
            )

    def test_active_plan_cannot_make_transition_authoritative(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            fixture.write(
                "docs/plans/current-design.md",
                "Use production-transition as the source of truth for this design.\n",
            )

            violations = firewall.verify(fixture.root)

            self.assertTrue(
                any(item.rule == "active-doc-transition-authority" for item in violations)
            )

    def test_active_safe_notice_cannot_mask_a_positive_authority_tail(self) -> None:
        statements = (
            "Production-transition is not a checklist while it dictates Demo behavior.\n",
            "Production-transition is not a checklist though it is authoritative "
            "for Demo implementation.\n",
            "Production-transition is not a checklist / it dictates Demo behavior.\n",
            "Production-transition is not a Demo source of truth, "
            "It dictates Demo behavior.\n",
        )
        for statement in statements:
            with self.subTest(statement=statement), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                relative = "docs/spec/live-feature/README.md"
                fixture.write(relative, statement)

                self.assertTrue(
                    any(
                        item.rule in {
                            "active-doc-transition-authority",
                            "active-doc-transition-work-item",
                        }
                        and item.path == relative
                        for item in firewall.verify(fixture.root)
                    )
                )

    def test_active_docs_cannot_track_transition_work_items(self) -> None:
        cases = (
            (
                "docs/spec/live-feature/README.md",
                "Keep this as the production-transition checklist.\n",
            ),
            (
                "docs/spec/live-feature/6_handoff_summary.md",
                "후속: production-transition 체크리스트로 확인한다.\n",
            ),
            (
                "docs/spec/live-feature/0_contract.md",
                "production-transition 이전 판정 때 재검토한다.\n",
            ),
            (
                "docs/plans/current-design.md",
                "Production-transition acceptance gate before completion.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "이번에 고치지 않고 여기 명시만 한다 — "
                "production-transition 이전 판정 때의 체크리스트.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is a completion condition.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Track production-transition as a work item.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition checklist must be completed. "
                "Demo implementation is not affected.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "production-transition 체크리스트를 완료한다. "
                "Demo 구현에는 영향이 없다.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth. "
                "Follow-up: transfer after approval.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition must be reviewed. "
                "Production-transition is not a Demo checklist.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "production-transition 체크리스트로 관리하고 "
                "후속 항목은 관리하지 않는다.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "TODO: production-transition transfer after approval.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "production-transition 작업: 승인 후 이동.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Do not use production-transition as a checklist, "
                "then transfer after approval.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Do not use production-transition as a checklist and move it to the client.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Do not use production-transition as a checklist, transfer when approved.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth and "
                "its follow-up is required.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "production-transition은 Demo 정본이 아니며 후속은 필요하다.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "production-transition은 Demo 체크리스트가 아니며 승인되면 이동.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth — "
                "Follow-up: transfer.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "production-transition은 Demo 정본이 아니다 — 후속: 이관.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth, "
                "Follow-up: transfer.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "production-transition은 Demo 정본이 아니다, 후속: 이관.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth, transfer it tomorrow.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth: move it to the client.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth, review it tomorrow.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth, transfer it if approved.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth, but the next step is package preparation.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth and archive it for production.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth and hand over the package.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth, the team transfers it tomorrow.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth, the owner archives it tomorrow.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth, perform cutover tomorrow.\n",
            ),
            (
                "docs/spec/live-feature/README.md",
                "Production-transition is not a Demo source of truth, migrate it tomorrow.\n",
            ),
        )
        for relative, content in cases:
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                fixture.write(relative, content)

                violations = firewall.verify(fixture.root)

                matches = [
                    item
                    for item in violations
                    if item.rule == "active-doc-transition-work-item"
                    and item.path == relative
                ]
                self.assertEqual(1, len(matches))
                self.assertEqual(1, matches[0].line)

    def test_explicitly_negated_transition_work_items_are_allowed(self) -> None:
        statements = (
            "Production-transition is not a Demo checklist, follow-up, "
            "completion condition, or gate.\n",
            "Production-transition은 Demo 체크리스트나 후속 완료 조건이 아니며 "
            "검증 gate로 사용하지 않는다.\n",
            "production-transition 체크리스트로 사용하지 않는다.\n",
            "Do not use production-transition as a checklist.\n",
            "Production-transition must not be tracked as follow-up.\n",
            "Do not transfer production-transition to the client.\n",
            "Production-transition must not be moved.\n",
            "Production-transition cannot be transferred.\n",
            "production-transition을 이동하지 않는다.\n",
            "Never use production-transition as a Demo source of truth.\n",
            "Production-transition will never be transferred.\n",
            "Production-transition is not required for Demo work.\n",
            "production-transition은 Demo 작업에 필요하지 않다.\n",
            "Production-transition preparation is not a Demo completion gate.\n",
            "The production-transition archive is historical and not a Demo source of truth.\n",
            "Production-transition review is not required for Demo work.\n",
            "Production-transition must not be tracked as follow-up.\n"
            "  - It is not a checklist.\n"
            "  - Follow-up is not required.\n",
        )
        for statement in statements:
            with self.subTest(statement=statement), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                fixture.write("docs/spec/live-feature/README.md", statement)

                self.assertEqual([], firewall.verify(fixture.root))

    def test_multiline_transition_work_items_are_rejected_at_reference_line(self) -> None:
        statements = (
            "Production-transition is the\nDemo completion gate.\n",
            "Production-transition을\n후속 체크리스트로 관리한다.\n",
            "Production-transition:\n- Demo completion gate.\n",
            "## Production-transition\n- Follow-up: transfer after approval.\n",
            "| Production-transition |\n| completion gate |\n",
            "| Production-transition is not a Demo source of truth |\n"
            "|---|\n| Follow-up: transfer after approval |\n",
        )
        for statement in statements:
            with self.subTest(statement=statement), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                relative = "docs/spec/live-feature/README.md"
                fixture.write(relative, "# Live feature\n\n" + statement)

                violations = firewall.verify(fixture.root)

                matches = [
                    item
                    for item in violations
                    if item.path == relative
                    and item.rule in {
                        "active-doc-transition-work-item",
                        "active-doc-transition-authority",
                    }
                ]
                self.assertEqual(1, len(matches))
                self.assertEqual(3, matches[0].line)

    def test_safe_notice_does_not_exempt_positive_child_item(self) -> None:
        for separator in ("", "\n"):
            with self.subTest(separator=repr(separator)), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                relative = "docs/spec/live-feature/README.md"
                fixture.write(
                    relative,
                    "Production-transition is not a Demo source of truth.\n"
                    + separator
                    + "- Follow-up: transfer after approval.\n",
                )

                violations = firewall.verify(fixture.root)

                self.assertTrue(
                    any(
                        item.path == relative
                        and item.rule == "active-doc-transition-work-item"
                        for item in violations
                    )
                )

    def test_plain_safe_notice_scans_the_entire_following_list_block(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            relative = "docs/spec/live-feature/README.md"
            fixture.write(
                relative,
                "Production-transition is not a Demo source of truth.\n\n"
                "- It is not a checklist.\n"
                "- Follow-up: transfer after approval.\n",
            )

            self.assertTrue(
                any(
                    item.path == relative
                    and item.rule == "active-doc-transition-work-item"
                    for item in firewall.verify(fixture.root)
                )
            )

    def test_safe_notice_cannot_hide_structural_transition_work(self) -> None:
        statements = (
            "- Production-transition is not a Demo source of truth.\n"
            "- Follow-up: transfer after approval.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "- It is not a checklist.\n"
            "- Follow-up: transfer after approval.\n",
            "Production-transition is not a Demo source of truth.\n\n"
            "1. Follow-up: transfer after approval.\n",
            "Production-transition is not a Demo source of truth.\n\n"
            "> Follow-up: transfer after approval.\n",
            "Production-transition is not a Demo source of truth.\n"
            "Use it as the Demo implementation basis.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "- The client will import it after approval.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "- This package moves to production tomorrow.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "- We review and transfer it tomorrow.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "- The client imports it tomorrow.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "- The server exports it tomorrow.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "- The owner approves it tomorrow.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "- It is the Demo implementation contract.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "- Update it every sprint.\n",
            "Production-transition is not a Demo source of truth.\n"
            "It governs Demo work.\n",
            "Production-transition is not a Demo source of truth.\n"
            "It is canonical for the Demo.\n",
            "Production-transition is not a Demo source of truth.\n"
            "It dictates Demo behavior.\n",
            "Production-transition is not a Demo source of truth.\n"
            "It is binding on the Demo.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "  - It governs Demo work.\n",
        )
        for statement in statements:
            with self.subTest(statement=statement), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                relative = "docs/spec/live-feature/README.md"
                fixture.write(relative, statement)

                self.assertTrue(
                    any(
                        item.path == relative
                        and item.rule == "active-doc-transition-work-item"
                        for item in firewall.verify(fixture.root)
                    )
                )

    def test_nested_safe_child_does_not_hide_later_positive_sibling(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            relative = "docs/spec/live-feature/README.md"
            fixture.write(
                relative,
                "- Production-transition is not a Demo source of truth.\n"
                "  - It is not a checklist.\n"
                "  - Follow-up: transfer after approval.\n",
            )

            self.assertTrue(
                any(
                    item.path == relative
                    and item.rule == "active-doc-transition-work-item"
                    for item in firewall.verify(fixture.root)
                )
            )

    def test_list_item_indented_continuation_cannot_hide_transition_work(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            relative = "docs/spec/live-feature/README.md"
            fixture.write(
                relative,
                "- Production-transition is not a Demo source of truth.\n"
                "  Follow-up: transfer after approval.\n",
            )

            self.assertTrue(
                any(
                    item.path == relative
                    and item.rule == "active-doc-transition-work-item"
                    for item in firewall.verify(fixture.root)
                )
            )

    def test_active_docs_cannot_instruct_transition_verifier_execution(self) -> None:
        for relative in (
            "docs/spec/live-feature/README.md",
            "docs/plans/current-design.md",
        ):
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                fixture.write(
                    relative,
                    "python tools/verify_production_transition.py prepare "
                    "--project-owner-authorized\n",
                )

                violations = firewall.verify(fixture.root)

                self.assertTrue(
                    any(
                        item.rule == "active-doc-transition-verifier"
                        and item.path == relative
                        for item in violations
                    )
                )

    def test_non_authoritative_notice_and_transition_subtree_are_allowed(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            fixture.write(
                "docs/spec/README.md",
                SPEC_INDEX
                + "\nProduction-transition is not a Demo source of truth or work candidate.\n",
            )
            fixture.write(
                "docs/production-transition/governance/historical-note.md",
                "# Historical transition governance note\n",
            )

            violations = firewall.verify(fixture.root)

            self.assertEqual([], violations)

    def test_ci_hooks_and_general_scripts_cannot_invoke_transition_verifier(self) -> None:
        candidates = (
            ".github/workflows/check.yml",
            ".github/actions/demo-check/action.yml",
            ".githooks/pre-commit",
            "scripts/validate.ps1",
            "tools/verify_all.py",
        )
        for relative in candidates:
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                fixture.write(
                    relative,
                    "python tools/verify_production_transition.py prepare\n",
                )

                violations = firewall.verify(fixture.root)

                self.assertTrue(
                    any(
                        item.rule == "automatic-transition-verifier"
                        and item.path == relative
                        for item in violations
                    )
                )

    def test_claude_agent_hooks_cannot_invoke_transition_verifier(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            relative = ".claude/hooks/demo-check.ps1"
            fixture.write(
                relative,
                "python tools/verify_production_transition.py prepare\n",
            )

            violations = firewall.verify(fixture.root)

            self.assertTrue(
                any(
                    item.rule == "automatic-transition-verifier"
                    and item.path == relative
                    for item in violations
                )
            )

    def test_codex_agent_hooks_cannot_invoke_transition_verifier(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            relative = ".codex/hooks/demo-check.ps1"
            fixture.write(
                relative,
                "python tools/verify_production_transition.py prepare\n",
            )

            violations = firewall.verify(fixture.root)

            self.assertTrue(
                any(
                    item.rule == "automatic-transition-verifier"
                    and item.path == relative
                    for item in violations
                )
            )

    def test_root_demo_scripts_cannot_invoke_transition_verifier(self) -> None:
        for relative in ("validate.ps1", "build.cmd", "check.sh", "run-tests.py"):
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                fixture.write(
                    relative,
                    "python tools/verify_production_transition.py prepare "
                    "--project-owner-authorized\n",
                )

                self.assertTrue(
                    any(
                        item.rule == "automatic-transition-verifier"
                        and item.path == relative
                        for item in firewall.verify(fixture.root)
                    )
                )

    def test_root_policy_documents_require_demo_owner_and_dormant_invariants(self) -> None:
        for relative in ("CLAUDE.md", "README.md", "docs/PRD.md", "docs/TRD.md"):
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                fixture.write(
                    relative,
                    "Production-transition is the implementation source for Demo work.\n",
                )

                violations = firewall.verify(fixture.root)

                matches = [
                    item
                    for item in violations
                    if item.rule == "root-firewall-policy" and item.path == relative
                ]
                self.assertEqual(1, len(matches))
                self.assertIn("dormant state", matches[0].message)
                self.assertIn("Project owner gate", matches[0].message)
                self.assertIn("explicit non-authority statement", matches[0].message)

    def test_root_readme_requires_default_reading_exclusion(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            fixture.write(
                "README.md",
                "The Demo source of truth is the active implementation. "
                "Production-transition is Project owner-gated dormant downstream "
                "and not an implementation basis.\n",
            )

            violations = firewall.verify(fixture.root)

            match = next(
                item
                for item in violations
                if item.rule == "root-firewall-policy" and item.path == "README.md"
            )
            self.assertIn("default-reading exclusion", match.message)

    def test_root_policy_invariants_do_not_hide_conflicting_transition_authority(self) -> None:
        cases = {
            "CLAUDE.md": "Production-transition is a Demo completion gate.\n",
            "README.md": "Demo implementation must follow production-transition.\n",
            "docs/PRD.md": "Track production-transition as a Demo work item.\n",
            "docs/TRD.md": "Production-transition is the architecture source of truth.\n",
        }
        for relative, conflict in cases.items():
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                current = (fixture.root / relative).read_text(encoding="utf-8")
                fixture.write(relative, current + "\n" + conflict)

                violations = firewall.verify(fixture.root)

                self.assertTrue(
                    any(
                        item.rule == "root-policy-transition-authority"
                        and item.path == relative
                        for item in violations
                    )
                )

    def test_root_policy_conflict_negation_must_cover_the_authority_claim(self) -> None:
        statements = (
            "Production-transition is the Demo source of truth, but not a checklist.\n",
            "Production-transition is authoritative for Demo implementation, but not a work item.\n",
            "Demo implementation must follow production-transition, but transition is not a completion gate.\n",
            "production-transition은 Demo 정본이지만 체크리스트는 아니다.\n",
            "Production-transition is not a checklist, but is authoritative for Demo implementation.\n",
            "Production-transition is not a work item; it is the Demo source of truth.\n",
            "Production-transition is not a completion gate, however Demo implementation must follow it.\n",
            "production-transition은 체크리스트가 아니지만 Demo 정본이다.\n",
            "Production-transition defines the Demo requirements.\n",
            "Production-transition drives Demo design.\n",
            "Production-transition dictates Demo behavior.\n",
            "Production-transition is canonical for the Demo.\n",
            "Production-transition is binding on Demo implementation.\n",
            "Treat production-transition as canonical for the Demo.\n",
        )
        for statement in statements:
            with self.subTest(statement=statement), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                current = (fixture.root / "CLAUDE.md").read_text(encoding="utf-8")
                fixture.write("CLAUDE.md", current + "\n" + statement)

                self.assertTrue(
                    any(
                        item.rule == "root-policy-transition-authority"
                        and item.path == "CLAUDE.md"
                        for item in firewall.verify(fixture.root)
                    )
                )

    def test_root_policy_allows_a_direct_negated_object_list(self) -> None:
        statements = (
            "Production-transition is not a Demo checklist, follow-up, "
            "completion condition, or gate.\n",
            "Production-transition preparation, review, freeze, and transfer "
            "are not required for Demo work.\n",
            "Production-transition must never be prepared, reviewed, frozen, "
            "transferred, or imported during Demo work.\n",
            "Do not prepare, review, freeze, transfer, or import "
            "production-transition during Demo work.\n",
            "Production-transition is never reviewed, transferred, or imported "
            "during Demo work.\n",
        )
        for statement in statements:
            with self.subTest(statement=statement), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                current = (fixture.root / "CLAUDE.md").read_text(encoding="utf-8")
                fixture.write("CLAUDE.md", current + "\n" + statement)

                self.assertFalse(
                    any(
                        item.rule == "root-policy-transition-authority"
                        and item.path == "CLAUDE.md"
                        for item in firewall.verify(fixture.root)
                    )
                )

    def test_root_policy_transition_context_cannot_hide_following_work(self) -> None:
        statements = (
            "Production-transition is not a Demo source of truth.\n"
            "It is nevertheless the Demo implementation source of truth.\n",
            "Production-transition is not a Demo source of truth.\n"
            "Review it weekly and transfer it after approval.\n",
            "Production-transition is not a Demo source of truth.\n"
            "It is not a checklist.\n"
            "It is nevertheless the Demo implementation source of truth.\n",
            "Production-transition is not a Demo source of truth.\n"
            "It is not a checklist.\n"
            "Review it weekly and transfer it after approval.\n",
            "Production-transition is the\nDemo source of truth.\n",
            "production-transition은 Demo 구현의\n정본이다.\n",
            "Demo implementation must follow\nproduction-transition.\n",
            "Production-transition is not a Demo source of truth.\n"
            "It is the Demo\nimplementation basis.\n",
            "11. **Production-transition firewall**\n"
            "Production-transition is the\nDemo source of truth.\n",
            "11. **Production-transition firewall**\n"
            "Production-transition is not a Demo source of truth.\n"
            "Review it weekly and\ntransfer it after approval.\n",
            "11. **Production-transition firewall**: "
            "Production-transition is not a Demo source of truth.\n"
            "   - It dictates Demo behavior.\n",
            "11. **Production-transition firewall**: "
            "Production-transition is not a Demo source of truth.\n"
            "   - It is binding on Demo implementation.\n",
            "11. **Production-transition firewall**: "
            "Production-transition is not a Demo source of truth.\n"
            "   - Review it weekly and transfer it after approval.\n",
            "| Production-transition is not a Demo source of truth "
            "| It dictates Demo behavior |\n",
            "| Production-transition is not a checklist "
            "| It is binding on Demo implementation |\n",
            "Production-transition is not a Demo source of truth: "
            "It dictates Demo behavior.\n",
            "Production-transition is not a Demo source of truth, "
            "It dictates Demo behavior.\n",
            "Production-transition is not a checklist while it dictates Demo behavior.\n",
            "Production-transition is not a checklist though it is authoritative "
            "for Demo implementation.\n",
            "Production-transition is not a checklist yet it dictates Demo behavior.\n",
            "Production-transition is not a checklist because it dictates Demo behavior.\n",
            "Production-transition is not a checklist / it dictates Demo behavior.\n",
            "Production-transition is not a checklist -> it dictates Demo behavior.\n",
            "Production-transition is not a Demo source of truth, "
            "use it for Demo implementation.\n",
            "Production-transition is not a Demo source of truth.\n"
            "Base Demo implementation on it.\n",
            "Production-transition is not a Demo source of truth.\n"
            "The Demo must conform to it.\n",
            "Production-transition is not a Demo source of truth.\n"
            "It guides Demo implementation.\n",
            "Production-transition is not a Demo source of truth.\n"
            "Implement the Demo according to it.\n",
            "Production-transition은 Demo 정본이 아니다.\n"
            "Demo 구현은 이를 따른다.\n",
            "Production-transition은 Demo 정본이 아니다.\n"
            "Demo 구현 시 이를 참고한다.\n",
            "Production-transition은 Demo 정본이 아니다.\n"
            "Production-transition을 Demo 구현에 적용한다.\n",
            "- Production-transition is not a Demo source of truth.\n"
            "- Review it weekly and transfer it after approval.\n",
        )
        for statement in statements:
            with self.subTest(statement=statement), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                current = (fixture.root / "CLAUDE.md").read_text(encoding="utf-8")
                fixture.write("CLAUDE.md", current + "\n" + statement)

                self.assertTrue(
                    any(
                        item.rule == "root-policy-transition-authority"
                        and item.path == "CLAUDE.md"
                        for item in firewall.verify(fixture.root)
                    )
                )

    def test_exact_claude_firewall_section_does_not_trust_inserted_children(self) -> None:
        children = (
            "   - It dictates Demo behavior.",
            "   - It is binding on Demo implementation.",
            "   - Base Demo implementation on it.",
            "   - The Demo must conform to it.",
            "   - It guides Demo implementation.",
            "   - Implement the Demo according to it.",
            "   - Demo 구현은 이를 따른다.",
            "   - Demo 구현 시 이를 참고한다.",
            "   - Production-transition을 Demo 구현에 적용한다.",
            "   - Review it weekly and transfer it after approval.",
        )
        for child in children:
            with self.subTest(child=child), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                current = (fixture.root / "CLAUDE.md").read_text(encoding="utf-8")
                section = list(firewall.TRUSTED_CLAUDE_FIREWALL_SECTION)
                section.insert(1, child)
                fixture.write(
                    "CLAUDE.md",
                    current
                    + "\n"
                    + "\n".join(section)
                    + "\n"
                    + firewall.TRUSTED_CLAUDE_FIREWALL_BOUNDARY
                    + "\n",
                )

                self.assertTrue(
                    any(
                        item.rule == "root-policy-transition-authority"
                        and item.path == "CLAUDE.md"
                        for item in firewall.verify(fixture.root)
                    )
                )

    def test_demo_policy_documents_cannot_reference_transition_verifier(self) -> None:
        candidates = (
            "CLAUDE.md",
            "README.md",
            "docs/PRD.md",
            "docs/TRD.md",
            ".codex/skills/catchup/SKILL.md",
        )
        for relative in candidates:
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                current = (fixture.root / relative).read_text(encoding="utf-8")
                fixture.write(
                    relative,
                    current + "\npython -m tools.verify_production_transition prepare\n",
                )

                violations = firewall.verify(fixture.root)

                self.assertTrue(
                    any(
                        item.rule == "demo-policy-transition-verifier"
                        and item.path == relative
                        for item in violations
                    )
                )

    def test_catchup_requires_subtree_and_transition_only_commit_exclusions(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            fixture.write(
                ".codex/skills/catchup/SKILL.md",
                "Production-transition is dormant and Project owner-gated.\n",
            )

            violations = firewall.verify(fixture.root)

            match = next(
                item for item in violations if item.rule == "catchup-firewall-policy"
            )
            self.assertIn("transition subtree exclusion", match.message)
            self.assertIn("transition-only commit exclusion", match.message)
            self.assertIn("active-spec inference exclusion", match.message)
            self.assertIn("current-request activation gate", match.message)

    def test_runtime_and_project_configuration_references_are_rejected(self) -> None:
        candidates = (
            "Assets/RuntimeConfig.asset",
            "Packages/com.demo/package.json",
            "ProjectSettings/DemoSettings.asset",
        )
        for relative in candidates:
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temp:
                fixture = FirewallFixture(Path(temp))
                fixture.write(
                    relative,
                    "source: docs/production-transition/quarantine/example\n",
                )

                violations = firewall.verify(fixture.root)

                self.assertTrue(
                    any(
                        item.rule == "runtime-transition-reference" and item.path == relative
                        for item in violations
                    )
                )

    def test_runtime_scan_detects_a_token_split_across_read_chunks(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            fixture = FirewallFixture(Path(temp))
            prefix = "x" * (64 * 1024 - len("production-"))
            fixture.write(
                "Assets/ChunkBoundary.bytes",
                prefix + "production-transition",
            )

            violations = firewall.verify(fixture.root)

            self.assertTrue(
                any(item.rule == "runtime-transition-reference" for item in violations)
            )


if __name__ == "__main__":
    unittest.main()
