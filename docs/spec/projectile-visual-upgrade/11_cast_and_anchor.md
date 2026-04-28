# Cast Prefab + Weapon Anchor (rev2)

**작업 구분**: 11 (rev2)
**근거**: 본체 spec README 의 후속 후보 "Cast prefab (머즐 플래시) — 디펜더 무기 anchor" 를 정식 task 로 승격.

## 목적

디펜더 발사 시점에 무기 끝점에서 cast(머즐 플래시) prefab 1회 재생. 첫 단계는 Archer 만 검증하고, 인프라는 모든 projectile 디펜더(8대) 가 데이터만 채우면 동작하도록 generic 하게 설계.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/ProjectileData.cs` (castPrefab 필드)
- Modify: `Assets/_Project/Scripts/Data/DefenderUnitData.cs` (castAnchorBone + castAnchorLocalOffset 필드)
- Modify: `Assets/_Project/Scripts/Presentation/SpineDefenderView.cs` (bone 위치 lookup API + X-flip 고려)
- Modify: `Assets/_Project/Scripts/Presentation/SpineDefenderPool.cs` (NotifyAttack 시 cast 콜백 위임 또는 anchor 조회 API 노출)
- Modify: `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` (PlayCast 메서드)
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (DrainDefenderAttackEvents 안에서 cast spawn)
- Modify: `Assets/_Project/Data/Projectiles/Projectile_Arrow.asset` (castPrefab 와이어링)
- Modify: `Assets/_Project/Data/Defenders/Defender_Archer.asset` (castAnchorBone 또는 offset 설정)

## 데이터 스키마 추가

`ProjectileData`:
```csharp
[Header("Cast VFX")]
public GameObject castPrefab;            // null 이면 cast 안 함
public float castVfxLifetime = 0f;       // 0 = auto-detect (PlayHit 와 동일 패턴)
```

`DefenderUnitData`:
```csharp
[Header("Cast Anchor")]
public string castAnchorBone = "";       // Spine bone 이름. 비어있거나 못 찾으면 offset 폴백.
public Vector3 castAnchorLocalOffset = new Vector3(0.5f, 1f, 0f); // 디펜더 root 기준 local offset
```

## 인프라 — Anchor 조회 (Q2 = (c) bone + offset 폴백)

`SpineDefenderView` 에 새 API:

```csharp
// 무기 끝점 world position 반환. 본 이름이 비어있거나 본을 못 찾으면 offset 폴백.
// X-flip 은 Spine 의 ScaleX 가 자동 처리하므로 bone.WorldX/WorldY 결과를 그대로
// transform.TransformPoint 통해 world 로 변환. offset 폴백은 ScaleX 부호에 따라 X 반전.
public Vector3 ResolveCastAnchor()
{
    if (_skeleton != null && _skeleton.Skeleton != null && !string.IsNullOrEmpty(_unitData.castAnchorBone))
    {
        var bone = _skeleton.Skeleton.FindBone(_unitData.castAnchorBone);
        if (bone != null)
        {
            // Spine 의 bone.WorldX/WorldY 는 skeleton local 좌표.
            // SkeletonAnimation transform 의 lossyScale 을 곱한 뒤 TransformPoint.
            var local = new Vector3(bone.WorldX, bone.WorldY, 0f);
            return transform.TransformPoint(local);
        }
    }
    // 폴백: ScaleX 부호로 X-flip 반영
    var off = _unitData.castAnchorLocalOffset;
    if (_skeleton != null && _skeleton.Skeleton != null && _skeleton.Skeleton.ScaleX < 0f)
        off.x = -off.x;
    return transform.TransformPoint(off);
}
```

`SpineDefenderPool` 에 wrapper:

```csharp
public bool TryResolveAnchor(Entity entity, out Vector3 worldPos)
{
    if (TryGet(entity, out var view))
    {
        worldPos = view.ResolveCastAnchor();
        return true;
    }
    worldPos = default;
    return false;
}
```

## 인프라 — Cast 재생 (`ProjectileViewPool`)

`PlayHit` 와 같은 패턴:

```csharp
public void PlayCast(GameObject castPrefab, Vector3 position, Vector3 facingDir, float lifetime = 0f)
{
    var view = GetOrCreate(castPrefab);
    view.SetActive(true);
    view.transform.position = position;
    if (math.lengthsq(facingDir) > 0.0001f)
        view.transform.rotation = Quaternion.LookRotation(facingDir, Vector3.up);
    float life = lifetime > 0f ? lifetime : GetParticleLifetime(view);
    StartCoroutine(DespawnAfter(view, castPrefab, life));
}
```

## 트리거 위치 (Q3 = (b) 스파인 이벤트, 지금은 첫 프레임)

`BattleBridge.DrainDefenderAttackEvents` 안에서 PlayAttack 직후 즉시 cast spawn (= attack animation 첫 프레임 = AttackEvent 도착 frame).

```csharp
private void DrainDefenderAttackEvents()
{
    if (!_defenderAttackQueue.IsCreated) return;
    while (_defenderAttackQueue.TryDequeue(out var evt))
    {
        if (spineDefenderPool == null) continue;
        var targetWorld = new Vector3(evt.targetWorld.x, evt.targetWorld.y, evt.targetWorld.z);
        spineDefenderPool.NotifyAttack(evt.defender, targetWorld);

        // Cast VFX — defender 가 ProjectileRef 를 갖고, 그 ProjectileData 의 castPrefab 이 set 되어 있으면 spawn.
        TrySpawnCastVfx(evt.defender, targetWorld);
    }
}

private void TrySpawnCastVfx(Entity defender, Vector3 targetWorld)
{
    if (_projectileViewPool == null) return;
    if (!_em.HasComponent<ProjectileRef>(defender)) return;
    var pref = _em.GetComponentData<ProjectileRef>(defender);
    if (pref.dataIndex < 0 || pref.dataIndex >= _projectileDataByIndex.Count) return;
    var data = _projectileDataByIndex[pref.dataIndex];
    if (data.castPrefab == null) return;
    if (!spineDefenderPool.TryResolveAnchor(defender, out var anchor)) return;
    var dir = (targetWorld - anchor); dir.y = 0f;
    _projectileViewPool.PlayCast(data.castPrefab, anchor, dir, data.castVfxLifetime);
}
```

후속(이번 task 밖): SpineDefenderView 가 attack animation 의 keyed event 를 수신해서 callback emit, BattleBridge 가 그 callback 시점에 PlayCast. 본 task 의 "첫 프레임 트리거" 는 동등한 fallback 이며 인프라 자체는 이미 위 generic 코드로 완비.

## 자산 와이어링 (Archer 만)

- `Projectile_Arrow.asset`:
  - `castPrefab` = `Assets/PixPlays/ElementalProjectiles/Windbullet/Version_BuiltIn/WindbulletCast/<cast prefab>.prefab` (URP variant 가 있으면 우선)
  - `castVfxLifetime` = 0 (auto-detect 사용)

- `Defender_Archer.asset`:
  - `castAnchorBone` = "" (Lamb 스켈레톤에 적합 본이 있는지 확인 — `weapon-tip`/`muzzle`/`hand-r` 등 후보. 없으면 offset 폴백)
  - `castAnchorLocalOffset` = (0.5, 1.0, 0) — 시연 후 디자이너가 튜닝

스켈레톤 본 확인은 Unity Editor 의 SkeletonAnimation Inspector → Skeleton tree 또는 코드 `_skeleton.Skeleton.Bones` 순회 한 번. 본 이름 결정 후 `castAnchorBone` 채움.

## 다른 7 디펜더 (이번 task 밖)

검증 후 follow-up 으로:

- Marksman / Sniper / Piercer / Scout / Ranger / Guardian / Cannon
- 각자의 ProjectileData (Bolt/CannonBall) 에 castPrefab 채움 (Stonebullet/Fireball Cast prefab).
- 각자의 DefenderUnitData 에 castAnchorBone 채움 (또는 offset 사용).
- Cannon (melee=False but uses CannonBall) 은 Fireball Cast 매칭.

이번 task 의 인프라는 generic 이라 데이터 와이어링만 하면 동작.

## Play 검증 시나리오

1. Editor Play → BattleScene → Archer 배치 → 적 등장 대기.
2. Archer 가 처음 발사하는 순간:
   - 무기 끝점(또는 offset 위치) 에서 Wind cast prefab 1회 재생.
   - 동시에 화살 비행체 spawn (회귀 없음).
   - cast prefab 이 적 방향을 바라봄.
   - cast lifetime 후 자체 종료 (풀 반환).
3. Archer 가 좌/우 방향 적을 번갈아 공격할 때 cast 위치가 ScaleX flip 따라 좌/우로 미러링.
4. 다른 디펜더 (Cannon, Marksman 등) 발사 시 cast 안 뜸 (castPrefab=null fallback).

## 완료 기준

- compile: 7 파일 변경 모두 에러 없이 통과.
- BattleScene Play smoke: Archer 의 cast VFX 가 발사 첫 프레임에 1회 재생, 적 방향 정렬, lifetime 후 자체 종료.
- 좌/우 facing flip 시 anchor 위치가 좌/우로 따라옴 (육안 검증).
- 풀 누수 없음: 100발 연속 발사 후 pool `_active` 카운트 = projectile 수 (cast 는 자체 lifetime 후 0 으로 복귀).
- 다른 7 디펜더의 castPrefab 미설정 → cast 발생 안 함, 회귀 없음.
- read_console Error/Warning 0.

확인 2026-04-28 / 커밋: 4f18376
