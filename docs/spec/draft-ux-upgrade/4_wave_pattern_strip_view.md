# 4. Wave Pattern Strip View

## 목적

화면 상단에 두루마리처럼 unroll/roll 되는 가로 strip + 좌측 중앙 토글 버튼. 자동 unroll 후 2초 dwell 동안 사용자 입력으로 즉시 roll 가능. 모든 트랜지션은 task 0 에서 검증된 PrimeTween API 사용.

> **API (task 0 smoke 확정)**: PrimeTween 에는 `Tween.UIScaleX` 가 없다. RectTransform 의 좌→우 펼침은 `Tween.ScaleX(rectTransform, end, dur, ease)` 로 처리한다 (RectTransform 의 `localScale.x` 를 트윈 — RectTransform 도 Transform 의 하위라 정상 동작). 종료 대기는 `await tween` 또는 `tween.ToYieldInstruction()` (코루틴), 정리는 `Tween.StopAll(target)`.

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs`
- 의존: `WavePatternGenerator`, `AttackDeck`, `GeneratedWavePlan`

## 구현

1. MonoBehaviour. 직렬화 필드: `[SerializeField] AttackDeck deck`. 상위 DraftView Canvas 의 자식 전제.
2. UI 빌드 (Awake 1회):
   - 상단 strip 패널: anchor `(0,1)-(1,1)`, pivot `(0, 1)` (좌→우 펼침을 위한 좌측 고정), anchoredPosition `(0,0)`, sizeDelta `(0, 140)`. 배경색 `(0.04, 0.06, 0.10, 0.92)`.
   - strip 내부 가로 HorizontalLayoutGroup: padding 24, spacing 18, childForceExpandWidth=false.
   - wave 행: 기존 `TimelineBriefingView.AddWaveRow` 의 가로 압축판 (`Wxx | unitA | unitB | TOTAL`).
   - 토글 버튼: anchor `(0,0.5)-(0,0.5)`, anchoredPosition `(40,0)`, sizeDelta `(64,64)`. 텍스트 "≡" 24pt.
   - 토글 버튼은 항상 화면에 보임. strip 만 scaleX 로 펼침/접힘.
3. 상태 enum: `Hidden, Unrolling, Shown, Rolling`. 시작 상태 `Hidden`, strip RectTransform 의 `localScale.x = 0`.
4. PrimeTween 시퀀스:
   - Unroll: `Tween.ScaleX(stripRect, 1f, 0.45f, Ease.OutQuad)`. pivot.x=0 으로 좌→우 펼침.
   - Roll: `Tween.ScaleX(stripRect, 0f, 0.35f, Ease.InQuad)`.
   - 두 호출은 본 view 의 보유 Tween 핸들로 저장하여 OnDisable / 재진입 시 `.Stop()` 가능.
5. 공개 API:
   - `void RebuildFromDeck()` — `WavePatternGenerator.Generate(deck)` 호출 후 wave 행 재구성. strip 비활성 상태에서도 호출 가능.
   - `Tween Unroll()` — 시퀀스 시작 + 종료 Tween 반환. 오케스트레이터가 `await tween.ToYieldInstruction()` 또는 `tween.OnComplete(...)` 로 합류 가능. (정확한 await 패턴은 task 0 smoke 결과로 확정.)
   - `Tween Roll()` — 동일 패턴.
   - `void SetToggleEnabled(bool)` — 시퀀스 진행 중 토글 비활성화.
   - `event Action OnDwellInterrupt` — strip 영역 또는 토글 버튼 클릭이 dwell 도중 발생했을 때 발화. 오케스트레이터가 구독.
6. 토글 버튼 onClick:
   - State `Hidden` → `Unroll()` + `OnDwellInterrupt?.Invoke()` (dwell 도중이면 즉시 roll 로 가도록 오케스트레이터에 신호)
   - State `Shown` → `Roll()`
   - State `Unrolling`/`Rolling` → 무시.
7. strip 패널 자체에 Image + Button 또는 IPointerClickHandler 를 붙여 strip 영역 클릭도 `OnDwellInterrupt` 발화. (Drafting state 에서는 strip 클릭은 토글 의미 없음 → State 분기로 무시.)
8. OnDisable 에서 `Tween.StopAll(this)` 호출하여 본 view 발생 트윈 일괄 정리.

## 완료 기준

- DraftView active 시 strip 빌드 + RebuildFromDeck 결과가 wave 행으로 표시.
- `Unroll()` 호출 → 좌→우 0.45s 펼침. `Roll()` → 우→좌 0.35s 접힘.
- 토글 버튼 클릭으로 unroll/roll 토글 (시퀀스 진행 중 입력 무시).
- strip 영역 클릭 / 토글 클릭이 `OnDwellInterrupt` 발화.
- 토글 버튼은 좌측 중앙에 항상 표시.
- 컴파일 에러 0, PrimeTween 호출 에러 0.
