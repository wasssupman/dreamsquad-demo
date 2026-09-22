# 5 — 드라이버 · 뷰 · 입력 · HUD (조각 B · 첫 플레이)

## 목적

**카드 없이 판이 돈다.** 새 씬 `BattleCoreScene` 에서 전투 코어를 틱으로 돌리고, 적이 걷고, 유닛을 드래그로 놓고, 쏘고, 죽고, 점수·타이머·코스트가 HUD 에 보인다. 옛 씬·옛 브리지는 무변. 이 unit 이 끝나면 **사용자 플레이 1차**가 검증이다. 뷰 계층의 원칙은 계약 12(통합 뷰 없음 — 풀마다 자기 구독)와 계약 7(이벤트는 값 스냅샷, `SimEntityId` 키).

## 변경 대상

| 항목 | 경로 |
|---|---|
| 드라이버 | `BattleCoreUnity/BattleDriver.cs`(unit 1 확장): 누산기 · 틱 발행률(`SetTimeScale`·`Pause` — `TimeManager` Battle 도메인 리스를 **읽어** 발행률로 번역, 코어 dt 불변) · 틱 뒤 이벤트 방출 · 읽기 모델 노출 · `MatchDefinitionBuilder` 호출 진입(모드 선택 3단) |
| 뷰 풀(신설, `BattleCoreUnity/View/`) | 옛 `Presentation/` 을 **복사·적응**(키 `Entity`→`SimEntityId`, `EntityManager` 폴링→이벤트 구독 + `IsAlive` 자가 치유): `CoreUnitViewPool`(← `SpineUnitPool`·`QuadUnitViewPool`, 백엔드 선택 `TrySpawn` 한 곳 유지) · `CoreProjectileViewPool` · `CoreDamageNumberSpawner` · `CoreEnemyHitBarSpawner` · `CoreStatusFxSpawner` · `CoreUnitOverheadUiLayer` · `CoreBeamPresenter` · `CoreVfxSpawner` · 도약 연출(`CoreLeapPresenter` — 유닛 동기 **앞** 구독 순서). `UnitView` 3백엔드(`SpineUnitView`·`SpriteUnitView`·`QuadUnitView`)는 **키 타입만 교체한 복사본**(옛 것은 unit 9 까지 잔존). `BoardSpace` 는 그대로 사용(sim-Y 무시) |
| 뷰 설정 SO(신설, `Data/BattleView/`) | 브리지 직렬화 필드 91 의 새 주인: `UnitLiftKnobs`(lift 4 + `spineDefenderYOffset` + `spawnHeight`) · `BlobShadowConfig`(6) · `CharacterViewConfig`(`tilemapCharacterScale`·`BillboardTilt`·prop tilt 3·`healthDisplayStyle`·`walkAnimSpeedStyle`·`unitHealthPresentationMode`·`enemyDragDim*`) · `HeartHudConfig`(heart 7 + `coreBurst*` + `goalOverheadHeight`) · `LeapVisualConfig`(보스 10 + 궁극기 5) · `PickupViewConfig`(5 + 사직서 2) · `DcVisualConfig`(`dcProcImpactMinIntervalSec`). **값은 옛 씬 블록에서 그대로 복사**하고 `ledgers/bridge-fields.md` 의 「새 주인」·「씬 값」 열을 채운다. 코어로 가는 값(`agentRadiusTiles`·`spawnSpread*`·`tileSize`·`deck`·`mapPool`·`bonusWaveData`·`seasonRegistry`·`stackModifierAuthoring`·`defenderPool`)은 `MatchDefinitionBuilder` 입력 — SO 가 아니라 정의표 |
| 입력 | `BattleCoreUnity/Input/`: `DragPlacementInput`(← `DefenderDragPlacementController` 의 드래그·스냅·프리뷰 — **판정은 없음**, 커맨드 `PlaceDefender` → receipt 로 성공/복귀) · `RetireInput` · `SubmitInput`. 이름에 Controller 금지(계약 12). `PlacementCellSnap`·`PlacementPointerOffset`·`PlacementSnapDebounce` 는 순수라 재사용 |
| HUD | `ScoreHudView`·`CostDisplay`·`DefenderSelector`(트레이)·`PlacementPhaseView`·`MenuPopup` 을 **읽기 모델 구독형 복사본**(`Core*`)으로. 정본 값은 `ScoreLedger`·`MatchClock`·`CostLedger`·`PlacementService` 읽기 모델 |
| 씬 | `BattleCoreScene.unity`: `BattleDriver` 컴포넌트 + 뷰 풀 7 + HUD 캔버스 + 카메라(`CameraDirector` 재사용 — 상태별 레시피 무변) + `TilemapMapView` 오버레이 복사본(`CoreMapOverlay` — 격자·사거리 링·배치 가이드, 판정 호출은 `AttackReach.InReach` 코어 버전) |
| 테스트 | **새 PlayMode lane** `Assets/_Project/Tests/PlayModeCore/Wassup.Tests.PlayMode.Core.asmdef`(refs BattleCore·Runtime·TestRunner — Entities 0): 새 씬 부팅 스모크(콘솔 에러 0 · 3분 완주 · 뷰 수 = 코어 유닛 수) · 뷰 구독 순서 테스트(도약 드레인 → 유닛 동기) · 드래그 배치 e2e(커맨드 → receipt → 뷰 스폰). 옛 PlayMode lane 은 무변 |
| 문서 | `test-procedure.md` 에 4번째 lane 행(unit 9 에서 옛 lane 행 제거) |

## 구현

1. **틱 발행률.** `acc += Time.deltaTime × rate; while (acc ≥ 1/60) match.Tick()`. `rate` = `TimeManager.ScaleOf(Battle)`(카드 잡는 동안 0.3, 메뉴 0). 코어는 배율을 모른다(계약 5). 종료 후 드라이버는 틱을 멈추고 결과 화면 진입만(여운은 뷰 소관 — X1).
2. **이벤트 → 뷰.** `BattleDriver` 가 틱 뒤 `EventBus` 방출을 C# 이벤트로 재방출, 각 풀이 자기 종류만 구독. 구독 순서가 계약인 곳: `CoreLeapPresenter`(도약 시작/착지) → `CoreUnitViewPool.Sync` → 오라/오버헤드(뷰 좌표 갱신 뒤). 보간: 풀은 틱 사이 `alpha` 로 위치 보간(코어 읽기 모델의 이전/현재 위치).
3. **소멸.** 뷰 풀은 `UnitDestroyed`/`ProjectileDestroyed` 로 회수. 자가 치유: 매 초 1회 `IsAlive(id)` 로 유령 뷰 검출 시 **경고 로그 + 회수**(이벤트 누락의 신호이지 정상 경로가 아님 — 계약 7).
4. **입력 → 커맨드.** 드래그 종료 = `PlaceDefender{defIndex, anchorCell, facing}` → receipt 거절이면 트레이로 복귀 애니 + 사유 표시(`RejectReason` ↔ 트레이 표현 「소진 > 쿨타임 > 코스트」 순서 동일). 프리뷰 고스트 색은 `PlacementService.Preview(cell, def)` 읽기 전용 질의(재판정 없음 — 같은 함수).
5. **HUD.** 점수·타이머·코스트·트레이·배치 페이즈 카운트다운은 읽기 모델 폴링(프레임) + 사건은 이벤트(킬 버스트·코스트 변화). 마음 스트레스 바(`HeartStressPulse` 재사용) 는 `HeartMeter` 읽기 모델.
6. **카메라·맵 비주얼.** 스테이지 프리팹 인스턴스가 바닥(무변). `CameraDirector` 는 페이즈 이벤트 구독(`GamePhase` 값 append-only, X16). `MarkerPropInstaller` 재사용.
7. **모드 선택 3단.** `BattleDriver.Begin(ModeSelection)`: 테스트 모드 강제 > 로비 지정 > 기본 모드 SO. 로비 UI 는 범위 밖 — dev 토글로 새 씬 진입.
8. **이식 제외 표 필수.** 카드·기믹·상태 FX 의 일부는 조각 C·D 뒤에 켜진다 — 이 unit 에서 「아직 안 보이는 것」을 표로.

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 뷰의 `EntityManager.Exists` 매 프레임 폴링(3곳) | 이벤트 + 자가 치유 경고로 | 보류(계약 7) |
| `_defenderByTile` 등 브리지 등록부 11 | 코어 담당자 소유(`PlacementService` 등). 뷰는 `SimEntityId → 뷰` 사전만 | 필수 규칙의 소유 이전 |
| `LateUpdate` 순서 계약 | 구독 순서로 | 보류(X3) |
| 뷰 스포너 null 이면 큐 `Clear()` | 이벤트는 뷰 유무와 무관(X5) | 제거 |
| `Controller` 이름 3종 | `*Input` 으로 | 계약 12 |

## 완료 기준

- [ ] 새 PlayMode lane 초록(부팅 스모크 · 구독 순서 · 드래그 e2e). 옛 lane 기준선 무변(59 실패 그대로).
- [ ] `ledgers/bridge-fields.md` 91행 전부 「새 주인」 채움(코어 정의표 / 뷰 설정 SO / 삭제), 씬 값 대조표 일치.
- [ ] `BattleCoreScene` 부팅 콘솔 에러 0, 3분 완주, 뷰 수 = 코어 유닛 수(매 초 검사).
- [ ] 뷰 코드에 `Unity.Entities` 0, 판정 코드 0(`AttackReach.InReach` 호출만 — 제약 13).
- [ ] **사용자 플레이 1차**: 새 씬에서 배치·전투·점수·종료가 「지금 게임과 같은 게임」인가. 다른 점은 이식 제외 표와 대조.
- [ ] `core-reviewer` APPROVE(Unity 층 포함: 매니저/컨트롤러 이름 0 · 판정 이전 0).
