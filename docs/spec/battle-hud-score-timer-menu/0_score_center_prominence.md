# 0 — 스코어 중앙 상단 강조

## 목적

남은 시간이 점수 위에서 사라지므로(1번 작업), `ScoreHudView` 가 상단 중앙을 온전히 크게 차지하도록 위치/크기를 상향한다. 점수를 "화면의 주인공"으로 읽히게 한다. 연출 로직(PunchScale/플래시/버스트/마일스톤)은 손대지 않는다 — 위치/크기만.

## 변경 대상

- `Assets/_Project/Scripts/UI/ScoreHudView.cs`
- 씬 값: `BattleScene.unity` 의 `ScoreHud` GameObject(직렬화된 `topOffset`/`valueFontSize`/`captionFontSize` 가 코드 기본값을 덮을 수 있음 — 씬 값도 갱신 필요)

## 구현

1. `[Header("Layout")]` 기본값 상향:
   - `topOffset`: `-76f` → **`-24f`** 근처 (타이머가 빠진 상단 여백을 채워 위로 당김).
   - `valueFontSize`: `83f` → **`104f`** 근처 (주인공감).
   - `captionFontSize`: `29f` → **`34f`** 근처.
   - 위 수치는 시작점. Play/스크린샷 육안 확인 후 튜닝(배경/프랍 변경 아님이지만 HUD 위치는 게임뷰 확인이 정확).
2. `_panel` `sizeDelta`(현 420x140) 는 폰트 상향에 맞춰 필요 시 소폭 확대. `_valueRect` 오프셋(현 -34) 도 캡션-값 간격 유지되게 확인.
3. `topOffset` 을 코드에서 참조하는 `StopFeedbackTweens()` 의 복귀 위치(`prt.anchoredPosition = new Vector2(0f, topOffset)`) 는 필드 참조라 자동 반영 — 하드코딩 없음 확인만.
4. **씬 값 동기화**: 인스펙터에 직렬화된 `topOffset/valueFontSize/captionFontSize` 가 있으면 같은 값으로 맞춰 저장(코드 기본값과 씬 값 불일치로 실기기에서 다르게 보이는 함정 회피). 씬 저장 시 무관한 WIP/`sparkColorBoost` 재유입 주의(내 delta 만 커밋).

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러 없음.
- [ ] Play(BattleScene, 전투 진입) → 점수가 상단 중앙에 크게, 연출(처치 시 펀치/골드 플래시/스파크) 정상.
- [ ] 스크린샷으로 점수가 이전보다 크고 위로 붙었는지 육안 확인.
- [ ] 연출 로직 회귀 없음(펀치/버스트/마일스톤/사운드 동작).

> 확인: 2026-07-08 사용자 Play 확인 통과 (작업 1 과 묶어 검증). 스코어 패널 y−24·값 104pt·캡션 34pt, 중앙 타이머 겹침 해소.
