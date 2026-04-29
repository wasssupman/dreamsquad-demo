# SpawnHazard API

**작업 구분**: 4

## 목적

`EffectSpawner.SpawnHazard(em, HazardSO, originCell)` 단일 진입점. 모든 producer (debug/skill/place/equipment) 가 이 API 만 호출. 본 spec 의 *encapsulation 검증 질문* 답 핵심.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/HazardShapeSampler.cs` — 셀 계산 순수 함수

## EffectSpawner.SpawnHazard

```csharp
public static Entity SpawnHazard(EntityManager em, HazardSO so, int2 originCell)
{
    if (so == null) return Entity.Null;

    var e = em.CreateEntity();
    em.AddComponentData(e, new Hazard
    {
        remainingLife = so.lifetime,
    });

    var cellsBuffer = em.AddBuffer<HazardCellsBuffer>(e);
    var cells = HazardShapeSampler.Sample(so.shape, originCell, so.radius);
    for (int i = 0; i < cells.Count; i++)
        cellsBuffer.Add(new HazardCellsBuffer { cell = cells[i] });

    var effectsBuffer = em.AddBuffer<HazardEffectsBuffer>(e);
    if (so.effects != null)
        for (int i = 0; i < so.effects.Length; i++)
            effectsBuffer.Add(new HazardEffectsBuffer { effect = so.effects[i] });

    return e;
}
```

= 한 entity + 두 buffer 생성. visual 은 본 API 가 *몰음* (Unit 6 의 BattleBridge wrapper 가 처리).

## HazardShapeSampler

```csharp
public static class HazardShapeSampler
{
    public static System.Collections.Generic.List<int2> Sample(HazardShape shape, int2 origin, int radius)
    {
        switch (shape)
        {
            case HazardShape.SingleCell:
                return new System.Collections.Generic.List<int2>(1) { origin };
            case HazardShape.Square3x3:
                return Enumerate(origin, 1);
            case HazardShape.RadiusSquare:
                return Enumerate(origin, math.max(1, radius));
            default:
                return new System.Collections.Generic.List<int2>(0);
        }
    }

    private static System.Collections.Generic.List<int2> Enumerate(int2 origin, int r)
    {
        int side = 2 * r + 1;
        var list = new System.Collections.Generic.List<int2>(side * side);
        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
                list.Add(new int2(origin.x + dx, origin.y + dy));
        return list;
    }
}
```

= MVP 는 정사각 (Chebyshev) sampling. radius=1 → 3×3 (9), radius=2 → 5×5 (25). 4-neighbor circle / 임의 cell list 는 후속 후보.

## 미래 producer wiring 예시 (의사코드)

본 spec 의 *encapsulation 검증* 의도 명시:

```csharp
// 후속 spec 또는 통합 시점 — 미래 producer 들의 호출 패턴
// 1) 디펜더 on-place hazard 능력
public void OnDefenderPlaced(...) {
    if (defenderData.onPlaceHazardSO != null)
        battleBridge.SpawnHazardWithVisual(defenderData.onPlaceHazardSO, placedCell);
}

// 2) 스킬 카드
public void CastHazardSkill(HazardSO so, Vector2Int targetCell) {
    battleBridge.SpawnHazardWithVisual(so, targetCell.ToInt2());
}

// 3) 장비 효과 (적 사망 시 trigger)
public void OnEnemyDeath(...) {
    if (equippedItem.deathTriggerHazard != null)
        battleBridge.SpawnHazardWithVisual(equippedItem.deathTriggerHazard, deathCell);
}
```

= **모두 동일 `SpawnHazardWithVisual` API 호출**. 본 spec 의 디버그 메뉴 (Unit 7) 도 같은 진입점.

## Burst 호환

- `SpawnHazard` 는 main-thread 호출 (EntityManager 직접 접근 → Burst 비대상).
- `HazardShapeSampler.Sample` 은 List 사용 → Burst 비호환. 본 spec 에서는 main-thread 만 호출되므로 OK. Job 에서 호출 필요 시 NativeList 버전 추가.

## 단위 테스트 (EditMode)

`SpawnHazardApiTests`:
- shape=SingleCell → cellsBuffer.Length == 1
- shape=Square3x3 → cellsBuffer.Length == 9
- shape=RadiusSquare, radius=2 → cellsBuffer.Length == 25
- effects.Length == SO 의 effects 길이
- so == null → Entity.Null 반환, entity 미생성
- effects 미할당 (null SO 필드) → empty buffer

## 완료 기준

- 컴파일.
- 단위 테스트 통과.
- producer 가 SpawnHazard 호출 시 entity + 두 buffer 생성, HazardLifetimeSystem 이 정상 인지 + cellToEffects 갱신 확인.
- 콘솔 에러/경고 0.
