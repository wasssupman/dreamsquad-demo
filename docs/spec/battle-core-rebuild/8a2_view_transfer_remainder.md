> 상태: 구현 완료 2026-09-25(`f7fb71693`~`41b57d06d`, 행 9 포함) · core-reviewer APPROVE(2026-09-25) · 플레이 4차 대기

# 8a2 — 뷰 이전 잔여: 장부가 「새 주인」이라 적었지만 실체가 없던 9행 (조각 E · 8c 뒤 · 9 앞)

> 8c 의 `--owners` 대조(2026-09-25)가 찾아낸 것이다. `rule-holders`·`bridge-methods` 가 새 주인을 배정했는데 새 씬에는 호출처가 0 인 **옛 플레이어 가시 기능**. 8a 의 「미정 0 은 배정의 끝이지 실현의 끝이 아니다」 가 다시 확인된 사례라 8c 안에서 이식하지 않고 이 unit 으로 뗀다. **unit 9 가 옛 코드를 지우기 전에 끝나야 한다.** 리드 판단(2026-09-25): 1~6 은 「기획 그대로」 이식, 7 은 새 트레이스에 같은 사건이 있으면 흡수.

## 목적

옛 판에서 플레이어가 보던 것 7 + 진단 채널 1 을 새 씬에 실체로 만든다. 규칙은 이미 코어에서 돈다 — 옮기는 것은 **그리기·흐리기·느리게 하기·트레이스**뿐이다. 끝나면 `check_ledgers.py --owners` exit 0.

## 변경 대상 (정본 표는 `8c_…md` 「미실현 뷰 7건」 — 옛 `파일:줄` 과 새 자리는 거기서 읽는다)

| # | 옛 기능 | 옛 근거 | 새 층의 닿을 자리 |
|---|---|---|---|
| 1 | 효과 타일 칸 표시(어느 칸이 효과 타일인지 판 위에) | rule-holders T15 · `BattleBridge.cs`(효과 타일 그리기) | 정의표의 효과 타일 칸 → `CoreMapOverlay`(하이라이트와 같은 층) — 소비·회복 사건이 있으면 구독 |
| 2 | 궁극기 착지 예고 칸 | T16·T17 · `BattleBridge.UltimateLeap.cs:87·110 ShowLandingTelegraph` | 이탈 사건(`CoreEvent` 의 `areaTiles: TelegraphTileRange`)을 `CoreMapOverlay` 착탄 예고 표식(6c)으로 — 아치 비행(8a)은 그대로 |
| 3 | 마음 붕괴 연출 + 슬로모 | `BattleBridge.cs PlayCoreBurst`·`DrainGoalCollapsedEvents`·`coreBurstTimeScale` | `HeartCollapsed`(30) 구독 뷰 신설 + 슬로모 = **틱 발행률**(`BattleDriver` 의 정지 리스와 같은 축 — `Time.timeScale` 금지). 슬로모 배율·길이는 옛 저작 값을 SO 로 |
| 4 | 배치 드래그 중 적 흐리게 | `BattleBridge.cs SetEnemiesDimmed` | `DragPlacementInput` 드래그 시작/끝 → `CoreUnitViewPool` 적 전원 `SetDimmed`(추상 메서드 이미 있음 `CoreUnitView.cs:49`) |
| 5 | 적 체력 틴트 | `BattleBridge.cs EvaluateEnemyHealthTint` | 피해 사건 뒤 `SetHealthTint`(`CoreUnitView.cs:47`) — 산식은 옛 것 그대로(순수 함수로, 값은 SO) |
| 6 | 소환사 유지 애니메이션 | `BattleBridge.cs SyncSummonerAnimationState` | AI 상태 읽기 창 → `SetAiState`(`CoreUnitView.cs:59`) — 지속 루프/상실 원샷 이름은 저작 SO |
| 7 | 방어유닛 AI 전이 트레이스 | `BattleBridge.cs TraceDefenderAiTransition` | 새 트레이스(60종)에 AI 전이 사건이 **있으면** 장부만 정정(흡수). 없으면 코어 사건 하나(값 스냅샷: id·이전·이후·틱) + 트레이스 정거장 |
| 8 | 배치 사거리 **칸 채움**(링과 함께 「어느 칸이 사거리 안인가」를 칠한다 · 링 있으면 투명) | rule-holders T3·T13 · `SetPlacementRange`·`RangeFillAlpha`·`IsPlacementRangeCell` | `CoreMapOverlay.PaintRange` 옆에 칸 채움 층 + 자리 고스트가 사거리 칸을 비켜 가는 read seam. ⚠ T13 비고 「되돌리면 채움이 두 겹」 — 링과 채움을 한 곳이 그린다. **리드 기본값 = 이식**(옛 플레이어 가시 · 은퇴 결정 없음) · 사용자가 은퇴를 택하면 이 행만 「삭제」로 닫는다 |
| 9 | 월드 마음 스트레스 틴트·심박(마음 프랍이 스트레스만큼 붉어지고 박동에 맞춰 뛴다) | `BattleBridge.SyncGoalOverheadGauges:9659-9689` → `GoalMarker.SetStressTint` | `CoreScoreHud.PaintHeart`(바와 같은 위상) — **리드 추가(2026-09-25)**: 장부가 `HeartMeter` 로 해석돼 `--owners` 가 못 잡은 플레이어 가시 옛 기능 |

## 구현

1. 행마다 옛 코드를 먼저 읽고 **값**(알파·틴트 커브·슬로모 배율·예고 길이)을 SO 로 옮긴다 — 코어·뷰에 리터럴 금지(제약 6). 옛 값 = 옛 씬 브리지 블록(bridge-fields 머리말과 같은 방식).
2. 뷰는 사건 값 스냅샷과 읽기 창만 쓴다. 이벤트로 상태를 되묻지 않는다(계약 4). 드라이버·풀에 규칙을 넣지 않는다.
3. 슬로모(3)는 8a 의 정지 리스와 같은 진입점을 쓴다 — 리스 둘이 겹치면 더 느린 쪽이 이긴다(옛 `TimeManager.Request` 결합 규칙 인용).
4. 행마다 PlayMode 코어 테스트 1(사건 → 뷰 호출 증언) · 순수 함수는 EditMode. 골든 무변(뷰만).
5. 끝나면 `rule-holders`·`bridge-methods` 의 해당 행 「실현 위치」를 심볼로 채우고 `--owners` exit 0.

## 이식 제외

없음 — 7행 전부 옛 기능이고 은퇴 결정이 없다(있으면 옛 spec 인용 필수, 함정 5).

## 고친 것 (2026-09-25 구현)

| # | 옛 근거 | 새 자리 | 옮긴 값(SO/프리팹) |
|---|---|---|---|
| 1 | `BattleBridge.AddEffectTile:9075` → `TilemapMapView.SetEffectTile:1025` · 정렬 `:1072`(−15) | `CoreMapOverlay.PaintEffectTilesOnce`(판마다 1회 · `PlacementService.ArmedEffectTiles`) · 그림 `MatchViewAssets.EffectTile` · `BoardSortOrder.EffectTileOrder` | 테마 `effectTiles[].overlayTile` · `effectTileMaterial`(정의표 줄과 같은 순회) |
| 1′ | `BattleBridge.Awake:685-690` `SeasonRuntime.Bind`(옛 씬 등록부 배선 `BattleScene.unity:4659`) | `BattleDriver._seasonRegistry` → 판 짓기 전 `SeasonRuntime.Bind` · 씬 배선 `6a5f580bf` | **발견(플레이 4차 전에 잡은 결함)**: 새 씬에 묶는 자가 없어 로비 → 새 씬 판에서 효과 타일 0칸이었다(6b `LiveDefinitionSmokeTests` 는 등록부를 직접 읽어 못 잡았다) |
| 2 | `BattleBridge.UltimateLeap.cs:87 ShowLandingTelegraph` · 끄기 `:117-120` · 링 `TilemapMapView.SetTelegraphRing:690` · 색 `:40` | `CoreMapOverlay.ShowLandingTelegraph`(전용 채널 — 드래그·카드 채널에 양보 안 함) ← `CoreLeapPresenter`(이탈 → 링 · 강하 → 내림) · 코어 `LeapAscend.AreaTiles`(슬램 반경 값 스냅샷 — 트레이스 무관) | `LeapVisualConfig.landingTelegraphColor` = 옛 씬 `BattleScene.unity:588` (1, .45, .08, .42) — 알파 = 채움, 선 불투명 |
| 3 | `BattleBridge.PlayCoreBurst:7308` · `DrainGoalCollapsedEvents:9596` · 슬로모 `HoldThenShowResult:7282` | `CoreVfxSpawner.OnHeartCollapsed`(골 칸 붕괴 원샷 + `GoalMarker.MarkCollapsed`) · 슬로모 = 5c `CoreMatchOutcomePresenter` 도메인 리스(이미 실현) | 슬롯 `_goalCollapsePrefab`(옛 씬 `:4454`) · `_goalCollapseScale` 1.2(`:4455`) · `HeartHudConfig.CoreBurst*` 1.25초·0.3(`:4738-4739`) |
| 4 | `BattleBridge.SetEnemiesDimmed:79` · 페이드 `:3108-3110` · 적용 `:3866-3881` · 켜기 `DefenderDragPlacementController.BeginDrag:387` · 끄기 `CleanupSession:2107` | `DragPlacementInput`(드래그 승격에 켜고 `EndDrag` 에 끔) → `CoreUnitViewPool.SetEnemiesDimmed` · 페이드·적 전원 `SetDimmed` | `CharacterViewConfig.EnemyDragDim*` 0.3·8(`:4682-4683`, 5a 이관분) |
| 5 | `BattleBridge.EvaluateEnemyHealthTint:4084` · 호출 `:3863` | 순수 `CoreEnemyHealthTint.Resolve` ← `CoreUnitViewPool.SyncViews`(흐림 뒤에 틴트) | `CharacterViewConfig.healthDisplayStyle` · `healthPresentationMode` |
| 6 | `BattleBridge.SyncSummonerAnimationState:4108` | `CoreUnitViewPool.SyncViews` — 소환 정책 Spine 뷰에 매 프레임 `SetAiState`(읽기 창 `Unit.Ai.Defender`) | `SummonPatrolAbility.activeAnimation`·`lostAnimation` |
| 7 | `BattleBridge.TraceDefenderAiTransition:4127` · 옛 채널 22 | 새 트레이스 60종에 같은 사건이 **없어** 흡수 불가 → `CoreEvent.DefenderAiChanged`(64 · id·이후 `Arg`·이전 `Amount`·유닛 줄) ← `CombatPhase`(변할 때만) · `CoreTraceChannel.DefenderAiChanged`(61) | 골든 하네스는 **구독하지 않는다**(`GimmickTriggered` 형 진단 채널) — 골든 무변 |
| 8 | `TilemapMapView.SetPlacementRange:1226` · `RangeFillAlpha:1185` · `ApplyRingTint:1140` · `IsPlacementRangeCell:1537` | `CoreMapOverlay.PaintRangeFill`(칸 집합 = `AttackReach.InReach` · 표준 잡몹 몸 · 앵커 칸 제외) · `CoreMapOverlay.IsPlacementRangeCell` · 링 안 채움 한 겹 | `TileSetData.rangeFillAlphaUnderRing` · 채움 RGB = 링 선 `_ringColor` |
| 9 | `BattleBridge.SyncGoalOverheadGauges:9659-9689`(단계·bpm·위상 누적·`BeatScale(beat, heartBeatDepth)` → `SetStressTint(stress01, beatScale)`) · 마커 사상 `:1161-1169` | `CoreScoreHud.PaintMarkers` — 심박 계산 주체 하나(바와 같은 `_phase`, 옛 `:9662`) · 마커 사상 `CoreGoalMarkers.Collect`(붕괴 연출과 공유) · 장부 `bridge-methods` 278 첫 심볼 = `GoalMarker.SetStressTint`(기계가 소비자를 검사) · 값 흐름 = **읽기 창**(옛도 매 프레임 `Health` 폴링 — 사건 아님, 새 쪽은 `HeartMeter.Stress` 매 프레임) | `HeartHudConfig.BeatDepth` 0.5(옛 `heartBeatDepth` · `BattleScene.unity:4734`) · 씬 `CoreScoreHud._heartHud` |

### 옛 규칙과 다른 점 · 알아둘 것

- **행 5 — 라이브에선 틴트가 안 보인다(옛 게임과 같다).** 옛 규칙(`BattleBridge.cs:3863` `unifiedOverhead ? Color.white : EvaluateEnemyHealthTint(entity)`)이 「통합 머리 위 모드면 흰색」이고 라이브 저작(`CharacterViewConfig.asset`)이 그 모드다. 완료 기준의 「적 체력에 따라 틴트」는 레거시 모드에서만 보인다. 옛 판단은 「모드 + 머리 위 레이어가 있다」였고 새 판단은 모드만 본다(레이어 누락 폴백 없음).
- **행 7** — 옛 트레이스는 첫 관측(초기 상태)도 한 줄 적었다. 새 사건은 기본값 `Ready` 에서 **바뀔 때만** 난다. 배치 중(`Deploying`) 유닛은 공격 루프를 건너뛰어 그 상태로 가는 전이가 없다(코어 기존 성질).
- **행 8** — 새 오버레이의 링 선 색(`_ringColor`, 시안)이 옛 `rangeColor`(라임)와 이미 다르다(5b). 「선과 채움은 같은 색」 불변식을 지키려고 채움 RGB 를 선 색에서 가져왔다. 링 색 자체는 이 unit 에서 안 바꿨다.
- **행 3** — 마음 타워는 체력 저수지를 공유하므로(X29) 무너질 때 골 칸 전부에 연출이 난다. 옛 판은 이번 프레임에 무너진 칸에만 냈다(한 골 맵에선 같다).
- **행 9(리드 추가)** — 월드 마음 틴트의 bpm 은 새 HUD 가 이미 쓰는 52·168(옛 씬과 같은 값, `CoreScoreHud` 직렬화)을 공유한다. 바 밝기의 깊이(`_beatDepth` 0.35 × 스트레스)는 5b 의 결정이라 그대로 두고, 마커 깊이만 옛 `heartBeatDepth` 0.5 다.

## 파이프라인 커버리지

판 위 오브젝트 3종의 정거장. 가장 가까운 아키타입 = `object-pipeline-map.md` 「효과 타일」·「도약」·「거점」 표(같은 열). 코어 변경은 행 2 의 사건 값 하나(`LeapAscend.AreaTiles`)뿐이다.

| 정거장 | 효과 타일 칸(행 1) | 착지 예고 링(행 2) | 마음 붕괴 VFX(행 3) |
|---|---|---|---|
| 저작 SO | 테마 `effectTiles[].overlayTile` · `effectTileMaterial` | `LeapVisualConfig.landingTelegraphColor` | 슬롯 `_goalCollapsePrefab` · `_goalCollapseScale` |
| 정의표 행 | `BoardEffectDefinitionBuilder.FillEffectTiles`(기존) — 그림은 같은 순회로 `MatchViewAssets.EffectTile` | N/A — 반경은 정의표가 아니라 사건 값(슬램 칸 수) | N/A — 골 칸은 기존 `Map.Goals` 를 읽는다 |
| 코어 스폰 · 사건 | N/A — 사건 없음. 판 시작에 뽑힌 `PlacementService.ArmedEffectTiles`(판 내내 불변)를 읽는다 | `LeapAscend`(18, `AreaTiles` 값 스냅샷) 에 걸고 `LeapDescend`(19) 에 내린다 | `HeartCollapsed`(30) |
| 뷰 풀 | `CoreMapOverlay.PaintEffectTilesOnce`(판마다 1회) | `CoreMapOverlay.ShowLandingTelegraph` ← `CoreLeapPresenter` | `CoreVfxSpawner.OnHeartCollapsed` + `GoalMarker.MarkCollapsed` |
| 뷰 순서 | `BoardSortOrder.EffectTileOrder`(−15) | 오버레이 전용 채널(드래그·카드 채널에 양보 안 함) | N/A — 원샷(순서 경합 없음) |
| 소멸 회수 | N/A — 판 내내 유지, 다음 판에 다시 칠한다 | 그 도약자의 강하 | N/A — 원샷 자체 소멸 · 마커는 판 끝까지 무너진 상태 |
| 씬 배선 | `CoreMapOverlay` · `BattleDriver._seasonRegistry`(행 1′ `6a5f580bf`) | `CoreLeapPresenter` · `CoreMapOverlay` | `CoreVfxSpawner` 슬롯 |

## 완료 기준

- [x] `check_ledgers.py --owners` exit 0(8c 가 보류한 줄이 닫힌다). — bridge-methods 심볼 285 · 삭제 82 · rule-holders 심볼 119 · 삭제 14 · 미실현 0 · 기본·`--retire-assets` 도 exit 0 (2026-09-25 `41b57d06d`)
- [ ] 새 씬 Play: 효과 타일 칸이 보인다 · 궁극기 이탈 뒤 착지 칸 예고가 보인다 · 마음 붕괴 시 연출 + 슬로모 · 드래그 중 적이 흐려진다 · 적 체력에 따라 틴트 · 소환사 유지 루프. — **보류(플레이 4차)**: 9행 중 8행을 PlayMode 로(행 7 은 EditMode) 사건/입력 → 뷰 호출을 증언했다. 육안 확인은 하지 않았다. ⚠ 틴트는 라이브 모드(통합 머리 위)에서 옛 판처럼 흰색이다 — 위 「옛 규칙과 다른 점」
- [x] PlayMode 코어 +7(또는 +8) 초록 · EditMode 선행 2 외 빨강 0 · 골든 무변 · 헤드리스 3종 · Retire.Check 0. — PlayMode 코어 **93/93**(85 + 8 — 행 7 은 EditMode 코어 2건이 증언) · EditMode 코어+Assets **1011/1013**(선행 2 `boomerang`·`bomb_man` · 신규 6) · 골든 파일 diff 0 · 옛 부분집합 38/38 · CLI 63/63 · 헤드리스 export 0 · 687 · 0 · Retire.Check 0 (2026-09-25 `41b57d06d`)
- [x] `core-reviewer` **APPROVE**(2026-09-25 — 행 1~8 finding 0 · 행 9 부록 finding 0) → [ ] 8c 의 머지 게이트(플레이 4차)에 합류.

### 검증 중 발견 — 옛 lane 잔류는 도메인 리로드까지 간다

PlayMode 코어 lane 직후 옛 `BonusWavePullTest` 3건(보너스 적 스폰 0)이 **단독 재실행 두 번에도** 빨갰다. 8a2 파일 전부를 `a4e0180d1` 로 되돌려 재컴파일하자 13/13, HEAD 로 복원해 재컴파일하자 다시 13/13 이었다. 원인은 8a2 가 아니라 **코어 lane 이 남긴 잔류이고, 그 잔류는 도메인 리로드 전까지 살아 있다**(함정 20 의 「단독 재실행으로 판별」은 부족하다). 옛 부분집합은 **리로드 직후, 코어 lane 앞에서** 돌린다.

리드 재검증 2026-09-25 — HEAD `03982fae7` 클린 export: build 0 · test 687/687 · Check 0 · `check_ledgers.py` 기본·`--owners`(미실현 0)·`--retire-assets`(총계 590) 전부 통과 · `--retire-prune` 618 항목 뒤 `Retire.Check` 오류 0 · 골든 diff 0 · 씬 diff = 배선 6줄 · 옛 폴더 무변. Unity: 옛 씬 부분집합 38/38(리로드 직후·코어 lane 앞) · EditMode 코어+Assets 1011/1013(선행 2) · PlayMode 코어 93/93 · CLI 63/63. 남은 것 = 새 씬 육안(플레이 4차).
