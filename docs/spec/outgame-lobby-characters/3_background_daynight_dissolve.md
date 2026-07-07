# 3 — 배경 낮/밤 디졸브 전환

## 목적

터치 리액션이 시작되면 로비 배경이 낮↔밤으로 전환되는 연출. 단순 노이즈 디졸브의
"불타는" 인상을 피하고 시간대 변화답게 보이는 스타일을 고른다.

## 변경 대상

- `Assets/_Project/Shaders/Background_Dissolve_UI.shader` (`Wassup/UI/BackgroundDissolve`)
- `Assets/_Project/Art/LobbyBackgroundDissolve.mat` · `dissolve_noise.png`(타일링 밸류 노이즈)
- `Assets/_Project/Art/lobby_bg_day.png` · `lobby_bg_night.png`
- `Assets/_Project/Scripts/UI/Outgame/LobbyBackgroundDissolve.cs`
- 씬: `MenuCanvas/LobbyBackgroundUnder`(뒤 레이어) + `LobbyBackground`(앞 레이어)

## 구현

- 두 레이어 스왑: 뒤 레이어에 목표 시간대 스프라이트 → 앞 레이어 디졸브(2s) →
  완료 시 앞 레이어 스왑 + 리셋. 전환 중 재트리거 무시.
- 셰이더 1개에 4모드: 노이즈 디졸브 / 원형 확산 / 수평 스윕 / 크로스페이드.
  `TransitionStyle` enum 으로 인스펙터 선택, 기본값 `RadialWithGoldenTint`
  (원형 확산 + 골든 틴트). 원형 확산 중심은 트리거한 캐릭터 위치(UV 변환).
- 트리거: `LobbyReactionLock.ReactionStarted` 구독 — 개별 캐릭터에 묶지 않는다.
- 방향별 색 (인스펙터 4색): night→day 새벽 금빛 tint(1,.84,.52)/band(1,.88,.55),
  day→night 초저녁 남보라 tint(.42,.40,.58)/band(.52,.50,.78, 강도 0.7).
- 머티리얼은 런타임 인스턴스로 사용(공용 에셋 무오염, OnDestroy 파괴).

## 완료 기준

- Play: 캐릭터 클릭 → 리액션과 함께 배경 전환, 확산 중심이 클릭한 캐릭터를 따라감,
  왕복(밤→낮→밤) 및 방향별 색 구분. (2026-07-07 Play 실측 + 캡처로 확인)
