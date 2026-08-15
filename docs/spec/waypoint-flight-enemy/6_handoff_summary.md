# waypoint-routing — 구현 인계

## Commit

- `1535f7c4` — unit 0 저작 축
- `737b5807` — unit 1 슬롯 축 2차원화
- `e49833fd` — unit 2 순서 관리 순수 함수
- `5862f67d` — unit 3 웨이포인트 추종 활성화
- `ede9fc73` — unit 7 스웜별 경로 가이드
- `258c96ec` — unit 4 Air 통행·비주얼·타겟층과 검증 콘텐츠
- `c7db37b0` — unit 4 완료 상태·검증·handoff 마감

## Implemented

- 맵의 N개 웨이포인트 경로를 적 SO의 `waypointPathIndex`로 선택한다.
- 이동은 기존 flow field를 재사용하고, 웨이포인트 순서만 `WaypointFollow`가 소유한다.
- `PlacementLayer.Air`는 모든 타일에 열리며 지상 장애물 오버레이를 무시한다.
- Air 적의 분리 이동과 어그로 추격도 자기 통행층 NavGrid를 읽는다.
- `flightLift`는 sim 위치를 바꾸지 않고 기존 뷰 lift 파이프라인만 사용한다.
- 기존 방어유닛은 Path 전용, 대공사수는 Path와 Air를 모두 공격한다.
- 타겟층은 직접 공격뿐 아니라 투사체·재조준·튕김·스윕·광역·장판까지 전달된다.
- Skimmer는 단일 타겟·피해 10·0.2초 공격 주기, 대공사수는 피해 7·0.2초 공격 주기다.
- 스폰 가이드는 같은 웨이브에서도 `스웜 × 실제 레인`별로 각 경로를 예고한다.

## Key Files

- `docs/spec/waypoint-flight-enemy/README.md`
- `Assets/_Project/Scripts/Battle/Movement/WaypointFollow.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- `Assets/_Project/Scripts/Data/PlacementLayer.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/EnemyAiStateSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Data/Enemies/Enemy_Skimmer.asset`
- `Assets/_Project/Data/Defenders/Defender_AntiAir.asset`

## Verified

- unit 4 초기 전체 EditMode: 2,150건 통과, 기존 Ignore 3건.
- 타겟층 집중 검증: 최초 14/14, 최종 리뷰 회귀 포함 18/18 통과.
- WaypointLab PlayMode 최종 2/2 통과: 순서 통과·차단 대조·lift·런타임 타겟층 베이크.
- 사용자 Play 확인: 경로 가이드, Air 차단 무시, lift·체력바, 일반 방어 회피, 대공사수의 지상·공중 공격, Skimmer 단일 고속 공격.
- 최종 Unity 콘솔 에러 0, 관련 diff check 통과.

## Notes

- `BattleBridge`가 Mono↔ECS의 유일한 창구인 경계는 유지했다.
- Combat/Effects는 Movement 소유 `PathFollowState.traversalLayers`를 읽기만 한다.
- 새 시스템·이벤트 큐·인터페이스는 추가하지 않았다.
- 타겟층 `0`은 적 공격·플레이어 스킬·통행층 없는 구조물의 레거시 무필터 값이다.
- `EnemyAiStateSystem`도 AttackSystem과 같은 층 마스크를 읽는다. 빠뜨리면 Path 전용 순찰병이 Air 적만 보고 `Engaging`으로 멈춘다.
- `HazardEffect.targetTraversalLayers`는 복사본에만 쓰는 `[NonSerialized]` 런타임 스냅샷이다.
- Air 우선 타겟팅은 없다. 대공사수의 Path·Air 후보는 기존 거리순으로 동등하게 경쟁한다.
- ⚠ **rev 3 계약 «모든 방어유닛이 비행을 때린다(대공 축 없음)»는 구현에서 사용자 승인으로 반전됐다** — 기존 방어는 Path 전용 폴백이 됐고 고도 타겟층 축이 생겼다. 이전 문서·세션 메모리와 충돌하면 **이쪽(README rev 4 계약 7)이 정본**이다.
- ⚠ **Skimmer 는 아직 라이브에 안 나온다** — `EnemyCatalog` + `Deck_WaypointLab`(dev 슬롯) 뿐, 라이브 덱 7종 미편입. 반면 **대공사수는 DefenderCatalog 라이브 노출**이라 unit 5 전까지 Air 가치가 잠자는 비대칭 상태다(Path|Air 라 죽은 픽은 아님). 라이브 편입은 unit 5 에서 **웨이브 재추첨 규칙**(structure-hunter unit 1: 시드 재기준·풀 중간 삽입·7종 열거 정본 = `WaveKillBudgetPinTests`·`maxPerWave`)과 함께 처리한다.

## Resume (unit 5 구현 후 갱신, 2026-08-11)

1. **unit 5 구현 완료** — 페인터 경로 브러시(`Tool.Waypoint` + 전용 브러시 바 + 오버레이 + `WaypointAuthoringRules` 그대로 호출) · Serpent/Zig Air 경로 저작 · **Skimmer 라이브 편입**(`Deck_Serpent` 시드→20260821 · `Deck_Zig` →20260825, `minWaveNumber 8`·`maxPerWave 2`).
2. 검증: `WaypointPathBakeTests` 3건(교체/null 보존/빈 배열 삭제) · 페인터 왕복(라이브 사본 리플렉션) PASS · Serpent 라이브 계측 — 경로 순서 통과 위반 0·데코 통과 5,295프레임·done 후 골 전환 · EditMode 2,171/실패 0 · 두 덱 100웨이브 상한·게이트 위반 0.
3. **잔여 = 사용자 Play 체감 확인 1건** — Serpent/Zig 에서 웨이브 8+ Skimmer 등장, «다른 자리에 방어(대공사수)를 세우게 되는가».
4. ⚠ 하네스 함정 2건이 `5_painter_and_maps.md` 하단에 있다 — `Bake` 의 dev 슬롯 자동 등록(스크래치 잔여 참조) · `PendingSpawnEntry.deckIndex`→`laneIndex` 개명.

## 사용자 보고 조사 (2026-08-12) — «비행이 마음을 안 패고 바로 누수»

**비행의 공성은 정상이다.** 재현 2회: 마음이 살아 있으면 Engaging → 타워 1000→0 직접 파괴까지 확인.
증상의 원인은 **기존 규칙 2개의 합성**이다(비행 고유 아님):

1. 적의 골 목적지는 맵 빌드 때 고정 — **마음의 생사를 모른다**(최근접 골로 간다).
2. **부서진 골 도달 = 즉시 소멸 + 유출 카운트**(stress-after-breach, `canSiege && breached`).

→ 한 마음이 부서진 멀티골 맵에서, 부서진 쪽이 최근접인 적은 **살아있는 마음을 놔두고** 부서진
골에서 소멸한다. 통제 실험으로 확정: (7,4)만 붕괴시키자 스키머가 교전 0 프레임으로 (7,4)에서
소멸했고 그동안 (7,6)은 HP 709 로 생존. Serpent 는 두 마음이 2칸 거리라 «살아있는 바 옆에서
사라지는» 장면이 된다. 지상 적도 동일하나 오는 길에 죽어 관측이 드물고, **비행은 무저항 완주로
이 장면을 100% 노출**한다. `canSiege=false`(공격 수단 상실) 경로는 코드·실측 양쪽에서 배제됨.

**사용자 결정 B (2026-08-12): 규칙은 의도로 유지, 시인성만 보강.** 근거 — 진행 중인 맵 개편으로
폭1 단방향(멀티골 협곡) 맵 자체가 은퇴 예정이라 라우팅 재설계에 투자하지 않는다.
구현: 붕괴한 골 프랍을 **그을린 틴트 + 60% 주저앉음**으로 전환(`TilemapMapView.MarkGoalCollapsed`,
호출 = `OpenGoalCellAfterBreach` 단일 지점, 아트 0). 스크린샷 검증 — 2칸 거리에서 생존 골과 즉시 구분.

## rev 5 (units 8~10, 2026-08-13) — 레인이 경로를 갖는다

### Commit

- `66de3de1` unit 8 저작 축 · `3f3d464e` unit 9 켜기 · `c9659895` 이동 레퍼런스 · `835428c3` unit 10 Serpent 저작

### 무엇이 바뀌었나

**경로 선택 축이 둘이 됐다.** 기존은 적 SO 하나뿐이라 「이 종은 늘 옆길」만 됐고 「이 레인은 저 복도」가 안 됐다. 이제 `MapDocument.spawnRoutes` 가 스폰별 기본 경로를 갖고, 겹치면 **좁은 쪽(적 SO)이 이긴다**(`WaypointRouting.ResolvePathIndex`). rev 4 의 미결 D3 이 여기서 닫혔고, `map-rework` D2 가 미룬 「배정 규칙」이 이것이다.

**도달 판정이 셀 일치 → 체비셰프 1 이내로 완화됐다.** 정확 일치는 판당 2기(Skimmer)에서만 맞는 계약이었다 — 스웜은 축분리 스윕이 한 칸 수렴을 막아 스친 개체가 되돌아온다.

### 되돌리면 안 되는 의도

1. **우선순위는 순수 함수가 소유한다.** 스폰 지점에서 삼항으로 풀면 계약이 코드에만 남아 EditMode 로 고정할 곳이 없다.
2. **가드는 래퍼(`SpawnUnit`)에 남기고 본문(`CreateEnemyEntity`)은 plain int 만 받는다.** 그 파일의 ★규약 그대로다 — 분열 호출처는 레인이 없어 기본값 `-1`.
3. **`spawnRoutes` 는 「미생성 이거나 정확히 `spawns` 길이」다.** 공성 파생이 `spawns` 를 교체할 때 함께 버리는 이유가 이것이고, 안 하면 다른 레인의 경로를 조용히 읽는다.
4. **도달 반경 `1` 은 저작 값이 아니다.** 8이웃 격자에서 인접의 정의다. 노출하면 순서 관리가 맵 저작을 알게 되어 계약 1 이 깨진다.
5. **경로 0 은 Skimmer 의 Air 저작이다.** 지상 레인 기본이 가져가면 두 콘텐츠가 한 저작을 공유한다 — Serpent 이 경로 1 을 신설한 이유.
6. **맵마다 한 레인은 `-1` 로 남긴다.** 대조가 없으면 「다른 길」이 아니라 「그냥 먼 길」이 된다.

### 함정

- **인접한 두 웨이포인트는 뒤엣것이 자동 통과된다**(도달 반경의 귀결). `ValidatePaths` 가 경고한다. 스폰 바로 옆 첫 지점도 같은 이유로 무의미하다.
- **Hook 은 이 feature 를 받을 수 없다** — Walk 34칸·분기 셀 1개의 순수 단일 경로다. 저작해도 원래 가던 길을 간다. 착수 계획이 「Hook 먼저」였다가 타일 디코드로 뒤집혔다(unit 10 참조).
- **폭1 복도에서는 경로가 바뀌지 않는다.** Serpent 도 합류 전 복도는 못 바꾸고 광장 진입면만 갈랐다. 이 feature 의 값어치는 지형의 분기 밀도에 비례한다.

### 잔여

- **Serpent Play 체감 확인(사용자)** — 위 레인이 광장 북쪽, 아래 레인이 서쪽으로 들어오는가. 방어를 두 면에 나눠 세우게 되는가
- **나머지 4맵 저작** — `map-rework` units 8~12 종료 후. 그쪽 4맵은 EditMode 에서 폭 계약 실패 중이며 **이 spec 과 무관**하다
- **PlayMode 레인별 통과 순서 실측** — 계약 6(순수 함수 그린은 증거가 아니다)의 잔여분

---

## Follow-up

- **라이브 지상 경로 적** — 지상 2경로 맵을 라이브에 내려면 경로 따르는 지상 적의 이름·스탯·실루엣 저작이 선행(의도적 범위 축소, unit 5 문서 참조). `Enemy_WaypointBasic/Alt` 는 dev 전용.
- 나머지 맵 경로 저작, 대공사수 고유 아트·최종 밸런스, Air 우선/추가 피해는 README 후속 후보다.
- 비방향 defender Entity/Cell 투사체 패턴이 실제 콘텐츠에 들어올 때 emitter 후보에도 타겟층 필터를 추가한다.

---

## unit 11 (2026-08-15) — 예고선의 레인 기본 경로 해석

- **증상**: 레인 경로 맵(Coil·Zig)에서 가이드는 최단거리, 유닛은 웨이포인트. 원인 = `BuildSpawnGuideForecasts` 가 SO 축만 실음(unit 9 켜기의 누락분). 상세는 [11](11_guide_route_resolution.md).
- **수정**: 생성기에 `laneRoutes` 옵셔널 주입 + `WaypointRouting.ResolvePathIndex` 재사용(규칙은 계속 한 곳). `QueueWave` 가 `RouteForSpawn` 배열 전달.
- **검증**: Coil 재현 빨강 → 초록, EditMode 14/14, Duel 무회귀.
- ⚠ **`ValidationWave_ShowsGuides_ThenPassesWaypointsInAuthoredOrder` 는 unit 5(34c1603f) 이후 계속 빨강** — Skimmer `minWaveNumber 8` 이 WaypointLab 첫 웨이브 Air 전제를 깼다. unit 11 무관(스태시 대조 확인). 랩 전용 Air 검증 유닛 신설이 후보.
