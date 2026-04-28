# Projectile View Pool Skeleton

**작업 구분**: 1

## 목적

prefab 기반 투사체 view 를 ECS 엔티티 위치에 동기화하는 풀 컴포넌트를 도입한다. 이 task 에서는 골격만 — 회전/색조/텍스처 노브는 task 3,4,6 에서 누적.

## 변경 대상

- New: `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (필드 1개 + LateUpdate 호출 1줄)

## 클래스 계약

```csharp
public class ProjectileViewPool : MonoBehaviour
{
    public void Spawn(Entity entity, GameObject prefab, float scale);
    public void SyncTransforms(EntityManager em);   // BattleBridge.LateUpdate 에서 호출
    public void DespawnAll();                        // BattleScene 종료 시 정리
}
```

내부 자료구조:

- `Dictionary<Entity, GameObject> _active`
- `Dictionary<GameObject /*prefab*/, Stack<GameObject>> _pool` — prefab 별 풀
- `SpawnedView` 헬퍼는 만들지 않음 (지금은 GameObject 만 보관). task 4 에서 변형 노브 추가 시 struct 로 격상.

## 동작

- `Spawn(entity, prefab, scale)`: 풀에서 꺼내거나 Instantiate. `SetActive(true)`. Position 은 `LocalTransform.Position` 으로 즉시 sync. Scale 은 인자값. 회전은 이번 task 에서는 prefab 의 기본 rotation 그대로.
- `SyncTransforms`: `_active` 를 순회. `em.Exists(entity)` 가 false 면 풀로 반환. true 면 `LocalTransform.Position` 을 view transform 에 복사.
- BattleBridge 의 LateUpdate(또는 기존 `LateUpdate` 위치) 끝에 `_projectileViewPool?.SyncTransforms(_em)` 1줄 추가. 단, 이 task 에서는 아직 `Spawn()` 호출 site 가 없으므로 (task 2 에서 연결) 풀은 비어있고 SyncTransforms 는 no-op.

## 와이어링

- BattleBridge 에 `[SerializeField] private ProjectileViewPool projectileViewPool;` 추가.
- BattleScene 의 BattleBridge GameObject 자식으로 `ProjectileViewPool` 컴포넌트가 붙은 빈 GameObject 생성 (UnityMCP 로 자동화). Inspector 참조 연결.

## 완료 기준

- compile + Play smoke: 기존과 동일 동작 (풀이 빈 상태로 SyncTransforms 호출만 일어남).
- BattleScene 안에서 `ProjectileViewPool` 컴포넌트가 BattleBridge 의 SerializeField 에 연결되어 있고, 누락 시 BattleBridge 가 null-safe (warning 1회 후 동작은 기존대로).
- read_console Error/Warning 0.
