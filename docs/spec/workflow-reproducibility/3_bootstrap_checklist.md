# 3 — 부트스트랩 체크리스트 + 루트 README

## 목적

레포로 커밋할 수 없는 **(b) 환경/계정 의존**을 정직하게 문서화한다. (a) 항목(단위 0~2)은 클론하면 자동 재현되지만, OMC 설치·Codex 인증·Unity 라이선스는 커밋 불가 → fresh clone 시 사람이 밟아야 할 단계. 사람용 진입점(루트 README)도 여기서. (MCP 는 사용자가 수동 관리 — 이 spec 범위 밖.)

## 변경 대상

- `README.md` (신규, 레포 루트) — 사람 진입점 + 부트스트랩 체크리스트
- (선택) `docs/reference/bootstrap.md` — 체크리스트가 길어지면 분리

## 구현

**루트 README 섹션**:

1. **프로젝트 한 줄**: 비동기 토너먼트 디펜스, 하이브리드 ECS.
2. **요구사항**: Unity **6000.4.3f1**(정확히) · Git · (선택) AI 하네스.
3. **첫 실행** (게임 재현): 클론 → Unity Hub 로 열기 → 패키지 자동복원(manifest/lock tracked) → 씬 Play.
4. **(b) 환경 부트스트랩 체크리스트** — 커밋 불가, 새 클론마다 수행:
   - [ ] Unity 6000.4.3f1 설치 + 라이선스
   - [ ] Claude Code 첫 실행: 클론된 `.claude/settings.json` 의 **project 훅을 1회 검토/승인**(신뢰 프롬프트 — 정상 보안 게이트)
   - [ ] (선택) OMC/superpowers 설치 — 미설치 시 바닐라로도 규약 준수 가능. 설치법 링크. **단, 투트랙 리뷰(`two-track-review` 스킬)는 OMC 의 code-reviewer 에이전트에 의존 → 투트랙 리뷰를 쓰려면 OMC 필수**
   - [ ] Codex 사용 시: 인증 + `AGENTS.md`(=CLAUDE.md symlink) 자동 로드 확인
   - [ ] 개인 permission: 공용 read-only 5종은 `settings.json` 자동, 그 외는 각자 승인해 `settings.local.json` 에 축적
   - [ ] MCP: 사용자가 직접 설정 (이 spec 범위 밖 — README 엔 "각자 설정" 한 줄만)
5. **규약 읽는 순서**: `CLAUDE.md` → `docs/spec/README.md`(Follow-up Backlog) → 최근 spec README + handoff. 상태 재구성은 `catchup` 스킬.
6. **워크플로우 요약**: spec-driven · 1파일=1커밋 · 투트랙 리뷰 · 맥락(Units/Movement/Combat/Effects) 경계.

**(a)/(b) 경계 명시**: README 에 "클론하면 재현되는 것 / 손수 해야 하는 것"을 표로 구분해, (b)를 커밋한 척하지 않는다.

## 완료 기준

- 루트 `README.md` tracked.
- 프로젝트 무지한 사람(또는 새 경로 클론한 나)이 README 만으로 (a) 첫 Play 도달, (b) 부트스트랩 단계 완주, 규약 위치 파악.
- (b) 체크리스트가 실제 fresh clone 1회로 검증됨(단계 누락 없음).
- CLAUDE.md/AGENTS.md 와 중복 최소(README=사람·부트스트랩, CLAUDE=에이전트 정책).

확인 2026-07-06 — fresh clone(새 경로, scratchpad) 실측: settings.json 훅+allow 5종·훅 스크립트·스킬 7종·AGENTS symlink 해석·lessons 5파일·엔진 버전 전부 재현, `settings.local.json` 미유출, ECS 훅 스모크 실행 exit 0. Unity 첫 임포트/Play 와 Claude Code 훅 승인 프롬프트는 수동 단계라 자동 검증 범위 밖(체크리스트에 명시).
