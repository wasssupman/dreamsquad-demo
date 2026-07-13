# 3 — 퇴장 침강 (아치 → 하단 덱)

## 목적

손패를 닫을 때(각성 재클릭 / 카드 사용 후 자동복귀) 카드가 하단 덱으로 역스태거로 침강·축소하며
사라진 뒤 디펜더 strip 이 폴드 인. 진입(덱-드로우)의 거울.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`

## 구현

1. **`Close()` 재구성**: 기존 `StartFlip(from:_panel, to:strip)` 교체.
   - `StartSink()` — 카드 있는 슬롯을 역순/역스태거로
     `Tween.UIAnchoredPosition(rect, (base.x*clusterK, baseY - dealRise), sinkDur, Ease.InBack)`
     + `Tween.Scale(rect, dealStartScale, sinkDur, Ease.InBack)` (+ 알파 페이드 옵션).
   - 완료 콜백: `_panel.SetActive(false)` + strip 폴드 인(기존 `RotateX` to-절반) + 슬롯 target/위치 base 복원.
   - lease dispose·`costDisplay.SetSuppressed(false)` 등 기존 `Close` 부수효과는 **시작 시** 그대로(슬로모 즉시 해제).
   - 시작과 동시에 `CancelAllCardInteraction()`(진행 중 드래그 취소).
2. **`ForceClose` 무애니 유지**: `StopDeal()`/sink stop 후 즉시 스냅(기존 계약). 애니 경로 안 건드림.
3. **경합 가드**: unit 0 `Transitioning` 확장이 sink 도 커버 → mash-safe.
4. **튜닝 SerializeField**: `sinkDurationSec=0.26f`, `sinkStaggerSec=0.04f`.

## 완료 기준

- compile 성공, 콘솔 CS 에러 0.
- Play — 각성 재클릭 시 카드가 하단으로 빨려 내려가며 축소·소멸 후 디펜더 strip 복귀.
- 카드 사용(`HandChangeReason.Used`) 자동복귀도 침강 연출로 닫힘(즉시 끊김 없음).
- 페이즈 이탈 강제 클로즈는 여전히 즉시(애니 없음)·트윈 잔류 0.
- 재오픈 시 정상 덱-드로우로 다시 아치 형성(위치/스케일/타깃 복원 확인).
