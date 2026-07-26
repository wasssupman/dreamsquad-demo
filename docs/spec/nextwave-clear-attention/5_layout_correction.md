# 5 — 좌하단 위치·배경 비율·패딩 교정

## 목적

비주얼 개편 전 `NextWaveDock`의 좌하단 위치를 복원하고, 새 Sprite를 RectTransform에 강제로
늘리면서 생긴 배경 왜곡과 테두리에 붙은 라벨을 교정한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/NextWaveDock.cs`
- `Assets/_Project/Scenes/BattleScene.unity`
- `Assets/_Project/Tests/PlayMode/NextWaveClearAttentionSmokeTest.cs`
- `docs/spec/nextwave-clear-attention/README.md`
- `docs/spec/nextwave-clear-attention/4_handoff_summary.md`

## 구현

- `panelOffset`을 개편 전 좌하단 원점 `(40, 40)`으로 복원한다. 새 CTA를 화면 위로 옮겨 트레이
  충돌을 피하는 정책은 폐기한다.
- 타이머·CTA Rect 비율을 각 Sprite 원본 비율에 맞추고 `Image.preserveAspect`를 켠다. 투명
  hit root는 그대로 두어 터치 영역과 시각 Sprite를 분리한다.
- 라벨 content rect에 좌·상·우·하 safe padding을 명시한다. 우측 이중 화살표 예약폭을 포함한다.
- `enableAutoSizing`과 최소 font size를 사용해 `다음 웨이브 N`이 padding을 침범하지 않게 한다.
- normal·clear-ready 모두 같은 content rect를 쓰며 모션 scale이 padding 값을 바꾸지 않는다.
- 기존 Frame/Button/PulseRing PNG와 GUID는 유지한다. 원본 에셋은 정상이며 재생성하지 않는다.

## 완료 기준

- 1920×1080에서 Dock의 좌하단 원점이 개편 전 `(40, 40)`과 같다.
- 타이머/CTA 외곽선이 가로·세로로 찌그러지지 않는다.
- `다음 웨이브 2` 라벨과 이중 화살표가 배경 테두리에서 시각적으로 분리된다.
- normal·clear-ready 캡처에서 라벨이 잘리거나 ring이 hit target을 가리지 않는다.
- targeted PlayMode smoke green, C# compile error 0.

검증 2026-07-26 — targeted PlayMode 1/1, 교정 normal/clear 16:9 캡처 — commit `e45c8ebf`.
