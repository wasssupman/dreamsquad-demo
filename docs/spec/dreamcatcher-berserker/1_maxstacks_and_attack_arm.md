# 1 — 최대 중첩 저작 개방 + 「공격 N회 × 자기 버프」 arm

## 목적

두 가지를 한 커밋으로 한다. 나눌 수 없어서다 — 저작 칸만 열면 그 값을 읽는 arm 이 없고,
arm 만 만들면 실어 보낼 상한이 없다.

- 카드가 「이 버프는 N중첩까지 쌓인다」를 말할 수 있게 한다(저작 자리 = `tileRange`).
  자기 버프를 거는 **세 지점 전부**가 상한을 실어 보낸다 — 처치 갈래에는 이미 산 카드가
  둘(짱빠른·짱쎈버서커) 있어서, 이 커밋 직후 그 둘은 **시트 저작만으로** 누적이 된다.
- **「공격 N회 × 자기 버프」는 지금 붙지만 안 터진다** — 부착 판정도 통과하고 슬롯도
  구워지는데 공격 성사 지점의 payload 사슬에 자기 버프 갈래가 없어서, 발동하면
  `"DcTriggerSlot fired with unhandled payload kind"` 경고만 남기고 카운트를 태운다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` (주석 — `SelfStatBuff` 의 `tileRange` 의미)
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierAuthoring.cs` (`StackCap`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` (`SelfStatBuff` bake, ~765행)
- `Assets/_Project/Scripts/Battle/Combat/HealthThresholdSystem.cs` (경계 arm, ~131~153행)
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` (처치 arm, ~390~425행)
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` (공격 arm **신설**, ~1848~1927행)
- 신규: `Assets/_Project/Tests/EditMode/FrenzyStackingTests.cs`

## 구현

**① 상한식 — 한 곳에서.** 호출처가 arm 3곳이고 `-1` 규약이 비자명하다(아래 ②의 이유).
`FromMultiplier` 바로 옆에 둔다 — 그 규약 위에 서는 식이라 떨어뜨리면 근거가 사라진다.

```csharp
// 저작: multiplier=배율(1.08) · maxStacks=최대 중첩. 반환 = 누적 상한(가산 버킷 기준).
// 0 = 누적 안 함. `-1` 은 FromMultiplier 가 버프를 **가산 버킷**으로 보내기 때문.
public static float StackCap(float multiplier, int maxStacks)
    => maxStacks > 0 && multiplier > 1f ? (multiplier - 1f) * maxStacks : 0f;
```

**② bake — 최대 중첩을 슬롯에 싣는다.** `SelfStatBuff` 분기에
`slot.tileRange = math.max(0, m.payload.tileRange);` 한 줄. 지금 이 분기만 tileRange 를
안 싣는다(다른 payload 분기는 전부 자기 칸을 채운다).

**③ bake — 거절 하나.** `tileRange > 0` 인데 배율이 1 이하면 loud 경고 + 그 mechanic 만
skip(강타의 「트리거 강제」 선례와 같은 모양). 1 미만은 `FromMultiplier` 가 **곱셈 버킷**
으로 보내는데 곱셈 값을 더하면 의미가 없다(0.9 + 0.9 = 1.8 = 강화). 문구에 카드 id ·
mechanic 번호 · 문제된 값을 실을 것.

**트리거별 거절은 두지 않는다** — 세 arm 이 전부 상한을 싣기 때문에 최대 중첩의 뜻이
트리거에 따라 갈리지 않는다(README 계약 7).

**④ 기존 arm 2곳.** 경계·처치 arm 의 `StatModifierApplyEvent` 조립에 한 줄씩 더한다.
둘 다 이미 `FromMultiplier` 로 op/magnitude 를 뽑고 있으니 그 옆줄이다.

```
magnitudeCap = ModifierAuthoring.StackCap(slot.magnitude, slot.tileRange)
```

**⑤ 공격 arm 신설.** RESOLVE 의 payload 사슬(`ProjectileToTarget` / `ApplyCcToTarget` /
`ApplyStackToTarget` / `HeavyStrike` / unhandled 경고)에 자기 버프 갈래를 더한다. 조립은
경계 arm 과 **같은 모양**이다 — 같은 `FromMultiplier`, 같은 `statBuffStackId`, 같은
「지속 <=0 = 영구」 해석.

```
target = attackerEntity · source = attackerEntity · stackId = slot.statBuffStackId
op/magnitude = FromMultiplier(slot.magnitude)
duration     = slot.duration > 0 ? slot.duration : ∞
magnitudeCap = ModifierAuthoring.StackCap(slot.magnitude, slot.tileRange)
origin       = ModifierOrigin.Dreamcatcher
```

**⚠ `origin` 은 경계 arm 을 복사하지 말 것.** 그 값(`HealthThreshold`)은 「빈사에서
켜졌다」는 뜻이라 상태FX 가 다르게 읽는다. 공격으로 쌓이는 이 버프는 드림캐쳐 출처다.

**⚠ 적 host 를 막지 않는다.** 바로 위 `ProjectileToTarget` 은 적이 쓰면 자기 진영을 쏘기
때문에 진영 가드가 있지만, 자기 버프는 대상이 자기 자신뿐이라 오사 경로가 없다.

**⚠ 채널 부재 가드.** 경계 arm 과 같이 큐가 없으면 조용히 건너뛴다(카운트는 이미 소비됨).

**연출 신호는 건드리지 않는다.** 발동 신호는 이 갈래보다 위에서 이미 나가고 주기가 1이라
매 공격 나간다. 브리지에 0.25초 스로틀이 있고, **비수(`poke_needle`)가 이미 매 공격
발동으로 돌고 있다**(시트 실측) — 선례가 있으므로 여기서 예외를 만들지 않는다.

## 완료 기준

- [ ] 컴파일 통과
- [ ] EditMode — `StackCap`: 중첩 0/1 은 0(안 쌓임) · 배율 1 이하는 0 · 1.08×10 = 0.8
- [ ] EditMode — bake: 최대 중첩이 슬롯에 실린다 · 배율 1 이하 + 중첩>0 은 거절되고 경고가 뜬다
- [ ] EditMode — 최대 중첩을 안 적은 자기 버프는 슬롯 tileRange 가 0 이고 예전처럼 덮어쓴다
- [ ] ⚠ 회귀 핀은 **코드에서 카드를 조립해** 건다. 시트에서 꺼진 에셋(빈사폭주)에 걸면
      그 카드가 정리되는 날 테스트가 같이 죽고, 켜진 카드에 걸면 시트가 값을 바꿀 때마다
      빨개진다
- [ ] EditMode — 공격 1회 = 슬롯 1개 · 배율 1단계 · 지속이 저작값
- [ ] EditMode — 공격 3회 = magnitude 가 3단계로 자라고, **상한 초과 공격에도 지속은 갱신된다**
- [ ] EditMode — 공격을 멈추고 지속이 지나면 슬롯이 사라진다(전량 소멸)
- [ ] Console — 이 조합에서 `unhandled payload kind` 경고가 더 이상 안 뜬다

> 확인 2026-08-17 — EditMode 2504 중 2501 통과 · 0 실패. 커밋 `e4afa642`.
> ⚠ 공격 arm 은 병행 세션이 `a4818537`(넉백 작업)에 함께 커밋했다 — 아래 인계 요약 참조.
