# unit 1 — 브롤스타즈식 카운트다운 연출

## 목적

3초 자동 시작 창을 "그냥 기다리는 3초"가 아니라 **전투 시작을 예고하는 3초**로 만든다. 화면 중앙 대형 숫자 3·2·1 이 펀치와 함께 찍히고 GO! 로 닫힌다.

## 변경 대상

- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` — 카운트다운 라벨 빌드 + 틱 트윈 + 아웃트로

## 구현

**표시** — 자동 시작 모드에서만 켜지는 중앙 대형 TMP. 기존 배치 카운트다운 라벨(상단)은 이 모드에서 숨긴다. 폰트는 `startLabelFont`(Bangers SDF) 재사용, 외곽선은 `startLabelOutlineColor`/`Width` 재사용 — 배치 UI 와 같은 스티커 톤을 유지한다.

**틱 트윈** (PrimeTween, `useUnscaledTime: true` — 기존 START 주스와 동일 규율):

- 남은 초의 `CeilToInt` 가 바뀌는 프레임에만 갱신한다(매 프레임 문자열 재대입 금지).
- 숫자가 바뀔 때: `scale 1.6 → 1.0` (`Ease.OutBack`, ~0.25초) + `alpha 1 → 0.55` 로 다음 틱까지 잦아든다. 스케일 펀치가 이 연출의 전부다 — 회전·색 사이클은 넣지 않는다.
- 색은 인스펙터 노출(`countdownColor`, 기본 크림). 마지막 `1` 만 강조색(`countdownFinalColor`, 기본 앰버).

**아웃트로** — `0` 은 표시하지 않는다. 남은 시간이 0에 닿으면 라벨을 `GO!` 로 바꾸고 **기다리지 않고** `FinishPlacement()` 를 호출한다(계약 4). `GO!` 는 펀치 트윈을 타지 않는다 — 찍히자마자 `HideOverlay` 가 그 시퀀스를 `Stop()` 하고 아웃트로로 갈아탄다. 화면에 보이는 것은 **펀치 배수에서 시작해 더 커지며 사라지는** 한 동작이다(숫자들의 1.6→1.0 수축과 방향이 반대라 마무리로 읽힌다). 패널 은닉만 `HideOverlay()` 로 위임해 자동 시작 모드에서는 GO! 트윈(~0.35초 스케일업 + 페이드)이 끝난 뒤 꺼진다. **입력 차단막은 `FinishPlacement()` 시점에 즉시 해제**한다 — 전투는 이미 시작됐고 GO! 는 잔상일 뿐이다.

**타이밍 값**은 전부 `SerializeField`(`punchScale`, `punchDuration`, `outroDuration`) — 인스펙터에서 감각을 맞춘다. 총 길이만 `BattleConfig.autoStartCountdownSeconds` 가 소유한다.

## 완료 기준

- 컴파일 통과. `placementPhaseEnabled=true` 경로는 시각적으로 무변화(중앙 라벨이 생성되지 않음).
- `placementPhaseEnabled=false` Play: 중앙에 3 → 2 → 1 → GO! 가 각 1초 간격으로 펀치와 함께 표시되고, GO! 가 사라지는 동안 전투가 이미 진행 중이다.
- GO! 표시 중 트레이 드래그가 **된다**(블로커 해제 확인 — unit 0 완료 기준의 연장).
- 콘솔 에러 0. 특히 PrimeTween `OnComplete callback was ignored` 경고가 없어야 한다(패널 비활성 타이밍과 트윈 수명 충돌 신호).

> 확인 2026-08-18 — Play 실측 3→2→1→GO! 각 1초, 아웃트로 후 패널 자동 종료, PrimeTween 경고 0. 커밋 `b79859a7`.
