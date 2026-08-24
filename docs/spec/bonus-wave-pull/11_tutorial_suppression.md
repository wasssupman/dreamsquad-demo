# 11 — 온보딩 판에서는 뜨지 않는다

## 목적

첫 판(온보딩)에서 보라 알약이 뜨지 않게 한다. 온보딩은 「놓고 → 철수 → 다시 놓고 →
드림캐쳐를 붙인다」를 가르치는 판이고, 그 위에 조건부 두 번째 버튼이 얹히면 배우는 축이
하나 늘어난다.

> 사용자 결정 2026-08-24: *"튜토리얼하면서 나오던데?" → "안뜨게 하면된다"*

## 왜 이미 막혀 있지 않았나

**우연히 뚫려 있었다** — 세 조건이 전부 충족된다:

| 게이트 | 온보딩 판에서 | 근거 |
|---|---|---|
| 맵에 포탈이 저작됐나 | **예** | 튜토리얼 분기는 웨이브·손패만 갈고 맵은 안 건드린다. 라이브 `MapDocumentPool.entries` 는 Duel 하나이고 `bonusSpawns` 가 저작된 맵도 Duel 뿐이다 |
| 크레딧 30이 차나 | **예 (33)** | 저작 스폰 27기 + 슬라임 분열 사슬 |
| 스트레스 ≤ 30 인가 | 판마다 | 쉬운 웨이브라 오히려 잘 열린다 |

분열 사슬: `Enemy_Slime` →(magnitude 2)→ `Enemy_Slime_Mid` →(magnitude 2)→
`Enemy_Slime_Small`. 슬라임 1기 = 처치 **7회**. 그래서 27 − 1 + 7 = **33 > 30**.

⚠ 「적 수를 줄여 임계 미달로 만든다」로 끄지 않는다. 온보딩 난이도와 보너스 임계가
결합돼, 둘 중 하나를 만질 때마다 다른 쪽이 조용히 뚫린다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.BonusWave.cs`
- `Assets/_Project/Scripts/Core/GameManager.cs`
- `Assets/_Project/Tests/PlayMode/BonusWavePullTest.cs`

## 구현

1. **억제는 규칙 층에 건다** — `BonusPullAvailable` 에 `&& !_bonusPullSuppressed`.
   그 하나가 단일 상태원이라(계약 9) 도크 알약·`TryBonusPull`·진단 신호가 함께 닫힌다.
   도크에 조건을 넣지 않는다 — 넣으면 상태원이 둘로 갈리고 `ShouldRun` 소비처가 늘어난다.

2. **`BonusPullBlockedByStress` 도 함께 닫는다.** 억제된 판에서 참이면 「스트레스 때문에
   막혔다」는 거짓 진단이 된다 — 스트레스와 무관하게 이 판은 기능 자체가 없다.

3. **`ForceBonusWave`(기제 층)는 막지 않는다.** 술어를 보지 않는 것이 그 함수의 계약이고
   호출처는 PlayMode 테스트뿐이다(프로덕션 경로 0). 플레이어 경로는 `TryBonusPull` 하나라
   규칙 층 차단으로 실기 진입이 전부 닫힌다.

4. **`ResetBonusWaveState()` 에서 리셋하지 않는다.** 이건 판 시작 **전에** 밖에서 주입되는
   설정이고(`SetAuthoredWavePlan` 과 같은 성격), 그 리셋은 `BeginPlacement`·`StartBattle`
   양쪽에서 불린다. 여기 넣으면 GameManager 가 켠 억제가 판 시작에 지워진다.

5. **GameManager 는 술어를 지역 변수로 올려 setter 를 무조건 부른다** —
   `bool isFirstRunTutorial = …; if (isFirstRunTutorial) { … }` +
   `battleBridge.SetBonusPullSuppressed(isFirstRunTutorial);`
   `if` 안에 두면 온보딩 이후 판이 `true` 를 물려받을 수 있다. `ShouldRun` 호출 횟수는
   그대로 1 이다(소비처를 늘리지 않는다 — GameManager 주석의 판단 유지).

## 완료 기준

- [ ] 컴파일 에러 0
- [ ] PlayMode: 억제 ON 이면 크레딧·스트레스가 다 맞아도 `BonusPullAvailable` 거짓
- [ ] PlayMode: 억제 ON 이면 `TryBonusPull()` 거짓 · `BonusPullBlockedByStress` 거짓
- [ ] PlayMode: 억제 OFF 기존 10건 무회귀
- [ ] 온보딩 판 Play — 33킬 전멸시켜도 보라 알약이 안 뜬다
- [ ] 두 번째 판(온보딩 아님) Play — 보라 알약이 정상 등장
