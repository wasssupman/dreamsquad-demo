# 9 — 스트레스 창 (등장 조건에 마음을 엮는다)

## 목적

「30킬마다 뜬다」에 **마음의 여유**를 AND 로 건다. 스트레스가 높을 때는 보너스 판이 열리지
않고, 마음이 진정되면 그때 열린다.

> 사용자 결정 2026-08-24: *"스트레스 수치가 30 이하에서만 등장. 킬 threshold 30 마다 등장.
> 첫 30킬에서 스트레스가 30 이상이면 등장하지 않다가 30 이하가 되면 그때 등장."*

선행: `docs/spec/heart-stress-axis/` — 스트레스는 마음 체력의 **표시 반전**이고
(`StressMath.FromHealth`) 0 = 만피, 100 = 마음 파괴다. 별도 리소스가 아니다.

## 두 축의 성격이 다르다

| 축 | 성격 | 조건이 안 맞으면 |
|---|---|---|
| 킬 크레딧 | 쌓이는 **자원** | **쌓인다**(소멸하지 않는다) |
| 스트레스 | 그 자원을 쓸 수 있는 **창** | 열릴 때까지 기다린다 |

「스트레스가 소비 조건」이 아니다 — **창이 열리는 순간(버튼이 뜨는 순간)에만** 판정한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/BonusWaveData.cs` — `maxStressToOffer`
- `Assets/_Project/Scripts/Data/BonusPullTrigger.cs` — `HasCredit` / `StressAllows` / `NextLatched`
- `Assets/_Project/Scripts/Bridge/BattleBridge.BonusWave.cs` — `CurrentStress` · `_bonusOfferLatched` · `TickBonusPullOffer`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 펌프 호출 위치
- `Assets/_Project/Tests/EditMode/BonusPullTriggerTests.cs` · `Tests/PlayMode/BonusWavePullTest.cs`

## 구현 — 막아야 할 구멍 4개

1. **떨림** — 스트레스는 매 프레임 오르내린다(맞으면 오르고 잡으면 내려간다). 매 프레임
   재평가하면 문턱 근처에서 버튼이 깜빡인다. → **래치**. 「30 이하」는 **등장 조건이지 유지
   조건이 아니다.** 한 번 뜨면 소비할 때까지 유지되고, 소비하면 다음 크레딧은 다시 창을
   통과해야 한다. 히스테리시스(등장 30 / 퇴장 40)보다 단순하고 떨림이 구조적으로 불가능하다.

2. **크레딧 증발** — 소비를 `consumed = normalKills` 로 두면 스트레스에 막혀 쌓인 초과분이
   통째로 사라진다(스트레스 높은 채 90킬 → 3회분이 1회로). → **`consumed += killThreshold`**.
   한 회분만 쓰고 나머지는 남는다. 동시 1벌(계약 13)이 있어 겹치지 않는다.

3. **마음 없는 맵** — `StressMath.FromHealth` 는 `max <= 0` 이면 100 이 아니라 **0** 을 준다
   (판 시작 즉시 종료를 막는 폴백). 그래서 마음 미저작 맵은 창이 항상 열린다. **fail-open 이
   맞다** — 게이트가 말하려는 대상 자체가 없다.

4. **한 프레임 지연** — 래치 갱신은 `SyncGoalStability` **직후**여야 한다. 앞에 두면 묵은
   스트레스로 판정하는데, 문턱 근처에서는 그 한 프레임이 곧 떨림이다.

추가로 **진행 중에는 래치를 켜지 않는다** — 웨이브가 도는 동안 스트레스가 잠깐 내려간 것만으로
래치가 서면, 끝나는 순간 스트레스가 80 이어도 버튼이 뜬다(「등장 시점의 스트레스로 판정한다」가
거짓이 된다).

## 완료 기준

- [x] 스트레스 > 문턱이면 크레딧이 차도 안 뜬다
- [x] 스트레스가 내려온 프레임에 뜬다 (사용자 시나리오)
- [x] 뜬 뒤 스트레스가 올라가도 유지된다 (떨림 차단)
- [x] 밀린 크레딧이 소비 후에도 남는다 (`_bonusConsumedKillMark == killThreshold`)
- [x] 마음 없는 맵은 창이 열린다
- [x] `killThreshold > enemyCount` 불변식을 `OnValidate` + EditModeAssets 가 잡는다
- [x] EditMode `BonusPullTriggerTests` · PlayMode `BonusWavePullTest` green

**확인 2026-08-24** — EditMode 2447 · 실패 0 / PlayMode 10/10.

## 진단 창구

`BattleBridge.BonusPullBlockedByStress` — 「크레딧은 찼는데 스트레스 때문에 막혀 있다」.
없으면 플레이어도 개발자도 「왜 안 뜨지」에 답할 수 없다. 지금은 API 만 있고 도크는 안 읽는다 —
힌트 문안을 붙일지는 Play 후 판단(후속 후보).
