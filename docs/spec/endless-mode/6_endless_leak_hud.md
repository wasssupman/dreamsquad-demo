# 6 — 무한 모드 누수 HUD 한계 숨김

## 목적

무한 모드는 누수로 죽는 한계가 없는데(unit 2), 누수 HUD 가 `"{현재} / {한계}"` + 남은수 기반
**위기색(빨강)** 을 그대로 표시해 "곧 죽음" 오해를 준다. 무한일 때 **한계·위기색을 숨기고 누수 개수만**
표시한다. **뷰만 바꾼다 — 점수 산식/스트레스 예산은 불변.**

## 변경 대상

- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — `SetLeakStatus(current, limit, showLimit=true)`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `RefreshLeakHud`
- `Assets/_Project/Tests/PlayMode/EndlessModeSmokeTest.cs` — HUD 어서션 추가

## 구현

1. `SetLeakStatus` 에 `bool showLimit = true` 추가(+ `_leakShowLimit` 필드). 기본값 true 라 메인 불변.
2. `RefreshLeakDisplay`: `showLimit` 이면 기존 `"현재 / 한계"` + 위기색; 아니면 `"현재"` 개수만 +
   `leakNormalColor`(위기색 없음).
3. `RefreshLeakHud` → `SetLeakStatus(_goalReachedCount, EffectiveLeakLimit(), !IsEndless)`.

## 완료 기준

- 컴파일 0. 메인 모드 HUD 불변(showLimit 기본 true).
- 무한 모드에서 누수 HUD 텍스트에 `/` 없음(개수만).

✅ 확인 2026-07-25 — 컴파일 0, PlayMode `EndlessModeSmokeTest` 1/1(HUD 텍스트 `/` 미포함 어서션 통과).
커밋 해시는 handoff 참조.
