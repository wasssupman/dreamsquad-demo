# design-blueprint — 현재 설계의 윤곽(blueprint) 다시 쓰기

**상태: 착수 대기**(2026-10-01 범위 합의 · 본 작업은 다음 세션 — `wassup-core` 를 루트로 하는 세션).

## 목적

**어떤 세션이나 사람이 봐도 현재 설계의 윤곽을 쉽게 볼 수 있게 한다**(사용자 2026-10-01).
blueprint = **현재 설계의 입구(지도)**다. 「정식 프로젝트로 옮겨 갈 입력 묶음」(옛 브랜치 `blueprint` 의 성격)이 아니다.

## 다음 세션 진입 가이드 — 먼저 읽는다

1. **작업 위치** — `/Users/sy/dev/wassup-core` 를 루트로 연다. 브랜치는 `unified-effect-layer`(이름만 옛 spec 이름 · GitHub `main` 과 같은 계보 — 그 위에 커밋한다). 푸시는 사용자 승인 후 `git push origin HEAD:main`. `/Users/sy/dev/wassup` 폴더(`main` 체크아웃)는 **열지도 고치지도 않는다**.
2. **옛 설계는 잊는다 — 근거로 쓰지 않는 것**:
   - `/Users/sy/dev/wassup` 폴더 · 그 폴더에 쌓인 자동 메모리 · `docs/blueprint/prd.zip`.
   - 태그 `archive/pre-spec-reset`(2026-10-01 초기화 전 spec · plans · prototype).
   - 로컬 브랜치 `blueprint` — STRATEGY · VERIFICATION · `data/` · PRD 본문 전부. 내용이 설계 전환 전이다(체비셰프 사각 판정 · 선물 페이즈 · 점수 3축 · 튜토리얼 · 락스텝 · 별도 프로젝트 이전 전제). **PRD 의 장 나누기만** 참고한다 — 개요 · 게임 흐름 · 방어유닛 · 적 · 웨이브 · 드림캐쳐 · 맵 · 기믹 · 전투 규칙 · 전투 UX · 아웃게임 · 토너먼트 · 백엔드 · 데이터 · 시뮬 규칙(이 목록이 전부다 — 브랜치를 열 필요 없다).
   - 코드 주석의 옛 출처 메모(「옛 `BattleBridge.X` 의 후계」 · 옛 spec 이름 꼬리표 — 941 파일 중 510)는 이력일 뿐이다. 현재 동작은 **코드 본문**으로 확인한다.
   - `docs/production-transition/`(`CLAUDE.md` 제약 11 그대로).
3. **현재 설계의 근거(이것만 쓴다)** — 코드 · 에셋(`wassup-core` 작업 트리) → `CLAUDE.md` → `docs/reference/` 현행 문서 → 남은 spec 3개(`battle-core-rebuild` · `unified-effect-layer` · `skill-data-table`). 장의 문장마다 근거 파일을 단다(대조 가능하게).
   - ⚠ `docs/reference` 중 옛 설계가 섞인 문서는 대조 후에만 쓴다: `dreamcatcher-portability.md` · `review-skill-comparison.md` · `object-pipeline-map.md`(옛 전투 이름이 섞여 있음) · `드림캐쳐_각성안_최종스펙_v1.md` · `keyring-portability.md`(설계 전환 전 작성). 처분은 unit 7.
4. **현행 방향** — 서버 권위 실시간 게임 서버(`battle-core-rebuild/README.md` 결정 ⑩). 옛 락스텝 · 별도 프로젝트 이전 · 히트박스 판정 교체 계획은 현재 설계가 아니다 — 쓰지 않는다(사용자가 살리면 그때).
5. 이 세션에서 넘어온 정리 항목(이 spec 과 별개) = `docs/spec/README.md` Follow-up Backlog 「spec 초기화 잔여」.

## 구조 (합의안)

```
docs/blueprint/
├── README.md         ← 한 장 윤곽: 게임 한 줄 · 한 판 흐름 · 시스템 지도(도메인 → 장 → 정본 위치) · 읽는 순서
├── units.md          ← 방어유닛 · 적 · 보스: 역할 축 · 몸 크기 표 · 발주 규격
├── skills.md         ← 효과 표 · 소유 줄 · 드림캐쳐 카드 · 각성 경제
├── outgame.md        ← 로비 · 스쿼드 · 덱 · 로그인 · 프로필
├── tournament.md     ← 토너먼트 · 제출 · 랭킹 · 서버 방향(⑩)
├── data-pipeline.md  ← 시트 8탭 ↔ 에셋 ↔ 전투 정의표
└── decisions.md      ← 의식적 채택/기각(저지 없음 등) · 현행 사용자 결정만
```

- 이미 현행인 reference 는 **옮기지 않고** README 가 장으로 가리킨다: 흐름 `ingame-flow.md` · 전투 규칙 `battle-core-architecture.md` §1 · 맵 · 웨이브 `map-wave-balancing.md` · 이동 `enemy-movement-algorithm.md` · 점수 `score-formula.md`.
- 기믹 · 전투 UX 는 우선 README 지도에서 `battle-core-architecture.md` §1.14 · `ingame-flow.md` §3 으로 가리킨다. 부족하면 장을 늘린다(사용자 확인).
- main 의 지금 `docs/blueprint/` 3장(몸 원장 2 · 전복 인벤토리)은 `units.md` · `decisions.md` 로 흡수하고 지운다.

## 작성 규칙

- **윤곽만** — 무엇이 있고 · 어떻게 맞물리고 · 왜 그런가. 수치는 적지 않고 **정본 위치**(에셋 · 시트 · 코드)만 가리킨다 — 적으면 그날부터 낡는다.
- **한 사실은 한 곳** — `docs/reference` 가 이미 말하는 것은 링크한다. 복제하지 않는다.
- **게임 언어가 먼저**, 코드 이름은 괄호. 옛 ECS · 옛 spec 용어를 쓰지 않는다.
- 장마다 머리에 「기준 커밋」 한 줄.

## 작업 단위 (장 하나 = 단위 하나 · 번호 문서는 착수할 때 쓴다)

| # | 산출물 | 목적 |
|---|---|---|
| 0 | `docs/blueprint/README.md` 골격 | 한 장 윤곽 · 시스템 지도 · 읽는 순서(장이 채워지면 마지막에 다시 다듬는다) |
| 1 | `units.md` | 방어유닛 · 적 · 보스 축 + 몸 원장 흡수 |
| 2 | `skills.md` | 효과 = 한 표 · 소유자가 쓴다 · 카드 · 각성 경제 |
| 3 | `outgame.md` | 로비 → 판 → 결과 밖의 흐름 전부 |
| 4 | `tournament.md` | 토너먼트 · 제출 · 랭킹 · 서버 계약 방향 |
| 5 | `data-pipeline.md` | 시트 ↔ 에셋 ↔ 정의표 · 값의 정본 |
| 6 | `decisions.md` | 의식적 채택/기각 + 현행 사용자 결정 |
| 7 | reference 정합 | 위 ⚠ 5개 판정(유지 · 장으로 흡수 · 삭제) |
| 8 | 연결 · 정리 | `CLAUDE.md` 참조 표 맨 위 「현재 설계 윤곽 → `docs/blueprint/README.md`」 + 워크플로에 「설계를 바꾸는 spec 은 끝날 때 해당 장 갱신」 · 옛 3장 삭제 · 브랜치 `blueprint` · `prd.zip` 처분 질문 |

## 진행 방식

- 장마다: 초안(현재 코드 · 에셋을 읽는 에이전트 — 장별 병렬 가능) → **리드가 문장별 근거를 직접 대조**(반증자 에이전트 fan-out 금지) → 사용자 확인 → 커밋.
- 커밋은 `git commit -m … -- <경로>` 로만(공유 인덱스) · git 쓰기는 샌드박스 해제 필요 · 푸시는 매번 승인.

## 완료 기준

- 새 세션이 `docs/blueprint/README.md` 하나로 게임 전체의 윤곽(무엇이 있고 · 어디가 정본인지)을 잡는다.
- 모든 장의 문장이 현재 코드 · 에셋 · 현행 reference 로 대조됐다 · 장 안에 옛 용어(`BattleBridge` · ECS · 체비셰프 사각 판정 · 선물 페이즈 · 점수 3축 · 튜토리얼) grep 0.
- `CLAUDE.md` 참조 표에서 blueprint 로 들어온다.
