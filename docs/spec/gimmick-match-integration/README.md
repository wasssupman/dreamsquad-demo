# gimmick-match-integration

> 상태: 계획 — 승인 대기. 구현 착수 전. (2026-07-15, review 반영 rev1)

## 상위 목표

야근 기믹(`season-gimmick-overwork`)을 **매치 라이프사이클에 정식 편입**한다. 기믹을 시즌 결합에서 떼어내 매치 시작 시 랜덤 배정하고, 배치 페이즈에 안내 UI 로 알리고, 활성/비활성을 `BattleConfig` SO 로 게이팅한다.

검증 질문 세 개:

1. **"매치 시작 시 전체 기믹 목록에서 1개가 (결정론적으로) 배정되는가?"** — 같은 matchSeed → 같은 기믹.
2. **"배치 페이즈에서 좌상단 메뉴버튼을 가리지 않는 안내 카드로 배정된 기믹이 읽히는가?"**
3. **"BattleConfig 하나로 기믹 기능을 통째로 on/off 할 수 있는가?"** (off = 기존 클린 플레이 무변화)

## 배경 (현행 구조)

- 기믹 소스: `SeasonData.gimmick`(SeasonRegistry.defaultSeason=season_overwork). 랜덤·게이트 없음, 항상 Overwork.
- **BattleBridge 는 `season.gimmick`(또는 `SeasonRuntime.Active.gimmick`)을 3곳에서 읽는다** — ① `CreateGimmickConfigIfActive()`(~4164, StartBattle 경로), ② `BuildPickupSpawnState()`(~647, 맵 빌드 시점), ③ 디버그 로그(~3878). unit 1 이 **셋 다** `_assignedGimmick` 으로 스왑한다(하나라도 놓치면 컴파일 실패).
- 페이즈 흐름: `GamePhase { None, Draft, Gift, Placement, Battle, Result }`. `PlacementPhaseView.BeginPlacementPhase()` 가 배치 캔버스(상단 중앙 카운트다운 배너 + 우하단 START) 빌드.
- 좌상단 메뉴버튼: `ReturnToMenuButton`(order 1000) → 정지형 `MenuPopup`.
- 매치 시드: `GameManager` 소유(`MatchSeed`), `MatchSeed.Derive{Map,Wave,Visual,Pickup}Seed` 파생 패턴 + `bridge.SetMatchSeed` 주입. `GameManager` 는 `[DefaultExecutionOrder(-100)]`.
- 현존 기믹은 **Overwork 1개뿐**.

## feature-wide 계약

1. **기믹 소스 = `BattleConfig`** (시즌 아님). `BattleConfig{ bool gimmickEnabled; GimmickData[] gimmickPool }`. `SeasonData.gimmick` **제거** — 시즌은 맵 테마 전담. (이 spec 이 `season-gimmick-overwork` unit 2 의 시즌 바인딩을 대체.)
2. **배정 = 매치당 1회**, `GameManager.Start` 의 `EnsureMatchSeed` 직후, TestMode/squad/draft 분기 **이전**(→ 모든 진입 경로 공통, 모든 `PrepareDraftMap` 호출 이전). `gimmickEnabled && pool.Length>0` 이면 시드 파생으로 pool 에서 1개, 아니면 `null`(기믹 없음 = 기존 클린 플레이).
3. **배정 결과 노출/주입**: `GameManager.AssignedGimmick`(읽기 전용) + `bridge.SetAssignedGimmick(g)`. BattleBridge 의 **3개 소비 지점**(config 주입·픽업 스폰 게이트·디버그 로그)이 모두 `_assignedGimmick` 을 읽는다(시즌 안 읽음). ECS 게이트웨이 경계 유지 — 배정은 Mono 측 결정, BattleBridge 가 blittable 로 복사.
4. **결정론**: 같은 matchSeed → 같은 기믹. 순수 `GimmickSelection.PickIndex(count, seed)` + EditMode 테스트로 고정. `MatchSeed.DeriveGimmickSeed` 신설(기존 salt 패턴 미러). `_assignedGimmick` 은 두 소비 지점(맵 빌드 L647·StartBattle L4164)보다 먼저 세팅됨이 보장(계약 2).
5. **안내 UI = `GimmickGuideView`**(MonoBehaviour, 자체 캔버스). `GameManager.PhaseChanged` 구독 — `Placement` 진입 시 `AssignedGimmick`(displayName+description) 카드 표시, 다른 페이즈 전이 시 숨김. `AssignedGimmick==null` 이면 표시 안 함. enable 시 현재 페이즈에 동기(재시작/late-enable 대비).
6. **UI 위치**: 좌상단 메뉴버튼 회피 — 상단 중앙 카운트다운 배너 **아래**. sortingOrder < 1000(메뉴 버튼이 위에 남음), `raycastTarget=false`(배치 입력 비차단).
7. **하드코딩 금지 유지**: 기믹 수치는 concrete SO(OverworkGimmickData), 표시 텍스트도 SO(displayName/description). `BattleConfig` 는 순수 데이터 컨테이너 — **추후 시트 임포트 대상**.
8. **무회귀 커밋 규율**: 각 유닛은 독립 검증 가능, `main` 을 깨거나 기믹을 조용히 dormant 로 두지 않는다. → BattleConfig **에셋은 unit 0**, `GameManager.battleConfig` **배선은 unit 1**(스왑과 동시). 각 뷰의 씬 배선은 그 코드 유닛과 같이.
9. **범위**: 새 기믹 종류 신설 금지(제약 9). pool 은 현재 Overwork 1개 엔트리. 메커니즘만 세운다.

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_battle_config_and_gimmick_meta.md` | `BattleConfig` SO + **기본 에셋 생성** + `GimmickData.description` |
| 1 | 로직/배관 | `1_random_assignment_and_injection.md` | 시즌 분리(**3곳 스왑**) + 랜덤 배정 + **GM 배선** + BattleBridge 주입 + 결정론 테스트 |
| 2 | 신규 UI | `2_placement_guide_view.md` | `GimmickGuideView` 안내 카드 + **씬 배선** |
| 3 | 통합 검증 | `3_verify_matrix.md` | on/off·재시작 통합 Play 검증 매트릭스 |
| 4 | handoff | `4_handoff_summary.md` | 종료/인계 요약 (구현 후 작성) |

## 파이프라인 커버리지

N/A — 이 spec 은 데이터 SO(`BattleConfig`) + 매치 셋업 로직 + MonoBehaviour View(안내 UI)만 다룬다. 새 플레이 오브젝트(유닛/적/투사체/해저드/VFX)나 생성→렌더 경로 신설/변경 없음. 레드불 픽업 등 기믹의 플레이 오브젝트는 이미 `season-gimmick-overwork` 에서 파이프라인 등록 완료. `docs/reference/object-pipeline-map.md` 신규 대조 대상 아님.

## 후속 후보 (현 spec 범위 밖)

- 기믹 종류 확장(2번째 기믹) — 생길 때 `effect-trigger-unification`(파킹 문서) 착수 트리거.
- `BattleConfig` 시트 임포터(구글시트→SO). 지금은 수기 편집.
- 안내 카드 아이콘/일러스트, 등장 연출(현재는 정적 카드).
- 기믹 배정 결과 `BattleLogger`/토너먼트 리포트 기록.
- 배틀 중 상시 기믹 배지(placement 이후에도 요약 표시).
