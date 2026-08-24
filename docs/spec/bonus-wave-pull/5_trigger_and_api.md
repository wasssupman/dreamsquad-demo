# 5 — 트리거 술어와 공개 API

## 목적

「누적 처치 N기마다 버튼이 뜬다」를 구현하고, 일반 당김과 같은 **기제/규칙 2층 구조**로 노출한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/BonusPullTrigger.cs` — 신규(순수 술어)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Tests/EditMode/BonusPullTriggerTests.cs` — 신규
- `Assets/_Project/Tests/EditMode/BattleLogPullEventTests.cs` — 형제 단언 추가

## 구현

1. **순수 술어** — unit 9 에서 스트레스 축이 붙어 셋으로 갈렸다:
   `BonusPullTrigger.HasCredit(normalKills, consumedKills, threshold)` ·
   `StressAllows(stress, maxStress)` · `NextLatched(latched, …)`.
   (초판의 단일 `ShouldOffer` 는 없다 — `9_stress_gate.md` 가 그 계약의 정본이다.)

2. **트리거 카운터는 일반 적만 센다**(계약 12). `DrainEnemyKilledEvents` 에서 처치된 적이
   `BonusWaveTag` 를 갖고 있었는지로 가른다:
   - `_killCount`(점수)는 **모든** 처치를 계속 센다 — 계약 7, 1킬 1점 불변
   - `_normalKillCount`(트리거)는 보너스 적을 **제외**한다

   ⚠ 이걸 안 가르면 실효 임계가 `N − 10` 이 되고 **N ≤ 10 에서는 발산**한다(보너스 웨이브가
   자기 자신을 무한 재발화). 죽은 엔티티의 태그를 드레인 시점에 읽을 수 없으면 스폰 시점에
   기록해 이벤트 페이로드로 나른다.

3. 공개 API:
   - `BonusPullAvailable` = 실행 중 && `bonusWaveData != null` && 포탈 저작됨 &&
     `!_bonusWaveActive`(계약 13) && 래치 ON(계약 15 — unit 9 가 이 항을 추가했다)
   - `TryBonusPull()` — 규칙 층. 성공 시 **`consumed += killThreshold`**(한 회분만 —
     `= normalKills` 로 두면 밀린 크레딧이 증발한다, unit 9)
   - `ForceBonusWave()` — 기제 층. 술어를 보지 않는다(테스트·디버그 진입점).
     **`bool` 을 돌려준다** — 스케줄이 비어 아무것도 안 열렸는데 크레딧만 사라지는 것을
     규칙 층이 막을 수 있어야 한다(리뷰 M4)

   **플레이어 경로는 `TryBonusPull` 하나다**(계약 9). UI 가 `ForceBonusWave` 를 직접
   부르면 트리거가 우회된다.

4. `RecordWaveEvent("bonus_pull", …)` 로 배틀 로그에 남긴다. 일반 당김이 `wave_forced` 로
   남기는 것과 같은 자리 — 안 남기면 랭킹 점수의 절반이 어디서 왔는지 사후 판독이 불가능해진다.

## 완료 기준

- [x] 컴파일 에러 0
- [x] `BonusPullTriggerTests` — 임계 미만/도달/초과 · **보너스 킬은 카운터에 안 들어간다**
- [x] 보너스 웨이브 진행 중에는 `BonusPullAvailable` 이 거짓(계약 13)
- [x] 소비 후 카운터가 리셋되고 다시 채우면 참으로 돌아온다
- [x] `bonus_pull` 이 `SnapshotJson()` 에 실린다
- [x] `_killCount`(점수)는 보너스 킬을 계속 센다 — 1킬 1점 무회귀
- [x] EditMode green

**확인 2026-08-24** — `BonusPullTriggerTests`(12) + `BattleLogPullEventTests.보너스_당김도_로컬_로그에_남는다`.
`ForceBonusWave` 는 `bool` 이라 실패한 당김이 크레딧을 먹지 않는다(리뷰 M4).
