# 5a — 드라이버 · 뷰 설정 · 유닛/투사체 뷰 (조각 B · 1/3)

> 조각 B 는 원래 unit 5 하나였다. 복사·적응 대상이 실측 10,055줄(드래그 배치 2,144 · 맵 뷰 1,608 · 트레이 1,281 · HUD 1,137 · Spine 뷰 970 …)이라 한 커밋이 될 수 없어 **5a·5b·5c** 로 나눴다(2026-09-23 리뷰). 셋이 끝나야 「카드 없이 판이 돈다 → 사용자 플레이 1차」다. 번호 체계는 그대로다(unit 6~10 참조 무변).

## 목적

새 씬 `BattleCoreScene` 에서 전투 코어가 틱으로 돌고, 적·방어유닛·투사체가 **보인다**. 입력·HUD 는 5b, 판 종료·사운드는 5c. 뷰 계층의 원칙은 계약 12(통합 뷰 없음 — 풀마다 자기 구독)와 계약 7(이벤트는 값 스냅샷, `SimEntityId` 키).

## 변경 대상

| 항목 | 경로 |
|---|---|
| 드라이버 | `BattleCoreUnity/BattleDriver.cs`(unit 1 확장): 누산기 · 틱 발행률 — **`TimeManager` Battle 도메인 리스를 읽어 발행률로 번역**(코어 dt 불변, 지금은 주석뿐이고 참조 0) · 틱 뒤 이벤트 방출 · 읽기 모델 노출 · `MatchDefinitionBuilder` 호출 진입. ⚠ **스테이지 프리팹의 거점 목록을 `Build(…, structures:)` 로 반드시 넘긴다** — 안 넘기면 마음 타워·본능이 스탯 없이 서거나 안 서고, 콘솔 에러 0 으로 조용히 실패한다(`55688ef5`) |
| 뷰 방출 순서 | `BattleCoreUnity/ViewOrder.cs`: 풀별 순서 **상수**(도약 연출 → 유닛 동기 → 오버헤드/오라). 드라이버가 이 값으로 정렬해 방출한다 — C# 이벤트 등록 순서(= 씬 컴포넌트 순서)에 기대지 않는다. 코어 `EventOrder` 의 뷰 쪽 짝 |
| 뷰 풀(신설, `BattleCoreUnity/View/`) | 옛 `Presentation/` 을 **복사·적응**(키 `Entity`→`SimEntityId`, `EntityManager` 폴링→이벤트 구독 + `IsAlive` 자가 치유): `CoreUnitViewPool`(← `SpineUnitPool`·`QuadUnitViewPool`, 백엔드 선택 `TrySpawn` 한 곳 유지) · `CoreProjectileViewPool` · `CoreDamageNumberSpawner` · `CoreEnemyHitBarSpawner` · `CoreUnitOverheadUiLayer` · `CoreLeapPresenter`(유닛 동기 **앞**). `UnitView` 3백엔드(`SpineUnitView`·`SpriteUnitView`·`QuadUnitView`)는 **키 타입만 교체한 복사본**(옛 것은 unit 9 까지 잔존). `BoardSpace` 는 그대로(sim-Y 무시). ⚠ **`CoreStatusFxSpawner`·`CoreBeamPresenter`·`CoreVfxSpawner`·`CoreDcAuraVisualPool` 은 여기서 만들지 않는다** — 구독할 사건(CC·DoT·부착·실드 부여)이 코어에 아직 없다(`CoreEventKind` 33종 중 `ShieldBroken` 뿐). unit 6·7 이 그 사건을 열 때 함께 만든다. 빈 풀은 「뷰 수 = 유닛 수」 검사를 통과하는 껍데기다 |
| 뷰 설정 SO(신설, `Data/BattleView/`) | 브리지 직렬화 필드 91 의 새 주인: `UnitLiftKnobs`(lift 4 + `spineDefenderYOffset` + `spawnHeight`) · `BlobShadowConfig`(6) · `CharacterViewConfig`(`tilemapCharacterScale`·`BillboardTilt`·prop tilt 3·`healthDisplayStyle`·`walkAnimSpeedStyle`·`unitHealthPresentationMode`·`enemyDragDim*`) · `HeartHudConfig`(heart 7 + `coreBurst*` + `goalOverheadHeight`) · `LeapVisualConfig`(보스 10 + 궁극기 5) · `PickupViewConfig`(5 + 사직서 2) · `DcVisualConfig`(`dcProcImpactMinIntervalSec`). **값은 옛 씬 블록에서 그대로 복사.** 코어로 가는 값(`agentRadiusTiles`·`spawnSpread*`·`tileSize`·`deck`·`mapPool`·`bonusWaveData`·`seasonRegistry`·`stackModifierAuthoring`·`defenderPool`)은 `MatchDefinitionBuilder` 입력. **분류는 4개다**: 코어 정의표 / 뷰 설정 SO / 씬 배선 참조(프리팹·컴포넌트 슬롯) / 삭제. SO 7 + 정의표 9 로는 약 66행이고 나머지 ~25행(`fixedMapSeed`·`tileSet`·`bonusPortalPrefab`·`retireFlight`·`_bossWarning` …)을 「배선 참조」로 둘 때는 **그것이 값이 아님을 한 줄로 밝힌다** — `fixedMapSeed`·`tileSet` 은 배선이 아니라 콘텐츠·규칙 값이다 |
| 디버그 도구 | `Editor/BattleCore/`: `CoreDetectionProbeMenu`·`CoreObstacleDebugMenu` 재작성(코어 읽기 모델 + Debug 커맨드). `ledgers/tools.md` 의 「unit 2 뒤」 행 2개(5·8)를 여기서 닫는다 — 감지 반경 계측기가 없으면 「재현이 먼저다」가 그 영역에서 집행 불가 |
| 테스트 | **새 PlayMode lane** `Assets/_Project/Tests/PlayModeCore/Wassup.Tests.PlayMode.Core.asmdef`(refs BattleCore·Runtime·TestRunner — Entities 0): 새 씬 부팅 스모크(콘솔 에러 0 · 3분 완주 · 뷰 수 = 코어 유닛 수) · 뷰 순서 테스트(`ViewOrder` 로 정렬됨을 씬 순서를 뒤집어 확인) · 틱 발행률 테스트. 옛 PlayMode lane 은 무변 |
| 문서 | `test-procedure.md` 에 4번째 lane 행(unit 9 에서 옛 lane 행 제거) |

## 구현

1. **틱 발행률.** `acc += Time.unscaledDeltaTime × rate; while (acc ≥ 1/60) match.Tick()`. `rate` = `TimeManager.ScaleOf(Battle)`(카드 잡는 동안 0.3, 메뉴 0). 코어는 배율을 모른다(계약 5). 종료 후 드라이버는 틱을 멈춘다(결과 화면 진입은 5c).
2. **이벤트 → 뷰.** 드라이버가 틱 뒤 `EventBus` 방출을 `ViewOrder` 순으로 재방출, 각 풀이 자기 종류만 구독. 보간: 풀은 틱 사이 `alpha` 로 위치 보간(읽기 모델의 이전/현재 위치).
3. **소멸.** 뷰 풀은 `UnitDestroyed`/`ProjectileDespawned` 로 회수. 자가 치유: 매 초 1회 `IsAlive(id)` 로 유령 뷰 검출 시 **경고 로그 + 회수**(이벤트 누락의 신호이지 정상 경로가 아님 — 계약 7).
4. **피해 숫자의 비율(C7, 필수).** 화면 숫자의 체력 비율은 **그 프레임의 최종값**이고 실드가 일부만 막으면 관통분 비례다 — `DamageApplied` 페이로드가 나르고 뷰는 계산하지 않는다.

## 파이프라인 커버리지

`object-pipeline-map.md` 의 유닛·투사체 아키타입 표를 이 unit 이 연다(전면 재작성은 unit 8).

| 정거장 | 유닛 | 투사체 |
|---|---|---|
| 저작 | SO(무변) | SO(무변) |
| 정의표 | `MatchDefinitionBuilder` → `UnitDef`/`EnemyDef` | `ProjectileDef` |
| 생성 | `BattleWorld.Spawn*` → `UnitSpawned` | `TickProjectilePhase` → `ProjectileSpawned` |
| 뷰 생성 | `CoreUnitViewPool.TrySpawn`(백엔드 선택 한 곳) | `CoreProjectileViewPool` |
| 매 프레임 | 읽기 모델 보간 · lift/그림자 = `UnitLiftKnobs`/`BlobShadowConfig` | 위치 보간 |
| 소멸 | `UnitDestroyed` → 회수 | `ProjectileDespawned` → 회수 |
| 상태 FX·오라·빔 | N/A — 사건이 unit 6·7 에서 열린다 | N/A |

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 뷰의 `EntityManager.Exists` 매 프레임 폴링(3곳) | 이벤트 + 자가 치유 경고로 | 보류(계약 7) |
| `LateUpdate` 순서 계약 | `ViewOrder` 상수로 | 보류(X3) |
| 뷰 스포너 null 이면 큐 `Clear()` | 이벤트는 뷰 유무와 무관(X5) | 제거 |
| 피격 팝 0.15초 코드 상수(E27) | `CharacterViewConfig` 노브로 — 제약 6 | 보류 → 이 unit 에서 SO 로 |
| 상태 FX·빔·VFX·오라 풀 | 사건이 아직 없다 — unit 6·7 | 보류(빈 풀 금지) |

## 완료 기준

- [ ] 새 PlayMode lane 초록(부팅 스모크 · `ViewOrder` 순서 · 발행률). 옛 lane 기준선 무변(59 실패 그대로).
- [ ] **발행률**: `TimeManager.Request(Battle, 0.3)` 중 60프레임에 코어 틱 18±1회, `Request(Battle, 0)` 중 0회.
- [ ] `BattleCoreScene` 부팅 콘솔 에러 0, 3분 완주, 뷰 수 = 코어 유닛 수(매 초 검사), **스테이지 저작 거점 수 = `World` 의 Structure 유닛 수**.
- [ ] `ledgers/bridge-fields.md` 91행 전부 4분류 중 하나로 「새 주인」 채움, 씬 값 대조표 일치.
- [ ] `ledgers/bridge-methods.md` 미정 128 중 **뷰·카메라·거점 스폰·유닛 뷰 호출 몫이 「새 주인」 또는 「삭제」** 로 닫힘. 종료 시 잔량을 README 상태 라인에 숫자로 적는다.
- [ ] `ledgers/rules.md` 의 unit 5 귀속 5행(E27·M9·C7·X3·X16)이 코드 포인터로 매핑.
- [ ] `GamePhase` 정수값이 안 밀렸다(`CameraDirectionConfig.breathPhases` 대조 1행, X16).
- [ ] `ledgers/tools.md` 5·8행 닫힘(감지 프로브 · 장애물 디버그).
- [x] **`rule-holders.md` 미정 2행 닫힘(사용자 답 2026-09-23)**: 판 안 재시작 **없음** · 쿨다운은 **판의 시계**(감속·정지에 같이 느려진다). 기본값 구현과 일치. C7·S6·K5 의 「재시작」 전제는 unit 7 이식 제외 표로.
- [ ] 새 lane 의 골든·상태 해시 대조는 **Unity 에서 구운 골든**과만(계약 5).
- [ ] 뷰 코드에 `Unity.Entities` 0, 판정 코드 0.
- [ ] `core-reviewer` APPROVE(Unity 층 포함: 매니저/컨트롤러 이름 0 · 판정 이전 0).
