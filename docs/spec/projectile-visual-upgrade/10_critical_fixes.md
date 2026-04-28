# Critical Fixes (rev1)

**작업 구분**: 10 (rev1)
**근거**: 2026-04-28 종합 리뷰에서 식별된 3 CRITICAL + 2 MEDIUM. spec 본체 (0..8) 종료 후 발견.

## 목적

`ProjectileViewPool` 의 시각 결정성/재사용 안정성/모바일 GC 부담을 잡는다. 게임 동작 변화는 없고 시각/성능 결함만 봉합.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (Spawn 시그너처/시드 주입)
- Modify: `Assets/_Project/Scripts/Data/ProjectileData.cs` (`hitVfxLifetime` 필드)
- Modify: `Assets/_Project/Tests/EditMode/ProjectileVariationTests.cs` (의식 테스트 4건 정리 + 회귀 테스트 추가)

## 수정 1 — 첫 프레임 회전 (CRITICAL)

`Spawn` 에서 `lastPosition = float3.zero` 라 첫 `SyncTransforms` 의 velocity 가 spawn 좌표 자체로 계산돼 1프레임 잘못된 방향 표시.

```csharp
// 현재 시그너처
public void Spawn(Entity entity, ProjectileData data)

// 변경
public void Spawn(Entity entity, ProjectileData data, float3 initialPosition)
{
    ...
    _active[entity] = new ProjectileViewState
    {
        ...
        lastPosition = initialPosition,
    };
}
```

`BattleBridge.SpawnProjectile` 에서 `req.origin` (이미 LocalTransform 으로 전달되는 값) 을 함께 전달.

## 수정 2 — Roll 누적 (CRITICAL)

`view.transform.localRotation *= Quaternion.Euler(0, 0, rollDeg)` 가 풀 재사용마다 누적.

```csharp
// prefab 기준 reset 후 적용
view.transform.localRotation = data.projectilePrefab.transform.localRotation
    * Quaternion.Euler(0f, 0f, rollDeg);
```

AlongVelocity 자산은 다음 SyncTransforms 가 덮어쓰므로 영향 없음. SpinAroundUp/FixedUp 자산만 누적 leak 이었지만 일관성 위해 모든 facing 에 적용.

## 수정 3 — Visual RNG 결정 시드 (CRITICAL)

`new System.Random()` (시간 기반) → 시드 주입형으로 교체.

```csharp
public class ProjectileViewPool : MonoBehaviour
{
    public void Initialize(int seed)
    {
        _visualRng = new System.Random(seed);
        _spawnCounters.Clear();
    }
}
```

`BattleBridge.EnsureQueriesAndQueues` 에서 wave 시작 시 호출:

```csharp
if (projectileViewPool != null)
{
    int seed = (int)(_seed ^ 0x5A5A5A5A); // 시뮬 시드와 분리, 결정 재현 가능
    projectileViewPool.Initialize(seed);
}
```

(`_seed` 가 BattleBridge 에 없으면 `deck.battleSeed` 또는 `WavePatternGenerator` 가 쓰는 시드를 차용.)

`Awake` 의 `_visualRng = new System.Random()` 는 fallback 으로 유지 (Initialize 누락 시 동작은 함, 결정성만 잃음).

## 수정 4 — Renderer 캐시 (MEDIUM)

`Spawn / PlayHit / ReturnToPool` 마다 `GetComponentsInChildren<Renderer>` 호출 → 핫패스 GC.

`ProjectileViewState` 와 hit-prefab 풀 양쪽에 `Renderer[] renderers` 캐시 필드 추가. `GetOrCreate(prefab)` 가 풀에서 꺼내거나 새로 Instantiate 한 직후 1회만 `GetComponentsInChildren<Renderer>(true)` 호출 후 view GameObject 에 작은 컴포넌트 (`ViewRendererCache`) 로 attach. 재사용 시 attached cache 의 배열 그대로 사용.

```csharp
// 새 컴포넌트
public class ViewRendererCache : MonoBehaviour
{
    public Renderer[] renderers;
}

// GetOrCreate 직후
if (!view.TryGetComponent<ViewRendererCache>(out var cache))
{
    cache = view.AddComponent<ViewRendererCache>();
    cache.renderers = view.GetComponentsInChildren<Renderer>(includeInactive: true);
}
```

`ApplyMpb` / `ReturnToPool` 의 foreach 가 `cache.renderers` 를 사용.

## 수정 5 — Hit prefab lifetime 명시 필드 (MEDIUM)

`GetParticleLifetime` 의 `main.startLifetime.constantMax` 가 curve 모드 자산에서 0 반환. loop=true particle 도 detect 불가.

`ProjectileData` 에 명시 필드 추가:

```csharp
[Header("Hit VFX")]
public float hitVfxLifetime = 0f; // 0 = auto-detect (기존 동작), >0 = 강제 지정
```

`PlayHit` 가 데이터의 hitVfxLifetime > 0 이면 그 값 사용, 아니면 기존 detect 폴백.

## 테스트 갱신

`ProjectileVariationTests.cs` 의식 테스트 정리:

- **삭제**: `RandomSelect_DeterministicWithSeed` (stdlib 테스트), `SequentialSelect_WrapsAroundLength` (`%` 연산 테스트), `ScaleJitter_ZeroProducesIdentity` (자명).
- **유지**: `HueShift_ZeroPreservesColor`, `HueShift_WrapsAroundOne`.
- **추가**:
  - `Initialize_SameSeedProducesSameSequence`: `Initialize(42)` 두 번 → 동일 hueJitter/scaleJitter sampling 확인.
  - `RollDoesNotAccumulate_AcrossPoolReuse`: 동일 prefab 으로 Spawn → Return → Spawn 시 두 번째 spawn 의 시작 rotation 이 첫 번째와 무관하게 prefab 기준에서 시작 (rollJitter=0 일 때).

PlayMode smoke 는 그대로 유지.

## 완료 기준

- compile + Play smoke: 발사 첫 프레임 회전이 즉시 이동방향 정렬.
- BattleScene 에서 wave 재시작 (Restart) 후 동일 시드로 같은 wave 재생 시 시각 시퀀스 (jitter 결과) 재현 가능 — Sniper_Crimson 의 텍스처 선택 순서가 두 번 동일.
- CannonBall 100발 연속 발사 후 view 의 rotation 이 prefab 기준에서 출발 (누적 없음 — Editor Inspector 로 확인).
- Profiler GC.Alloc 표본: 100 projectile burst frame 의 Renderer/ParticleSystem 배열 할당 0.
- 모든 EditMode 테스트 그린 (3 → 의식 3건 제거, 신규 2건 추가, ApplyHueShift 2건 유지 = 4건).
- read_console Error/Warning 0.
