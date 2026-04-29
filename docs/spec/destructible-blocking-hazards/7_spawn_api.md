# Spawn API + Collision Rejection

**작업 구분**: 7

## 목적

`EffectSpawner.SpawnBlockingHazard` 단일 spawn 진입점 + `BattleBridge.SpawnBlockingHazardWithVisual` 매개 메서드. 멀티셀 cell 충돌 거부 정책 적용.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs` (SpawnBlockingHazard 추가)
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (SpawnBlockingHazardWithVisual + HazardDestroyedEventsSingleton lifecycle + visual map 갱신)

## 구현

### EffectSpawner.SpawnBlockingHazard

```csharp
public static Entity SpawnBlockingHazard(EntityManager em, BlockingHazardSO so, int2 originCell, int hazardSoIndex)
{
    if (so == null) return Entity.Null;

    // 0. FlowFieldSingleton 한 번 fetch (tileSize / gridSize / goal cell 공유)
    if (!em.CreateEntityQuery(typeof(FlowFieldSingleton)).TryGetSingleton<FlowFieldSingleton>(out var ff))
        return Entity.Null;

    // 1. 셀 샘플 (HazardShapeSampler 재사용). Square3x3 은 radius 무시 (sampler 내부 hardcoded) — 1 전달.
    //    BlockingHazardSO 에 radius 필드 추가는 후속 (Circle / Diamond shape 도입 시).
    var cells = HazardShapeSampler.Sample(so.shape, originCell, radius: 1);
    if (cells == null || cells.Count == 0) return Entity.Null;

    // 2. 충돌 거부 검증
    if (!ValidateCellsForBlockingHazard(em, cells, ff, out string reason))
    {
        UnityEngine.Debug.LogWarning($"[BlockingHazard] spawn rejected at {originCell}: {reason}");
        return Entity.Null;
    }

    // 3. center cell + worldPosition 계산 (tileSize 는 FlowFieldSingleton 에서)
    int2 centerCell = ComputeCenterCell(cells);
    float3 worldPos = GridMath.CellToWorldCenter(centerCell, ff.tileSize);

    // 4. entity 생성 + 컴포넌트 조합
    var entity = em.CreateEntity();
    em.AddComponentData(entity, new Obstacle { cell = centerCell, worldPosition = worldPos, remainingLife = float.PositiveInfinity });
    em.AddComponentData(entity, new BlockingHazard { hazardSoIndex = hazardSoIndex, maxHp = so.maxHp });
    var buf = em.AddBuffer<BlockingHazardCellsBuffer>(entity);
    foreach (var c in cells) buf.Add(new BlockingHazardCellsBuffer { cell = c });
    em.AddComponentData(entity, new Health { value = so.maxHp, max = so.maxHp });
    em.AddBuffer<IncomingDamage>(entity);
    em.AddComponentData(entity, new FactionTag { value = Faction.BlockingHazard });
    // HealthBar 는 별도 bar entity 모델 — Unit 8 의 BattleBridge.CreateHealthBar(hazardEntity, ...) 가 매핑 처리.
    // hazard entity 자체에는 HealthBarTag/HealthBarState 직접 부착 ❌.
    em.AddComponentData(entity, LocalTransform.FromPosition(worldPos));
    return entity;
}

private static bool ValidateCellsForBlockingHazard(EntityManager em, List<int2> cells, FlowFieldSingleton ff, out string reason)
{
    // (a) OOB 검증 — ff.gridSize 와 비교
    // (b) 골 cell 충돌 — ff.goalCell (또는 동등 필드) 와 비교
    // (c) 기존 blockedCells 충돌 — ObstacleSingleton.blockedCells.Contains
    // (d) DefenderTile.cell 충돌 — DefenderTile query 순회
    // 첫 충돌 시 reason 설정 후 false 반환.
    // path-zone hazard cell 과의 중첩은 검증 X (양립 허용).
    // 구현 상세는 코드에서.
}

private static int2 ComputeCenterCell(List<int2> cells)
{
    // 점유 cell 들의 산술 평균 (round). HP bar / LocalTransform anchor.
    int sx = 0, sy = 0;
    foreach (var c in cells) { sx += c.x; sy += c.y; }
    return new int2(sx / cells.Count, sy / cells.Count);
}
```

### BattleBridge.SpawnBlockingHazardWithVisual

```csharp
public Entity SpawnBlockingHazardWithVisual(BlockingHazardSO so, int2 originCell)
{
    int idx = RegisterHazardSO(so);  // 내부 List<BlockingHazardSO> 인덱스. **idempotent — 같은 SO 재등록 시 기존 index 반환** (Dictionary<BlockingHazardSO,int> 룩업). null SO → -1.
    var entity = EffectSpawner.SpawnBlockingHazard(_em, so, originCell, idx);
    if (entity == Entity.Null) return entity;

    // Visual 동기 spawn
    if (so.visualPrefab != null)
    {
        float3 worldPos = _em.GetComponentData<LocalTransform>(entity).Position;
        var visual = Instantiate(so.visualPrefab, worldPos, Quaternion.identity, _hazardVisualRoot);
        var presenter = visual.GetComponent<BlockingHazardPresenter>();
        presenter?.Bind(entity);   // Unit 8 에서 정의 (entity 만 — bridge 참조 불요)
        _hazardVisualMap[entity] = visual;
    }
    return entity;
}
```

### HazardDestroyedEventsSingleton lifecycle

`Awake` / Start (BattleBridge):
```csharp
var em = World.DefaultGameObjectInjectionWorld.EntityManager;
var hazardSinkEntity = em.CreateSingleton(new HazardDestroyedEventsSingleton
{
    queue = new NativeQueue<HazardDestroyedEvent>(Allocator.Persistent)
});
```

`OnDestroy`: queue.Dispose + entity destroy. (`DefenderDeathEventsSingleton` 패턴 동일.)

### 핵심 결정

- **Spawn 거부 시 `Entity.Null` 반환 + 경고 로그** — 호출자 (디버그 메뉴 / 미래 producer) 가 명확히 감지. 부분 spawn / clamp / 셀 swap 안 함 (silent 위험).
- **path-zone hazard 와 cell 중첩은 허용** — zone 효과는 통과형, blocking 은 차단형 — 의미상 동시 존재 가능. `blockedCells` (Effects/Obstacle) 와 `cellToEffects` (Effects/Hazard) 가 별개 상태라 자연 양립.
- **center cell** = 점유 cell 산술 평균. `LocalTransform.Position` + HP bar anchor 의 단일 진실.

## 단위 테스트 (EditMode)

`SpawnBlockingHazardTests`:
- 정상 spawn → entity + 8개 컴포넌트 부착 확인.
- OOB cell → Entity.Null + 경고.
- 골 cell 중첩 → Entity.Null.
- 기존 blockedCells 중첩 → Entity.Null.
- DefenderTile 중첩 → Entity.Null.
- center cell 계산 검증 (Square3x3 에서 origin 입력 시 origin 자체가 center).

## 완료 기준

- 컴파일 + Burst 활성 (EffectSpawner.SpawnBlockingHazard 자체는 ECB structural change 로 비-Burst 일 수 있음 — `CcApplySystem` 패턴 참조).
- EditMode 신규 spawn 테스트 통과 + 기존 회귀 0.
- HazardDestroyedEventsSingleton 의 NativeQueue 가 Editor 종료 시 leak 0.
- 본 unit 단독 검증: BattleBridge 의 디버그 헬퍼 (Unit 9) 또는 코드에서 직접 호출 후 Editor entity inspector 로 확인.
- 콘솔 에러/경고 0 (의도된 거부 경고 제외).

검증: 2026-04-29 — `EffectSpawner.SpawnBlockingHazard`, `BattleBridge.SpawnBlockingHazardWithVisual`, SO registry, visual map 구현. `SpawnBlockingHazardTests` 5/5 통과(TestResults.xml 기준). 커밋 미작성.
