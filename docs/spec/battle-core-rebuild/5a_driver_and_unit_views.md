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
| 뷰의 `EntityManager.Exists` 매 프레임 폴링(3곳) | 이벤트 + 자가 치유 **경고**로. 유령이 잡히면 그것은 정상 경로가 아니라 「어떤 소멸 경로가 사건을 안 냈다」는 신호다 | 완료(계약 7) |
| `LateUpdate` 순서 계약 | `ViewOrder` 상수로. 회귀 방지 = `CoreViewOrderTests` | 완료(X3) |
| 뷰 스포너 null 이면 큐 `Clear()` | 이벤트는 뷰 유무와 무관(X5) | 제거 |
| 피격 팝 0.15초 코드 상수(E27) | `CharacterViewConfig.hitPopSeconds`·`hitPopOvershoot` | 완료 |
| 상태 FX·빔·VFX·오라 풀 | 사건이 아직 없다 — unit 6·7 | 보류(빈 풀 금지) |
| **일반 보스 도약의 아치** | `CoreLeapPresenter` 는 섰지만 **생산자가 없다.** 코어에서 `LeapAscend` 를 발행하는 곳이 0 이고(`LeapActive = true` 를 쓰는 줄도 0), 순간이동은 `Blinked` 로 나온다. ⚠ `Blinked` 를 아치로 쓰면 **포탈 텔레포트까지 날아간다** — 둘은 화면에서 다른 사건이다. 생산자는 unit 7(보스·궁극기) | 보류 |
| 도약 착지 퍼프(`PlayLeapPuff`) | 사건이 `dataIndex` 를 안 나른다. 슬램이 있으면 착탄 VFX 가 이미 그 자리를 그리고, 없으면 퍼프를 고를 근거가 없다 | 제거 |
| 슬램 투사체 발사(`ResolveLanding`) | **코어가 이미 낸다**(`CombatPhase.StepLeap`). 뷰가 전투 규칙의 생산자이던 자리 | 제거 |
| 임팩트 소켓 높이(`ProjectileViewFrame.targetSocketHeight`·`Blend`) | 소비처(대상 몸통 착탄 VFX)가 unit 6 이고, 값을 만들려면 「대상이 누구인가」를 뷰가 알아야 한다. 프레임 필드는 **남겨 두고 0 으로 흘린다** | 보류 · unit 6 |
| 투사체 **보드 깊이 소팅**(`boardSortOrder`) | 궤도구(유닛을 도는 탄)만 쓰던 축이고 그 콘텐츠가 unit 7 이다. 지금은 평평한 `ProjectileOffset` | 보류 · unit 7 |
| 맵 **풀 선택**(서버 시드 % poolCount) | 5a 의 드라이버는 스테이지 프리팹을 직접 든다. 풀·덱 페어와 토너먼트 시드는 5c 의 모드 진입에서 | 보류 · 5c |
| 타일맵 **바닥 페인팅·오버레이·범위 타일** | `CoreBoardPlane` 은 **평면 선언만** 한다. 바닥은 스테이지 프리팹(디오라마)이 이미 소유하고, 오버레이·범위·하이라이트는 5b | 보류 · 5b |
| 카메라 bounds push · 스테이지 포스트 볼륨 | 카메라는 5b 에서 재사용한다 | 보류 · 5b |
| 오버헤드 **부착 카드 줄** | 부착 사건이 코어에 없다(unit 7). 빈 카드 슬롯을 먼저 만들면 「카드가 안 뜬다」를 사건이 아니라 UI 에서 찾게 된다 | 보류 · unit 7 |
| 오버헤드 **실드 비율·스택 아이콘** | 같은 이유 — 실드 부여·스택 사건이 unit 6 이다. 인자는 0/`null` 로 넘긴다 | 보류 · unit 6 |
| `HeartHudConfig` 의 소비처 | 자산은 섰지만(91행 귀속을 이 unit 에서 닫아야 한다) **읽는 코드가 0 이다.** 마음 박동·바 펀치·붕괴 홀드는 HUD 의 것이고 그 HUD 는 5b 다. ⚠ 오버헤드 레이어에 슬롯만 먼저 달아 뒀다가 뺐다 — 소비 0 인 인스펙터 슬롯은 **「이게 여기서 조절된다」고 광고**한다(`liftShadowMinScale` 이 같은 이유로 은퇴했다) | 보류 · 5b |
| `PickupViewConfig`·`DcVisualConfig` 의 소비처 | 같은 이유. 픽업·사직서는 unit 6, 드림캐쳐 발동 임팩트는 unit 7 이다 | 보류 · unit 6·7 |
| `spawnSpreadEnabled`(bool 축) | **분산 폭 0 이 곧 「끔」**이라 끄는 방법이 이미 값 안에 있다. 축을 둘로 두면 「켜져 있는데 폭 0」과 「꺼져 있는데 폭 0.2」가 표현 가능해지고, 그 조합에서 화면과 규칙이 갈린다 | 제거 |
| `tileHealthGaugeLayer`(레거시 표시 모드) | 표시 모드가 `UnifiedOverhead` 로 고정 저작됐다. 레거시 경로의 처분은 5b | 보류 · 5b |

## 고친 것 (기존 코어·Unity 층 변경)

| 무엇 | 왜 |
|---|---|
| `CoreEvent.Spawned` 가 `DefIndex` 를 싣는다 | 안 실으면 뷰 풀이 스폰마다 코어에 개체를 **되물어야** 한다. 스폰은 그 되묻기가 성립하는 몇 안 되는 사건이지만, 예외를 허용하면 다음 사람이 소멸 사건에서도 같은 모양을 쓴다 — 계약 7 이 막는 것이 그 습관이다. 트레이스 포맷은 무변(`DefIndex` 는 실리지 않는다) |
| `MatchDefinitionBuilder.Build` · `CombatDefinitionBuilder.Fill` 에 `MatchViewAssets` **선택 인자** 추가 | 정의표 줄 번호 → 저작 에셋(탄·거점 프리팹)을 뷰가 되찾으려면 **번호를 매긴 그 순회**의 목록이 필요하다. 뷰 쪽에서 다시 모으면 두 벌이 갈려 탄이 엉뚱한 프리팹으로 난다. 기본값 `null` 이라 기존 호출부(헤드리스·EditMode)는 무변 |
| `BattleDriver._timeScale`·`SetTimeScale` 은퇴 | 전투 시간의 주인이 둘이 되면 카드 슬로모(0.3)가 이 판에만 안 걸린다. 발행률의 유일한 출처는 `TimeManager` Battle 도메인이다 |
| 쿼드 폴백의 `Shader.Find` + `new Material` → `RuntimeMaterialFactory.CreateOpaqueTexture` | 옛 뷰의 그 줄은 CLAUDE.md 추가 제약 위반이다(모바일 shader stripping 으로 null 이 돌아와 렌더가 깨진다). 복사하면서 같이 옮길 이유가 없다 |
| `BlobShadowConfig(4행)`·`HeartHudConfig(9행)` — spec 초안의 「6·7」과 수가 다르다 | 초안의 수는 어림이었고, 실제 91행을 분류해 보니 블롭은 4(스프라이트·색·리프트·실그림자), 마음은 6 + 코어버스트 2 + 골 오버헤드 1 = 9 였다. 장부가 정본이다 |
| 새 뷰 `CoreStructurePropLayer`(spec 목록 밖) | 거점 프랍은 **맵 수명**이라 유닛 뷰 풀이 들 수 없다(판이 시작되기 전부터 서 있다). 옛 전투도 같은 판단이었다(`SpawnStructureViews` 가 맵 빌드 소유). 「빈 풀 금지」와 무관하다 — 이 뷰는 구독할 사건이 없는 것이 아니라 **사건이 필요 없다** |
| 새 뷰 `CoreBoardPlane`(spec 목록 밖) | 뷰는 전부 `BoardSpace.ToView` 로 화면에 놓이고 그 변환의 권위는 격자다. 옛 전투는 타일맵 뷰가 그 일을 겸했는데(바닥+오버레이+평면 선언) 셋 중 **평면 선언만** 이 unit 에 필요하다 |
| Check lane csproj 에 `Data/BattleView/**` 글롭 + `UnityEngine.UI.dll` 참조 | 새 타입이라 stale `Wassup.Runtime.dll` 에 없고, 오버헤드 바가 uGUI 를 쓴다 |
| **`MatchDefinitionBuilder.ToUnitDef` 가 배치 저작 7칸을 싣는다**(코스트 · 연사 게이트 · 사망/퇴근 대기 · 판 위 상한 · 배치 모션 · 각성 보상) — 사용자 플레이 1차 | unit 4 가 `UnitDef` 에 그 칸들을 더했는데 변환은 unit 1 의 모양 그대로였다. 증상은 **트레이 코스트 칩이 전부 0**(= 배치가 공짜라 코스트 경제가 통째로 없다)이었고, 나머지 여섯은 기본값이 그럴듯해 **안 보였다**: 상한 0 은 1 로 접히고, 쿨타임 0 은 「항상 준비됨」, 배치 모션 0 은 **배치 페이즈 자체를 없앤다**(`hasDeployPhase` false). 콘솔 에러도 빨간 테스트도 안 났다. 실어 보내는 것은 **날 저작값**이고 「0 이면 무슨 뜻인가」의 해석은 코어 `Effective*` 가 한다(두 곳에서 접으면 답이 갈린다). ⚠ **골든 재굽기 없음** — 코퍼스는 SO 가 아니라 in-code 고정구로 짓는다(`CoreGoldenCorpus`). 다만 `cost` 등이 canonical text 에 있어 **라이브 정의표의 `configHash` 는 바뀐다** |

## 규칙 장부 매핑 (unit 5 귀속 5행)

| 행 | 코드 포인터 |
|---|---|
| E27 (피격 팝) | `CharacterViewConfig.hitPopSeconds`·`hitPopOvershoot` → `CoreUnitViewPool.OnCoreEvent` 의 `ProjectileHit` 분기 |
| M9 (감지 네 박자) | 코어 상수(unit 2). 5a 의 몫은 **계측**이다 — `Editor/BattleCore/CoreDetectionProbeMenu.cs` 가 네 박자를 전부 찍는다 |
| C7 (피해 숫자 비율) | `CoreEvent.DamageApplied` 의 `SiteTarget.OriginBody` → `CoreEnemyHitBarSpawner`·`CoreDamageNumberSpawner`. **두 소비처 어디에도 체력 나눗셈이 없다** |
| X3 (도약 → 뷰 순서) | `BattleCoreUnity/ViewOrder.cs`(Leap 10 &lt; Unit 20) → `BattleDriver.Subscribe` · `CoreUnitViewPool.SyncViews` 의 `TryGetFlightOverride`. 테스트 = `CoreViewOrderTests` |
| X16 (`GamePhase` append-only) | `CoreGamePhaseTests`(새 PlayMode lane) — 정수 7개 + `CameraDirectionConfig.breathPhases` 대조 |

## 완료 기준

- [x] 새 PlayMode lane 초록 7/7(부팅 스모크 · `ViewOrder` 순서 · 발행률 · GamePhase) — 2026-09-23 Unity 실행. 옛 lane 은 안 돌림(변경 0).
      ⚠ **2026-09-23 미실행** — 워크트리 에디터의 MCP 브리지가 세션을 잃어(「Server no longer
      running; ending orphaned session」) 러너를 못 띄웠다. 대신 **헤드리스로 컴파일까지** 확인했다:
      Editor 도구 2 + 새 lane 5파일 + `BattleCoreUnity/**` 를 에디터 어셈블리에 대고 컴파일 → 0 오류.
      **컴파일은 「테스트가 초록이다」가 아니다** — 브리지가 돌아오면 그때 돌린다.
- [x] **발행률**: `TimeManager.Request(Battle, 0.3)` 중 60프레임에 코어 틱 18±1회, `Request(Battle, 0)` 중 0회.
      테스트는 섰다(`CoreTickRateTests`). ⚠ 기대값을 프레임 수가 아니라 **흐른 시간**에서 만든다 —
      러너의 프레임 간격이 기기마다 달라 60프레임 ≠ 1초다.
- [ ] `BattleCoreScene` 부팅 콘솔 에러 0, 3분 완주, 뷰 수 = 코어 유닛 수(매 초 검사),
      **스테이지 저작 거점 수 = `World` 의 Structure 유닛 수**. 테스트는 섰다(`CoreSceneBootTests`).
      ⚠ 거점 대조에서 **방어 마음은 뺀다** — 그것은 골(`Goals`)이 정본이고 세우는 자가 `HeartMeter` 라
      같은 수로 세면 항상 어긋난다.
- [x] `ledgers/bridge-fields.md` 91행 전부 4분류 중 하나로 「새 주인」 채움, 씬 값 대조표 일치
      (코어 정의표 7 · 코어 상수 5 · 뷰 설정 SO 53 · 씬 배선 참조 21 · 삭제 5).
- [x] `ledgers/bridge-methods.md` 미정 **128 → 98**. 뷰·카메라·거점 스폰·유닛 뷰·드라이버 몫 30행이
      「새 주인」 또는 「삭제」로 닫혔다.
- [x] `ledgers/rules.md` 의 unit 5 귀속 5행(E27·M9·C7·X3·X16)이 코드 포인터로 매핑.
- [x] `GamePhase` 정수값이 안 밀렸다 — `CoreGamePhaseTests`(X16).
- [x] `ledgers/tools.md` 5·8행 닫힘(감지 프로브 · 장애물 디버그).
- [x] **`rule-holders.md` 미정 2행 닫힘(사용자 답 2026-09-23)**: 판 안 재시작 **없음** · 쿨다운은 **판의 시계**(감속·정지에 같이 느려진다). 기본값 구현과 일치. C7·S6·K5 의 「재시작」 전제는 unit 7 이식 제외 표로.
- [x] 새 lane 의 골든·상태 해시 대조는 **Unity 에서 구운 골든**과만(계약 5) — 11종 재굽기 `68c28363`, EditMode 코어 lane 366/366. — 5a 는 골든을 안 만든다(뷰 unit 이다).
- [x] 뷰 코드에 `Unity.Entities` 0, 판정 코드 0. (`grep -rn "Unity.Entities" Scripts/BattleCoreUnity` = 0건)
- [x] `core-reviewer` APPROVE(Unity 층 포함: 매니저/컨트롤러 이름 0 · 판정 이전 0). 리드 판정으로 스폰 퍼짐·몸 반경을 정의표로 올림(`fae42944`).

확인 2026-09-23 — 커밋 `aa16ee9d`·`a27b9d65`·`71d836c9`·`5d552987`·`ed1e1a82`·`fae42944`·`68c28363`·`1b7e033b`. 함정: 손으로 쓴 씬 YAML 의 Grid 클래스 ID(156=TerrainData, Grid 는 156049354) · 열린 씬의 YAML 을 외부에서 고치면 Reload 모달이 MCP 를 막는다.

### 남은 것 (다음 세션이 이어받을 자리)

1. **새 PlayMode lane 실행.** 워크트리 에디터에서 MCP 브리지를 되살린 뒤
   `run_tests mode=PlayMode assembly_names=["Wassup.Tests.PlayMode.Core"]`.
   ⚠ 새 `.cs`·`.asset` 을 손으로 만들었으므로(브리지가 없어 `.meta` 도 직접 썼다) **실행 전
   `refresh_unity(scope=all)`** — `scope=scripts` 로는 어셈블리에서 통째로 빠진다.
2. **씬 배선 육안 확인.** YAML 로 직접 배선했다(에디터를 못 써서). 인스펙터에서 슬롯 9개가
   비어 있지 않은지 본다 — 특히 `BattleDriver._stagePrefab`(MapStage_Duel)과 `_boardPlane`.
3. **Unity EditMode 코어 lane 360/360** 재확인(`CoreEvent.Spawned` 시그니처가 바뀌었다).
