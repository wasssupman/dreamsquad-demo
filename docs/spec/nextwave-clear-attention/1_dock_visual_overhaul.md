# 1 — NextWaveDock 비주얼 격변

## 목적

현재의 “큰 프레임 안에 타이머와 가로 버튼” 구성을 해체하고, 다음 웨이브 행동이 전투 화면의
좌하단 주 CTA로 읽히게 한다. 장식이 아니라 실루엣·크기·아이콘에서 행동 의미가 먼저 보여야 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/NextWaveDock.cs`
- `Assets/_Project/Art/UI/NextWaveDockFrame.png(.meta)` (교체)
- `Assets/_Project/Art/UI/NextWaveButtonFace.png(.meta)` (교체)
- `Assets/_Project/Art/UI/NextWavePulseRing.png(.meta)` (신규)
- `Assets/_Project/Scenes/BattleScene.unity` (Sprite/튜닝 배선)

## 구현

- 전체 footprint는 좌하단 SafeArea와 중앙 트레이 예약폭 안에 둔다. 약 300×170 크기와 트레이
  위쪽 오프셋은 serialized layout 값으로 둔다.
- **타이머 캡슐**: 작은 네이비 플레이트, `남은 시간` 보조 라벨, 큰 `m:ss`. 현 경고색과 초 tick
  punch를 유지하되 행동 버튼보다 면적·채도를 낮춘다.
- **행동 버튼**: 타이머보다 크고 돌출된 청록/블루 젤리 면, 두꺼운 네이비 하단 그림자,
  UGUI bar 이중 화살표, `다음 웨이브 N` 라벨. 번호가 바뀌어도 레이아웃이 흔들리지 않는다.
- 버튼이 없는 전투(legacy)는 기존처럼 숨긴다. 다음 웨이브가 없으면 채도·명도·화살표를 함께
  낮춰 disabled를 색 하나에만 의존하지 않는다.
- pointer down squash / release overshoot / `ForceNextWave()` 1회 호출 / 기존 클릭 SFX를 보존한다.
- UI 에셋은 ImageGen 원본을 크로마 제거해 제작한다. 투명 배경·무텍스트·작은 크기 판독·밝고
  깨끗한 캐주얼 디펜스 톤을 지킨다. 화살표는 코드형 UGUI로 둔다.
- Sprite 누락 시 `buttonColor`/`backingColor`와 절차 Image로 기능·hit target을 유지한다.
- 단일 소비처이므로 신규 Style SO·인터페이스·공용 애니메이터 추상화는 만들지 않는다.

## 완료 기준

- UI Sprite / alpha transparency / mipmap off, Android에서도 가장자리 검은 halo 없음.
- 1920×1080에서 타이머보다 행동 버튼과 이중 화살표가 먼저 읽힌다.
- 중앙 액션 트레이·좌측 SafeArea·우측 각성 버튼과 겹치지 않는다.
- normal / pressed / disabled 세 상태가 정지 캡처에서도 구분된다.
- 버튼 클릭은 한 번에 한 웨이브만 호출하고 기존 스케줄 테스트가 무회귀다.

검증 2026-07-26 — 16:9·20:9 캡처, Sprite import/alpha 확인 — commit `663ad01c`.
