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
    r"|\bproduction[-_ ]transition(?:[은는이가을를])?(?=\b|[^A-Za-z0-9_])"
    r"|\bproduction-transition-governance\b"
    r")",
    re.IGNORECASE,
)
TRANSITION_VERIFIER_INVOCATION = re.compile(
    r"\bverify_production_transition(?:\.py)?\b",
    re.IGNORECASE,
)
TRANSITION_WORK_TERM_PATTERN = (
    r"(?:"
    r"\bcheck[- ]?list\b"
    r"|\bfollow[- ]?up\b"
    r"|\bbacklog\b"
    r"|\bwork[- ]?item\b"
    r"|\b(?:task|to[- ]?do|todo|fixme)\b"
    r"|\bcompletion\s+(?:condition|criterion)\b"
    r"|\b(?:completion|acceptance|validation)\s+gate\b"
    r"|\bgate\b"
    r"|체크\s*리스트"
    r"|후속(?:\s*항목)?"
    r"|백로그"
    r"|작업(?:\s*(?:항목|후보))?"
    r"|할\s*일"
    r"|완료\s*조건"
    r"|이전\s*판정"
    r")"
)
TRANSITION_WORK_TRACKING = re.compile(
    TRANSITION_WORK_TERM_PATTERN,
    re.IGNORECASE,
)
TRANSITION_LIFECYCLE_ACTION = re.compile(
    r"(?:"
    r"\b(?:transfer(?:s|red|ring)?|hand[- ]?off|hand(?:s|ed|ing)?\s+over|"
    r"mov(?:e|es|ed|ing)|cop(?:y|ies|ied|ying)|archiv(?:e|es|ed|ing)|"
    r"prepar(?:e|es|ed|ing|ation)|"
    r"export(?:s|ed|ing)?|import(?:s|ed|ing)?|freez(?:e|es|ing)|frozen|"
    r"approv(?:e|es|ed|al)|reconcil(?:e|es|ed|iation)|review(?:s|ed|ing)?|"
    r"cutover|migrat(?:e|es|ed|ing|ion)|updat(?:e|es|ed|ing)|maintain(?:s|ed|ing)?)\b"
    r"|이동|이관|전달|전송|복사|반출|동결|승인|조정|대사|검토|점검|갱신|유지"
    r")",
    re.IGNORECASE,
)
SAFE_TRANSITION_OBJECT = re.compile(
    r"(?:"
    + TRANSITION_WORK_TERM_PATTERN
    + r"|"
    + TRANSITION_LIFECYCLE_ACTION.pattern
    + r"|\b(?:source\s+of\s+truth|authority|implementation\s+(?:source|basis|dependency)|"
    r"work\s+candidate|design\s+(?:source|basis)|reference|baseline|requirement|"
    r"required|needed|contract)\b"
    r"|정본|기준|근거|참고|참조|권위|후보|의존|요구|계약|사용|영향|필요|필수)",
    re.IGNORECASE,
)
CLAUSE_BOUNDARY = re.compile(
    r"(?:[.!?:;/—–|]+|(?:-{1,2}|=)>|\b(?:but|however|then|while|though|"
    r"although|yet|because|whereas|nevertheless)\b|하지만|그러나|그리고|이며|지만)",
    re.IGNORECASE,
)
DIRECT_SAFE_NEGATION = re.compile(
    r"(?:"
    r"\b(?:is|are|was|were)\s+(?:explicitly\s+)?not\b"
    r"|\b(?:do|does|did|must|should|shall|will)\s+not\b"
    r"|\b(?:don't|doesn't|didn't|mustn't|shouldn't|shan't|won't|cannot|can't|never)\b"
    r"|\b(?:must|should|shall|will)\s+never\b"
    r"|\b(?:excluded|prohibited|forbidden)\b"
    r"|아니(?:다|며|고)?"
    r"|(?:사용|추적|관리|유지|완료|수행|실행|작성|추가|진행|이동|이관|전달|필요|필수|"
    r"전송|복사|검토|점검)하지\s*않(?:는다|음|으며)?"
    r"|금지|제외|차단"
    r")",
    re.IGNORECASE,
)
POSITIVE_WORK_DIRECTIVE = re.compile(
    r"(?:"
    r"\b(?:must|should|shall|will)\s+(?!not\b)"
    r"|\b(?:todo|fixme|planned|pending)\b"
    r"|(?:\bfollow[- ]?up\b|\bcheck[- ]?list\b|후속|체크\s*리스트)\s*:"
    r"|\b(?:required|needed)\b"
    r"|\b(?:transfer|hand[- ]?off|move|copy|export|import|freez(?:e|ing)|"
    r"approv(?:e|ed|al)|reconcil(?:e|ed|iation)|review(?:ed|ing)?)\b"
    r"\s+(?:when|after|before|upon|once|pending)\b"
    r"|\b(?:when|after|before|upon|once|pending)\b[^.!?;]{0,50}"
    r"\b(?:transfer|hand[- ]?off|move|copy|export|import|freez(?:e|ing)|"
    r"approv(?:e|ed|al)|reconcil(?:e|ed|iation)|review(?:ed|ing)?)\b"
    r"|\b(?:is|are|was|were)\s+(?!not\b)(?:\w+\s+){0,2}"
    r"(?:tracked|maintained|managed|completed|executed|reviewed|transferred|exported)\b"
    r"|(?:완료|수행|실행|추적|관리|유지|작성|추가|진행|사용|이동|이관|전달|"
    r"전송|복사|동결|승인|검토|점검)\s*"
    r"(?:한다|해야\s*한다|하라|할\s*것|하기로|중이다|예정이다|하고|하며)"
    r"|(?:승인|동결)(?:되면|하면|\s*후)[^.!?;]{0,50}(?:이동|이관|전달|전송|복사)"
    r"|(?:필요|필수)(?:하다|한다|함|이다)?"
    r")",
    re.IGNORECASE,
)
NEGATED_REQUIREMENT = re.compile(
    r"(?:\b(?:is|are|was|were)\s+not\s+(?:explicitly\s+)?|"
    r"\b(?:isn't|aren't|wasn't|weren't)\s+)(?:required|needed)\b|"
    r"(?:필요|필수)하지\s*않",
    re.IGNORECASE,
)
ENGLISH_PREFIXED_LIFECYCLE_NEGATION = re.compile(
    (
        r"(?:\b(?:do|does|did|must|should|shall|will)\s+not\b|"
        r"\b(?:don't|doesn't|didn't|mustn't|shouldn't|shan't|won't|cannot|can't|never)\b)"
        r"[^.!?;]{0,80}"
        + TRANSITION_LIFECYCLE_ACTION.pattern
        + r"[^.!?;]{0,80}"
        + TRANSITION_REFERENCE.pattern
    ),
    re.IGNORECASE,
)
ENGLISH_DIRECT_SAFE_NOTICE = re.compile(
    (
        r"(?:"
        + r"(?:\b(?:do|does|did|must|should|shall|will)\s+not\b|"
        + r"\b(?:don't|doesn't|didn't|mustn't|shouldn't|shan't|won't|cannot|can't|never)\b|"
        + r"\b(?:must|should|shall|will)\s+never\b)"
        + r"[^.!?;]{0,160}"
        + TRANSITION_REFERENCE.pattern
        + r"[^.!?;]{0,160}"
        + SAFE_TRANSITION_OBJECT.pattern
        + r"|"
        + TRANSITION_REFERENCE.pattern
        + r"[^.!?;]{0,160}(?:\b(?:is|are|was|were)\s+(?:explicitly\s+)?not\b|"
        + r"\b(?:must|should|shall|will)\s+(?:not|never)\b|\b(?:cannot|can't)\b)"
        + r"[^.!?;]{0,160}"
        + SAFE_TRANSITION_OBJECT.pattern
        + r")"
    ),
    re.IGNORECASE,
)
KOREAN_DIRECT_SAFE_NOTICE = re.compile(
    (
        TRANSITION_REFERENCE.pattern
        + r"[^.!?;]{0,220}"
        + SAFE_TRANSITION_OBJECT.pattern
        + r"[^.!?;]{0,160}"
        + DIRECT_SAFE_NEGATION.pattern
    ),
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
    r"|(?:active[- ]?spec|plan|source|basis|reference|baseline|candidate|authority|"
    r"checklist|follow[- ]?up|work[- ]?item|gate|completion\s+(?:condition|criterion))"
    r"[\s\S]{0,40}(?:is|are)\s+(?:explicitly\s+)?excluded"
    r"|(?:is|are)\s+(?:explicitly\s+)?excluded\s+from[\s\S]{0,40}"
    r"(?:active[- ]?spec|plan|source|basis|reference|baseline|candidate|authority|"
    r"checklist|follow[- ]?up|work[- ]?item|gate|completion\s+(?:condition|criterion))"
    r"|not\s+(?:(?:a|an)\s+)?(?:\w+\s+){0,3}"
    r"(?:source|basis|reference|baseline|candidate|authority|checklist|"
    r"follow[- ]?up|work[- ]?item|gate|completion\s+(?:condition|criterion))"
    r"|(?:정본|기준|근거|참고|참조|후보|gate|사용|영향|체크\s*리스트|후속|"
    r"작업\s*항목|완료\s*조건)[\s\S]{0,50}"
    r"(?:아니|않|없|금지|제외|차단)"
    r"|(?:아니|않|없|금지|제외|차단)[\s\S]{0,50}"
    r"(?:정본|기준|근거|참고|참조|후보|gate|사용|영향|체크\s*리스트|후속|"
    r"작업\s*항목|완료\s*조건)"
    r")",
    re.IGNORECASE | re.DOTALL,
)

ACTION_SCOPE_BOUNDARY = re.compile(
    r"(?:[,.:;!?—–]|\b(?:and|then|but|however)\b|그리고|이며|하지만|그러나)",
    re.IGNORECASE,
)
HARD_ACTION_SCOPE_BOUNDARY = re.compile(r"[.!?;—–]")
ENGLISH_ACTION_NEGATION = re.compile(
    r"(?:"
    r"\b(?:do|does|did|must|should|shall|will|is|are|was|were)\s+not\b"
    r"|\b(?:don't|doesn't|didn't|mustn't|shouldn't|shan't|won't|cannot|can't|never)\b"
    r"|\b(?:must|should|shall|will)\s+never\b"
    r")",
    re.IGNORECASE,
)
KOREAN_ACTION_NEGATION = re.compile(
    r"^(?:\s*(?:을|를|은|는|이|가|로|으로))?\s*하지\s*않(?:는다|음|으며)?",
    re.IGNORECASE,
)
ROOT_POSITIVE_AUTHORITY = re.compile(
    r"(?:"
    r"\bproduction[-_ ]transition\b[^\r\n]{0,80}"
    r"\b(?:is|are|becomes?|serves?\s+as)\b[^\r\n]{0,80}"
    r"(?:\b(?:source\s+of\s+truth|implementation\s+basis|architecture\s+source|"
    r"completion\s+(?:gate|condition|criterion)|work[- ]?item)\b|정본|기준|근거|완료\s*조건)"
    r"|\b(?:demo\s+implementation|demo\s+design|architecture)\b[^\r\n]{0,50}"
    r"\b(?:must|should|shall|will)\s+(?!not\b)[^\r\n]{0,20}"
    r"\b(?:follow|use|adopt|reference)\b[^\r\n]{0,60}\bproduction[-_ ]transition\b"
    r"|\b(?:track|use|treat|make|maintain|manage)\b[^\r\n]{0,50}"
    r"\bproduction[-_ ]transition\b[^\r\n]{0,80}"
    r"(?:\b(?:work[- ]?item|completion\s+gate|source\s+of\s+truth)\b|작업\s*항목|정본|기준)"
    r")",
    re.IGNORECASE,
)
ROOT_AUTHORITY_CUE = re.compile(
    r"(?:"
    r"\b(?:source\s+of\s+truth|authoritative|canonical|binding|governs?|"
    r"implementation\s+(?:source|basis|requirement|contract)|architecture\s+(?:source|basis)|"
    r"completion\s+(?:gate|condition|criterion)|depends?\s+on|"
    r"defines?\s+(?:the\s+)?Demo\s+requirements?|drives?\s+Demo\s+design|"
    r"dictates?\s+Demo\s+behavio[u]?r)\b"
    r"|(?:Demo[^\r\n]{0,50})?(?:정본|기준|근거|완료\s*조건)이(?:다)?"
    r"|따라야|의존|구속)"
    ,
    re.IGNORECASE,
)
ROOT_SUBJECT_RELATION = re.compile(
    r"(?:"
    r"\b(?:Demo\s+(?:implementation|design)|architecture)[^\r\n]{0,50}"
    r"(?:must|should|shall|will|depends?)[^\r\n]{0,40}\bproduction[-_ ]transition\b"
    r"|\b(?:use|treat|make|track|adopt|reference)[^\r\n]{0,50}"
    r"\bproduction[-_ ]transition\b"
    r"|Demo[^\r\n]{0,50}production[-_ ]transition(?:을|를)?[^\r\n]{0,30}"
    r"(?:따라야|의존|정본|기준|근거))",
    re.IGNORECASE,
)
ROOT_ASSERTIVE_PREDICATE = re.compile(
    r"(?:\b(?:is|are|was|were|must|should|shall|will|defines?|drives?|dictates?|"
    r"governs?|depends?|uses?|tracks?|treats?|makes?|maintains?|manages?|updates?)\b|"
    r"이다|한다|해야|따라야|의존|구속)",
    re.IGNORECASE,
)
ROOT_POSITIVE_TAIL = re.compile(
    r"(?:\b(?:it|this\s+package|the\s+package|production[-_ ]transition)\b"
    r"[^.!?;]{0,40})?"
    r"(?:\b(?:is|are|was|were)\s+(?:authoritative|canonical|binding)|"
    r"\b(?:defines?|drives?|dictates?|governs?|depends?\s+on)\b|"
    r"\b(?:use|adopt|follow|reference|treat|make|update|maintain)\b"
    r"[^.!?;]{0,80}\bDemo\s+(?:implementation|design|work)\b|"
    r"(?:Demo[^.!?;]{0,50})?(?:정본이다|기준이다|근거이다|따라야|의존|구속)|"
    r"(?:사용|채택|적용|갱신|유지)[^.!?;]{0,60}Demo\s*(?:구현|설계|작업))",
    re.IGNORECASE,
)
INHERITED_DEMO_AUTHORITY = re.compile(
    r"(?:"
    r"(?=.*\b(?:Demo|implementation|design|architecture|work)\b)"
    r"(?=.*\b(?:it|this\s+(?:package|material)|the\s+package|production[-_ ]transition)\b)"
    r"(?=.*\b(?:bases?|conforms?|guides?|implements?|derives?|designs?|follows?|adopts?|uses?|references?|"
    r"according\s+to|canonical|authoritative|binding|must|should|shall|will)\b)"
    r"|(?=.*Demo\s*(?:구현|설계|작업))"
    r"(?=.*(?:이를|이것을|해당\s*(?:자료|문서)|production[-_ ]transition(?:을|를)?))"
    r"(?=.*(?:따른다|따라야|참고한다|참조한다|적용한다|기준으로\s*삼는다|구속한다))"
    r")",
    re.IGNORECASE,
)
COORDINATED_LIFECYCLE_NEGATION = re.compile(
    r"(?s)^(?=.*" + TRANSITION_REFERENCE.pattern + r")(?=.*" + TRANSITION_LIFECYCLE_ACTION.pattern + r")(?:(?:"
    r"(?:.*\b(?:do|does|did|must|should|shall|will)\s+(?:not|never)\b[^.!?;]*)"
    r"|(?:.*\b(?:cannot|can't|never)\b[^.!?;]*)"
    r"|(?:.*\b(?:is|are|was|were)\s+(?:not|never)\b[^.!?;]*)"
    r"|(?:.*\b(?:is|are|was|were)\s+not\s+required\b[^.!?;]*)"
    r"|(?:.*(?:하지\s*않|필요하지\s*않|아니다|금지|제외|차단)[^.!?;]*)))$",
    re.IGNORECASE,
)
COORDINATED_LIFECYCLE_LIST = re.compile(
    TRANSITION_LIFECYCLE_ACTION.pattern
    + r"(?:\s*(?:,|\band\b|\bor\b)\s*"
    + TRANSITION_LIFECYCLE_ACTION.pattern
    + r")+",
    re.IGNORECASE,
)

# CLAUDE.md's firewall section contains several safe, owner-gated policy clauses
# whose lifecycle vocabulary is intentionally broader than the general prose
# detector accepts.  Trust only these exact bytes and their exact following
# boundary.  A changed line or an inserted child makes the section fall back to
# the fail-closed logical-block checks below; the title alone grants no exemption.
TRUSTED_CLAUDE_FIREWALL_SECTION = (
    "11. **Production-transition firewall** (2026-08-11 사용자 결정): Demo가 유일한 upstream이다. `docs/production-transition/`은 Project owner가 미래 전환을 위해 미리 보관하는 **dormant downstream 자료**이며 Demo의 설계·구현·검증 정본이 아니다.",
    "   - 현재 사용자 요청이 production-transition의 시작·갱신·검증을 **명시적으로** 지시하지 않으면 해당 subtree와 전용 verifier를 읽거나 실행하거나 작업 후보로 제안하지 않는다. 최근 커밋, stale 표시, watch path 변화, backlog 링크는 활성화 근거가 아니다.",
    "   - Demo의 정본 우선순위는 `CLAUDE.md` → 활성 `docs/spec/{feature-slug}/` → `docs/TRD.md`/`docs/PRD.md`의 적용 가능한 Demo 계약 → 코드·에셋·테스트다. Transition 문서와 충돌하면 Demo를 고치는 대신 transition 자료가 stale한 것으로 둔다.",
    "   - Transition maintenance/change register/coverage/decision/freeze audit는 Demo 작업의 시작·완료·검증·커밋을 절대 차단하지 않는다. Demo 변경에 맞춘 transition 문서 갱신도 같은 작업에 끼워 넣지 않으며, 명시적인 별도 후행 task와 별도 commit에서만 수행한다.",
    "   - Freeze, cutover, production import와 후속 wave의 시점·범위는 Project owner만 결정한다. 명시적 활성화 전 agent는 이를 계획하거나 선제 작업하지 않는다.",
    "   - Transition과 무관한 Demo 아키텍처 변경은 Demo 목표만으로 별도 승인받고 이 파일과 TRD를 먼저 갱신해야 한다. Transition 문서를 근거로 ECS 경계나 네트워크 금지를 우회할 수 없다.",
)
TRUSTED_CLAUDE_FIREWALL_BOUNDARY = (
    "**전체 제약 목록은 `docs/TRD.md` 섹션 3(추상화 규칙), 섹션 5(금지 패턴)를 반드시 참조**하라."
)

STRUCTURAL_ITEM = re.compile(r"^(?P<indent>\s*)(?P<marker>[-*+]|\d+[.)]|>)\s+")
TRANSITION_CHILD_LEAD = re.compile(
    r"^\s*(?:(?:[-*+]|\d+[.)]|>)\s+)?(?:"
    r"follow[- ]?up|check[- ]?list|todo|fixme|backlog|work[- ]?item|"
    r"transfer|hand[- ]?off|hand\s+over|move|copy|archive|prepare|preparation|"
    r"export|import|freeze|approval|review|"
    r"후속|체크\s*리스트|백로그|작업\s*(?:항목|후보)?|이동|이관|전달|전송|"
    r"복사|반출|동결|승인|검토|점검)",
    re.IGNORECASE,
)
TRANSITION_CHILD_CONTEXT = re.compile(
    r"(?:\b(?:it|this\s+package|the\s+package|client|server|production|transition)\b|"
    r"(?:이|그|해당)\s*(?:패키지|자료|문서)|클라이언트|서버|프로덕션)",
    re.IGNORECASE,
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


def _has_unnegated_lifecycle_action(text: str, inherited_transition: bool = False) -> bool:
    """Return whether a transition action is asserted outside direct negation."""

    for action in TRANSITION_LIFECYCLE_ACTION.finditer(text):
        boundaries = list(ACTION_SCOPE_BOUNDARY.finditer(text, 0, action.start()))
        scope_start = boundaries[-1].end() if boundaries else 0
        following_boundary = ACTION_SCOPE_BOUNDARY.search(text, action.end())
        scope_end = following_boundary.start() if following_boundary else len(text)
        hard_boundaries = list(HARD_ACTION_SCOPE_BOUNDARY.finditer(text, 0, action.start()))
        hard_scope_start = hard_boundaries[-1].end() if hard_boundaries else 0
        action_scope = text[scope_start:scope_end]
        if not inherited_transition and not (
            TRANSITION_REFERENCE.search(action_scope)
            or TRANSITION_REFERENCE.search(text[hard_scope_start : action.start()])
        ):
            continue
        prefix = text[scope_start : action.start()]
        suffix = text[action.end() : scope_end]
        if re.match(r"\s+(?:is|are|was|were)\s+not\b", suffix, re.IGNORECASE):
            continue
        if re.match(
            r"\s+(?:is|are|was|were)\s+historical(?:\s+and\s+not\b)?",
            suffix,
            re.IGNORECASE,
        ):
            continue
        if ENGLISH_ACTION_NEGATION.search(prefix):
            continue
        if KOREAN_ACTION_NEGATION.search(suffix):
            continue
        return True
    return False


def _has_positive_root_transition_statement(line: str) -> bool:
    """Detect authority/work assertions within one normalized Markdown block."""

    if TRANSITION_REFERENCE.search(line) is None:
        return False
    clauses = [part.strip() for part in CLAUSE_BOUNDARY.split(line) if part.strip()]
    inherited_transition = False
    for clause in clauses:
        reference = TRANSITION_REFERENCE.search(clause)
        has_transition = reference is not None
        inherited_transition = inherited_transition or has_transition
        if not inherited_transition:
            continue
        # A direct negative may cover a coordinated list, but it cannot mask a
        # later positive authority predicate in the same syntactic fragment.
        first_negation = DIRECT_SAFE_NEGATION.search(clause)
        if first_negation is not None and ROOT_POSITIVE_TAIL.search(
            clause[first_negation.end() :]
        ):
            return True
        if _clause_is_direct_safe_notice(clause, inherited_transition=not has_transition):
            continue
        comma_parts = [part.strip() for part in clause.split(",") if part.strip()]
        if len(comma_parts) > 1:
            comma_inherited = False
            for part_index, part in enumerate(comma_parts):
                part_has_transition = TRANSITION_REFERENCE.search(part) is not None
                comma_inherited = comma_inherited or part_has_transition
                if (
                    part_index > 0
                    and comma_inherited
                    and ROOT_ASSERTIVE_PREDICATE.search(part) is not None
                    and _has_positive_root_transition_statement(
                        part
                        if part_has_transition
                        else "Production-transition. " + part
                    )
                ):
                    return True
        relevant = clause[reference.start() :] if reference is not None else clause
        if ROOT_AUTHORITY_CUE.search(relevant):
            return True
        if reference is not None and ROOT_SUBJECT_RELATION.search(clause):
            return True
        if inherited_transition and re.search(
            r"(?:\b(?:Demo\s+(?:implementation|design)|architecture)\b[^.!?;]{0,50}"
            r"\b(?:must|should|shall|will)\b|Demo[^.!?;]{0,50}(?:따라야|의존|정본|기준))",
            clause,
            re.IGNORECASE,
        ):
            return True
        if inherited_transition and re.search(
            r"\b(?:use|adopt|follow|reference|treat|make|update|maintain)\b"
            r"[^.!?;]{0,80}\bDemo\s+(?:implementation|design|work)\b|"
            r"(?:사용|채택|적용|갱신|유지)[^.!?;]{0,60}Demo\s*(?:구현|설계|작업)",
            clause,
            re.IGNORECASE,
        ):
            return True
        if inherited_transition and INHERITED_DEMO_AUTHORITY.search(clause):
            return True
        if _has_unnegated_lifecycle_action(
            clause, inherited_transition=not has_transition
        ):
            return True
        if TRANSITION_WORK_TRACKING.search(clause) and (
            POSITIVE_WORK_DIRECTIVE.search(clause)
            or re.search(
                r"(?:\b(?:track|use|treat|make|maintain|manage)\b|추적|관리|사용|완료|수행)",
                clause,
                re.IGNORECASE,
            )
        ):
            return True
    return False


def _clause_is_direct_safe_notice(clause: str, inherited_transition: bool) -> bool:
    has_object = bool(SAFE_TRANSITION_OBJECT.search(clause))
    positive = POSITIVE_WORK_DIRECTIVE.search(clause)
    if positive is not None:
        matched_positive = positive.group(0).strip().lower()
        if matched_positive in {"required", "needed", "필요", "필수"}:
            positive = None if NEGATED_REQUIREMENT.search(clause) else positive
        elif matched_positive in {"must", "should", "shall", "will"} and re.match(
            r"\s*never\b", clause[positive.end() :], re.IGNORECASE
        ):
            positive = None
    if (
        COORDINATED_LIFECYCLE_NEGATION.fullmatch(clause)
        and COORDINATED_LIFECYCLE_LIST.search(clause)
    ):
        return ROOT_POSITIVE_TAIL.search(clause) is None
    if (
        not has_object
        or positive is not None
        or _has_unnegated_lifecycle_action(clause, inherited_transition)
    ):
        return False
    if inherited_transition and re.match(r"^not\b", clause, re.IGNORECASE):
        return True
    if re.search(
        r"(?:요청|request)[^.!?;]{0,120}(?:명시적|explicit)[^.!?;]{0,120}"
        r"(?:않으면|unless|only\s+if)",
        clause,
        re.IGNORECASE,
    ):
        return True
    if ENGLISH_PREFIXED_LIFECYCLE_NEGATION.search(clause):
        return True
    if re.search(
        r"\b(?:preparation|archive|review)\s+(?:is|are|was|were)\s+historical\b",
        clause,
        re.IGNORECASE,
    ):
        return True
    direct_negation = DIRECT_SAFE_NEGATION.search(clause)
    if direct_negation is not None and TRANSITION_LIFECYCLE_ACTION.search(
        direct_negation.group(0)
    ):
        return True
    if ENGLISH_DIRECT_SAFE_NOTICE.search(clause) or KOREAN_DIRECT_SAFE_NOTICE.search(clause):
        return True
    return inherited_transition and bool(DIRECT_SAFE_NEGATION.search(clause))


def _is_explicit_safe_transition_notice(line: str) -> bool:
    """Allow only clauses that directly deny authority or transition work."""

    if TRANSITION_REFERENCE.search(line) is None:
        return False
    clauses = [part.strip() for part in CLAUSE_BOUNDARY.split(line) if part.strip()]
    inherited_transition = False
    saw_safe_clause = False
    for clause_index, clause in enumerate(clauses):
        has_transition = bool(TRANSITION_REFERENCE.search(clause))
        danger = bool(
            SAFE_TRANSITION_OBJECT.search(clause)
            or TRANSITION_LIFECYCLE_ACTION.search(clause)
        )
        if has_transition:
            inherited_transition = True
            if _clause_is_direct_safe_notice(clause, False):
                saw_safe_clause = True
            else:
                anchor_danger = bool(
                    SAFE_TRANSITION_OBJECT.search(clause)
                    or TRANSITION_LIFECYCLE_ACTION.search(clause)
                    or IMPLEMENTATION_AUTHORITY.search(clause)
                    or POSITIVE_WORK_DIRECTIVE.search(clause)
                )
                later = [
                    candidate
                    for candidate in clauses[clause_index + 1 :]
                    if SAFE_TRANSITION_OBJECT.search(candidate)
                    or TRANSITION_LIFECYCLE_ACTION.search(candidate)
                ]
                if anchor_danger or not later or not all(
                    _clause_is_direct_safe_notice(candidate, True) for candidate in later
                ):
                    return False
        elif inherited_transition and danger:
            if not (
                _clause_is_direct_safe_notice(clause, True)
                or re.match(r"^not\b", clause, re.IGNORECASE)
            ):
                return False
            saw_safe_clause = True
    return saw_safe_clause


def _structural_children(lines: Sequence[str], index: int) -> Iterable[str]:
    parent = lines[index]
    parent_list = STRUCTURAL_ITEM.match(parent)
    parent_heading = re.match(r"^\s*(?P<marks>#{1,6})\s+", parent)
    parent_is_table = parent.lstrip().startswith("|")
    inherited_plain_list = False
    saw_blank = False
    position = index + 1
    while position < len(lines):
        child = lines[position]
        position += 1
        if not child.strip():
            saw_blank = True
            continue
        child_list = STRUCTURAL_ITEM.match(child)
        child_heading = re.match(r"^\s*(?P<marks>#{1,6})\s+", child)
        if parent_list is not None:
            parent_indent = len(parent_list.group("indent"))
            if child_list is None:
                continuation_indent = len(child) - len(child.lstrip())
                if continuation_indent <= parent_indent:
                    return
            else:
                child_indent = len(child_list.group("indent"))
                if (
                    child_indent < parent_indent
                    if inherited_plain_list
                    else child_indent <= parent_indent
                ):
                    if (
                        not inherited_plain_list
                        and child_indent == parent_indent
                        and (
                            TRANSITION_CHILD_LEAD.search(child)
                            or (
                                TRANSITION_CHILD_CONTEXT.search(child)
                                and (
                                 TRANSITION_LIFECYCLE_ACTION.search(child)
                                 or IMPLEMENTATION_AUTHORITY.search(child)
                                 or ROOT_AUTHORITY_CUE.search(child)
                                 or ROOT_SUBJECT_RELATION.search(child)
                             )
                            )
                            or _clause_is_direct_safe_notice(child, True)
                        )
                    ):
                        yield child
                        if _clause_is_direct_safe_notice(child, True):
                            continue
                    return
        elif parent_is_table:
            if not child.lstrip().startswith("|"):
                return
        elif parent_heading is not None:
            if child_heading is not None and len(child_heading.group("marks")) <= len(
                parent_heading.group("marks")
            ):
                return
        else:
            if child_heading is not None:
                return
            if saw_blank and child_list is None:
                return
        yield child
        if parent_list is None and not parent_is_table and parent_heading is None and saw_blank:
            # For a plain notice, only the immediately following list block may
            # inherit its transition anchor across a blank line.
            parent_list = STRUCTURAL_ITEM.match(child)
            inherited_plain_list = parent_list is not None
        saw_blank = False


def _safe_notice_has_positive_child(lines: Sequence[str], index: int) -> bool:
    logical_block = lines[index].strip()
    for child in _structural_children(lines, index):
        logical_block += " " + child.strip()
        if _has_positive_root_transition_statement(logical_block):
            return True
        danger = bool(
            TRANSITION_WORK_TRACKING.search(child)
            or TRANSITION_LIFECYCLE_ACTION.search(child)
            or IMPLEMENTATION_AUTHORITY.search(child)
            or ROOT_AUTHORITY_CUE.search(child)
            or ROOT_SUBJECT_RELATION.search(child)
            or INHERITED_DEMO_AUTHORITY.search(child)
        )
        if danger and not _clause_is_direct_safe_notice(child, True):
            return True
    return False


def _root_notice_has_positive_child(lines: Sequence[str], index: int) -> bool:
    """Check authority/work inherited by structural children of a root notice."""

    logical_block = lines[index].strip()
    for child in _structural_children(lines, index):
        logical_block += " " + child.strip()
        if _has_positive_root_transition_statement(logical_block):
            return True
    return False


def _trusted_claude_firewall_lines(lines: Sequence[str]) -> Set[int]:
    """Return exact trusted section indexes, or none after any local change."""

    width = len(TRUSTED_CLAUDE_FIREWALL_SECTION)
    matches = [
        start
        for start in range(0, len(lines) - width)
        if tuple(lines[start : start + width]) == TRUSTED_CLAUDE_FIREWALL_SECTION
        and lines[start + width] == TRUSTED_CLAUDE_FIREWALL_BOUNDARY
    ]
    if len(matches) != 1:
        return set()
    return set(range(matches[0], matches[0] + width))


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
            lines = text.splitlines()
            for index, line in enumerate(lines):
                line_number = index + 1
                verifier_invocation = TRANSITION_VERIFIER_INVOCATION.search(line)
                if verifier_invocation:
                    violations.append(
                        Violation(
                            "active-doc-transition-verifier",
                            relative,
                            line_number,
                            "active Demo documentation must not instruct agents to run the owner-gated transition verifier",
                        )
                    )
                    continue
                transition_reference = TRANSITION_REFERENCE.search(line)
                if transition_reference is None:
                    continue
                positive_child = _safe_notice_has_positive_child(lines, index)
                positive_claim = _has_positive_root_transition_statement(line)
                if (
                    _is_explicit_safe_transition_notice(line)
                    and not positive_child
                    and not positive_claim
                ):
                    continue
                if (
                    TRANSITION_WORK_TRACKING.search(line)
                    or TRANSITION_LIFECYCLE_ACTION.search(line)
                    or positive_child
                ):
                    violations.append(
                        Violation(
                            "active-doc-transition-work-item",
                            relative,
                            line_number,
                            "active Demo documentation must not track transition follow-up, checklist, gate, or completion work",
                        )
                    )
                    continue
                violations.append(
                    Violation(
                        "active-doc-transition-authority",
                        relative,
                        line_number,
                        "active Demo documentation may mention transition material only in a direct non-authoritative notice",
                    )
                )
    return violations


def _invocation_candidates(root: Path) -> Iterable[Path]:
    seen: Set[str] = set()
    for path in sorted(root.iterdir()) if root.is_dir() else ():
        if (
            path.is_file()
            and not path.is_symlink()
            and path.suffix.lower() in INVOCATION_TEXT_SUFFIXES
        ):
            relative = _relative(root, path)
            if relative not in seen:
                seen.add(relative)
                yield path
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

        lines = text.splitlines()
        trusted_lines = (
            _trusted_claude_firewall_lines(lines) if relative == "CLAUDE.md" else set()
        )
        for line_index, line in enumerate(lines):
            line_number = line_index + 1
            if TRANSITION_VERIFIER_INVOCATION.search(line):
                violations.append(
                    Violation(
                        "demo-policy-transition-verifier",
                        relative,
                        line_number,
                        "Demo policy documents must not instruct agents to invoke the owner-gated transition verifier",
                    )
                )
            if line_index not in trusted_lines and (
                _has_positive_root_transition_statement(line)
                or (
                    TRANSITION_REFERENCE.search(line) is not None
                    and not line.lstrip().startswith("|")
                    and _root_notice_has_positive_child(lines, line_index)
                )
            ):
                violations.append(
                    Violation(
                        "root-policy-transition-authority",
                        relative,
                        line_number,
                        "Demo policy documents must not make transition material authoritative or schedule transition work",
                    )
                )

        index = 0
        while index < len(lines):
            if not lines[index].strip():
                index += 1
                continue
            block_start = index
            block: List[str] = []
            first = lines[index]
            first_list = STRUCTURAL_ITEM.match(first)
            first_table = first.lstrip().startswith("|")
            first_quote = first.lstrip().startswith(">")
            if first_table:
                # A Markdown table row is a complete semantic record.  Joining
                # the whole table would incorrectly inherit a transition anchor
                # into unrelated reference rows.
                block.append(first)
                index += 1
            elif first_list is not None:
                block.append(first)
                index += 1
                while index < len(lines):
                    candidate = lines[index]
                    if not candidate.strip():
                        break
                    candidate_list = STRUCTURAL_ITEM.match(candidate)
                    if candidate_list is not None and len(
                        candidate_list.group("indent")
                    ) <= len(first_list.group("indent")):
                        break
                    if len(candidate) - len(candidate.lstrip()) <= len(
                        first_list.group("indent")
                    ):
                        break
                    block.append(candidate)
                    index += 1
            while (
                first_list is None
                and not first_table
                and index < len(lines)
                and lines[index].strip()
            ):
                candidate = lines[index]
                if block and (
                    candidate.lstrip().startswith("#")
                    or (first_table and not candidate.lstrip().startswith("|"))
                    or (first_quote and not candidate.lstrip().startswith(">"))
                    or (
                        first_list is not None
                        and (candidate_list := STRUCTURAL_ITEM.match(candidate)) is not None
                        and len(candidate_list.group("indent")) <= len(first_list.group("indent"))
                    )
                ):
                    break
                block.append(lines[index])
                index += 1
            joined = " ".join(part.strip() for part in block)
            block_lines = set(range(block_start, index))
            policy_block = "" if block_lines and block_lines <= trusted_lines else joined
            if _has_positive_root_transition_statement(policy_block) and not any(
                item.rule == "root-policy-transition-authority"
                and item.path == relative
                and block_start + 1 <= (item.line or 0) <= index
                for item in violations
            ):
                violations.append(
                    Violation(
                        "root-policy-transition-authority",
                        relative,
                        block_start + 1,
                        "Demo policy documents must not make transition material authoritative or schedule transition work",
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
