# 9 — 선택 중 각성 버튼 = 기본 전투 상태로 복귀

> 추가 2026-07-30 (사용자 결정). 계약 7 의 **두 번째 예외** — unit 8 과 함께 "손패 단독 닫힘이
> 선택을 데려가는" 경우를 이룬다.

## 목적

선택 중 각성 버튼(항아리)을 누르면 손패만 걷히고 선택은 남는 것이 기존 계약 7 이었다. 그
상태는 줌 + 슬로모 0.3× 가 걸린 채 손패도 없어 **할 일이 없다** — 빈 보드를 따로 탭해야 풀린다.
각성 버튼을 누르는 행위는 "그만하기" 의 명시적 표현이므로, 그 한 번으로 **기본 전투 상태**
(선택·줌·슬로모·리티클·패널·손패 전부 해제)로 되돌린다.

unit 8 이 "자원이 바닥나서 끝났다" 를 다뤘다면 이 unit 은 "사용자가 끝내겠다고 말했다" 를 다룬다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — `SelectionDismissed` 이벤트
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 구독 → `Close()`

## 구현

`OnToggled()` 의 **닫힘 분기**에서 `InSelectionMode` 일 때만 이벤트를 발화하고, 컨트롤러가 자기
`Close()` 를 부른다. 양쪽 `Close()` 가 이미 멱등이라 새 상태·새 시그니처가 없다(unit 8 과 동형).

**발화 위치가 계약이다** — `Close()` **앞**:

- `Close()` 뒤면 뷰의 `Close()` 가 쏘는 `FocusCleared` 시점에 선택이 아직 살아 있어 컨트롤러가
  **리티클을 재주장한 직후 곧바로 지우는** 1프레임 깜빡임 + lease churn 이 생긴다(unit 8 과 동일).

`CancelAllCardInteraction()` 은 기존 순서(맨 앞)를 유지한다. 드래그가 실제로 물려 있을 때만
`InteractionEnded` 를 지나 재주장이 한 번 돌지만, 같은 콜스택 안에서 곧바로 `Close()` 가 걷으므로
프레임 경계를 넘지 않는다(렌더 사이에 아무것도 끼지 않는다).

실행 흐름:

1. 항아리 탭 → `OnToggled` → (전이 중이면 mash 가드로 무시) → `CancelAllCardInteraction()`
2. `State == Hand` 이고 `InSelectionMode` → **`SelectionDismissed`**
3. 컨트롤러 `Close()`: 선택 해제 + 슬로모 lease 해제 + 리티클 off + 패널 숨김 →
   `ClearSelectionTarget()` + `CloseFromSelection()` → 뷰 `Close()`(침강 시작)
4. 뷰 `Close()` 의 `FocusCleared` → 컨트롤러는 `_selected == Null` 이라 즉시 반환(재주장 없음)
5. `OnToggled` 로 복귀 → `Close()` 는 이미 `UnitStrip` 이라 no-op

무선택(항아리 단독 오픈)에서는 이벤트가 발화하지 않아 기존 dismiss 동작 그대로다.

## 파생 결과 — "선택 있음 + 손패 닫힘" 이 도달 불가가 된다

손패를 닫으면서 선택을 남기던 경로가 전부 사라진다:

| 경로 | 이후 |
|---|---|
| 항아리 토글 | **선택도 해제** (이 unit) |
| 사용 가능 0장 자동 닫힘 | 선택도 해제 (unit 8) |
| 페이즈 이탈 · `ForceClose` | 컨트롤러도 함께 닫힘 (기존) |
| 게이지 0 인 채 선택만 | 손패가 **열린 채** 유지 (unit 8 의도 — 해당 없음) |

따라서:

- `1_selection_opens_hand.md` C 의 "손패가 닫힌 채 선택이 살아 있으면 항아리 탭으로 재오픈
  가능" 은 도달 불가 서술이 된다(해당 파일에 주석 처리).
- `4_focus_session_handoff.md` 의 H2 핵심 검증(항아리 탭 → 리티클 생존)도 도달 불가가 된다.
  **`FocusCleared` 는 그대로 둔다** — `_reticleShown` stale 리셋과 `ForceClose` 커버가 남아 있고,
  훗날 손패를 선택보다 먼저 닫는 경로가 다시 생기면 H2 가 그대로 재발한다. 재주장의 주
  깔때기는 `InteractionEnded`(커밋/취소/탭 즉발)로 좁아진 것이지 사라진 것이 아니다.

## 완료 기준

- [x] compile 클린 (2026-07-30 — `dotnet build Wassup.Runtime.csproj` 오류 0, 편집 2파일 경고 0.
      잔여 경고 23건은 전부 무관한 파일의 기존 `TMP_Text.enableWordWrapping` obsolete)
- [x] Play: 유닛 선택 → 각성 버튼 탭 → **손패 침강 + 선택 해제**(줌·슬로모·리티클·패널)가 함께
      일어나 기본 전투 상태로 돌아온다
- [ ] Play: 각성 버튼 재탭 → 손패가 **무선택 모드**로 열린다(Pulse 정상, 카드 탭 시 움찔 없이
      브리핑만 — 즉발 대상 없음)
- [ ] Play: 무선택 상태에서 항아리 열기 → 항아리 재탭 → 손패만 닫힘(기존 dismiss 불변)
- [ ] Play: 선택 중 카드를 **잡은 채** 각성 버튼 탭 → 드래그 취소(무차감) + 전부 해제, 잔류
      리티클/lease 없음
- [ ] Play: 전이 중(딜인·침강 0.4초) 각성 버튼 연타 → mash 가드로 무시, 상태 꼬임 없음

확인: 2026-07-30 사용자 Play — **기본 동작 확인**("기능 자체는 잘 동작한다"). 나머지 4항목(재탭
무선택 오픈 · 무선택 dismiss 불변 · 드래그 중 탭 · 전이 중 연타)은 **개별 확인 전**이며 unit 5
Play e2e 에서 함께 훑는다.
