# 1 — 재앙의 심장

## 목적

기존 시한부 공속 강화·주기 강공·사망 폭발을 조합해, 유닛 하나를 잃는 대신 짧은 폭발력을 얻는 무의식 유물을 만든다.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/Card_SubDeepSleep.asset/.meta`
  → `Card_CalamityHeart.asset/.meta` (GUID 보존 rename)
- `Assets/_Project/Tests/PlayMode/DreamcatcherCursedRelicTest.cs`

## 구현

```text
id / displayName = calamity_heart / 재앙의 심장
type / category  = Unit / Subconscious
axis             = All
effects          = []
attackMods       = []

mechanics[0] = None × SelfBuffLethal
               magnitude=100, duration=6
mechanics[1] = AttackN(period=3) × HeavyStrike
               magnitude=2.0
mechanics[2] = OnDeath × SelfTileAoe
               magnitude=400, tileRange=2
               projectile=Card_Farewell의 기존 AOE ProjectileData
```

설명:

> 부착 즉시 공격속도가 100% 증가하고 세 번째 공격마다 피해가 2배가 된다. 6초 뒤 사망하며, 주변 2타일에 400 피해를 준다.

플레이스홀더를 재저작할 때 기존 Squad `effects`를 비운다. 에셋과 meta는 함께 rename해 GUID와 카탈로그 참조를 보존한다.

## 완료 기준

- [ ] SO가 위 3개 mechanic과 값을 정확히 가지며 `effects`가 비어 있다.
- [ ] 부착 직후 공격속도 +100%, `LethalTimer.remaining≈6`이다.
- [ ] 3·6번째 공격이 기존 HeavyStrike ×2 계약을 따른다.
- [ ] 시간 만료 또는 조기 사망 시 OnDeath AOE와 카드 회수가 한 번 발생한다.
- [ ] 사망 셀 2타일 내 적은 400 피해를 받고 범위 밖 적은 받지 않는다.
- [ ] 기존 `마지막 불꽃`·`응축된 일격`·`작별 선물` 동작을 깨지 않는다.
