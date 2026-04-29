# Hazard Visual (Self-Managed)

**작업 구분**: 6

## 목적

Hazard ECS entity 의 visual prefab 을 world 에 인스턴스화 + visual 자가 lifetime 으로 자동 destroy. 기존 `VfxSpawner.SpawnTornado(...)` / `BattleBridge.SpawnMeteorWarningVisual(...)` 의 *fire-and-self-manage* 패턴을 미러 — visual 이 ECS 와 *별도 lifecycle* 이지만 동일 lifetime 값으로 시작 → 동시 종료.

**중요**: 본 spec 의 코드베이스에 `TornadoFieldPresenter` / `MeteorWarningPresenter` 같은 sync-by-frame Presenter 패턴은 **존재하지 않는다**. spawn 측 (`VfxSpawner` 메서드 + 자가 destroy timer) 가 모든 lifecycle 을 들고 있는 패턴이 기존 관행. 이 spec 도 같은 결을 따른다.

## 변경 대상

- Add: `Assets/_Project/Scripts/Presentation/HazardVisualLifetime.cs` — MonoBehaviour 자가 destroy timer
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnHazardWithVisual(HazardSO, int2)` wrapper 메서드 추가

## HazardVisualLifetime

```csharp
namespace Wassup.Presentation
{
    public class HazardVisualLifetime : MonoBehaviour
    {
        [SerializeField] private float remainingLife = 5f;

        public void Init(float lifetime) => remainingLife = lifetime;

        private void Update()
        {
            remainingLife -= Time.deltaTime;
            if (remainingLife <= 0f) Destroy(gameObject);
        }
    }
}
```

= 가장 단순한 self-destroy timer. visual prefab 에 부착하면 lifetime 후 자동 정리. 기존 `MeteorFall.cs` / `BeamPulse.cs` 등 self-managed visual 패턴과 동일 결.

## BattleBridge.SpawnHazardWithVisual

```csharp
public Entity SpawnHazardWithVisual(HazardSO so, int2 cell)
{
    if (so == null || _em == null) return Entity.Null;

    // 1. ECS hazard entity (lifetime = so.lifetime, HazardLifetimeSystem 가 tick + destroy)
    var e = EffectSpawner.SpawnHazard(_em, so, cell);
    if (e == Entity.Null) return e;

    // 2. Visual prefab 인스턴스화 — 자가 lifecycle (so.lifetime 만큼)
    if (so.visualPrefab == null)
    {
        Debug.LogWarning($"[BattleBridge] HazardSO '{so.name}' has no visualPrefab. Spawned hazard will be invisible.");
        return e;
    }

    Vector3 worldOrigin = GridToWorldXZ(cell);   // 기존 grid math 재사용 (정확 함수명은 BattleBridge 내 검색)
    var vis = Instantiate(so.visualPrefab, worldOrigin, Quaternion.identity);
    vis.transform.localScale = ShapeToScaleVec(so.shape, so.radius, vis.transform.localScale.y);

    // 자가 destroy timer 부착 (이미 prefab 에 있으면 reuse)
    var lifetime = vis.GetComponent<HazardVisualLifetime>() ?? vis.AddComponent<HazardVisualLifetime>();
    lifetime.Init(so.lifetime);

    return e;
}

private static Vector3 ShapeToScaleVec(HazardShape shape, int radius, float yScale)
{
    float side = shape switch
    {
        HazardShape.SingleCell => 1f,
        HazardShape.Square3x3 => 3f,
        HazardShape.RadiusSquare => 2f * radius + 1f,
        _ => 1f,
    };
    return new Vector3(side, yScale, side);
}
```

## 의도와 한계

- **ECS 와 visual 이 별도 lifecycle**. 둘 다 같은 시점 (SpawnHazardWithVisual 호출 순간) 에 시작 + 같은 `lifetime` 값을 받음 → 자연스럽게 비슷한 시점에 종료.
- 일반 case (timer 만료) 에서 둘이 거의 동시 종료 (1프레임 차이 가능, 시각상 무영향).
- **한계**: ECS 가 *조기* destroy 되면 (디버그 강제 제거 등) visual 은 자기 timer 까지 남음. MVP 에서 허용. 정확 sync 가 필요한 미래 use case (예: 적이 hazard 부숨, 외부 cancel) 는 후속 후보 — spec 차단형 hazard 또는 별도 sync system.

## visualPrefab null 처리

- visualPrefab 이 null 이면 ECS hazard 만 생성, visual 없음. `Debug.LogWarning` 출력.
- 이렇게 두는 이유: 효과만 있는 invisible hazard (트랩 같은 미래 use case) 를 막지 않기 위해. 단 본 spec 의 3 sample SO 는 모두 visualPrefab 필수.

## 단위 테스트 (선택)

- `HazardVisualLifetime` 은 PlayMode 에서 timer 동작만 시각 확인 (Unit 7 시나리오에서 자연 검증).
- EditMode 단위 테스트 작성 부담 vs 가치 낮음 — Update + Time.deltaTime 의존하므로 PlayMode smoke 만으로 충분.

## 완료 기준

- 컴파일.
- Presentation 계층만 추가. ECS 동작 변화 0.
- `BattleBridge.SpawnHazardWithVisual` 컴파일 + 호출 가능 (Unit 7 의 진입점이 이걸 호출).
- 콘솔 에러/경고 0 (단, visualPrefab=null 호출 시 의도된 LogWarning 1건 OK).
