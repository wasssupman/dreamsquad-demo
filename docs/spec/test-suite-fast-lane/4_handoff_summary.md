# 4 — handoff summary

## Commit

- `dd3642a8` feat(unit 0) — 에셋 검증 테스트를 별도 asmdef 로 분리
- `eff16758` test(unit 1) — 밸런스 리터럴 de-pin
- `0f1ca944` test(unit 2) — 상시 빨강 청산, 빨강 = 회귀 신호 복구
- (unit 3 + 이 문서) docs — 테스트 절차 정본 신설

## Implemented

- EditMode 를 **두 어셈블리**로 분리. `Wassup.Tests.EditMode`(코어 2,233개 26초)는 실제
  프로젝트 에셋을 로드하지 않아 시트·에셋·맵 편집에 구조적으로 면역이고,
  `Wassup.Tests.EditMode.Assets`(161개 5초)가 실에셋 저작 검증을 전담한다.
- 통이동 18파일 + 혼합 8파일 메서드 추출(신규 파일 8개). 합계 2,394 = 분리 전과 동일.
- `InternalsVisibleTo` 에 새 어셈블리 등록 (Scripts · Editor.UnitStatImport) — 누락 시
  `SetFrom`/`ResolveRunnerAnimation` 류 CS1061/CS0117 로 나타난다.
- 시트 DTO 가 덮는 필드의 리터럴 단언을 부호·배율·구조 단언으로 전환 (4파일).
  카드 44장 개수 pin → «비정형 카드 목록이 빈다» 직접 단언.
- MultiGoal «의도적 빨강» 4건 → `PendingMeleeRework` 목록 + **래칫 테스트**.
  → EditMode 두 lane 상시 빨강 0.
- PlayMode stale 3건 기대값 갱신, AuthE2E `[Explicit]` 격리. 실패 12 → 9.
- `docs/reference/test-procedure.md` 신설 (실행표·lane 판별·수치 단언 규율) + CLAUDE.md
  참조표 · TRD 4.5 정정.

## Key Files

- `Assets/_Project/Tests/EditModeAssets/Wassup.Tests.EditMode.Assets.asmdef` — lane 경계
- `Assets/_Project/Tests/EditModeAssets/WaveKillBudgetPinTests.cs` — de-pin 모범 사례(헤더 주석)
- `Assets/_Project/Tests/EditModeAssets/MultiGoalPoolSeparationTests.cs` — pending 목록 + 래칫
- `docs/reference/test-procedure.md` — 실행 절차 정본
- `docs/spec/README.md` «PlayMode 사전 실패» 절 — 남은 9건의 정본

## Verified

- 코어 lane 2,233개 / 실패 0 / 26초 · Assets lane 161개 / 실패 0 / 5초 (기계 검증)
- PlayMode 144개 2회 전체 실행 대조 (에디터 실행). 실패 9건 — 전부 분류 완료
- read_console 컴파일 에러 0

## Notes

- **되돌리지 말 것**: 코어 lane 에 `AssetDatabase.Load*`/`FindAssets` 를 들이면 lane 의
  존재 이유가 사라진다. 한 파일에 로직+실에셋이 섞이면 파일을 나눈다.
- `run_tests` 의 `test_names`/`group_names` 는 여전히 0-match. 어셈블리가 유일한 입도다.
- 시트가 **안** 덮는 저작 계약(패턴 각도·애니 이름·프리팹 배선·등급 공식 유도값)의
  리터럴은 의도적으로 유지했다. `DreamstoneCatalogTests` 는 오딧이 de-pin 후보로
  지목했으나 등급 공식(cap/4·0.8·0.6) 유도값이라 제외한 것 — 다시 건드리지 말 것.
- 래칫(`PendingMeleeRework_OnlyHoldsMapsThatStillFailTheContract`)이 빨개지면 버그가
  아니라 **맵 재저작 완료 신호**다. 해당 맵을 목록에서 빼서 choke 계약을 재무장한다.

## Follow-up

- **`DropDismountTest`** [M] · 7월엔 없던 신규 실패. 실행마다 증상이 다르다(cell occupied
  → InvalidCastException) — 배치/재배치 계열 최근 변경과 접점. 우선 조사 후보.
- **PrimeTween «OnComplete ignored» 2건** [M] · 에러 1개가 그때 돌던 테스트에 임의 귀속.
  gift-phase-removal 의 트윈 풀·teardown(`2e4aaf63`·`abf0115a`)과 시기 일치 — 그 spec 후속.
- **`PlacementAuraTest` 3건** [S] · 기대 1.0 vs 실측 1.012. +1.2% 가 Common 최하 티어
  드림스톤과 일치 — 프로필 스톤 교차 오염 가설. 격리 실행으로 검증부터.
- **`DragCancelZoneTest`** [S] · 트레이 기하 stale 인지 제품 버그인지 판별 필요.
- **`SceneTransitionSmokeTest`** [S] · 순서 의존(격리 통과). 스위트 순서 위생.
- **`BossLullabyLiveTest`** [S] · flaky(1회차 통과·2회차 실패). 계측 창 보강 후보.
- PlayMode 풀 Battle 씬 로드 103회 → 공유 부팅 픽스처 (수 분 단축 여지, 격리 위험으로 별도 spec)
- `run_tests` 이름 필터 0-match 원인 조사 — 해결되면 PlayMode 선택 실행이 열린다
