# 17 — 두 부착 안내를 서로 독립으로 (한쪽이 다른 쪽을 삼키지 않게)

> 추가 2026-07-30 (사용자 발의). unit 16 이 "후속 후보"로 미뤄둔 항목을 정식으로 푼다.
> unit 16 선행 필수(분기 자체가 거기서 생겼다).

## 목적

unit 16 은 문구를 오픈 경로로 갈랐지만 **완료 저장은 하나**로 남겨뒀다. 그래서 먼저 뜬 쪽이
`awakeningHintVersion` 을 저장하고 `_awakeningArmedThisBattle` 을 내리면 **반대쪽은 그 판에도,
이후 판에도 영영 안 나온다**. 항아리로 먼저 열어본 플레이어는 탭 즉발을 못 배운다(사용자 관찰).

두 안내는 **서로 다른 조작**을 가르친다. 하나를 봤다고 다른 하나가 필요 없어지지 않는다.
각자의 상태에서 각자 한 번씩 뜨게 한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` — 신규 진행 필드 1개
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs` — 경로별 API + 파생 인트로 술어
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — 게이트 재배선
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs` — 리네임 반영 + 신규 커버리지

## 구현

### A. 플래그 2개 · 인트로는 파생

| 개념 | 저장 위치 | 언제 완료 |
|---|---|---|
| 드래그 부착 안내 | `awakeningHintVersion` (기존 필드 재사용) | 항아리 오픈에서 안내가 떴을 때 |
| 탭 즉발 안내 | `awakeningTapAttachHintVersion` (**신규**) | 선택 오픈에서 안내가 떴을 때 |
| 인트로(0·A단계) | **저장하지 않는다 — 파생** | 둘 중 **하나라도** 배우면 끝 |

```
ShouldRunAwakeningIntro = IsDragAttachHintPending && IsTapAttachHintPending
```

**세 번째 필드를 만들지 않는 이유**: 인트로를 별도 플래그로 두면 "둘 다 pending 일 때만"과
같은 값을 두 곳에 들고 있게 된다(이중 상태). 파생이면 정의상 어긋날 수 없다.

**`||` 가 아니라 `&&` 인 이유**: 한쪽만 쓰는 플레이어에게 인트로가 **영원히** 뜬다.
이미 아는 조작을 매 판 안내하는 잔소리가 된다. 하나를 배우면 "덱을 여는 법"은 이해한 것이다.

**JSON 필드 이름은 `awakeningHintVersion` 그대로 둔다.** 이름을 바꾸면 기존 프로필의 진행이
0 으로 읽혀 튜토리얼이 되살아난다. 의미가 좁아진 것은 주석과 **API 이름**으로 드러낸다.

### B. API 이름을 의미에 맞춘다

`ShouldRunAwakeningHint` 는 이제 "드래그 안내"만 뜻하므로 이름을 좁힌다. 저장 필드명과 달리
API 는 호출처가 레포 안에만 있어 리네임 비용이 없다(컨트롤러 + `TutorialProgressTests`).

- `AwakeningHintVersion` → `DragAttachHintVersion`
- `IsAwakeningHintPending` → `IsDragAttachHintPending`
- `ShouldRunAwakeningHint` → `ShouldRunDragAttachHint`
- `CompleteAwakeningHint` → `CompleteDragAttachHint`
- 신규: `TapAttachHintVersion` · `IsTapAttachHintPending` · `ShouldRunTapAttachHint` ·
  `CompleteTapAttachHint` · `ShouldRunAwakeningIntro`

`ResetAll` / `ResetAllInJson` 에 신규 토큰을 더한다. **`changed` 표현식에 반드시 포함**한다 —
`ProfileStore.ResetTutorialProgressAt` 이 그 bool 로 백업과 파일 교체를 게이팅하므로, 쓰기만
하고 표현식에서 빠지면 그 토큰만 다를 때 디스크에 영영 안 닿는다(파일의 기존 주석 경고).

### B-rev. 진입 신호가 하나로는 부족하다 (2026-07-30 사용자 보고)

초판은 `HandOpened` 만 들었는데, **항아리로 먼저 열어둔 뒤 유닛을 탭하면 안내가 안 떴다.**

`HandOpened` 는 `OpenRoutine()` 끝에서 발화하고, 그 코루틴은 `Open()` 이 돌 때만 시작된다.
이미 열려 있으면 `OpenForSelection()` 이 `State == HandState.Hand` 에서 즉시 return 하므로
(`DreamcatcherHandView.cs:183` — selection-hand-attach 계약 "선택 전환은 재딜 없음")
**Open 도 HandOpened 도 돌지 않는다.** 저장 분리는 맞았는데 신호를 잘못 골랐다.

그래서 신호를 **대상이 잡히는 시점**으로 옮긴다 — `DreamcatcherHandView.SelectionTargetSet`
(non-null 대입 시 발화). 두 신호는 상보적이다:

| 상황 | 오는 신호 | 처리 |
|---|---|---|
| 손패 닫힘 → 유닛 탭 | `SelectionTargetSet`(State 아직 UnitStrip → 흘림) 그다음 `HandOpened` | HandOpened 가 처리 |
| 손패 열림(항아리) → 유닛 탭 | `SelectionTargetSet` 만 | 이 신호가 처리 |
| 손패 닫힘 → 항아리 탭 | `HandOpened` 만 | HandOpened 가 처리 |

둘 다 같은 `EvaluateCardHint()` 로 들어간다. 중복은 경로별 판당 래치가 막는다.

**대칭에 대한 정확한 서술**: "선택 먼저 → 항아리" 는 항아리 탭이 **한 번 더** 필요하다.
선택 중 항아리 탭은 unit 9 계약상 "그만하기"(손패+선택 동시 해제)이므로, 다시 탭해야 무선택
오픈이 되고 그때 드래그 안내가 뜬다. 이건 설계된 동작이지 결함이 아니다.

### C. B단계 가드 — A 선행 요구를 뗀다

unit 12 는 B 에 `_awakeningOfferedThisBattle && _awakeningArmedThisBattle`(= A 가 실제로 떴다)를
요구했다. 그 목적은 **"B 가 A 의 완료 저장을 앞당겨 훔치는 것"** 이었다.

그 위험이 사라졌다: 저장이 경로별이라 탭 안내가 드래그 안내를 소비할 수 없고, 인트로는
파생이라 훔칠 저장 자체가 없다. 반대로 **가드를 남기면 이 unit 이 성립하지 않는다** — 인트로가
끝난 뒤엔 A 가 안 뜨므로 `_awakeningOfferedThisBattle` 이 false 로 고정돼 남은 한쪽 안내가
영영 발화하지 못한다.

가드의 나머지 의미("낼 수 있는 카드가 있을 때만 사용법을 말한다")는 기존 **usable 슬롯 탐색**이
이미 강제한다(`usable == null` 이면 return). 그러므로 A 선행 요구만 뗀다.

`_awakeningArmedThisBattle` 은 B 가 유일한 독자였으므로 **제거**한다. 남겨두면 아무도 안 읽는
필드가 unit 12 주석과 함께 남아 계약을 오독하게 만든다. 대신 경로별 판당 래치를 둔다
(`_dragHintShownThisBattle` · `_tapHintShownThisBattle`) — 저장이 실패해도(`TrySaveProfile` 은
예외를 삼킨다) 한 판에서 같은 안내가 반복되지 않는다.

`_awakeningOfferedThisBattle` 은 **남긴다** — A단계의 판당 1회를 여전히 지킨다.

**대신 첫 판 봉인을 직접 건다.** 예전엔 카드 안내가 `_awakeningOfferedThisBattle` 요구를 통해
`_awakeningLockedThisMatch` 의 보호를 **간접적으로** 받고 있었는데, 그 요구를 걷어내며 보호도
함께 사라졌다. 지금은 첫 판에 손패를 열 방법이 없어(항아리 숨김 + unit 15 선택 봉인) 도달
불가지만, 그 두 장치에만 기대지 않도록 `EvaluateCardHint` 첫 줄에서 직접 막는다.

### D. 기존 계정 이관

`awakeningHintVersion = 1` 인 계정은 드래그 안내를 배운 것으로 읽힌다 → 인트로는 파생 완료
(잔소리 없음), 탭 플래그는 pending → **다음에 유닛을 선택해 손패를 열 때 탭 즉발 안내가 한 번
뜬다.** 실제로 그들은 탭 즉발을 배운 적이 없으므로 이관 방향이 맞다.

## 완료 기준

- [x] compile 클린 (2026-07-30 — Runtime · Tests.EditMode · Tests.PlayMode 3개 어셈블리 오류 0)
- [x] EditMode: 경로별 pending/complete 독립 · 인트로 파생(`&&`) · `ResetAll` 과
      `ResetAllInJson` 이 신규 토큰까지 되돌리고 `changed` 에 반영한다 — 신규 4건 통과
- [x] EditMode 전체 회귀 0 (`TutorialProgressTests` 리네임 반영 포함) —
      **testrig 배치 실행 1611 / 통과 1609 / 실패 0 / 스킵 2**
- [ ] Play: 둘째 판에 **항아리로 먼저** 열어 드래그 안내를 본 뒤, 같은 판에서 유닛을 선택해
      손패를 열면 **탭 즉발 안내가 뜬다**(이 unit 의 핵심 — 지금은 안 뜬다)
- [ ] Play: 반대 순서(선택 먼저 → 항아리)도 대칭으로 동작한다
- [ ] Play: 각 안내는 **각자 한 번씩만** — 같은 판에서 같은 경로로 다시 열면 안 뜬다
- [ ] Play: 한쪽만 본 채 판을 끝내면 **다음 판에 인트로는 안 뜨고**(잔소리 없음) 못 본 쪽
      안내는 그 경로를 처음 쓸 때 뜬다
- [ ] Play: 둘 다 본 뒤에는 0·A·B 어느 것도 다시 뜨지 않는다

구현 `4665fc1f` (2026-07-30). **Play 항목은 사용자 확인 대기** — units 15·16 과 같은 리셋
1회로 함께 본다.

> **검증 환경 메모**: 에디터가 Play Mode 로 점유돼 있어(병행 세션) MCP `run_tests` 가
> `"Cannot start a test run while the Editor is in or entering Play Mode"` 로 막혔다.
> `wassup-testrig` 워크트리에서 배치로 돌려 우회했다. **`-runTests` 에 `-quit` 를 같이 주면
> 테스트 전에 종료돼 결과 XML 이 안 나온다**(exit 0 이라 성공으로 보인다) — 빼야 한다.
> 이 배치 런은 깨끗한 HEAD 라 워크트리에서 상시 실패하던 `MultiGoalPoolSeparationTests` 도
> 통과했다 — 그 실패가 타 세션의 dirty `MapDocument_Zig` 탓임이 확인된다.
