# 13 — 키링 드롭 → 재출발 START 포커스

## 목적

시퀀스의 마지막. 키링을 잡아 흔들고 **놓아 착지한 뒤** START 버튼을 지목해
`새로 구성한 덱으로 다시 게임시작!` 로 닫는다 — 스쿼드·드림캐쳐에서 손본 결과를 바로 다음 판에
써보게 한다. 선행: unit 12(이 스텝의 진입 조건인 키링 완료를 12 가 만든다).

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs`
- `Assets/_Project/Tests/EditMode/OutgameTutorialChapterCTests.cs`

**`LobbyKeyringDrag` 수정 없음 · 씬 배선 없음** — `startButton` 은 챕터 A 의 SerializeField 재사용.

## 구현

**시퀀스에서 로비 복귀 이벤트가 없는 유일한 이음매다.** B1→B2→C 는 패널 왕복이 `OnLobbyShown`
을 다시 불러 다음 스텝을 집어주지만, 키링 드래그는 화면을 떠나지 않는다. 여기만 in-visit 전이다.

**신규 스텝 2개**: `Step.KeyringSettling`(dim·말풍선이 꺼진 대기 — `_step` 이 None 이 **아니어야**
재진입 가드가 유지된다) · `Step.StartFocus`.

**전이**: `OnKeyringDragStarted` 는 지금처럼 그 자리에서 키링 토큰을 저장하고 dim 을 걷는다 —
잡는 순간 화면을 비워야 스윙과 낙하가 가려지지 않는다(unit 6 계약, 되돌리지 말 것). 다만
`EndChapter` 로 끝내지 않고 `_step = KeyringSettling` 로 넘어간다.

**⚠️ 대기 시각을 반드시 새로 찍는다.** `_stepEnteredAt` 은 `EnterStep` 안에서만 갱신되는데
(`OutgameTutorialController.cs:145`) 이 전이는 `EnterStep` 을 타지 않는다. 그대로 두면 타임아웃
기준이 **KeyringFocus 진입 시각**이 되어, 안내를 읽고 4초 넘게 지난 뒤 캐릭터를 잡으면 잡자마자
폴백이 만료돼 **드래그 중에 dim 이 올라온다**(8초 Skip 설계가 전제하는 체감 속도가 바로 그
구간이다). 전용 필드 `_settleStartedAt` 을 두고 전이 시점에 찍는다.

**폴링**: `Update` 에서 `keyringCharacter != null && !keyringCharacter.IsBusy` 면
`EnterStep(StartFocus)`.

- **신규 이벤트를 만들지 않는 이유**: `IsBusy` 는 `Dragging` 뿐 아니라 `Falling` 까지 포함하므로
  "놓았다"가 아니라 **착지까지** 기다려 준다(`LobbyKeyringDrag.cs:40`·`:154`). 놓자마자 dim 을
  올리면 낙하 연출을 덮는다. 낙하 중 재잡기(`Falling → Dragging`)도 자동으로 흡수된다.
- **배치**: `Update` 첫 줄은 포커스 단계가 아니면 즉시 return 하므로(`:355-356`), 폴링은
  **그 가드보다 앞**에 별도 블록으로 둔다. `KeyringSettling` 을 Skip 노출 목록에 넣어 해결하려
  하지 말 것 — 말풍선이 없는 구간에 Skip 만 뜬다.
- **타임아웃 폴백 필수**: `keyringSettleTimeoutSeconds`(SerializeField, 기본 4초). 착지 신호가
  오지 않으면 화면에 아무 표시도 없는 채로 시퀀스가 영영 멈춰 **육안 발견이 늦다**.
  단 **드래그 중에는 타이머를 계속 리셋한다**(`LobbyKeyringDrag.AnyDragging` 이 true 인 프레임마다
  `_settleStartedAt` 갱신) — 키링은 만지작거리라고 만든 장난감이라 4초 초과 홀드가 예외가 아니다.
  낙하는 `gravity 4000px/s²` 라 폴백과 여유가 크다.

**`OnOverlayTapped`**: `StartFocus` 는 **무반응**(`IntroFocus` 와 같은 편, START 를 실제로 눌러야
끝난다). `KeyringSettling` 은 오버레이가 꺼져 탭이 도달하지 않지만 case 를 명시해 "빠뜨려서
무반응"과 "골라서 무반응"을 구분한다.

**`StartFocus` 의 fail-open 은 C·D 계약을 **통째로** 따른다** — `overlay.Show()` 를 직접 부르는
것뿐 아니라 **대상이 null/비활성이면 스텝을 아예 열지 않는 사전 검사까지**. `KeyringSettling`
중에 플레이어가 로비 버튼을 누르면 `RaiseExclusive` 가 `menuRoot` 를 비활성화하므로
(`OutgameMenuController.cs:306`) `startButton` 이 비활성이 될 수 있다. 검사가 없으면 `ShowFocus`
의 `_holes.Count == 0` 폴백을 타 **구멍 없는 풀 dim 이 열린 패널 위에 얹히고**, 이 스텝은 dim
탭이 무반응이라 8초 Skip 까지 화면이 통째로 잠긴다. 완료를 저장하지 않으므로 다음 로비 도착에서
정상 노출된다.

**완료**: `OnFocusedButtonClicked` 에 `StartFocus` 추가(`ShowFocus` 의 임시 구독 통과구멍 그대로).
**`CompleteAndEnd` 의 case 를 반드시 따로 둔다** — 챕터 A 의 `IntroFocus` 가 같은 `startButton` 을
쓰므로, 빠뜨리면 이 스텝이 A 의 플래그를 다시 쓰고 자기 토큰은 0 으로 남아 영원히 pending 이
된다(챕터 C 에서 실제로 났던 결함과 동형).

**알려진 fail-open**: `OnStartGame` 은 로드아웃 게이트 미충족이나 매칭 실패로 입장하지 않을 수
있다. 그래도 클릭은 발생했으므로 완료가 저장되고 안내는 끝난다 — 목적("여기서 다시 시작한다")은
달성됐고, 저장을 건너뛰면 다음 로비 진입마다 반복된다.

## 완료 기준

- 컴파일 0 (Runtime · Tests.EditMode)
- EditMode (하네스로 작성 가능한 것만):
  - `DragStarted` 가 **키링 토큰만** 저장하고 `_step` 이 `KeyringSettling` 이 된다
    (스타트 토큰 pending 유지 · `_saved.Count == 1`)
  - `StartFocus` 의 dim 탭이 완료를 저장하지 않는다
  - `StartFocus` 에서 `OnFocusedButtonClicked` 가 **스타트 토큰을 1 로 만들고** `_saved.Count == 1`
    (챕터 A 회귀 — `lobbyIntroVersion` 이 이미 1 인 하네스에서는 "값 불변" 단언이 멱등 return
    때문에 결함을 못 잡는다. 저장 호출 수로 본다)
  - **폴링·폴백의 `StartFocus` 진입은 EditMode 로 관측할 수 없다**(unit 12 와 같은 이유 —
    `EnterStep(StartFocus)` 가 `overlay.Show()` 를 탄다). Play 검증으로 대체한다
- Play 확인: 드림캐쳐 페이지를 닫고 복귀 → 키링 안내 → 잡으면 dim 즉시 소멸 → **4초 넘게 붙잡고
  흔들어도 dim 이 올라오지 않는다** → 놓고 착지한 뒤 START 포커스와 문구 → 클릭 시 정상 입장.
  콘솔 경고 0.
- 회귀 확인: 챕터 A(최초 로비)에서 START 를 눌렀을 때 스타트 토큰이 저장되지 않는다.
