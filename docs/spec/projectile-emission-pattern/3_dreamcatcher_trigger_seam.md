# 3 — 드림캐쳐 트리거 seam

## 목적

"패턴을 트리거한다"를 드림캐쳐 페이로드 하나로 개통한다. emitter 는 드림캐쳐를 모르고, 드림캐쳐는 발사 내부를 모른다 — 접점은 **인스턴스 push** 다.

다른 트리거 소스(해저드 캐스트·카드·`AttackN`)로 확장하는 실비용은 push 1줄이 아니라 **3요건**이다(spec-review M3): ① 그 host 클래스에 `EmitterInstance` 버퍼 사전 부착 ② bake 밖의 template 조립 경로 ③ 해당 트리거 시스템의 arm. 이 unit 은 ②를 재사용 함수(`BuildPatternTemplate`)로 분리해 향후 비용을 ①+③으로 줄여둔다. host 없는 발사(bridge-cast 스킬·사망 유언)는 경로 자체가 별도다 — README 후속 후보.

## 변경 대상

- `Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.EmitProjectilePattern = 17` **append** + `DcPayloadSpec.pattern` 필드(`ProjectilePatternData`)
- `Battle/Combat/DcTriggerSlot.cs` — `short patternIndex` **1필드만** append (기본 −1)
- 신규 `Battle/Combat/Projectile/Emission/PatternSlot.cs` — 보스 전용 병렬 버퍼(spec + template)
- `Bridge/BattleBridge.cs` — `BuildPatternTemplate` + bake 분기 + `PatternSlot`/`EmitterInstance` 버퍼 부착
- `Battle/Combat/BossPeriodicTriggerSystem.cs` — payload 디스패치에 arm 1개

## 구현

### 정의 계층

`DcPayloadSpec` 에 `public ProjectilePatternData pattern;` 을 추가한다. 정의 계층은 SO 참조를 허용한다(기존 `projectile`·`auraPrefab` 선례) — 금지 대상은 `Entities`/`Battle` 타입이다.

`AreaBarrage`(5) 는 unit 4 에서 arm 이 제거되지만 enum 값은 **append-only 계약상 남는다**(기존 카드의 int 직렬화 보존). bake 가 loud 거절한다.

### `PatternSlot` — 보스 전용 병렬 버퍼 (spec-review M1)

`DcTriggerSlot` 은 defender 카드 슬롯과 공유하는 원소 타입이다(`[InternalBufferCapacity(2)]`). 여기에 spec+template(~200B)을 임베드하면 **모든 드림캐쳐 보유 유닛**의 chunk 상주 크기가 커진다 — 소비자는 보스뿐인데 과세는 전역이다. 그래서 패턴 자료는 별도 버퍼로 분리하고, `DcTriggerSlot` 에는 그 버퍼로의 index 하나만 둔다:

```
[InternalBufferCapacity(1)]
struct PatternSlot : IBufferElementData
{
    public PatternSpec spec;
    public ProjectileSpawnRequest template;
    public int fireCountBase;   // 영속 발사 카운터 (spec-review C2) — push 가 시드하고 증가시킨다
}
```

패턴 mechanic 이 없는 유닛은 이 버퍼가 아예 없다(부착 자체를 안 함) — 기존 유닛 비용 0.

### bake (유일한 SO 해석 seam)

`BakeNightmareMechanics` 의 payload 분기에 `EmitProjectilePattern` 케이스를 추가한다. template 조립은 **`BuildPatternTemplate(ProjectilePatternData, Entity owner, bool hostIsEnemy)` 재사용 함수**로 분리한다 — 향후 defender/카드 경로가 같은 함수를 호출한다(M3 의 요건 ② 선불).

1. 가드: `pattern == null` 또는 `pattern.barrel == null` → 경고 + skip(`AreaBarrage` 가드 선례).
2. `PatternSpec` 변환. `barrelDataIndex = GetOrCreateProjectileDataIndex(pattern.barrel)`.
3. template = `ProjectileSpawnRequest` 원본 조립. **기존 발사 지점들이 barrel SO 를 읽어 request 를 채우는 방식과 동일**하게 하고, 새 컨벤션을 만들지 않는다:
   - `ResolveProjectileAxes(pattern.barrel.flightMode)` → `movement`/`payload`
   - barrel 에서 request 에 대응 필드가 있는 것만: `speed`·`hitThreshold`·`visualScale`·`arcHeight`·`impactTileRange`·`onHitEffect`·`splashRadius`·`splashDamageMul` (기존 `SpawnUnit`/`AttackSystem` 의 `ProjectileRef` 조립 목록과 같다)
   - `damage = pattern.damage`, `dataIndex = barrelDataIndex`, `owner = entity`
   - `targetFaction` = host 진영의 반대(계약 7). 적 host → `Defender`.
   - **드레인이 SO 에서 직접 읽는 값은 싣지 않는다** — `dropHeight`(기존), 베지어 `lateral`/`forwardBias`(unit 1).
   - 타겟 의존 필드(`target`/`impact`/`swingIndex`)는 **비운다** — emitter 가 채운다.
4. `PatternSlot` 버퍼에 `{spec, template, fireCountBase: 0}` append, `slot.patternIndex` = 그 원소 index. `EmitterInstance` 버퍼도 함께 사전 부착(`AddBuffer`, 멱등) — 런타임 구조 변경 회피, `IncomingDamage`/`IncomingHeal` 선례.

### arm (`BossPeriodicTriggerSystem`)

`PeriodicTimer` 슬롯이 발화했을 때의 payload 디스패치에 케이스를 추가한다. 하는 일은 전부:

```
if (slot.payload == EmitProjectilePattern) {
    // 버퍼/index 부재 = bake 누락 → 조용히 skip (발화는 소모)
    if (slot.patternIndex < 0 || !patternLookup.HasBuffer(entity)) continue;
    var pat = patternLookup[entity][slot.patternIndex];
    var inst = new EmitterInstance { spec = pat.spec, template = pat.template };
    EmitterTick.Begin(ref inst.runtime, inst.spec, baseFireCount: pat.fireCountBase);
    pat.fireCountBase += pat.spec.shotCount;    // 영속 카운터 전진 (C2) — 다음 발화가 이어받는다
    patternLookup[entity][slot.patternIndex] = pat;
    instanceLookup[entity].Add(inst);
}
```

`spec`/`template` 을 **값으로 복사**하므로 발사 도중 무엇이 바뀌어도 이미 시작된 버스트는 불변이다(계약 8). 영속시켜야 하는 것은 발사 카운터 하나뿐이고, 그것만 durable 소유자(`PatternSlot`)에 남는다 — RoundRobin 순회와 셔플의 결정론 진행이 트리거 발화를 넘어 이어진다.

`HealthThreshold` 트리거에서도 같은 payload 를 쓸 수 있게 하려면 `HealthThresholdSystem` 에 동일 arm 3줄을 추가하면 된다 — **v1 에서는 하지 않는다**(소비자 0, 미사용 라이브 경로 금지). 붙는 비용만 여기 기록한다.

## 완료 기준

- 컴파일 클린. 기존 카드/보스 mechanic 무회귀(신규 필드 default: `pattern=null`·`patternIndex` 는 bake 가 −1 초기화 — struct default 0 이 유효 index 라 **bake 에서 명시 −1 세팅 필수**).
- 보스 SO 에 패턴 mechanic 을 임시로 하나 넣으면 발사가 관측된다 — 정식 authoring 은 unit 4·5. 이 unit 의 검증은 **임시 배선 후 콘솔/스크린샷 1회**로 seam 만 확인하고 되돌린다.
- ecs-review 대상: bake 의 managed 접근이 bridge 안에만 있는지 · 슬롯 임베드가 unmanaged 유지되는지(`ProjectileSpawnRequest` 는 이미 unmanaged) · arm 이 Combat 쓰기만 하는지.
