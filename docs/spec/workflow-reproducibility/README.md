# workflow-reproducibility

> 상태: **초안 (재현성 프레임 · MCP 제외) 2026-07-06** — 이전 프레임(team-onboarding)에서 확장.
> 검증 질문: **새 폴더에 fresh clone 한 뒤, 최소 수작업으로 기존 워크플로우(스펙 주도 · 하네스 · 스킬/훅 · 축적 지식 · 투트랙 리뷰)가 재현되는가?** — 팀원뿐 아니라 **다른 경로로 재클론한 나 자신**에게도.

## 배경 — "협업"이 아니라 "재현성" 문제 (실측)

클론은 *게임*을 결정적으로 재현하지만(엔진 버전·패키지 lock·docs tracked), *일하는 환경*은 재현하지 못한다. Claude Code 상태가 **cwd 경로를 키로** 개인 환경에 저장되기 때문이다. 이미 겪은 한계다:

- **상태 사일로 3개 실존**: `~/.claude/projects/` 에 `-Users-sy-dev-wassup`, `...-wassup-gen`, `...-wassup-gen-docs-SoT-Project-ONE`. → 다른 폴더에서 작업할 때마다 메모리·permission·세션 상태가 **각각 분절**.
- **메모리 38개**: 그 경로에만. 재클론(새 경로)·머신 교체·재설치 시 소실.
- **permission allow 188개** (`settings.local.json`): 그 폴더에만. 재클론 시 전부 소실 → 프롬프트 폭증.
- **훅 배선**(`settings.json`)·**OMC 설치**: 레포 밖.

## fresh clone(새 경로)에서 무엇이 사라지나 — 이식 인벤토리

| 자산 | 현재 위치 | 새 경로 클론 | 조치 |
|---|---|---|---|
| ECS 리뷰 훅 배선 | `.claude/settings.json` (gitignored) | ✗ | **(a)** 커밋 → 단위 0 |
| permission allow 188 | `settings.local.json` (개인) | ✗ | **(a)** 프로젝트 공용·안전분만 승격 → 단위 0 |
| 축적 지식 38 | `~/.claude` 메모리 (경로 키) | ✗ | **(a)** 참조 문서 승격 → 단위 1 |
| Codex 정책 로드 | AGENTS soft 포인터 | △ | **(a)** symlink → 단위 2 |
| 프로젝트 스킬/에이전트 | `.claude/skills`,`agents` (tracked) | ✓ | 이미 됨 |
| OMC/superpowers | `~/.claude` 전역 | 머신 유지·재설치 시 ✗ | **(b)** 부트스트랩 문서 → 단위 3 |
| Codex 인증·Unity 라이선스 | 머신/계정 | ✗ | **(b)** 부트스트랩 체크리스트 → 단위 3 |
| Unity MCP 연결 | per-user | ✗ | **범위 밖** — 사용자 수동 관리 |

**핵심 규율**: **(a) 레포로 커밋해 재현 / (b) 커밋 불가 → 부트스트랩 체크리스트로 문서화.** 이 둘을 섞지 않는다. (b)를 커밋한 척하지 않는다.

## 확정 결정 (유지 — flip 경로 명시)

- **하네스 공유 = 프로젝트 전용(thin)**: 스킬·에이전트·훅·문서·승격 지식만 레포로. OMC/superpowers 는 (b) 부트스트랩 안내. *(thick 승격: `enabledPlugins`+marketplace 커밋)*
- **AGENTS↔CLAUDE = symlink**: 단일 소스·drift 0. *(Windows 합류 시 `@import`)*
- **MCP = 사용자 수동 관리**: 이 spec 범위에서 제외(사용자 요청). 부트스트랩 README 엔 "각자 설정" 한 줄만.

## 구현 문서 목록 (파일번호 = 재현 임팩트 순)

| 단위 | 파일 | 핵심 |
|---|---|---|
| **0** | `0_gitignore_and_settings_split.md` | `.claude/*` 통무시 해제 + 훅·**permission 공용분** 커밋, 개인분 분리 |
| **1** | `1_memory_promotion.md` | 축적 지식 → 커밋 참조 문서 (경로 소실 방지) |
| **2** | `2_doc_unification.md` | AGENTS↔CLAUDE symlink + stale 스냅샷 제거 |
| **3** | `3_bootstrap_checklist.md` | 부트스트랩 체크리스트 (사람 + (b) 환경 의존) + 루트 README |
| — | `4_handoff_summary.md` | 종료/인계 요약 (실행 후) |

## feature-wide 계약 / 공통 원칙

1. **재현성 우선**: 모든 조치의 완료 기준은 "fresh clone(새 경로)에서 재현되는가". 같은 경로 재클론이 아니라 **새 경로**를 기준으로 검증.
2. **(a)/(b) 정직 분리**: 커밋 가능분만 커밋, 불가분은 부트스트랩 체크리스트. "다 고쳤다" 착시 금지.
3. **표준 컨벤션 준수**: `settings.local.json`·`CLAUDE.local.md` 만 gitignore, 나머지 `.claude/` 공유분 커밋.
4. **permission 승격은 공용·안전분만**: 프로젝트 반복 작업(표준 빌드/테스트, `git status` 류 등)만 `settings.json` 승격. 광범위·개인(무제한 Bash 등)은 `settings.local.json` 유지.
5. **메모리는 자동 공유·자동 이식 불가**: 승격 대상은 프로젝트/환경 지식만. 원본 메모리 무손상.
6. **문서 계층 불변**: 승격 지식은 참조 문서(`docs/reference/…`), 정책 문서 비대화 금지.
7. **스코프 엄수**: 재현성 세팅만. 게임 기능·리팩터·MCP 는 범위 밖.

## 후속 후보 (현 범위 밖)

- **문서 수명주기 정리** [P2]: PRD/TRD 는 "프로토타입/Phase" 프레임에 동결된 legacy(자기를 "매번 주입"이라 서술하나 실제 주입은 CLAUDE.md뿐). staleness 배너 + supersession 포인터로 정직하게 동결.
- **ADR 로그** [P2]: 횡단 결정(TimeManager·구조적 결정론·ECS 맥락 규칙)을 `docs/decisions/` 에 동결·번호·supersede 규칙으로. 단위 1(지식 승격)과 합류 가능.
- **thick 하네스 표준화**: OMC/superpowers 마켓플레이스 커밋(사용자 결정 시).
