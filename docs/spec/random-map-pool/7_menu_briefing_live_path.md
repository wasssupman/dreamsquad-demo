# 7. 일시정지 메뉴 웨이브 예고 — 진짜 라이브 경로 픽스

## 목적

unit 6 은 draft 스트립을 고쳤지만, **실게임(스쿼드/드림캐쳐 모드)은 draft 를 건너뛴다**(`GameManager.Start` → `StartSquadMatch` → `return`). 실게임에서 웨이브 패턴을 보는 유일한 경로는 **인배틀 일시정지 메뉴**(`MenuPopup`). 그 경로가 정적 WaveA 를 보여주던 것을 선택된 맵의 실전 덱으로 바로잡는다.

## 배경 (왜 unit 6 로 부족했나)

- 실게임 스트립 빌더 = `SquadPrepView.OnMapSetupRequested` → `wavePatternStrip.RebuildFromDeck()`(정적 `deck`=WaveA) → `SnapHidden()`. 주석대로 "prep just readies the deck", 표시는 `MenuPopup` 이 `FadeIn` 으로.
- `MenuPopup.Open()` 은 재빌드 없이 `FadeIn` 만 → SquadPrepView 가 준비한 **WaveA** 카드를 그대로 노출. TwinLane(WaveB) 판이어도 메뉴는 WaveA(예: Rootcaster/Vanguard) 예고 = 불일치.

## 변경 대상

- `Assets/_Project/Scripts/Core/GameManager.cs` — `BuildBriefingWavePlan()` 패스스루(메뉴용 owner)
- `Assets/_Project/Scripts/UI/MenuPopup.cs` — `Open()` 이 표시 직전 액티브 플랜으로 재빌드

## 구현

- `GameManager.BuildBriefingWavePlan()` → `battleBridge?.BuildBriefingWavePlan() ?? default`.
- `MenuPopup.Open()`: `FadeIn` 직전에
  ```
  var plan = GameManager.Instance != null ? GameManager.Instance.BuildBriefingWavePlan() : default;
  if (plan.waves != null) wavePatternStrip.RebuildFromPlan(plan);
  ```
  메뉴가 열릴 때마다 **현재 ActiveDeck** 로 재빌드 → SquadPrepView 의 정적 WaveA 준비를 덮어씀. bridge 부재면 기존 카드 유지(무회귀). 재사용: `BattleBridge.BuildBriefingWavePlan`·`WavePatternStripView.RebuildFromPlan`(unit 6 도입).
- unit 6(DraftView) 은 유지 — draft 는 no-squad 폴백으로만 도는 경로라 죽었지만, 도달 시 올바르게 동작(무해).

## 완료 기준

- [x] compile 0 errors, EditMode green (1261/1263, 0 fail)
- [x] 판별: 정적 WaveA 플랜 `Rootcaster/Vanguard/Basic`(seed 20260720) vs 액티브 WaveB 플랜 `Swift/Needler/Sniper`(seed 587014748) — W1 동일=False
- [x] `MenuPopup.Open()` → `GameManager.BuildBriefingWavePlan()`(WaveB) → 스트립 waves=11 액티브 일치. 메뉴가 WaveA 아닌 WaveB 예고
- [ ] (사용자) 실 스쿼드 플레이에서 TwinLane 판 메뉴가 WaveB(빠른 교란형) 예고

확인 2026-07-22 (unit 7 — MenuPopup 라이브 경로 픽스, 플랜/시드 판별 실증). unfocused 라 렌더 픽셀은 FadeIn 트윈 정지로 불안정 → 사용자 실플레이 육안 확인 남음.
