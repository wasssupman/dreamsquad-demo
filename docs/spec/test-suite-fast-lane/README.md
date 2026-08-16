# test-suite-fast-lane — 테스트 절차 개편: 핵심만 빠르게, 밸런스에 면역으로

상태: 계획 (사용자 승인 대기) 2026-08-16

## 진단 (2026-08-16 실측 + 전수 분류)

- 규모: EditMode 2,394 + PlayMode ~90개, 테스트 파일 321개 (헬퍼 제외).
- **EditMode 전체 실행 = 34초 실측** — EditMode 속도는 병목이 아니다. 느린 것은 PlayMode: 67파일 중 59개가 씬을 로드하고, 풀 Battle 씬 `LoadSceneAsync` 가 103회다.
- 밸런스 시트가 덮는 필드(health / atk→outputs[].magnitude / attackRange / cost / moveSpeed / DC percent·magnitude 등)를 **실제 에셋 로드와 함께 리터럴로 못박은 파일은 5개뿐**:
  `DirectionalVolleyIntegrationTests`(최악, ~8개 — `:293` "시트가 정본이라 여기를 맞춘다" 주석으로 시트를 쫓아다니는 구조) · `DreamcatcherCatalogSyncTests`(12개, 단 본체는 등록 동기 가드) · `PlacementLayerTests`(4개) · `DreamstoneCatalogTests`(1개) · `SlimeSplitAuthoringTests`(0-단언이라 위험 낮음, 제외).
  콘텐츠 **개수 pin** 3곳 추가: `DreamcatcherCardTextTests:372`(44장) · `DreamstoneCatalogTests:30`(64개) · `DeckInfoDisplayTests:125`(14개).
- 실제 SO 를 로드하는 EditMode 파일은 ~23개뿐. 나머지 EditMode(~230파일: 순수 수학 151 + 합성 픽스처 ECS/UI)는 **구조적으로 시트·에셋 편집에 면역**.
- 부분 실행 수단 부재: `[Category]` 0개, asmdef 는 EditMode/PlayMode 2개뿐. `run_tests` 의 `test_names`/`group_names` 는 이 셋업에서 0-match (lessons 01 §run_tests) — **동작하는 유일한 입도 = 어셈블리**.
- 신호 오염: EditMode 상시 빨강 5건(`MultiGoalPoolSeparationTests` 4건은 맵 재저작 대기 "의도적 빨강", `ObstaclePlacerTests` 1건은 기록된 상시 실패) + PlayMode 사전 실패 13건(docs/spec/README.md 2026-07-31 재측정). **빨강이 평상시 상태라 진짜 회귀가 묻힌다.**
- 중복 없음: 이름 클러스터(Flipbook 4파일, WaveConcept 6파일 등)는 헤더에 상호 범위 배제가 명시된 의도적 계층 분리. 통폐합·삭제로 얻을 것이 없다.

## 목표

1. **핵심만 빠르게**: 시트·에셋 편집에 면역인 고속 코어 lane 을 어셈블리로 분리해 선택 실행.
2. **밸런스 조정이 테스트를 깨지 않게**: 시트 필드 리터럴 pin 을 불변식·상대 비교로 전환.
3. **빨강 = 회귀** 로 신호 복구: 상시 빨강 청산.
4. **절차의 문서화**: 상황별 실행 매트릭스를 reference 문서로.

## 작업 단위

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | asmdef 분리 | `0_assets_asmdef_split.md` | 실제 SO 로드 테스트 ~23파일을 `Wassup.Tests.EditMode.Assets` 로 이동(.meta 동반) → 코어 lane 확보 |
| 1 | de-pin | `1_depin_balance_literals.md` | 시트 필드 리터럴 4파일 + 개수 pin 3곳을 `WaveKillBudgetPinTests` 패턴으로 전환 |
| 2 | 신호 청산 | `2_red_signal_cleanup.md` | EditMode 상시 빨강 5건 + PlayMode 사전 실패 13건 triage (기대값 갱신 / Ignore+사유 / 환경 의존 격리) |
| 3 | 절차 문서화 | `3_test_procedure_doc.md` | `docs/reference/test-procedure.md` 신설 + CLAUDE.md 참조표 1줄 |

## 공통 계약

1. **테스트 삭제는 이 spec 의 수단이 아니다** — 전환·이동·격리만 한다.
2. de-pin 판별 기준: `UnitStatImportDto`/`DcSheetImportDto` 가 덮어쓰는 필드는 리터럴 단언 금지 — 구조(존재·배선·enum)·상대 비교(보스>잡몹)·부호(>0) 단언만. 시트가 안 덮는 저작 계약(애니 이름, 프리팹 배선, 패턴 각도)의 리터럴은 **유지**한다.
3. 하드닝 패턴의 정본 = `WaveKillBudgetPinTests` (리터럴 pin 이 밸런싱 머지에서 깨진 사고 이력과 전환 근거가 헤더 주석에 있다).
4. 코어 lane 의 정의 = "실제 프로젝트 에셋을 로드하지 않는 EditMode 테스트". 시트·에셋·맵 편집으로 깨질 수 있는 것은 전부 Assets lane 으로.
5. `run_tests` 는 `assembly_names` 전체 실행 + `failures_so_far` 스캔 방식 유지 (lessons 01). 필터 0-match 원인 조사는 후속 후보.
6. PlayMode 판정은 에디터 실행으로 한다 (배치 금지 — docs/spec/README.md 기록 유지).

## 실행 매트릭스 (unit 3 문서화 내용의 초안)

| 상황 | 실행 | 예상 시간 |
|---|---|---|
| 코드 변경 루프 중 | 코어 lane (`assembly_names=["Wassup.Tests.EditMode"]`) | ~30초 |
| 시트 임포트·에셋·맵 편집 후 | + Assets lane (`Wassup.Tests.EditMode.Assets`) | +수 초 |
| 작업 단위 완료·커밋 전 | EditMode 두 lane + 관련 PlayMode 파일(에디터 Test Runner 수동 선택) | 분 단위 |
| spec 종료·머지 전 | PlayMode 전체 (에디터 실행) | 수 분 이상 |

## 결정 필요 (착수 전)

1. **`MultiGoalPoolSeparationTests` 의도적 빨강 4건** (Coil/Twin/Spiral/Zig 재저작 대기): `[Ignore("map-rework unit N 대기")]` 로 내려 신호를 복구할지, 재저작 임박이면 유지할지. 권고: Ignore + 백로그 — 빨강은 회귀만 의미해야 한다.
2. **`DreamcatcherCatalogSyncTests` 값 리터럴 12개**: 등록 동기 가드(본체)는 유지하고 값 pin 만 de-pin 이 기본안. 값 pin 이 "에셋=시트 스냅샷 일치" 게이트로 의도된 것이면 전환하지 않고 Assets lane 이동으로 충분.

## 후속 후보

- `run_tests` `test_names`/`group_names` 0-match 원인 조사 — 해결되면 PlayMode 선택 실행이 열린다.
- PlayMode 풀 Battle 씬 로드 103회 → 공유 부팅 픽스처 검토 (수 분 단축 여지, 테스트 격리가 깨질 위험이 있어 별도 spec).
- e2e 5개 `BattleBridgeTestAccess` 이관 (기존 백로그 — elite-enemy-tier).
- `[Category]` 도입 (어셈블리 입도가 부족해질 때, 필터 동작 검증 선행).
