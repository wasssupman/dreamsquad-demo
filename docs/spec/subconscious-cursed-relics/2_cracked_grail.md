# 2 — 금이 간 성배

## 목적

전 스쿼드의 생존력을 대가로 지속 화력을 얻는 hosted Squad 무의식 유물을 만든다. 호스트가 죽으면 강화와 저주가 함께 철회된다.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/Card_SubDreamHaste.asset/.meta`
  → `Card_CrackedGrail.asset/.meta` (GUID 보존 rename)
- `Assets/_Project/Tests/PlayMode/DreamcatcherEffectTest.cs`
- `Assets/_Project/Tests/EditMode/DreamcatcherCardTextTests.cs`

## 구현

```text
id / displayName = cracked_grail / 금이 간 성배
type / category  = Squad / Subconscious
axis             = All
mechanics        = []
attackMods       = []
effects[0]       = AttackDamage +70%
effects[1]       = EffectiveHealth -40%
```

설명:

> 호스트가 살아있는 동안 모든 아군의 공격력이 70% 증가하지만 체력이 40% 감소한다.

기존 매핑에 따라 `DamageMul=1.7`, `DmgTakenMul≈1.667`이 된다. 두 효과는 같은 hosted handle을 사용하고 기존 revoke 경로로 함께 중립화한다. 음수 표시에는 기존 카드 텍스트 포맷을 사용한다.

## 완료 기준

- [ ] SO가 `Squad/Subconscious/All`과 위 두 effect를 정확히 가진다.
- [ ] 현재·이후 배치 아군이 `damageMul=1.7`, `dmgTakenMul≈1.667`을 받는다.
- [ ] 일반 피해 공격 기준으로 가한 피해 약 70%, 받은 피해 약 67% 증가를 확인한다.
- [ ] 한 번의 revoke 후 두 값이 다른 효과가 없을 때 1.0으로 돌아온다.
- [ ] 카드 본문에 `Attack +70%`, `Health -40%`와 위험 설명이 표시된다.
- [ ] 신규 modifier kind·철회 채널·UI 타입이 없다.
