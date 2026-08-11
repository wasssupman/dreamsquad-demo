#!/usr/bin/env python3
"""Read-only repository firewall between Demo work and transition material.

This verifier deliberately does not import or invoke the production-transition
verifier.  It inspects repository files only and never reads Git state, follows
transition freshness, or writes a report.  Its purpose is to keep dormant
transition preparation out of normal Demo design and implementation workflows.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, List, Optional, Sequence, Set, Tuple


LEGACY_ACTIVE_PATHS = (
    "docs/spec/production-transition-governance",
)

ACTIVE_DOC_ROOTS = ("docs/spec", "docs/plans")
RUNTIME_ROOTS = ("Assets", "Packages", "ProjectSettings")
ACTIVE_DOC_SUFFIXES = {".adoc", ".json", ".md", ".rst", ".txt", ".yaml", ".yml"}
INVOCATION_TEXT_SUFFIXES = {
    "",
    ".bat",
    ".cfg",
    ".cjs",
    ".cmd",
    ".gradle",
    ".ini",
    ".js",
    ".json",
    ".md",
    ".mjs",
    ".pl",
    ".ps1",
    ".py",
    ".rb",
    ".sh",
    ".toml",
    ".ts",
    ".txt",
    ".xml",
    ".yaml",
    ".yml",
}

TRANSITION_REFERENCE = re.compile(
    r"(?:"
    r"docs[/\\]production-transition(?:[/\\]|\b)"
    r"|(?:\.\.[/\\])+production-transition(?:[/\\]|\b)"
    r"|\bproduction[-_ ]transition\b"
    r"|\bproduction-transition-governance\b"
    r")",
    re.IGNORECASE,
)
TRANSITION_VERIFIER_INVOCATION = re.compile(
    r"\bverify_production_transition(?:\.py)?\b",
    re.IGNORECASE,
)
IMPLEMENTATION_AUTHORITY = re.compile(
    r"(?:source\s+of\s+truth|implement(?:ation)?|design|architecture|basis|"
    r"reference|refer\s+to|follow|adopt|must|should|use|"
    r"구현|설계|아키텍처|정본|기준|근거|참고|참조|계약|요구|따라|사용)",
    re.IGNORECASE,
)
NEGATED_AUTHORITY = re.compile(
    r"(?:"
    r"(?:do\s+not|don't|must\s+not|never|cannot|can't)[\s\S]{0,80}"
    r"(?:use|treat|make|source|basis|reference|baseline|candidate|authority)"
    r"|(?:active[- ]?spec|plan|source|basis|reference|baseline|candidate|authority)"
    r"[\s\S]{0,40}(?:is|are)\s+(?:explicitly\s+)?excluded"
    r"|(?:is|are)\s+(?:explicitly\s+)?excluded\s+from[\s\S]{0,40}"
    r"(?:active[- ]?spec|plan|source|basis|reference|baseline|candidate|authority)"
    r"|not\s+(?:(?:a|an)\s+)?(?:\w+\s+){0,3}"
    r"(?:source|basis|reference|baseline|candidate|authority)"
    r"|(?:정본|기준|근거|참고|참조|후보|gate|사용|영향)[\s\S]{0,50}"
    r"(?:아니|않|없|금지|제외|차단)"
    r"|(?:아니|않|없|금지|제외|차단)[\s\S]{0,50}"
    r"(?:정본|기준|근거|참고|참조|후보|gate|사용|영향)"
    r")",
    re.IGNORECASE | re.DOTALL,
)

VERIFIER_TOKEN = b"verify_production_transition"
INVOCATION_SCAN_DIRS = (
    ".claude",
    ".codex",
    ".github",
    ".gitlab",
    ".circleci",
    ".azure",
    ".githooks",
    ".husky",
    "hooks",
    "ci",
    "scripts",
    "tools",
)
INVOCATION_ROOT_FILES = {
    ".gitlab-ci.yml",
    ".gitlab-ci.yaml",
    ".pre-commit-config.yaml",
    ".pre-commit-config.yml",
    "azure-pipelines.yml",
    "azure-pipelines.yaml",
    "Jenkinsfile",
    "Makefile",
    "jenkinsfile",
    "makefile",
    "package.json",
    "pyproject.toml",
    "taskfile.yml",
    "taskfile.yaml",
    "tox.ini",
}
INVOCATION_ALLOWLIST = {
    "tools/verify_production_transition.py",
    "tools/test_verify_production_transition.py",
    "tools/README.md",
    "tools/verify_demo_transition_firewall.py",
    "tools/test_verify_demo_transition_firewall.py",
}

RUNTIME_TOKENS = (
    b"production-transition",
    b"production_transition",
    b"production transition",
    b"production-transition-governance",
)


@dataclass(frozen=True, order=True)
class Violation:
    rule: str
    path: str
    line: Optional[int]
    message: str

    def format(self) -> str:
        location = self.path if self.line is None else f"{self.path}:{self.line}"
        return f"[{self.rule}] {location}: {self.message}"


def _relative(root: Path, path: Path) -> str:
    return path.relative_to(root).as_posix()


def _iter_files(path: Path) -> Iterable[Path]:
    if path.is_file() and not path.is_symlink():
        yield path
        return
    if not path.is_dir() or path.is_symlink():
        return
    for candidate in sorted(path.rglob("*")):
        if candidate.is_file() and not candidate.is_symlink():
            yield candidate


def _read_text(path: Path) -> Tuple[Optional[str], Optional[str]]:
    try:
        return path.read_text(encoding="utf-8-sig"), None
    except (OSError, UnicodeError) as exc:
        return None, str(exc)


def _contains_token(path: Path, tokens: Sequence[bytes]) -> Tuple[bool, Optional[str]]:
    """Search arbitrary files without loading large binary assets into memory."""

    lowered_tokens = tuple(token.lower() for token in tokens)
    overlap = max(len(token) for token in lowered_tokens) - 1
    carry = b""
    try:
        with path.open("rb") as stream:
            while True:
                chunk = stream.read(64 * 1024)
                if not chunk:
                    return False, None
                haystack = (carry + chunk).lower()
                if any(token in haystack for token in lowered_tokens):
                    return True, None
                carry = haystack[-overlap:] if overlap else b""
    except OSError as exc:
        return False, str(exc)


def _check_legacy_paths(root: Path) -> List[Violation]:
    violations: List[Violation] = []
    for relative in LEGACY_ACTIVE_PATHS:
        if os.path.lexists(str(root / Path(relative))):
            violations.append(
                Violation(
                    "legacy-active-path",
                    relative,
                    None,
                    "transition-only material must live outside active docs/spec",
                )
            )
    return violations


def _is_non_authoritative_notice(line: str) -> bool:
    return bool(NEGATED_AUTHORITY.search(line))


def _check_active_docs(root: Path) -> List[Violation]:
    violations: List[Violation] = []
    for relative_root in ACTIVE_DOC_ROOTS:
        base = root / Path(relative_root)
        for path in _iter_files(base):
            if path.suffix.lower() not in ACTIVE_DOC_SUFFIXES:
                continue
            relative = _relative(root, path)
            text, error = _read_text(path)
            if error is not None:
                violations.append(
                    Violation("active-doc-read", relative, None, f"cannot inspect file: {error}")
                )
                continue
            assert text is not None
            for line_number, line in enumerate(text.splitlines(), start=1):
                if TRANSITION_VERIFIER_INVOCATION.search(line):
                    violations.append(
                        Violation(
                            "active-doc-transition-verifier",
                            relative,
                            line_number,
                            "active Demo documentation must not instruct agents to run the owner-gated transition verifier",
                        )
                    )
                    continue
                if not TRANSITION_REFERENCE.search(line):
                    continue
                # A firewall/closed-status statement may name transition
                # material only to say that it is not authoritative. It is not
                # an implementation dependency and is therefore safe.
                if _is_non_authoritative_notice(line):
                    continue
                has_path_reference = bool(
                    re.search(
                        r"(?:docs[/\\]|(?:\.\.[/\\])+|\]\([^)]*)"
                        r"production-transition",
                        line,
                        re.IGNORECASE,
                    )
                )
                if has_path_reference or IMPLEMENTATION_AUTHORITY.search(line):
                    violations.append(
                        Violation(
                            "active-doc-transition-authority",
                            relative,
                            line_number,
                            "active Demo documentation must not use transition material as an implementation source",
                        )
                    )
    return violations


def _invocation_candidates(root: Path) -> Iterable[Path]:
    seen: Set[str] = set()
    for relative_dir in INVOCATION_SCAN_DIRS:
        for path in _iter_files(root / Path(relative_dir)):
            relative = _relative(root, path)
            if "__pycache__" in path.parts or path.suffix.lower() not in INVOCATION_TEXT_SUFFIXES:
                continue
            if relative not in seen:
                seen.add(relative)
                yield path
    for name in INVOCATION_ROOT_FILES:
        path = root / name
        if path.is_file() and not path.is_symlink():
            relative = _relative(root, path)
            if relative not in seen:
                seen.add(relative)
                yield path


def _check_automatic_invocations(root: Path) -> List[Violation]:
    violations: List[Violation] = []
    for path in _invocation_candidates(root):
        relative = _relative(root, path)
        if relative in INVOCATION_ALLOWLIST or relative.startswith(
            "docs/production-transition/"
        ):
            continue
        found, error = _contains_token(path, (VERIFIER_TOKEN,))
        if error is not None:
            violations.append(
                Violation("invocation-read", relative, None, f"cannot inspect file: {error}")
            )
        elif found:
            violations.append(
                Violation(
                    "automatic-transition-verifier",
                    relative,
                    None,
                    "Demo CI, hooks, and general validation scripts must not invoke the owner-gated transition verifier",
                )
            )
    return violations


def _has_demo_authority(text: str) -> bool:
    return bool(
        re.search(
            r"demo.{0,240}(?:source\s+of\s+truth|정본|우선|upstream|기준선|설계|구현|검증)",
            text,
            re.IGNORECASE | re.DOTALL,
        )
        or re.search(
            r"(?:source\s+of\s+truth|정본|우선|upstream|기준선).{0,240}demo",
            text,
            re.IGNORECASE | re.DOTALL,
        )
    )


def _transition_policy_context(text: str, radius: int = 320) -> str:
    """Return local contexts around transition mentions, not unrelated prose."""

    matches = list(re.finditer(r"production[-_ ]transition", text, re.IGNORECASE))
    return "\n".join(
        text[max(0, match.start() - radius) : match.end() + radius] for match in matches
    )


def _check_root_policy_documents(root: Path) -> List[Violation]:
    violations: List[Violation] = []
    policy_files = ("CLAUDE.md", "README.md", "docs/PRD.md", "docs/TRD.md")
    for relative in policy_files:
        path = root / Path(relative)
        text, error = _read_text(path)
        if error is not None or text is None:
            violations.append(
                Violation(
                    "root-firewall-policy",
                    relative,
                    None,
                    "required policy document is missing or unreadable",
                )
            )
            continue

        for line_number, line in enumerate(text.splitlines(), start=1):
            if TRANSITION_VERIFIER_INVOCATION.search(line):
                violations.append(
                    Violation(
                        "demo-policy-transition-verifier",
                        relative,
                        line_number,
                        "Demo policy documents must not instruct agents to invoke the owner-gated transition verifier",
                    )
                )

        missing: List[str] = []
        policy_context = _transition_policy_context(text)
        if not policy_context:
            missing.append("production-transition scope")
        if not re.search(r"\bdormant\b", policy_context, re.IGNORECASE):
            missing.append("dormant state")
        if not re.search(
            r"(?:project\s+owner|owner[- ]gated)", policy_context, re.IGNORECASE
        ):
            missing.append("Project owner gate")
        if not _has_demo_authority(policy_context):
            missing.append("Demo authority")
        if not NEGATED_AUTHORITY.search(policy_context):
            missing.append("explicit non-authority statement")
        if relative == "README.md" and not (
            re.search(r"기본\s*읽기.{0,60}(?:아니|않|제외)", text, re.IGNORECASE | re.DOTALL)
            or re.search(
                r"default.{0,40}(?:read|reading).{0,60}(?:not|exclude|only)",
                text,
                re.IGNORECASE | re.DOTALL,
            )
        ):
            missing.append("default-reading exclusion")
        if missing:
            violations.append(
                Violation(
                    "root-firewall-policy",
                    relative,
                    None,
                    "missing invariant(s): " + ", ".join(missing),
                )
            )
    return violations


def _check_catchup_policy(root: Path) -> List[Violation]:
    relative = ".codex/skills/catchup/SKILL.md"
    text, error = _read_text(root / Path(relative))
    if error is not None or text is None:
        return [
            Violation(
                "catchup-firewall-policy",
                relative,
                None,
                "catchup policy is missing or unreadable",
            )
        ]

    violations: List[Violation] = []
    for line_number, line in enumerate(text.splitlines(), start=1):
        if TRANSITION_VERIFIER_INVOCATION.search(line):
            violations.append(
                Violation(
                    "demo-policy-transition-verifier",
                    relative,
                    line_number,
                    "catchup must not invoke the owner-gated transition verifier",
                )
            )

    missing: List[str] = []
    required = (
        (r"docs/production-transition/\*\*", "transition subtree exclusion"),
        (r"transition[- ]only", "transition-only commit exclusion"),
        (r"active[- ]spec", "active-spec inference exclusion"),
        (r"(?:exclude|ignore|제외)", "explicit exclusion verb"),
        (r"\bdormant\b", "dormant state"),
        (r"(?:project\s+owner|owner[- ]gated)", "Project owner gate"),
        (
            r"(?:current\s+(?:user\s+)?request.{0,100}(?:explicit|activat)"
            r"|현재\s*요청.{0,100}명시)",
            "current-request activation gate",
        ),
    )
    for pattern, label in required:
        if not re.search(pattern, text, re.IGNORECASE | re.DOTALL):
            missing.append(label)
    if missing:
        violations.append(
            Violation(
                "catchup-firewall-policy",
                relative,
                None,
                "missing invariant(s): " + ", ".join(missing),
            )
        )
    return violations


def _check_runtime_roots(root: Path) -> List[Violation]:
    violations: List[Violation] = []
    for relative_root in RUNTIME_ROOTS:
        for path in _iter_files(root / Path(relative_root)):
            relative = _relative(root, path)
            found, error = _contains_token(path, RUNTIME_TOKENS)
            if error is not None:
                violations.append(
                    Violation("runtime-read", relative, None, f"cannot inspect file: {error}")
                )
            elif found:
                violations.append(
                    Violation(
                        "runtime-transition-reference",
                        relative,
                        None,
                        "runtime, package, and ProjectSettings files must not reference transition material",
                    )
                )
    return violations


def verify(root: Path) -> List[Violation]:
    """Return all firewall violations without mutating the repository."""

    root = root.resolve()
    violations: List[Violation] = []
    violations.extend(_check_legacy_paths(root))
    violations.extend(_check_active_docs(root))
    violations.extend(_check_automatic_invocations(root))
    violations.extend(_check_root_policy_documents(root))
    violations.extend(_check_catchup_policy(root))
    violations.extend(_check_runtime_roots(root))
    return sorted(violations)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Verify that dormant production-transition material cannot affect Demo work."
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="repository root (defaults to the parent of tools/)",
    )
    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = build_parser().parse_args(argv)
    violations = verify(args.root)
    if violations:
        print(f"Demo production-transition firewall: FAIL ({len(violations)} violation(s))")
        for violation in violations:
            print(violation.format())
        return 1
    print("Demo production-transition firewall: PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
