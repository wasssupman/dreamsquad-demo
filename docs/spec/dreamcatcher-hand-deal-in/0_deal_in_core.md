# 0 — 딜-인 코어

## 목적

각성 버튼을 눌러 손패가 열릴 때, 카드가 각성 버튼 정확한 좌표에서 시작해 좌→우
스태거로 부채꼴 위치에 OutBack 안착하는 딜링 연출로 교체한다. (입체감/커브는 unit 1·2.)

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` — 버튼 rect 노출.
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — 딜 시퀀스.

## 구현

1. **`AwakeningGaugeView.PanelRect` 노출**: `public RectTransform PanelRect => _panel != null ? (RectTransform)_panel.transform : null;`
   (버튼 패널은 이미 `_panel`. 손패 오픈 시점엔 Placement/Battle 이라 활성.)

2. **버튼 중심 → 손패 패널 로컬 변환** (`DreamcatcherHandView`):
   - `gaugeView.PanelRect` 월드 중심 → `RectTransformUtility.WorldToScreenPoint`(overlay 는 카메라 null)
     → `RectTransformUtility.ScreenPointToLocalPointInRectangle(handPanelRect, screen, cam, out local)`.
   - 캔버스가 다른 두 뷰라도 스크린 경유로 안전 변환. 헬퍼 `Vector2 DealSourceLocal()`, 실패/미배선 시
     화면 하단-우측 근사 폴백(GiftPhaseView `FlyTarget` 정신).

3. **`Open()` 재구성**: strip 폴드-아웃(기존 `RotateX` 절반)은 유지. 손패는 폴드 대신
   - `_panel.SetActive(true)`; 패널 회전 0 고정(더 이상 X-fold 안 함).
   - 트레이 backing `CanvasGroup` 짧은 페이드/스케일 인(딜 무대). backing 에 CanvasGroup 없으면 추가.
   - `StartDeal()` — 각 슬롯을 `DealSourceLocal()` 위치·작은 스케일(0.6)·미세 회전으로 세팅 후
     `Sequence.Create()` 로 `i` 스태거 → `Tween.UIAnchoredPosition(rect, homePos, dealDur, Ease.OutQuad, startDelay: i*stagger)`
     + `Tween.Scale(rect, one, dealDur, Ease.OutBack, ...)` + `Tween.LocalRotation`(z→homeRotZ).
   - 빈 슬롯(entryId<0)은 딜 제외(현 상태 유지) 또는 함께 딜 — 카드 있는 슬롯만 딜.

4. **시퀀스 필드 + 정리**: `Sequence _dealSeq;` 보유. `StopDeal()` = `if (_dealSeq.isAlive) _dealSeq.Stop();`.
   `ForceClose`·`Close`(unit 3 에서 교체)·`OnDisable` 에서 호출. `Refresh()` 가 딜 도중 재바인딩되면
   home 스냅되므로, 딜 진행 중 표식(`_dealSeq.isAlive`)을 `Transitioning` 에 OR 하여 드래그/토글 가드.

5. **튜닝 SerializeField**: `dealStaggerSec=0.05f`, `dealDurationSec=0.32f`, `dealStartScale=0.6f`,
   `trayFadeSec=0.12f`. (`flipHalfDuration`·`fanAngle` 관례와 일치. 새 SO 안 만듦.)

## 완료 기준

- compile 성공, 콘솔 에러 0.
- Play(사용자 포커스) — 각성 버튼 클릭 시 카드가 **버튼 위치에서** 좌→우로 딜되어 부채꼴 안착.
  버튼과 첫 카드 시작점이 시각적으로 붙어 있음.
- 딜 도중 카드 드래그 시작 불가(`CanStartDrag` false). 딜 완료 후 정상 드래그.
- 페이즈 이탈/재시작으로 강제 클로즈 시 트윈 잔류·late-land 없음(콘솔 warning 0, 카드가 유령처럼 남지 않음).
- 슬로모 상태에서도 딜 속도가 실시간(UI 안 눌림).
