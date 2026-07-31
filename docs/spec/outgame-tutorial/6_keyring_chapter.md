# 6 — 챕터 C: 로비 캐릭터 키링 드래그

## 목적

챕터 B(스쿼드·드림캐쳐 버튼)를 통과해 패널을 열었다 닫고 로비로 돌아온 순간, 로비 배경 캐릭터를
**실제로 끌어보게** 한다. 문구 1개 · 단계 1개.

검증 질문: 신규 플레이어가 로비 배경 캐릭터가 만질 수 있는 물체임을 첫 복귀에서 알아채는가?

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` — `lobbyKeyringHintVersion` 필드 추가
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs` — 상수 · pending · Complete · 리셋 2경로
- `Assets/_Project/Scripts/UI/Outgame/LobbyKeyringDrag.cs` — `DragStarted` 이벤트
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs` — `Step.KeyringFocus`
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `ClosePanels(bool restoreLobby)`
  + 복귀 시에만 챕터 재평가
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs`
- `Assets/_Project/Scenes/OutgameScene.unity` — 신규 SerializeField 1개 배선

> 신규 필드는 `RectTransform` 이 아니라 **`LobbyKeyringDrag` 컴포넌트**로 받는다. 홀/링 대상
> RectTransform 은 그 컴포넌트의 transform 에서 파생시킨다 — 두 필드로 나누면 배선이 서로
> 다른 오브젝트를 가리켜도 컴파일·플레이가 조용히 통과한다.

## 구현

### 진행 상태

- `LobbyKeyringHintVersion = 1` · `IsLobbyKeyringHintPending` · `CompleteLobbyKeyringHint`
- `ShouldRunLobbyKeyringHint(holder)` = `IsLoadedThisSession && !IsLobbyLoadoutHintPending(profile)
  && IsLobbyKeyringHintPending(profile)`. **B 완료를 전제로 걸어** A·B·C 가 동시에 pending 될 수
  없게 한다(`ShouldRunLobbyLoadoutHint` 와 동형 — 순서를 위한 별도 상태가 필요 없다).
- 신규 토큰을 `ResetAll` **과** `ResetAllInJson` **양쪽의 `changed` 표현식에** 넣는다.
  빠지면 이 토큰만 0 이 아닐 때 `ResetTutorialProgressAt` 이 백업·파일 교체를 건너뛰어
  **리셋이 디스크에 영영 안 닿는다**(unit 17 교훈, 테스트로 고정).

### 진입

패널을 닫고 로비가 돌아온 지점에서 `outgameTutorial?.OnLobbyShown(UserSession.IsSignedIn)` 을 부른다
(`ClosePanels` 말미, 아래 `restoreLobby` 조건부). `OnLobbyShown` 은 멱등하고 `_step != Step.None`
가드가 재진입을 막으므로 신규 메서드를 만들지 않는다 — "챕터 B 완료 → 패널 열림 → 닫힘" 이
정확히 이 경로다.

> **`ClosePanels()` 는 패널을 여는 경로에서도 먼저 호출된다.** `RaiseExclusive(panel)` 의 첫 줄이
> `ClosePanels()` 이고 그 다음 줄이 `panel.SetActive(true)` 다. 그대로 훅을 달면 스쿼드/드림캐쳐를
> **여는 순간** 챕터 C 가 시작해 열리는 패널 위에 dim 과 말풍선이 얹힌다(캐릭터는
> `lobbyCharactersRoot` 라 `menuRoot` 와 달리 숨겨지지도 않는다).
>
> 그래서 `ClosePanels(bool restoreLobby = true)` 로 바꾸고 **`restoreLobby == true` 일 때만** 알린다.
> `RaiseExclusive` 와 `OnResetAccount` 는 `false` 를 넘긴다(전자는 곧 패널을 띄우고, 후자는 곧
> `ApplyAuthGate` 가 로그아웃 중단을 호출한다 — 알렸다가 즉시 중단시킬 이유가 없다).

`TryBeginChapter` 의 분기 말미에 `else if (ShouldRunLobbyKeyringHint) EnterStep(Step.KeyringFocus)`.
**A → B → C 순서가 계약이다.**

### 단계 (1개)

- `ShowFocus(KeyringText, keyringCharacter)` — 문구와 포커스를 **동시에** 낸다(사용자 결정
  2026-08-01). 문구가 하나뿐이라 A·B 의 "읽기 → 지목" 2단계가 필요 없다.
- 문구: `배경에 있는 캐릭터를 끌고 드래그 해보세요` — **사용자 작성본. 임의로 고치지 않는다.**
- 대상: `World`(제자리형) 캐릭터 하나(사용자 결정). `Hello`(배회형)는 홀이 매 프레임 움직여
  조준이 어렵다. 오버레이는 이미 대상 코너 변화를 추종하므로 기술적 제약은 아니다.
- dim 홀 1개 → 홀에는 그래픽이 없어 레이캐스트가 authored Canvas(order 0)로 떨어지고
  `LobbyKeyringDrag` 가 드래그를 받는다. 챕터 A·B 와 **같은** 통과구멍이다.
- **캐릭터 rect 에 투명 여백이 있는지 실측한다**(`LobbyKeyringDrag.HeadOffsetY` 주석이 상단 투명
  여백의 존재를 전제한다). 여백이 크면 홀과 링이 캐릭터보다 훨씬 크게 잡혀 "무엇을 끌라는지"가
  흐려진다. 그때는 `holePadding` 이 아니라 **대상 rect 를 좁히는 방향**으로 조정한다 —
  `holePadding` 은 전 챕터 공용이다.

### 완료 신호

- `LobbyKeyringDrag` 에 `public event Action DragStarted` 추가 → `OnBeginDrag` 의 **성공 경로
  말미**(`_phase = Phase.Dragging` 뒤)에서 발화. 조기 return(settings null · 두 번째 포인터 ·
  좌표 변환 실패)에서는 발화하지 않는다.
- 컨트롤러는 대상의 `LobbyKeyringDrag` 를 런타임 한정 구독하고 **종료·중단·파괴 3경로 모두**에서
  해제한다(`ReleaseButtonHooks` 와 같은 규율).
- **`ShowFocus` 의 기존 `GetComponent<Button>()` 훅은 캐릭터에서 조용히 no-op 이다** — 캐릭터에
  Button 이 없다. 드래그 구독을 별도 경로로 명시하지 않으면 챕터가 영원히 끝나지 않는다.
- 잡는 순간 완료·종료 → dim 이 걷혀 키링 스윙을 가리지 않는다.

### dim 탭은 무반응

`OnOverlayTapped` 의 `KeyringFocus` case 는 **명시적으로 no-op** 이다(`IntroFocus` 와 같은 쪽).
바로 위 case 인 `LoadoutFocus`(B)는 dim 탭으로도 `CompleteAndEnd()` 하므로, **그 줄을 복붙하면
드래그를 한 번도 안 하고 완료가 저장된다** — 이 챕터의 목적이 통째로 사라진다.

### 탈출구 · fail-open

- `Update` 의 스텝 가드에 `KeyringFocus` 를 추가해 `escapeDelaySeconds`(8초) Skip 노출과
  Esc/백키를 A·B 와 동일하게 받는다.
- **대상 미배선·비활성이면 챕터를 아예 열지 않는다**(경고 로그 + `_step = None`, 완료 저장 없음).
  A·B 의 "구멍 없이 표시 → dim 탭 종료" 폴백을 C 에 쓰면 안 된다 — C 의 dim 탭은 의도적
  no-op 이라 **8초 Skip 이 뜰 때까지 로비가 통째로 잠긴다**. 저장을 안 하므로 배선을 고치면
  다음 복귀에서 정상 노출된다.
- `EnterStep` 은 첫 줄에서 `_step` 을 세우므로, 생략 경로는 `_step` 을 `None` 으로 되돌려야
  한다. 안 하면 이 세션 내내 `_step != None` 재진입 가드에 걸려 어떤 챕터도 못 뜬다.
- **`CompleteAndEnd` 의 `isIntro` 2분기를 3분기로 확장한다.** 누락하면 챕터 C 가 챕터 B 의
  플래그를 다시 쓰고 **C 는 영원히 pending** 이 된다.

## 완료 기준

- 컴파일 오류 0 (Runtime · Tests.EditMode)
- `TutorialProgressTests` 신규 4건: ① C 는 B 완료 전엔 pending 아님 ② `CompleteLobbyKeyringHint`
  멱등 ③ `ResetAll` 이 이 토큰만 다를 때도 `changed = true` ④ `ResetAllInJson` 이 이 토큰을 0 으로
  되돌리고 미지 필드를 보존
- Play: 튜토리얼 리셋 → 로비 A → 첫 판 → 복귀 B → 스쿼드 열고 닫기 → **C 노출** →
  World 캐릭터 드래그 → dim·말풍선 즉시 종료 → 로비 재진입 시 미노출
- **패널을 여는 순간에는 C 가 뜨지 않는다**(위 `restoreLobby` 회귀). 스쿼드·드림캐쳐·테스트모드·
  히스토리 4개 모두 확인
- C 표시 중 dim 탭 → 무반응(완료 저장 없음). 8초 뒤 건너뛰기 노출
- 콘솔 경고·에러 0
