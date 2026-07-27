# 2 — Emitter 시스템 (아키텍처 계층)

## 목적

unit 0 의 순수 로직을 ECS 에 바인딩한다. 활성 발사 인스턴스를 tick 하고, 산출된 `ShotOrder` 를 기존 `ProjectileSpawnRequest` 캐리어로 번역한다. **투사체 라이프사이클은 신설하지 않는다**(계약 5).

## 변경 대상

- 신규 `Battle/Combat/Projectile/Emission/EmitterInstance.cs`
- 신규 `Battle/Combat/Projectile/Emission/ProjectileEmitterSystem.cs`

## 구현

### `EmitterInstance` (IBufferElementData, Combat)

```
[InternalBufferCapacity(2)]
struct EmitterInstance : IBufferElementData
{
    public PatternSpec    spec;      // 순수 명세 — 시작 시 값 스냅샷 (계약 8)
    public EmitterRuntime runtime;   // 순수 스케줄 상태
    public ProjectileSpawnRequest template;  // ← ECS 바인딩 1
    public Entity lockedTarget;      // ← ECS 바인딩 2 — reselectPerShot=false 잠금 신원 (spec-review H1)
}
```

**이 배치가 계약 1·2 를 지키는 방식**: 순수 부분(`spec`·`runtime`)은 아키텍처 컴포넌트 안에 **값으로 박히기만** 하고 아키텍처 타입을 참조하지 않는다. ECS 전용 자료는 `template`·`lockedTarget` 둘이며, Mono 이식 시 그 자리에 Mono 용 발사 파라미터/참조가 들어간다. 스케줄 상태가 `template` 을 **품는** 형태(현 `VolleyFireState`)는 순수 코어를 이식 불가로 만들므로 쓰지 않는다.

잠금 semantics: `reselectPerShot=false` 면 **첫 성공 선택**에서 해석한 Entity 를 `lockedTarget` 에 저장하고 이후 발은 재사용한다(index 재사용 금지 — 후보 스냅샷은 프레임-로컬이다). 잠금 대상이 버스트 도중 소멸하면(`LocalTransform` 부재) 남은 발은 조용히 소모한다.

`template` 은 bake(unit 3)가 SO 를 읽어 만든 request 원본이다 — `movement`/`payload`/`dataIndex`/`speed`/`arcHeight`/`impactTileRange`/`splash*`/`bezier*`/`owner`/`targetFaction` 등이 채워져 있고, **타겟 의존 필드(`target`/`impact`/`direction`/`control1`/`control2`)만 비어 있다**. emitter 가 발마다 그 빈칸을 채운다. `template.dataIndex` 는 `spec.barrelDataIndex` 에서 복사된 파생값이다(계약 3 의 "복제" 가 아님 — 명세는 중립 계층에서 완결되고 아키텍처가 자기 형태로 파생시킨다).

### `ProjectileEmitterSystem` (ISystem, Combat, `BattleSimGroup`)

```
[UpdateAfter(typeof(BossPeriodicTriggerSystem))]   // push 와 같은 프레임에 첫 발 (spec-review L1)
OnCreate: RequireForUpdate<EmitterInstance>, RequireForUpdate<FlowFieldSingleton>
```

매 프레임:

1. **후보 스냅샷** — host 진영에서 도출(계약 7). 적 host → 방어유닛 풀, 방어유닛 host → 적 풀. `BossPeriodicTriggerSystem` 의 whip 풀처럼 **첫 소비 때 lazy 빌드**(인스턴스 0 이면 쿼리·할당 0). 풀 = parallel 2배열: `NativeArray<Entity>` + `NativeArray<int2>` 셀.
2. **인스턴스 tick** — `EmitterInstance` 버퍼를 순회하며 `EmitterTick.Advance` 로 이번 프레임 발사 수를 얻는다.
3. **발사** — 발마다:
   - `PatternTargeting.Select(cells, spec.selection, runtime.fireCount, gridSize)` → 후보 index
   - `PatternLogic.BuildOrder(spec, ref runtime, idx)` → `ShotOrder`. 타겟 잠금(`reselectPerShot`) 판단은 **여기 안에서** 끝난다 — 아키텍처가 되풀이하지 않는다.
   - `order.targetCandidateIndex < 0`(후보 0) → 그 발을 조용히 소모(융단폭격의 "방어유닛 0 = 발사 소모, 위상 보존" 선례).
   - `template` 복사 → `order` 의 값(`damage`·`telegraphSec`)을 얹고 빈칸을 채운다. **분기 축은 개별 `MovementKind` 가 아니라 타겟 바인딩 클래스다**(README 계약 11) — 발사 시점에 궤적이 요구하는 것은 "엔티티냐 셀이냐 방향이냐" 뿐이므로, 순수 헬퍼 `MovementBinding.Of(MovementKind)` (3값 enum 반환, unit 0 에 동거)로 분류한다. 기존 바인딩을 재사용하는 새 궤적은 emitter 변경 0:
     - **EntityBound** (`HomingToEntity`·`BezierHomingToEntity`) → `target` = 잠금/선택 해석 결과(위 잠금 semantics). `swingIndex = order.shotIndex` 세팅(비-베지어 궤적은 이 필드를 안 읽어 무해) — 제어점 산출은 드레인 몫이다(unit 1, `dropHeight` 선례). **emitter 는 SO 를 읽지 않는다.**
     - **CellBound** (`SkyFall`·`BallisticArcToPoint`·`GrenadeToCell`) → `impact = GridMath.CellToWorldCenter(cells[order.targetCandidateIndex], …)`, `flightTime = order.telegraphSec`(SkyFall 이 소비 · BallisticArc 는 드레인이 speed 로 재산출해 무시 · Grenade 는 굴림 시간으로 해석 — "도착 지연" 일반화). v1 실소비자는 SkyFall(폭격)뿐이지만 분기 코드가 클래스 공유라 별도 arm 이 늘지 않는다 — spec-review M2 의 "미소비 arm" 문제가 분기 축 교정으로 소멸.
     - **DirectionBound** (`DirectionalLinear`) → **미개통**: loud warn 후 소모. 방향 발사는 타겟 후보 선택과 결이 다르고(무타겟 패턴) `maxDistance` 출처도 미정 — 후속 후보 "무타겟 패턴" 에서 함께 연다.
   - `origin` = host 위치.
   - **outputs 버퍼는 싣지 않는다** — SingleSplash 해결은 outputs 부재 시 `state.damage` 폴백을 탄다(`ProjectileHitSystem` 확인, dc 니들/스킬 발사 선례). 이 폴백이 load-bearing 이다: 패턴 데미지는 Damage-only 계약이고, **non-Damage outputs(Stat/Stack/Heal)는 패턴으로 나가지 않는다**(범용성 한계 — README 후속 후보).
   - 캐리어 엔티티 생성: `ecb.CreateEntity()` + `ProjectileSpawnRequest` + `ProjectileRequestCarrier` — 기존 3개 stage 지점과 동형이라 브리지 드레인이 스폰 후 캐리어를 파괴한다.
4. **완주 제거** — `runtime.burstRemaining == 0` 인 인스턴스를 버퍼에서 swap-back 제거. 버퍼 자체는 남긴다(구조 변경 0).

### 경계

- `AttackState`/`AiState`/이동/쿨다운을 **건드리지 않는다** — 발사는 기본공격과 직교다(nightmare-catcher 계약 4).
- CC(Sleep/Stun)는 게이트하지 않는다. 현 보스 스킬(융단폭격·채찍질)과 동일한 기존 사양이며, 잠든 보스가 계속 쏘는지는 별도 결정 사항이다(nightmare 보스 조사 2026-07-27 에서 확인된 공백 — 이 spec 밖).
- `ProjectileEmitterSystem` 은 `AttackSystem` 과 순서 제약이 없다(같은 프레임 캐리어 2개가 독립).

## 완료 기준

- 컴파일 클린 (`refresh_unity scope=all`).
- 이 unit 만으로는 어떤 발사도 일어나지 않는다(`EmitterInstance` 를 만드는 주체가 unit 3). `RequireForUpdate` 로 시스템이 조용히 idle.
- ecs-review 대상: 맥락 경계(Combat 내부 쓰기만) · `Allocator.Temp` 배열 전량 Dispose · lazy 풀 · ECB Playback 1회 · Burst 호환 · teardown(`ProjectileTag` 캐리어 경로 상속).
- 기존 EditMode/PlayMode 무회귀.
