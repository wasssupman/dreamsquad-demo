# Defense Tournament (wassup)

비동기 토너먼트 디펜스 게임. Unity 하이브리드 ECS — 전투 시뮬레이션만 ECS(Entities 6.4), 나머지 MonoBehaviour. 스펙 주도 개발(`docs/spec/`)로 진행한다.

## 요구사항

- **Unity 6000.4.3f1** (정확히 이 버전 — `ProjectSettings/ProjectVersion.txt`)
- Git (macOS 기준. `AGENTS.md` 가 symlink 라 Windows 는 developer mode 필요)
- (선택) AI 하네스: Claude Code / Codex — 아래 부트스트랩 참조

## 첫 실행 (게임 재현)

1. 클론 → Unity Hub 로 폴더 열기 (버전 정확히 일치시킬 것)
2. 패키지는 `Packages/manifest.json` + `packages-lock.json` 으로 자동 복원 (첫 임포트 수 분 소요)
3. `Assets/_Project/Scenes/OutgameScene.unity` 열고 **Play** → Start 로 드래프트→배틀 진입 (또는 `BattleScene.unity` 에서 바로 Play)

## 클론하면 재현되는 것 vs 손수 해야 하는 것

| 클론으로 재현 (커밋됨) | 수동 부트스트랩 (커밋 불가) |
|---|---|
| 엔진 버전·패키지 lock·전체 소스/에셋 | Unity 설치 + 라이선스 |
| 규약·스펙 문서 (`CLAUDE.md`, `docs/`) | Claude Code / Codex 설치·인증 |
| `.claude/` 프로젝트 스킬·에이전트·훅·공용 permission | OMC/superpowers 등 개인 하네스 플러그인 |
| `AGENTS.md`(=CLAUDE.md symlink, Codex 정책 로드) | MCP 서버 연결 (각자 설정) |
| 프로젝트 교훈 (`docs/reference/lessons/`) | 개인 permission 축적 (`.claude/settings.local.json`) |

## AI 하네스 부트스트랩 체크리스트 (새 클론마다)

- [ ] **Claude Code 첫 실행**: 클론된 `.claude/settings.json` 의 project 훅(ECS 리뷰 감지)을 1회 검토/승인 — 정상 보안 게이트
- [ ] **(선택) oh-my-claudecode / superpowers 설치** — 미설치여도 바닐라 Claude Code 로 규약 준수 가능. 단 **투트랙 리뷰**(`two-track-review` 스킬)는 OMC 의 code-reviewer 에이전트에 의존하므로, 그 워크플로우를 쓰려면 OMC 필수
- [ ] **Codex 사용 시**: 인증 후 `AGENTS.md` 가 CLAUDE.md 전체 정책을 자동 로드하는지 확인
- [ ] **MCP**: 각자 설정 (Unity MCP 서버 패키지는 프로젝트에 포함돼 있음 — 클라이언트 연결만)
- [ ] **permission**: 공용 read-only 5종은 자동 적용, 그 외는 작업하며 각자 승인 (`settings.local.json` 에 축적)

## 읽는 순서 (규약과 현재 상태)

1. **`CLAUDE.md`** — 절대 제약·ECS 맥락 경계·워크플로우 (에이전트 정책의 단일 소스)
2. **`docs/spec/README.md`** — 스펙 구조 + Follow-up Backlog (다음 작업 후보)
3. 최근 Demo spec 의 `README.md` + `{N}_handoff_summary.md` — 진행 중 작업 파악 (Claude Code 에선 `catchup` 스킬이 이걸 자동으로 함)
4. **`docs/reference/lessons/`** — 프로젝트·환경 고유의 함정 모음 (작업 전 一讀 권장)

`docs/production-transition/`은 기본 읽기 대상이 아니다. Project owner가 현재 요청에서
production-transition 작업을 명시적으로 활성화한 경우에만 읽는 dormant downstream 자료이며,
Demo의 설계·구현·검증과 다음 작업 선정에는 사용하지 않는다.

## 워크플로우 요약

- **스펙 주도**: 기능 추가/변경은 `docs/spec/{feature-slug}/` 에 스펙 먼저, 작업 단위 파일(0~N) 순서로 구현. 1 파일 = 1 커밋
- **리뷰**: 각 코드 작업 단위 종료 후 code-review, ECS 전투 코드 변경은 투트랙 리뷰
- **ECS 경계**: `BattleBridge` 가 MonoBehaviour↔ECS 유일 창구. 맥락(Units/Movement/Combat/Effects) 간 Component 직접 쓰기 금지 — 상세는 `CLAUDE.md`·`docs/TRD.md`
