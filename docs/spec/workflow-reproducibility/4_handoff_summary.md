# 4 — Handoff Summary

## Commit

- `f9522e2` docs(spec): 재현성 계획 (MCP 제외)
- `6658cc7` unit 0 — `.claude` 표준 추적 + settings 분할
- `6467f4b` unit 2 — AGENTS.md → CLAUDE.md symlink
- `6b8cfc6` unit 1 — auto-memory 27건 → `docs/reference/lessons/` 승격 (+prop-tile 스킬 stale 동기화)
- `9845778` critic 리뷰 반영 (APPROVE-WITH-CHANGES, 8건)
- (unit 3) 루트 `README.md` + 스펙 종료 — 이 커밋

## Implemented

- `.gitignore` 의 `.claude/*` 통무시 → 표준(개인 파일만 무시)으로 전환. 스킬/에이전트/훅 자동 추적.
- `.claude/settings.json` 커밋: ECS 리뷰 훅(`$CLAUDE_PROJECT_DIR` 경로) + read-only permission 5종(git status/diff/log·grep·rg). OMC/context7 plugins 는 `settings.local.json`(개인)으로 이관.
- `AGENTS.md` = CLAUDE.md symlink — Codex 가 전체 정책 자동 로드. stale "Current Spec Status" 스냅샷 소멸(catchup 스킬이 대체).
- auto-memory 38건 중 27건을 `docs/reference/lessons/` 4파일로 승격(증상→원인→처방). 미승격 11건 = 개인 스타일 피드백 9 + transient 상태 2(의도적).
- 루트 `README.md`: 사람 진입점 + (a)커밋 재현/(b)수동 부트스트랩 경계 표 + 체크리스트.

## Key Files

- `.claude/settings.json` · `.gitignore` (88행 부근)
- `AGENTS.md` (symlink) · `README.md` (루트)
- `docs/reference/lessons/{README,01..04}.md`

## Verified

- fresh clone(새 경로) 실측: 훅/권한/스킬 7종/symlink/lessons/엔진버전 재현, `settings.local.json` 미유출, ECS 훅 스모크 exit 0.
- critic 리뷰(APPROVE-WITH-CHANGES) 8건 전부 반영: unit 0 as-built rev(HIGH)·투트랙 OMC 의존 명시·스킬 stale 동기화·lessons 중복 축약·숫자 정정.
- 로컬 세션 effective 설정 무손실(OMC·context7 활성 유지).

## Notes (되돌리면 안 되는 것 / 경계)

- **deepinit 재실행 시 `AGENTS.md` 가 실제 파일로 재생성돼 symlink 이 풀린다** → `rm AGENTS.md && ln -s CLAUDE.md AGENTS.md` 재적용.
- 커밋된 allowlist 는 클론한 누구에게나 자동 적용 → `settings.json` 에 쓰기/광범위/네트워크/MCP 권한 승격 금지(의도된 thin 정책).
- 투트랙 리뷰는 OMC code-reviewer 의존 — thin clone 에선 그 워크플로우만 OMC 필수.
- MCP 는 사용자 수동 관리(스펙 범위 밖). LFS 미도입(사용자 결정).
- lessons 는 참조 문서 — 커밋 해시/파일:라인은 당시 근거, 코드가 진실원.

## Follow-up

- Claude Code 훅 승인 프롬프트·Unity 첫 Play 는 수동 단계 — 실제 새 머신/팀원 첫 클론 때 README 체크리스트 완주 확인.
- ~~개인 auto-memory 승격 27건의 포인터 슬림화~~ — **불필요 판정(2026-07-06)**: drift 는 "lessons 먼저" 규칙(memory `project-lessons-repo-first`)으로 예방되고, 재현성에는 무기여(개인 사일로 정리일 뿐). stale memory 는 발견 시점에 해당 건만 삭제/포인터화(on-contact).
- 문서 수명주기 정리(PRD/TRD staleness 배너)·ADR 로그 — README 후속 후보 참조.
- deepinit 을 다시 쓸 경우 AGENTS.md 생성 정책과 symlink 충돌 해소 방안.
