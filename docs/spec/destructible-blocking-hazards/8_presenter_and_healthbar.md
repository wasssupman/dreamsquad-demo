# BlockingHazardPresenter + HealthBar Attach + Destruction VFX

**작업 구분**: 8

## 목적

차단형 hazard 의 visual 계층 — `BlockingHazardPresenter` MonoBehaviour, HP bar 시각 (`HealthBarState` 인프라 재사용), destruction VFX 트리거 (HazardDestroyedEvents drain).

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/BlockingHazardPresenter.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (HazardDestroyedEvents drain loop)
- Modify: `Assets/_Project/Prefabs/Hazards/BlockingHazard_Placeholder.prefab` (BlockingHazardPresenter 부착, HealthBar prefab 자식 추가)

## 구현

### BlockingHazardPresenter.cs

```csharp
using Unity.Entities;
using UnityEngine;

namespace Wassup.Battle.Effects
{
    // Self-managed visual lifetime helper. ECS hazard entity 와 직접 의존 X — BattleBridge 가 매개.
    // path-zone-hazards 의 HazardPresenter / HazardVisualLifetime 패턴.
    public class BlockingHazardPresenter : MonoBehaviour
    {
        [SerializeField] private Transform healthBarAnchor; // HP bar 부착 자식 (선택)

        public Entity Entity { get; private set; }

        // BattleBridge 가 spawn 직후 호출 — bridge 참조 불요 (drain 은 BattleBridge 단방향).
        public void Bind(Entity entity)
        {
            Entity = entity;
            // HealthBar prefab 은 BattleBridge 가 자식으로 instantiate (HealthBarState 인프라 따라감) — 본 unit 에서 통합.
        }

        // BattleBridge 의 drain 이 호출 — destruction VFX 트리거 후 visual GameObject destroy
        public void OnDestroyed(GameObject vfxPrefab)
        {
            if (vfxPrefab != null)
                Instantiate(vfxPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
```

### BattleBridge HazardDestroyedEvents drain

기존 `DefenderDeathEvents` drain 패턴 그대로 (`Update` 또는 별도 polling 메서드). Unit 7 에서 추가한 `_hazardVisualMap : Dictionary<Entity, GameObject>` 와 `RegisterHazardSO` 의 `_hazardSoRegistry : List<BlockingHazardSO>` 활용.

```csharp
private void DrainHazardDestroyedEvents()
{
    if (!_em.CreateEntityQuery(typeof(HazardDestroyedEventsSingleton)).TryGetSingletonRW<HazardDestroyedEventsSingleton>(out var sink)) return;
    while (sink.queue.TryDequeue(out var ev))
    {
        if (_hazardVisualMap.TryGetValue(ev.hazardEntity, out var visual) && visual != null)
        {
            var presenter = visual.GetComponent<BlockingHazardPresenter>();
            BlockingHazardSO so = (ev.hazardSoIndex >= 0 && ev.hazardSoIndex < _hazardSoRegistry.Count)
                                   ? _hazardSoRegistry[ev.hazardSoIndex] : null;
            presenter?.OnDestroyed(so?.destructionVfxPrefab);
        }
        _hazardVisualMap.Remove(ev.hazardEntity);
    }
}
```

`Update()` 에 `DrainHazardDestroyedEvents()` 호출 추가 (기존 다른 drain 함수 옆).

### HealthBar 부착

`HealthBarSystem` 은 별도 *bar entity* 모델 — owner entity 에 `HealthBarTag` 를 직접 붙이는 게 아니라, owner 마다 별도 bar entity 를 만들어 `HealthBarState.owner` 가 owner 를 참조하는 구조. 기존 헬퍼 `BattleBridge.CreateHealthBar(owner, ...)` 가 이 별도 bar entity 생성을 일괄 처리 (디펜더 / 적 spawn 시 호출).

→ **본 unit 작업**: spawn API (Unit 7) 의 hazard entity 생성 직후 `BattleBridge.CreateHealthBar(hazardEntity, ...)` 호출하여 bar entity 를 hazard 와 매핑. HealthBarSystem 의 query (`HealthBarTag` 만 필터) 와 코드 수정 0. visual prefab 의 anchor = hazard entity LocalTransform = center cell worldPos 이므로 bar 위치 자연 정렬.

(Unit 7 의 spawn 컴포넌트 목록에서 `em.AddComponent<HealthBarTag>(entity)` / `em.AddComponentData(entity, new HealthBarState { owner = entity })` 두 줄은 이 패턴으로 대체 — hazard 자체에는 HealthBar 컴포넌트 부착 X. CreateHealthBar 헬퍼가 별도 bar entity 생성/매핑.)

### 핵심 결정

- **Visual 의 lifetime = drain 이벤트 trigger** — ECS entity destroy 직전 enqueue, 같은/다음 프레임 BattleBridge drain → visual.OnDestroyed → VFX + GameObject destroy.
- **HazardSO 매핑 = 정수 인덱스** — Entity 가 destroy 후 invalid 이지만 `hazardSoIndex` 메타로 VFX prefab 찾음. dictionary key (Entity 값) 도 비교 가능 (Equals).
- **HealthBar 인프라 재사용** — 본 unit 작업: `HealthBarSystem` 의 query 가 hazard 합류 가능하도록 (필요 시 query 확장).

## 단위 테스트 (EditMode)

없음 — MonoBehaviour + 매핑 dictionary 위주. 검증은 PlayMode (Unit 9).

## 완료 기준

- 컴파일 성공.
- placeholder prefab 에 BlockingHazardPresenter 부착 + HealthBar 자식 prefab 정렬.
- PlayMode 에서 Hazard spawn → 시각 표시 + HP bar 정상 (적 공격 시 HP 감소 시각 피드백).
- HP 0 시 destruction VFX (placeholder 또는 simple particle) 트리거 + visual GameObject destroy.
- 기존 회귀 0.
- 콘솔 에러/경고 0.

검증: 2026-04-29 — `BlockingHazardPresenter`, BattleBridge destruction drain, 기존 `CreateHealthBar(owner, ...)` 기반 hazard HP bar 부착 구현. PlayMode 사용자 확인 통과 — handoff_summary.md Verified 섹션 참조 (spawn / 유효 cell 스냅 / 콘솔 에러 0). 적 부수기 + destruction VFX 의 정밀 시각 검증은 후속 PlayMode 세션에서 재확인 가능. 커밋 `3f5ab31`.
