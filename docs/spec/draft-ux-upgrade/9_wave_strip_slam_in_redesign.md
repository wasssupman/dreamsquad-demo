# 9. Wave Pattern Strip — Announcement Redesign

## 목적

기존 좌→우 ScaleX unroll/roll 메타포를 "Incoming Waves" 알림 스타일로 교체.
외부 API 시그니처 (`Unroll/Roll/FadeIn/RebuildFromDeck/SnapHidden/SetToggleEnabled/OnDwellInterrupt`) 유지.

아울러 spec 7 후속 fix 2건 포함:
- **카드 fan 활성 타이밍**: strip Roll 완료 후에만 fan 활성 + Build + PlayEnterSequence.
- **드래그 후 클릭 더블 발화 가드**: `_dragHappened` 플래그 (DraftCardView).

## 변경 대상

- `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs` (재작성)
- `Assets/_Project/Scripts/UI/Draft/DraftView.cs` (dwellSeconds = 2.0f)
- `Assets/_Project/Scripts/UI/Draft/DraftCardView.cs` (`_dragHappened` 가드 추가)

## 레이아웃

### Header
- anchor (0.5, 1) — 화면 상단 기준
- 정위치 y = −100 (상단에서 100px 아래)
- 시작/출구 y = +60 (상단 위로 완전히 벗어남)
- sizeDelta (900, 110), "INCOMING WAVES" 78pt Bold 노란색 (1, 0.86, 0.24)

### CardGrid
- anchor (0, 0.5), pivot (0, 0.5) — 좌측 중앙 기준
- 정위치 (24, −30): 좌측 24px 여백, 수직 중앙에서 30px 아래
- HorizontalLayoutGroup childAlignment = MiddleLeft (왼쪽 끝 정렬)
- ContentSizeFitter horizontalFit = PreferredSize

## Unroll() — 드라마틱 등장 (드래프트 시작 시 자동 호출)

`Sequence.Create()` + `Group()` 병렬 실행:

1. **Overlay fade-in**: alpha 0→0.6 (0.15s OutQuad)
2. **Header drop**: y=+60 → y=−100 (0.40s OutBounce, delay 0.05s) + alpha 0→1 (0.20s)
3. **Header shake** (drop 완료 직후): ShakeLocalPosition (8, 0, 0), 0.12s, freq 24
4. **카드 staggered fade-in** (i = 0..N-1):
   - `delay = 0.20 + i × 0.06s`
   - 개별 CanvasGroup alpha 0→1 (0.30s OutQuad) — HorizontalLayoutGroup 위치와 충돌 없음
   - 도착 직전 PunchScale (0.12, 0.12, 0), freq 6
5. **그룹 pulse** (마지막 카드 + 0.10s): PunchScale (0.05, 0.05, 0), freq 4
6. `OnComplete → _state = Shown`

reset 시 overlay alpha=0, header 위치=+60/alpha=0, cardGrid 위치=rest/alpha=1, 카드 개별 CG alpha=0.

## FadeIn() — 소프트 재등장 (토글 버튼으로 재호출)

위치는 SnapHidden 이후 rest 상태 그대로. alpha만 tweening.

1. Overlay alpha 0→0.6 (0.25s OutQuad)
2. Header alpha 0→1 (0.25s OutQuad, delay 0.05s)
3. CardGrid alpha 0→1 (0.25s OutQuad, delay 0.05s)
4. `OnComplete → _state = Shown`

## Roll() — 위로 fly-out + fadeout

1. Header y → +60, alpha → 0 (0.30s InQuad)
2. CardGrid y → rest+120, alpha → 0 (0.30s InQuad)
3. Overlay alpha → 0 (0.15s InQuad, delay 0.15s)
4. `OnComplete → SnapHidden()`

## SnapHidden()

- overlay alpha=0
- header alpha=0, pos=(0, −100) (rest 위치, FadeIn을 위해 off-screen이 아닌 rest에 보관)
- cardGrid alpha=0, pos=(24, −30)
- 카드 개별 CG alpha 유지 (FadeIn 시 cardGridGroup alpha로 일괄 제어)
- `_state = Hidden`

## 토글 버튼 동작

- `Hidden` → `FadeIn()` (DwellInterrupt 없음 — 드웰은 이미 지남)
- `Shown` → `Roll()`
- `Unrolling` / `Rolling` → 무시

## Overlay 클릭

`Shown` 상태에서만 `OnDwellInterrupt?.Invoke()` (드웰 중 빨리 넘기기용).
Roll 후 토글로 재등장한 상태에서 배경 클릭 → DraftView가 Dwelling 아니면 무시 → 토글로만 닫기.

## 카드 fan 활성 타이밍 (DraftView.RunFlow)

- `OnDraftStarted` → `fan.gameObject.SetActive(false)`
- strip Roll 완료 후 → `fan.gameObject.SetActive(true)` + `fan.Build(pool)` + `fan.PlayEnterSequence()`
- `ShowSubviews()`도 fan 비활성으로 시작

## 드래그 후 클릭 더블 발화 가드 (DraftCardView)

```
private bool _dragHappened;

OnBeginDrag:   _dragHappened = true;
OnPointerClick: if (_dragHappened) { _dragHappened = false; return; }
                if (_discardFired) { ... }
                if (_lastDragDistance >= 30) return;
                Discarded?.Invoke(this);
```

## DraftView 변경

- `dwellSeconds = 2.0f` (기존 5.0f → 2.0f)

## 완료 기준

- 컴파일 에러 0.
- Play: 화면 dim → 헤더 상단에서 100px 낙하 + 흔들림 → 카드 좌측 정렬로 순차 fade-in → pulse.
- 2초 dwell (또는 배경/토글 클릭) 후 위로 fly-out.
- 카드 fan 등장 (strip 시퀀스 동안 fan 미노출).
- 토글 재클릭 → 부드러운 fade-in 재등장.
- 드래프트 중 짧은 드래그(<120px)는 홈 복귀, 폐기 없음.
