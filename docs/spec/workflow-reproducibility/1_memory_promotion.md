# 1 — 메모리 → 커밋 문서 승격

## 목적

per-user 메모리(38개)는 `~/.claude/projects/<cwd-경로>/memory/` 에 **경로 키로** 저장된다. 이미 사일로 3개로 분절돼 있어(`wassup`·`wassup-gen`·`...ONE`), 팀원은 물론 **다른 경로로 재클론한 나 자신도** 이 지식을 못 받는다. 머신 교체·재설치 시엔 완전 소실. 그중 **프로젝트/환경 지식**을 커밋 참조 문서로 승격해 fresh clone 에서도 살아남게 한다. 순수 개인 취향/세션 국소 항목은 승격 제외.

## 변경 대상

- `docs/reference/lessons/` (as-built): `README.md`(인덱스) + `01-unity-mcp-operation.md` + `02-dev-workflow-git-scene.md` + `03-rendering-assets.md` + `04-sim-design.md`
- 원본 메모리 파일: **무손상**(삭제·이동 금지, 개인 유지).

## 구현

원문 복붙이 아니라 참조 문서 톤으로 재작성. 분류(초안, 승인 필요):

**승격 후보 — 프로젝트/환경 지식**
- *Unity MCP 운용*: Play 포커스 필요 · force reimport 브리지 끊김 · execute_code=method body · timeScale=0 anim 검증 · run_tests 필터/신규 .cs refresh gotcha
- *Spine*: spine-unity 3.8 고정 · 한글 import macOS 깨짐(NFC+.json+버전)
- *Tilemap/맵*: 카메라 pitch per-phase · 격자선=압축 · dirt autotile 유기적 경계 · 바닥=tileSet
- *테스트 인프라*: EditMode 테스트 폴더 위치 · 테스트 리그 worktree · EditMode Play 잔류 거짓실패
- *Authoring 함정*: 프랍 정식 경로(PropDataEditor) · 프랍 눕음/묻힘 · 벤더 투사체 VFX 통합 · 드래그 프리뷰 sway 위치
- *git/씬 위생*: git add 샌드박스 비활성 · Screenshots 비추적 스크래치 · in-memory 배선 검증 · SaveScene WIP 베이크 · 씬 checkout 카메라 날림 · 병행 세션 커밋 위생
- *설계*: 전투 시뮬 구조적 결정론(index 기반)

**개인 유지 — 작업 스타일/의견 (CLAUDE.md 작업지침이 이미 커버)**
- 질문 적게·기본값 진행 · 리뷰 케이던스 · no-bandaid · tests-as-spec · Codex 2차리뷰 실행계획 · 대규모 refactor A/B/C 분할 · 버그픽스≠기능 · 배경/프랍 스크린샷 검증 · Phase 종료 잔여이슈 점검
- *transient 상태*: 키링 브랜치 · unit-health-display 진행(이미 spec/git)

**결정 필요**: 스타일 피드백 일부(스크린샷 검증·no-bandaid·리뷰 케이던스)를 팀 공유가 유익하다고 보면 CLAUDE.md 작업지침으로 승격 가능. 기본값 = 개인 유지.

## 완료 기준

- `docs/reference/` 승격 문서 tracked. 승격 항목 목록을 사용자가 승인.
- 원본 메모리 파일 개수/내용 무손상.
- 승격 문서가 원문 복제가 아니라 참조 톤(문서 계층 불변식 준수).
