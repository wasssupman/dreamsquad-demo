# 3 — 퇴장 수렴

## 목적

손패를 닫을 때(각성 버튼 재클릭 / 카드 사용 후 자동복귀) 카드가 역스태거로 각성 버튼으로
수렴·축소하며 사라진 뒤 디펜더 strip 이 폴드 인. 진입 딜링과 대칭.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`

## 구현

1. **`Close()` 재구성**: 기존 `StartFlip(from:_panel, to:strip)` 를 교체.
   - `StartConverge()` — 카드 있는 슬롯을 역순/역스태거로
     `Tween.UIAnchoredPosition(rect, DealSourceLocal(), convergeDur, Ease.InBack, startDelay)`
     + `Tween.Scale(rect, zero, convergeDur, Ease.InBack, ...)`. (딜의 거울.)
   - 시퀀스 완료 콜백에서 `_panel.SetActive(false)` + strip 폴드 인(기존 `RotateX` to-절반 재사용)
     + 카드 스케일/위치 home 복원(다음 오픈 대비, `RestoreSlotHome`).
   - lease dispose·`costDisplay.SetSuppressed(false)` 등 기존 `Close` 부수효과는 시퀀스 **시작 시**
     그대로 수행(슬로모 즉시 해제).

2. **`ForceClose` 는 무애니 유지**: 페이즈 이탈/disable 은 `StopDeal()`/converge stop 후 즉시 스냅
   (기존 계약). 애니 없는 강제 종료 경로는 건드리지 않는다.

3. **경합 가드**: 딜 진행 중 토글 → 기존 `if (Transitioning) return;` 이 unit 0 확장으로 딜/수렴
   시퀀스까지 커버하므로 mash-safe. 수렴 시작과 동시에 `CancelAllCardInteraction()`(진행 중 드래그 취소).

4. **튜닝 SerializeField**: `convergeDurationSec=0.26f`, `convergeStaggerSec=0.04f`.

## 완료 기준

- compile 성공, 콘솔 에러 0.
- Play — 각성 재클릭 시 카드가 버튼으로 빨려들며 축소·소멸 후 디펜더 strip 복귀.
- 카드 사용(`HandChangeReason.Used`) 자동복귀 경로도 수렴 연출로 닫힘(즉시 끊김 없음).
- 페이즈 이탈 강제 클로즈는 여전히 즉시(애니 없음)·트윈 잔류 0.
- 재오픈 시 카드가 정상 home 부채꼴에서 다시 딜(위치/스케일 복원 확인).
