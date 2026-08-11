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
