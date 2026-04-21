# Phase 8 — Defender Spine 하이브리드 + Prefab VFX 파이프라인

> Phase 8은 Phase 5의 Billboard 기반 유닛 표시를 defender Spine 하이브리드로 확장하고, Phase 7 스킬/배치 피드백을 prefab 기반 VFX로 전환한 단계다. ECS 전투 시뮬레이션은 유지하고, 시각 표현만 MonoBehaviour/Presentation 계층에서 담당한다.

---

## 1. 목표

- 방어 유닛 10종을 `player-main` Spine skeleton skin으로 시각 구분한다.
- defender entity와 `SkeletonAnimation` GameObject를 1:1로 연결한다.
- idle / attack / die 애니메이션을 ECS 이벤트에 반응시킨다.
- attack frame 기준으로 target 방향을 바라보게 한다.
- Placement / Meteor Fall / Meteor Burst / Tornado / Portal VFX를 `_SKELETON.prefab` 기반으로 통합한다.
- Phase 7 Tornado의 cast-time snapshot 한계를 지속 field로 해결한다.

### 비목표

- 공격 유닛 Spine 전환.
- Spine skin 합성/IK/Timeline.
- VFX Graph 도입.
- HDR bloom 최종 연출.
- Flow Field 길찾기. Portal/Tornado 변위 후 자율 복귀는 Phase 9 대상이다.

---

## 2. 확정 결정

| 항목 | 구현 결과 |
|---|---|
| Defender 시각화 | Spine `SkeletonAnimation` GameObject + ECS entity 1:1 |
| fallback | `skeletonDataAsset` 또는 `spineSkinName` 없으면 기존 Billboard RenderMesh |
| Pool | `SpineDefenderPool` 비싱글톤 MonoBehaviour |
| View | `SpineDefenderView` 가 Spine 초기화/애니메이션/방향 snap 담당 |
| 공격 트리거 | `DefenderAttackEventsSingleton` NativeQueue |
| 사망 트리거 | `DefenderDeathEventsSingleton` drain → `SpineDefenderPool.NotifyDeath` |
| 방향 전환 | 매 프레임 polling이 아니라 attack event targetWorld 기준 snap |
| VFX | prefab slot 필수. null이면 error log 후 return |
| Tornado | `TornadoField` carrier entity 기반 지속 field |
| Melee AoE | `attackTargetCount` 로 melee defender 다중 타깃 허용 |

---

## 3. Spine 데이터

`DefenderUnitData` 확장:

```csharp
public SkeletonDataAsset skeletonDataAsset;
public string spineSkinName;
public string idleAnimation = "idle";
public string attackAnimation = "attack";
public string deathAnimation = "die";
public float spineVisualScale = 1f;
public int attackTargetCount = 1;
```

구현 상태:

- 10종 defender asset에 `skeletonDataAsset` 할당 완료.
- 10종 defender asset에 `spineSkinName` 입력 완료.
- 현재 skin은 Lamb / Owl / Goat 계열로 매핑되어 있다.
- animation 이름은 SO 필드로 유지해 skeleton 교체 시 Inspector에서 조정 가능하다.

---

## 4. Spine 런타임 구조

### 4.1 `SpineDefenderPool`

- `Dictionary<Entity, SpineDefenderView>` 로 live defender view를 추적한다.
- `TrySpawn(unitData, entity, worldPos, out view)` 는 skeleton/skin이 없으면 false를 반환해 Billboard fallback을 허용한다.
- `NotifyAttack(entity, targetWorld)` 는 view를 target 방향으로 snap하고 attack animation을 재생한다.
- `NotifyDeath(entity)` 는 die animation을 재생하고 mapping을 제거한다.
- `DisposeAll()` 은 Restart/Redraft/teardown에서 즉시 정리한다.

### 4.2 `SpineDefenderView`

- GameObject에 `SkeletonAnimation` 을 추가하고 SO의 `SkeletonDataAsset` / skin / animation 이름으로 초기화한다.
- spawn 시 world position과 `spineVisualScale` 을 적용한다.
- `PlayAttack()` 은 attack 1회 후 idle loop를 queue한다.
- `Kill()` 은 death animation Complete 콜백에서 GameObject를 destroy한다.
- `FaceToward(worldPoint)` 는 rig 기본 방향을 고려해 `Skeleton.ScaleX` 를 snap한다.

### 4.3 BattleBridge 연결

- defender 배치 성공 시 `SpineDefenderPool.TrySpawn` 을 먼저 시도한다.
- Spine spawn 실패 시 기존 RenderMesh billboard 경로를 사용한다.
- defender death queue drain 시 tile 점유 해제와 함께 pool에 death를 알린다.
- `DrainDefenderAttackEvents()` 에서 attack animation/facing을 일괄 처리한다.

---

## 5. Defender Attack Event

Phase 8에서 projectile defender와 melee defender의 attack animation trigger를 통합했다.

- `DefenderAttackEvent`: defender entity + targetWorld.
- `DefenderAttackEventsSingleton`: `NativeQueue<DefenderAttackEvent>` 보관.
- `AttackSystem`: projectile / melee 분기 모두 공격 성공 시 event enqueue.
- `BattleBridge`: queue drain 후 `SpineDefenderPool.NotifyAttack`.

이 구조로 melee defender도 projectile defender와 동일하게 attack animation을 재생한다.

---

## 6. Melee AoE

- `DefenderUnitData.attackTargetCount` 는 melee-only 다중 타깃 cap이다.
- `AttackState.attackTargetCount` 로 runtime에 복사된다.
- projectile defender는 기존 projectile/splash 경로를 사용한다.
- melee defender는 nearest N targets에 직접 `IncomingDamage` 를 append한다.
- 기본값 1은 기존 단일 타깃 동작을 보존한다.

---

## 7. VFX 정책

Phase 8 후반에 코드 기반 ParticleSystem fallback을 제거하고 prefab-only 정책으로 단일화했다.

- VFX Graph는 모바일 호환성과 스코프 관리를 위해 제외.
- Shuriken ParticleSystem 기반 `_SKELETON.prefab` 을 사용.
- `VfxSpawner` 는 비싱글톤 MonoBehaviour이며 `BattleBridge` 가 SerializeField로 참조한다.
- 모든 prefab slot은 필수다. null이면 `Debug.LogError` 후 return한다.
- 실제 authoring 운영 소스는 `.claude/skills/unity-vfx-authoring/` 스킬이다.

`VfxSpawner` slot:

```csharp
placementRingPrefab
meteorBurstPrefab
meteorFallPrefab
tornadoPrefab
portalPrefab
```

---

## 8. VFX Prefab 목록

| Prefab | 트리거 | 구성 |
|---|---|---|
| `Placement_SKELETON.prefab` | defender 배치 성공 | Ring / CenterFlash / RisingMotes |
| `Meteor_Falling_SKELETON.prefab` | Meteor warning 시작 | falling streak + `MeteorFall` MB |
| `Meteor_Burst_SKELETON.prefab` | Meteor damage resolve | CoreFlash / MainBurst / Debris |
| `Tornado_SKELETON.prefab` | Tornado cast | OuterDonut / InnerSpiral / GroundDust |
| `Portal_SKELETON.prefab` | Portal cast | Entry / Exit / LinkBeam |

Scene wiring:

- `BattleScene.unity` 의 `VfxSpawner` 에 5개 slot 연결 완료.
- `SpineDefenderPool` GameObject도 scene에 배치되어 `BattleBridge` 에 연결 완료.

---

## 9. Beam / Meteor 보조 MonoBehaviour

### `BeamPulse`

- `Portal_SKELETON` 의 `LinkBeam` 에 부착된다.
- `LineRenderer.startColor/endColor` alpha를 sin wave로 직접 갱신한다.
- MaterialPropertyBlock은 LineRenderer vertex color 전달 불확실성 때문에 사용하지 않는다.

### `MeteorFall`

- `Meteor_Falling_SKELETON` root에 부착된다.
- warning duration 동안 target 위 높이에서 impact point까지 quadratic ease-in으로 이동한다.
- 착지 시 self destroy, 실제 damage/VFX burst는 `MeteorResolutionSystem` + `MeteorBurstEventsSingleton` 경로가 담당한다.

---

## 10. Meteor Burst Event

- `MeteorResolutionSystem` 은 warning 시간이 끝난 `MeteorPending` 을 resolve한다.
- 범위 내 attacker에게 damage를 append한다.
- 같은 frame에 `MeteorBurstEventsSingleton` queue로 center/radius를 enqueue한다.
- `BattleBridge.DrainMeteorBurstEvents()` 가 `VfxSpawner.SpawnMeteorBurst` 를 호출한다.

이 경로는 ECS 시뮬레이션 완료 시점과 VFX 타이밍을 맞추기 위한 NativeQueue 패턴이다.

---

## 11. Tornado 지속 Field (§17)

Phase 7의 Tornado는 cast 순간 범위 내 적만 pull 대상으로 삼는 snapshot 구조였다. Phase 8 §17에서 이를 `TornadoField` carrier entity로 교체했다.

구현 결과:

- `TornadoField`: centerWorld / radius / pullSpeed / remaining.
- `EffectSpawner.SpawnTornadoField`: cast 시 field entity 1개 생성.
- `MovementSystem`: 매 프레임 live `TornadoField` 배열을 snapshot하고, 현재 field radius 안에 있는 attacker를 중심으로 pull한다.
- `EffectTickSystem`: remaining 감소 후 만료 시 field entity destroy.
- `BattleBridge.ApplyTornado`: per-attacker 반복 제거, field spawn + VFX spawn으로 단순화.

해결된 문제:

- duration 중 새로 범위에 들어온 적도 pull 영향을 받는다.

남은 문제:

- Tornado 종료 후 waypoint 기반 복귀가 직선적이다. 이 문제는 Phase 9 Flow Field 전환 대상이다.

---

## 12. 작업 결과

- [x] P8-01 — `DefenderUnitData` Spine 필드 추가.
- [x] P8-02 — `SpineDefenderView`.
- [x] P8-03 — `SpineDefenderPool`.
- [x] P8-04 — 배치 성공 시 Spine spawn + Billboard fallback.
- [x] P8-05 — defender attack/death event 연결.
- [x] P8-06 — attack-time facing snap.
- [x] P8-07 — death animation Complete 후 destroy.
- [x] P8-08 — skeleton/skin 누락 시 fallback.
- [x] P8-09 — `BattleLogEntry.phase = "phase8"`.
- [x] P8-11 — defender 10종 skin/skeleton 할당.
- [x] P8-12 — Placement prefab VFX.
- [x] P8-13 — Meteor fall/burst prefab VFX + burst queue.
- [x] P8-14 — Tornado prefab VFX + 지속 field.
- [x] P8-15 — Portal prefab VFX + BeamPulse.
- [x] P8-16 — DefenderAttackEvent 통합 채널.
- [x] P8-17 — Tornado 지속 field 전환.
- [ ] P8-10 — 사용자 Play 회귀: defender Spine 상태 전환 + 5종 VFX prefab 시각 확인.

---

## 13. 종료 조건

- defender 10종이 Spine skin으로 시각 구분된다.
- idle / attack / die 상태 전환이 이벤트에 반응한다.
- attack 시 target 방향으로 facing snap된다.
- skeleton/skin 누락 defender는 Billboard fallback으로 배치 가능하다.
- Placement / Meteor Fall / Meteor Burst / Tornado / Portal prefab VFX가 scene slot에서 스폰된다.
- Tornado는 duration 중 신규 진입 적도 pull한다.
- Unity 컴파일 에러 0.
- P8-10 사용자 Play 회귀가 남은 최종 검증이다.

---

## 14. TRD 금지 패턴 재적용

- `SpineDefenderPool` 과 `VfxSpawner` 는 비싱글톤이다.
- GameManager 외 static Instance를 만들지 않는다.
- Spine/VFX는 Presentation 계층이며 ECS 맥락 폴더가 아니다.
- `SpineDefenderView` 는 EntityManager를 직접 호출하지 않는다.
- Combat event는 NativeQueue로 MonoBehaviour layer에 전달한다.
- VFX prefab slot과 SO 필드가 수치/자산의 출처다.
- VFX Graph, Shader Graph JSON 생성, 미사용 추상화는 도입하지 않는다.

---

## 15. 잔여 / Phase 9 연결

- P8-10 Play 회귀는 사용자 검증 대기.
- VFX 카탈로그 10개 검토/승인은 후속 시각 정책 항목이다.
- Portal exit waypoint 이상, Tornado 종료 후 waypoint 복귀, 다중 레인은 Phase 9 Flow Field 대상이다.
- 공격 범위 표시 UI는 Phase 9 이후 UI 작업 후보로 residual에 유지한다.

---

**문서 버전**: v1.0 (구현 스펙 통합)
**상태**: 구현 완료. Unity 컴파일 0 에러 확인. P8-10 사용자 Play 회귀 대기.
