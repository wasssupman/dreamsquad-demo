# 9 — 챕터 D: 히스토리 버튼

## 목적

두 판을 뛴 뒤 로비에 도착하면 히스토리 버튼을 포커스하고 **실제로 눌러보게** 한다. 진행 신호는
언제나 "플레이어가 진짜 버튼을 누른 사건" 이라는 이 spec 의 철학을 그대로 잇는다.

**비목표 뒤집기**: 이 spec 의 "비목표" 는 히스토리 안내를 **제외**하고 있었다 —
근거는 "`HistoryButton` 은 게스트에게 아예 숨겨진다" 였다(`OutgameMenuController.cs:127-131` 의
`historyBtn.gameObject.SetActive(UserSession.HasAccount)`). 그 근거는 지금도 유효하므로 제거하지
않고 **게이트로 흡수**한다: 게스트에겐 챕터가 아예 열리지 않는다. 사용자 재승인 2026-08-01.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs` — `ShouldRunLobbyHistoryHint`
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs`
- `Assets/_Project/Scenes/OutgameScene.unity` — `historyButton` 배선

## 구현

**게이트**: `ShouldRunLobbyHistoryHint(holder, matchesRequired)` =
`IsLoadedThisSession && profile != null && matchesPlayed >= 2 && IsLobbyHistoryHintPending`.
**앞 챕터 완료를 체인하지 않는다**(unit 8 목적 참조). 계정 조건(`UserSession.HasAccount`)은
`TutorialProgress` 가 아니라 **컨트롤러**가 건다 — 진행 정책 순수 함수에 세션/네트워크 상태를
끌어들이면 EditMode 테스트가 전역 상태에 묶인다.

**스텝**: `Step.HistoryFocus` 를 열거에 추가하고 `TryStart` 의 if/else 사슬 **맨 뒤**에 잇는다.

구조는 **챕터 C 와 같은 1단계 포커스**다(문구 하나라 "읽기 → 지목" 2단계가 필요 없다). 그래서
C 가 남긴 두 함정을 그대로 따른다:

- `EnterStep` 에서 **`overlay.Show()` 를 직접 부른다.** 포커스 단계에서 시작하는 챕터는
  이전 챕터의 `Hide()` 로 DimRoot 가 비활성인 채라, 켜지 않으면 말풍선만 뜨고 dim 이 안 나온다.
- **대상이 없거나 비활성이면 챕터를 아예 열지 않는다**(경고 로그 + `_step = None`). A·B 의
  "구멍 없이 표시 → dim 탭 종료" 폴백은 여기 성립하지 않는다 — 아래처럼 dim 탭이 무반응이라
  8초 Skip 이 뜰 때까지 로비가 통째로 잠긴다. 완료를 저장하지 않으므로 배선을 고치면 다음
  복귀에서 정상 노출된다. 게스트도 버튼이 비활성이라 이 경로로 조용히 떨어진다(이중 안전).

**진행 = 실제 버튼 클릭만**(사용자 결정 2026-08-01). `OnOverlayTapped` 의 `HistoryFocus` case 는
**무반응** — `IntroFocus`·`KeyringFocus` 와 같은 편이다. 이 spec 의 계약이 "새 포커스 단계를
추가할 때 어느 쪽인지 **명시적으로** 고른다" 이므로 case 를 빠뜨리지 말 것. 완료는 `ShowFocus` 가
거는 `GetComponent<Button>()` 훅(`OnFocusedButtonClicked`)이 나른다.

**나머지 분기 3곳에 case 를 빠짐없이 추가한다** — `OnFocusedButtonClicked` 의 허용 스텝 목록 ·
`OnEscapeRequested` 의 허용 스텝 목록 · `CompleteAndEnd` 의 챕터별 저장 분기. 마지막 것은 이
spec 의 명시 계약이다("`CompleteAndEnd` 의 챕터 분기는 챕터 수만큼 있어야 한다. 2분기로 남기면
C 가 B 의 플래그를 다시 쓰고 C 는 영원히 pending 이 된다").

**문구**: `히스토리에서 지난 판의 기록을 볼 수 있어요!`

## 알려진 한계

`TryStart` 는 if/else 사슬이라 **한 번에 한 챕터**다. 챕터 C 가 아직 pending 이면 그 판의 로비
도착은 C 가 가져가고 D 는 다음 도착으로 밀린다. C 는 로비 도착마다 재시도되므로 곧 소진되지만,
"정확히 2번째 판 직후" 가 아니라 "2번째 판 이후 가장 이른 빈 자리" 가 실제 동작이다.

## 완료 기준

- [x] compile clean · EditMode **1796 중 실패 0**.
- [x] 씬 배선 실측: `historyButton` → 로비 `HistoryButton`(`Button` 존재, 형제 배선 무손상).
- [x] 두 판을 끝낸 실계정으로 로비 도착 → dim + 히스토리 버튼 포커스 + 문구.
      (**사용자 Play 확인 2026-08-01 통과.** 프로필 실측: `matchesPlayed 3` ·
      `lobbyHistoryHintVersion 1` — 완료 저장은 버튼 클릭 경로에만 있으므로 실제로 눌러 끝난 것.)
- [ ] **dim 탭 무반응** 재확인 (노출·클릭 경로는 확인됨).
- [ ] 재진입 시 미노출(완료 저장). `RESET TUTORIAL` 후에는 **판을 더 뛰지 않아도** 다시 뜬다
      (`matchesPlayed` 는 리셋 대상이 아니다).
- [ ] **게스트 계정에서 미노출** — 버튼이 숨겨져 있고 챕터가 열리지 않는다(로비가 잠기지 않는다).
- [ ] 8초 무진행 시 건너뛰기 노출.
- [ ] 챕터 A·B·C 회귀 없음.
