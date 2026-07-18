# 5 — 잔잔한 상시 인터랙션 affordance

## 목적

각성 버튼이 값 표시용 HUD로만 보이지 않도록, 숫자 가독성을 해치지 않는 작은 상시 생동감을
추가한다. 강한 pulse나 서사 상징 없이 캐주얼 젤리 버튼의 재질감과 터치 가능성을 전달한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs`
- `docs/spec/awakening-hud-resource-button/README.md`

## 구현

- `_visualRoot` 아래에 face 전용 `RectTransform`을 신설한다. halo·well·frame은 face 아래,
  숫자·`/100`·획득 텍스트는 기존 visual root에 남겨 상시 연출 중에도 수치를 고정한다.
- face는 약 3.2초 주기로 scale `1 → 1.018 → 1`, rotation `-0.6° ↔ +0.6°`의 느린
  호흡을 반복한다. 정확한 값은 SerializeField로 노출한다.
- `ChargeWell` Mask 안에 낮은 알파의 둥근 광택 띠를 1개 생성한다. 약 3.8초 간격으로
  좌→우 sweep하고 그 사이에는 완전히 숨긴다. 신규 bitmap 없이 기존 절차적 원형 sprite를 재사용한다.
- 루프는 `Time.unscaledDeltaTime`을 사용하고 Battle에서만 실행한다.
- 값 0에서는 진폭·광택 알파를 낮추되 완전히 정지하지 않는다. 0에서도 손패 확인이 가능한 기존
  인터랙션 계약을 시각적으로 보강한다.
- 손패 open, pointer down, 획득 squash, 공개 `Pulse()`, MAX burst/idle 중에는 face를 identity로
  복귀시키고 ambient를 잠시 쉰다. 기존 반응 연출이 끝나면 자동 재개한다.
- OnDisable/Placement 진입 시 코루틴을 중단하고 face scale/rotation과 광택 alpha를 초기화한다.
- 기존 `Toggled`, 게이지 경제, 손패 수명, 레이아웃, 에셋 참조는 변경하지 않는다.

## 완료 기준

- 전투 중 무입력 상태에서 숫자는 고정되고 뒤 젤리 face만 잔잔하게 살아 움직인다.
- 0/중간/MAX 상태에서 수치가 장식보다 먼저 읽힌다.
- 탭·획득·MAX 연출과 ambient transform이 서로 덮어쓰거나 종료 후 scale을 남기지 않는다.
- Placement 및 비활성화 상태에서는 루프와 광택이 남지 않는다.
- 1920×1080 Play에서 “누를 수 있는 버튼”으로 인지되며 과한 주의 유도가 없다.
- Unity 컴파일 및 Console error 0.

_구현 확인 2026-07-18: Unity 6000.4.3f1 script refresh/recompile 완료, Console error 0.
Battle 1920×1080 체감 Play 확인 대기._
