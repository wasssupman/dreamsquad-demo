# 4 — 악몽 사냥 카드 (레인 B)

## 목적

잠든 적을 때리면 **그 타격의 피해가 2배**. 트리거가 아니라 **상시 공격 변조**(attackMod)다.

판정 위치는 host 에 따라 갈린다 (Track A 리뷰 H1 반영):
- **근접/AoE** — `hitTarget` 별. 잠든 적 옆의 깨어 있는 적은 **그대로**다(이 카드가 강공과
  갈리는 지점이며 테스트가 고정한다).
- **원거리** — 발사 시점 `bestTarget` 기준으로 **탄의 damage 에 구워진다.** 따라서 그 탄의
  splash·bounce·관통 2차 피해가 배율을 **승계**한다. `shatter_hymn`(`DamageVsCcMul`)의 기존
  관례를 그대로 따른 것이며, 단발 호밍(궁수)처럼 2차 피해가 없는 host 에서는 차이가 없다.
  근본 해결은 README 후속 후보(두 카드를 같이 옮겨야 한다).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/DcAttackModSlot.cs` (필요 시 — 신규 필드 0 이 목표)
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Data/Dreamcatcher/Card_NightmareHunt.asset` **(신규)**
- `Assets/_Project/Tests/` — 아래 완료 기준 참조

> enum(`DcAttackModKind.DamageVsSleeping`) · 적용성 · bake · 문안은 **unit 0 이 이미 놓았다.**
> 이 unit 은 **AttackSystem 에서 실제로 곱하는 일**만 한다.

## 구현

### 1) 그대로 따라 쓸 선례 — `shatter_hymn`

`AttackSystem` 에는 이미 "대상이 CC 상태면 피해 배율"이 있다(`attackerVsCc`,
`StatKind.DamageVsCcMul`). **적용 지점 2곳이 이 unit 이 건드릴 전부**다:

| 지점 | 판정 대상 |
|---|---|
| 투사체 output 조립 | 발사 시점 `bestTarget` 스냅샷 |
| 근접/AoE 즉시 해결 | `hitTarget` 별 (cleave 전 대상 각각) |

두 곳 모두 `AnyActiveCc(ccActionLookup[victim])` 를 부르는 자리가 이미 있다. 그 옆에 형제를 놓는다.

### 2) 배율 집계

RESOLVE 진입부에서 host 의 `DcAttackModSlot` 버퍼를 훑어
`kind == DamageVsSleeping` 인 슬롯의 `damageMul` 을 **곱으로 집계**(같은 카드 2장 부착 시 중첩 —
`ProjectileBounce` 집계와 같은 관례). 슬롯이 없으면 `1f` = 무영향.
버퍼가 없는 host(대부분)에서 **추가 비용이 0**이어야 한다 — `HasBuffer` 가드 먼저.

### 3) 수면 판정 순수 헬퍼

`AnyActiveCc` 바로 옆에 형제를 만든다:
```
private static bool AnyActiveSleep(in DynamicBuffer<CcEffect> buf)
    // remainingTime > 0 && kind == CcKind.Sleep
```
`AnyActiveCc` 를 파라미터화(“어떤 kind?”)하지 않는다 — 호출처가 2벌뿐이고 각각 자기 술어를
가지는 편이 읽기 쉽다(제약 8: 소비자 없는 추상화 금지).

### 4) "2배"가 걸리는 자리 — 최종 결과 기준 정확히 2배다

배율은 **공격자가 내보내는 피해**에 곱한다. 전체 체인:

```
Combat  : output.magnitude × damageMul × attackerVsCc × [수면 ×2] × fmPrioMul × heavyMul
          → IncomingDamage 버퍼 append
Units   : × dmgTakenMul  →  실드 흡수  →  HP 차감      (DamageApplicationSystem)
```

전부 곱이므로 **같은 적을 재웠을 때 vs 안 재웠을 때의 최종 HP 감소도 정확히 2배**이고,
화면에 뜨는 데미지 숫자도 2배다(표시 데미지는 `dmgTakenMul` 적용 후 값).
**예외는 실드 하나** — 실드가 흡수하면 HP 감소만 보면 2배가 아니다(흡수량까지 합쳐야 2배).

`shatter_hymn`(`DamageVsCc`)과 함께 걸리면 잠든 적에게 **둘 다** 적용된다 — 수면은 CC 이기도
하므로 의도된 곱 중첩이다. 테스트로 고정.

⚠ **계약 5 — 잠을 깨우는 그 타격이 2배를 받는다.** 피해 계산(Combat)과 수면 해제
(`CcClearRequests`, Units→Effects)가 다른 시스템이라 구조적으로 그렇게 된다. 버그가 아니다.

### 5) 카드 에셋 — `Card_NightmareHunt.asset`

`id=nightmare_hunt` · `displayName="악몽 사냥"` · `type=Unit` · `art=null` ·
`attackMods[0] = { kind = DamageVsSleeping, damageMul = 2.0 }` · `mechanics` 는 비운다.
`description` 은 formatter 정확 미러.

## 완료 기준

- 컴파일 0 에러 · 콘솔 경고 0.
- **PlayMode 계측 2건**(`DreamcatcherCombatDamageTest` 패턴 재사용):
  ① 잠든 더미가 받은 피해 = 기준값 × 2
  ② **같은 공격의 깨어 있는 더미는 기준값 그대로** ← 이 카드가 강공(HeavyStrike)과 다른 이유를
     고정하는 단정. 이게 빠지면 사양 초과가 조용히 통과한다.
- **무회귀**: `shatter_hymn`(DamageVsCc) 기존 PlayMode green. 둘 다 걸었을 때 중첩(×2 ×n)이
  나오는 것을 케이스 1건으로 남긴다.
- 컴파일까지만 확인하고 **커밋하지 않는다**(README 계약 P3). 테스트 실행은 오케스트레이터가 한다.

---

확인 완료 2026-08-16 (사용자 Play 확인) — 커밋 `71da7335`
