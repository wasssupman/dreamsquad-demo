# 3 — 드림캐쳐 트리거 seam

## 목적

"패턴을 트리거한다"를 드림캐쳐 페이로드 하나로 개통한다. emitter 는 드림캐쳐를 모르고, 드림캐쳐는 발사 내부를 모른다 — 접점은 **인스턴스 push 한 줄**이다. 이 seam 이 열리면 해저드 캐스트·카드·`AttackN` 도 같은 방식으로 붙는다(각각 push 1줄).

## 변경 대상

- `Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.EmitProjectilePattern = 17` **append** + `DcPayloadSpec.pattern` 필드(`ProjectilePatternData`)
- `Battle/Combat/DcTriggerSlot.cs` — `PatternSpec pattern` + `ProjectileSpawnRequest patternTemplate` 임베드
- `Bridge/BattleBridge.cs` — `BakeNightmareMechanics` 에 패턴 bake 분기 + `EmitterInstance` 버퍼 사전 부착
- `Battle/Combat/BossPeriodicTriggerSystem.cs` — payload 디스패치에 arm 1개

## 구현

### 정의 계층

`DcPayloadSpec` 에 `public ProjectilePatternData pattern;` 을 추가한다. 정의 계층은 SO 참조를 허용한다(기존 `projectile`·`auraPrefab` 선례) — 금지 대상은 `Entities`/`Battle` 타입이다.

`AreaBarrage`(5) 는 unit 4 에서 arm 이 제거되지만 enum 값은 **append-only 계약상 남는다**(기존 카드의 int 직렬화 보존). bake 가 loud 거절한다.

### bake (유일한 SO 해석 seam)

`BakeNightmareMechanics` 의 payload 분기에 `EmitProjectilePattern` 케이스를 추가한다.

1. 가드: `pattern == null` 또는 `pattern.barrel == null` → 경고 + skip(`AreaBarrage` 가드 선례).
2. `slot.pattern` = SO → `PatternSpec` 변환. `barrelDataIndex = GetOrCreateProjectileDataIndex(pattern.barrel)`.
3. `slot.patternTemplate` = `ProjectileSpawnRequest` 원본 조립. **기존 발사 지점들이 barrel SO 를 읽어 request 를 채우는 방식과 동일**하게 하고, 새 컨벤션을 만들지 않는다:
   - `ResolveProjectileAxes(pattern.barrel.flightMode)` → `movement`/`payload`
   - barrel 에서 request 에 대응 필드가 있는 것만: `speed`·`hitThreshold`·`visualScale`·`arcHeight`·`impactTileRange`·`onHitEffect`·`splashRadius`·`splashDamageMul` (기존 `SpawnUnit`/`AttackSystem` 의 `ProjectileRef` 조립 목록과 같다)
   - `damage = pattern.damage`, `dataIndex = barrelDataIndex`, `owner = entity`
   - `targetFaction` = host 진영의 반대(계약 7). 적 host → `Defender`.
   - **드레인이 SO 에서 직접 읽는 값은 싣지 않는다** — `dropHeight`(기존), 베지어 `lateral`/`forwardBias`(unit 1).
   - 타겟 의존 필드(`target`/`impact`/`direction`/`swingIndex`)는 **비운다** — emitter 가 채운다.
4. `EmitterInstance` 버퍼를 host 에 사전 부착(`AddBuffer`, 멱등). 런타임 구조 변경 회피 — `IncomingDamage`/`IncomingHeal` 선례.

`ProjectileSpawnRequest` 를 슬롯에 임베드하는 비용은 슬롯당 ~150B 다. 보스 1기 × 슬롯 3개면 무시 가능하고, 신규 싱글턴/레지스트리를 만들지 않아 계약 5 를 지킨다. 패턴 수가 늘거나 방어유닛 전체가 패턴을 쓰게 되면 레지스트리 싱글턴으로 옮긴다(후속 후보).

### arm (`BossPeriodicTriggerSystem`)

`PeriodicTimer` 슬롯이 발화했을 때의 payload 디스패치에 케이스를 추가한다. 하는 일은 전부:

```
if (slot.payload == EmitProjectilePattern) {
    // 버퍼 부재 = bake 누락 → 조용히 skip (발화는 소모)
    if (!SystemAPI.HasBuffer<EmitterInstance>(entity)) continue;
    var inst = new EmitterInstance { spec = slot.pattern, template = slot.patternTemplate };
    EmitterTick.Begin(ref inst.runtime, inst.spec);
    instances.Add(inst);
}
```

`spec`/`template` 을 **값으로 복사**하므로 발사 도중 무엇이 바뀌어도 이미 시작된 버스트는 불변이다(계약 8).

`HealthThreshold` 트리거에서도 같은 payload 를 쓸 수 있게 하려면 `HealthThresholdSystem` 에 동일 arm 3줄을 추가하면 된다 — **v1 에서는 하지 않는다**(소비자 0, 미사용 라이브 경로 금지). 붙는 비용만 여기 기록한다.

## 완료 기준

- 컴파일 클린. 기존 카드/보스 mechanic 무회귀(신규 필드 default = null/0 이라 전부 inert).
- 보스 SO 에 패턴 mechanic 을 임시로 하나 넣으면 발사가 관측된다 — 정식 authoring 은 unit 4·5. 이 unit 의 검증은 **임시 배선 후 콘솔/스크린샷 1회**로 seam 만 확인하고 되돌린다.
- ecs-review 대상: bake 의 managed 접근이 bridge 안에만 있는지 · 슬롯 임베드가 unmanaged 유지되는지(`ProjectileSpawnRequest` 는 이미 unmanaged) · arm 이 Combat 쓰기만 하는지.
