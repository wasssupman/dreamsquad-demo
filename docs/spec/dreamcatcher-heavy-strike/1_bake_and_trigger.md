# 1 — Bake + AttackN 발동 (강공 mul 산출·전달)

## 목적

카드의 `AttackN × HeavyStrike` 를 slot 으로 bake 하고, `AttackSystem` RESOLVE 에서 "이번 공격이 N회째인가"를 판정해 강공 배율(`heavyMul`)을 산출·투사체 캐리어에 전달한다. **이 단위는 배율을 만들고 나르기만 한다 — 실제 데미지 적용(투사체 hit-site 소비 + melee 곱)은 unit 2.** 따라서 unit 1 후에도 동작 변화 0(투사체가 field 를 실어도 HitSystem 미소비, melee 미변경).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` — 순수 predicate `WouldFire`.
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — HeavyStrike bake 검증 분기.
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — RESOLVE 강공 pre-scan + 투사체 request 에 `heavyDamageMul` 세팅 + dc-trigger 루프 HeavyStrike no-op 케이스.
- `Assets/_Project/Tests/EditMode/DcTriggerTests.cs` — `WouldFire` ↔ `Tick` 일치 테스트.

## 구현

### 1. `DcTrigger.WouldFire` (순수 predicate)

`Tick` 은 `counter++; counter>=period 면 fire+reset`. 카운터를 **증가시키지 않고** "다음 Tick 이 발동할지" 예측:

```csharp
// dreamcatcher-heavy-strike unit 1 — non-mutating peek: does the NEXT Tick fire?
// AttackSystem 강공 pre-scan 이 카운터 소유 루프(Tick)보다 먼저 "이번 공격이
// N회째인가"를 결정하게 한다. Tick 의 발동 조건과 정확히 일치(period!=0 &&
// counter+1>=period) → willFire == 루프의 dcFired. 카운터 쓰기는 여전히 루프만.
public static bool WouldFire(ushort counter, ushort period)
    => period != 0 && counter + 1 >= period;
```

### 2. Bridge bake 검증 (HeavyStrike)

generic slot bake(`slot.magnitude = m.payload.magnitude`)가 이미 배율을 채운다. SelfStatBuff 분기 뒤(`buf.Add(slot)` 앞)에 검증 분기 추가:

```csharp
else if (m.payload.kind == Wassup.Data.DcPayloadKind.HeavyStrike)
{
    // 응축된 일격 — AttackN 전용 강공. N회째 공격의 출력 데미지를 magnitude 배.
    // 다른 트리거로는 무의미 → AttackN 강제. 배율<=1 = 강공 아님(1 평타/<1 약화) 거절.
    // host 는 곱할 Damage output 이 있어야 함(eye 선례 재사용 — 힐러/output 없는 caster 거절).
    if (m.trigger.kind != AttackN)      → skip warn
    if (m.payload.magnitude <= 1f)      → skip warn
    if (!HasPositiveDamageOutput(defender)) → skip warn
    // slot.magnitude 는 이미 배율. 추가 세팅 없음.
}
```

AttackN period<=0 은 상단 공통 가드(`:246`)가 이미 거절.

### 3. AttackSystem — pre-scan + 캐리어

RESOLVE 의 fmPrio 블록(`:438`) 뒤, `hasOutputs`(`:440`) 앞:

```csharp
// dreamcatcher-heavy-strike unit 1 — AttackN×HeavyStrike 슬롯 pre-scan: 이번 공격이
// N회째(→강공)인가? 배율 곱집계(복사본 다수 = 곱). dc-trigger 루프가 Tick 할 것과
// 같은 pre-increment 카운터를 read-only(WouldFire) 로 예측 → 예측==dcFired. 카운터
// 쓰기 소유는 여전히 루프. 투사체는 ProjectileState.heavyDamageMul 로 hit-site 전달(unit 2).
float heavyMul = 1f;
if (defenderTagLookup.HasComponent(attackerEntity) && dcSlotLookup.HasBuffer(attackerEntity)) {
    var hs = dcSlotLookup[attackerEntity];
    for (int si = 0; si < hs.Length; si++) {
        var s = hs[si];
        if (s.trigger == AttackN && s.payload == HeavyStrike && DcTrigger.WouldFire(s.counter, s.period))
            heavyMul *= (s.magnitude > 0f ? s.magnitude : 1f);
    }
}
```

- 두 `ProjectileSpawnRequest`(ballistic `:485`, homing `:524`)의 `priorityDamageMul` 뒤에 `heavyDamageMul = heavyMul,` 추가.
- dc-trigger 루프(`:856~`)의 payload 디스패치에 **no-op** 케이스 추가(ApplyStack 뒤, unhandled `else` 앞):

```csharp
else if (slot.payload == HeavyStrike) {
    // 강공은 pre-scan + 캐리어로 이미 처리. 여기서는 carrier 발사 없음. 이 케이스는
    // 발동한 슬롯이 unhandled-payload 경고에 안 걸리게 하기 위함(루프의 역할=카운터 Tick).
}
```

### 4. EditMode

`DcTriggerTests.cs` 에 `WouldFire` 케이스: period=5 에서 counter 0~4 순회하며 `WouldFire(c,5)` 가 `c==4` 에서만 true, 그리고 동일 counter 로 `Tick` 이 낸 결과와 일치(예측=발동). period=0/1 경계.

## 완료 기준

- [x] `DcTrigger.WouldFire` 존재, `Tick` 발동 조건과 일치. EditMode 2케이스 작성(`WouldFire_MatchesTick_ForEveryCounterInPeriod`, `..Period1_AlwaysTrue_Period0_NeverFires`). ⚠ **실행 pending**: Unity/MCP 끊겨 test runner 미가동 — 작성+컴파일까지, Unity 복귀 시 실행.
- [x] Bridge: HeavyStrike 가 AttackN+magnitude>1+`HasPositiveDamageOutput` host 에서만 bake, 아니면 skip+warn. slot.magnitude=배율(generic bake). eye 가드 재사용.
- [x] AttackSystem: pre-scan heavyMul 곱집계(WouldFire), 두 투사체 request 에 `heavyDamageMul` 세팅, dc-루프 HeavyStrike no-op(unhandled 경고 회피).
- [x] compile green — `dotnet build` 런타임 0오류(경고14 기존) + `Wassup.Tests.EditMode` 0오류 0경고. 2026-07-14.
- [x] **동작 변화 0**: heavyDamageMul 은 투사체가 실어 나르기만(ProjectileHitSystem 미소비=unit 2), melee 미변경. pre-scan read-only(카운터 미변). 무카드 경로 무회귀.
