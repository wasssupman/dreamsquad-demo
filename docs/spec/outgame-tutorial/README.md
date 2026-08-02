# outgame-tutorial — 로비 통과구멍 온보딩

> 상태: **units 0~4 완료 2026-07-21 · 동작 확인됨 · 모바일 실기기 QA 는 보류(사용자 결정) ·
> 챕터 C(unit 6) **완료 2026-08-01** (`f0d05bd7`, 리뷰 반영 포함) — **사용자 Play 확인 통과**.
> 인계는 `7_handoff_summary.md` ·
> 챕터 D 히스토리(units 8~9) **완료 2026-08-01** (`98c315d1`·`3c996168`, 카운터 fix `d37acc76`)
> — **사용자 Play 확인 통과**(노출·클릭 종료). dim 탭 무반응·게스트 미노출은 미확인.
> 인계는 `10_handoff_summary.md` ·
> 로드아웃 시퀀스 재편(units 11~13) **완료 2026-08-02** (`10fad0c2`)
> — **사용자 Play 확인 통과**. 씬 변경 없음. 인계는 `14_handoff_summary.md`
> 선행: `first-session-tutorial` (완료) · `outgame-lobby-layout` (완료) · `lobby-keyring-drag` (완료)

## 검증 질문

신규 플레이어가 첫 로비 진입과 첫 복귀에서 각각 **다음에 눌러야 할 버튼을 실제로 눌러보고** 넘어가는가?

확장(unit 6): 패널을 닫고 로비로 돌아온 순간, **로비 배경 캐릭터를 실제로 끌어보고** 그것이
만질 수 있는 물체임을 알아채는가?

인지 여부는 이 spec 이 검증하지 않는다 — 노출과 실행만 보장한다. 인지 지표는 "비목표" 참조.

## 상위 목표

인게임 튜토리얼(`first-session-tutorial`)의 **행동 성공 신호로 진행한다**는 철학을 로비에서도 유지한다.
dim 은 주의를 좁히는 수단일 뿐이고, 진행시키는 것은 언제나 **플레이어가 실제 버튼을 누른 사건**이다.

| 챕터 | 시점 | 문구 | 포커스 | 진행 신호 |
|---|---|---|---|---|
| A 인트로 | 로그인 후 로비 최초 노출 | `악몽이 몰려옵니다. 꿈결특공대, 출동!` → `이 버튼을 눌러 출발!` | `StartButton` | 실제 클릭 |
| B1 스쿼드 | 첫 판 이후 로비 복귀 | `더 잘 막고 싶다면, 함께 싸울 유닛부터!` / `스쿼드를 눌러보세요.` | `SquadButton` | 실제 클릭 |
| B2 드림캐쳐 | B1 패널을 닫고 로비 복귀 | `이번엔 드림캐쳐 덱 차례!` / `드림캐쳐를 눌러보세요.` | `DreamcatcherButton` | 실제 클릭 |
| C 키링 | B2 패널을 닫고 로비 복귀 | `배경에 있는 캐릭터를 끌고 드래그 해보세요` | `World` 캐릭터 | 드래그 시작 |
| E 재출발 | C 드롭 후 **착지** (같은 로비 방문) | `새로 구성한 덱으로 다시 게임시작!` | `StartButton` | 실제 클릭 |
| D 히스토리 | 두 판을 끝낸 뒤 로비 도착 (실계정 한정) | `히스토리에서 지난 판의 기록을 볼 수 있어요!` | `HistoryButton` | 실제 클릭 |

A 만 2탭이다(`문구` → 어디든 탭 → `문구 + 포커스`). **나머지는 전부 1단계 포커스** —
문구와 포커스를 한 번에 내고 지정된 조작으로 끝난다(units 11~13 이 B 의 2탭 프리앰블을
제거했다 — 사용자 결정 2026-08-02, 백로그 "온보딩 총량").

챕터 문자는 이름일 뿐 순서가 아니다. 진행 순서는 **A → B1 → B2 → C → E** 이고,
**D 는 플래그 사슬에 얹히지 않는 독립 게이트**(`matchesPlayed >= 2`)다. 다만 `TryBeginChapter`
의 else-if 는 **한 도착에서 하나만** 고르므로, 도착 경쟁에서는 D 도 사슬 뒤에 선다 —
정상 진행에서는 E 의 START 가 2판째를 시작하므로 그 복귀에서 D 가 잡힌다.

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_progress_state.md` | 프로필 상태 | 챕터 A/B 버전 플래그와 기존 리셋 경로 편입 |
| 1 | `1_dim_overlay.md` | 통과구멍 레이어 | 사각형 차집합 dim + 홀 통과. 차집합은 순수 함수 |
| 2 | `2_intro_chapter.md` | 챕터 A | 로비 최초 노출 → StartButton 실제 클릭 |
| 3 | `3_loadout_chapter.md` | 챕터 B | 첫 판 복귀 → 스쿼드/드림캐쳐 실제 클릭 |
| 4 | `4_scene_wiring_and_qa.md` | 통합/검증 | OutgameScene 배선 + 진입 훅 + Play QA |
| 5 | `5_handoff_summary.md` | 인계 | units 0~4 커밋·검증·되돌림 금지 항목 |
| 6 | `6_keyring_chapter.md` | 챕터 C | 패널 닫고 복귀 → 로비 캐릭터 실제 드래그 |
| 7 | `7_handoff_summary.md` | 인계 | 챕터 C 커밋·검증·되돌림 금지 항목 |
| 8 | `8_matches_played_counter.md` | 독립 신호 | `matchesPlayed` 카운터 + 챕터 D 진행 토큰 |
| 9 | `9_history_chapter.md` | 챕터 D | 두 판 이후 복귀 → 히스토리 버튼 실제 클릭 |
| 10 | `10_handoff_summary.md` | 인계 | units 8~9 커밋·검증·되돌림 금지 7건 |
| 11 | `11_loadout_sequence_tokens.md` | 토대 | 시퀀스 진행 토큰 2개 + 체인 재배열 (동작 변화 0) |
| 12 | `12_squad_deck_split.md` | 챕터 B 분리 | 스쿼드 → 페이지 → 복귀 → 드림캐쳐 → 페이지 → 복귀 |
| 13 | `13_keyring_to_start.md` | 챕터 E | 키링 드롭·착지 → START 포커스 → 재출발 |
| 14 | `14_handoff_summary.md` | 인계 | units 11~13 검증·되돌림 금지 7건·Play 확인 항목 |

11 → 12 → 13 순서 필수. 12 를 먼저 하면 저장할 토큰이 없어 스텝이 서로를 덮어쓰고,
13 의 진입 조건(키링 완료)은 12 가 만든다.

## Feature-wide 계약

- **차단은 dim 조각이, 진행은 실제 대상이 담당한다.** dim 조각은 `raycastTarget = true` 로 로비 입력을
  먹고, **홀 영역에는 그래픽이 없어 레이캐스트가 아래 `MenuCanvas`(order 0)로 떨어진다.** 즉 포커스된
  버튼은 진짜로 눌린다. 완료 저장은 그 버튼 `onClick` 을 런타임 한정으로 임시 구독해 수행하며,
  인스펙터 persistent call 은 건드리지 않는다. 풀스크린 탭 캐처는 쓰지 않는다.
- **1탭째(홀 0개)는 dim 한 장이 전체를 덮고 어디를 눌러도 진행한다.** 포커스 단계에서 dim 탭을
  받을지는 **챕터마다 다르다**(`OnOverlayTapped` 의 case 가 소유):
  - `IntroFocus`(A) · `SquadFocus`(B1) · `DeckFocus`(B2) · `KeyringFocus`(C) ·
    `StartFocus`(E) · `HistoryFocus`(D) = **무반응**. 지정된 조작을 실제로 해야 끝난다 —
    버튼 위치와 드래그 제스처를 학습시키는 것이 그 단계의 목적이다.
  - dim 탭으로 종료하는 단계는 **현재 없다.** 옛 `LoadoutFocus`(B)가 유일했고, units 11~13 이
    "페이지를 실제로 열어본다"로 목적을 바꾸면서 무반응 편으로 옮겼다.
  새 포커스 단계를 추가할 때 **어느 쪽인지 명시적으로 고른다.** case 를 빠뜨리면 조용히 무반응이
  되고, dim 탭 종료를 복붙하면 아무 탭에나 완료가 저장돼 조작을 한 번도 안 해본 채 넘어간다.
- **탈출구는 지연 노출 Skip 이다.** 포커스 단계 진입 후 `escapeDelaySeconds`(기본 8초) 동안 진행이
  없으면 `ShowMessage(같은 문구, showSkip: true)` 로 Skip 을 노출한다. Android 백키(`escapeKey`)도
  동일 취급한다. Skip 은 완료 저장 후 안내만 종료하며 **챕터 A 라도 전투를 시작하지 않는다**.
- **최소 표시 시간**: 각 단계는 진입 후 `minStepSeconds`(기본 0.5초) 동안 입력을 무시한다. 씬 전환
  직후의 잔여 탭이나 연타로 문구를 읽기 전에 챕터가 소진되는 것을 막는다.
- **기존 UI 레이어를 수정하지 않는다.** 별도 Canvas 로 얹는다. `MenuCanvas`(0) 위,
  `TutorialGuidanceView` 바로 아래, `SceneTransition`(10000) 아래. 실제 order는 공용
  `TutorialGuidanceStyle`의 `dimSortingOrder`/`guidanceSortingOrder`가 소유한다
  (`first-session-tutorial` unit 14).
- **두 뷰의 GameObject 는 씬 루트여야 한다** — 어떤 Canvas 의 자식도 아니어야 한다. 중첩 캔버스에서는
  `overrideSorting` 이 렌더 순서만 올리고 레이캐스트 우선순위는 마지막 sibling 이 이긴다
  (`LoadoutGatePopup.cs:44-47`, `SquadBuilderView.cs:392-399` 의 실패 기록). 루트 형제 캔버스여야
  `GraphicRaycaster.sortOrderPriority == canvas.sortingOrder` 가 성립해 이 설계가 동작한다.
- **`PlayerProfile` 참조를 캐시하지 않는다.** 판정·완료·저장 시점마다 `profileSO.profile` 을 다시 읽는다.
  `OutgameMenuController.Awake` 는 `ApplyAuthGate`(L52) 뒤 L68 에서 프로필 인스턴스를 교체하므로,
  캐시하면 곧 버려질 객체에 완료 플래그를 쓰게 된다.
- **완료 저장은 `Complete*` 반환값이 true 일 때만 `ProfileStore.Save` 를 호출한다**
  (`FirstSessionTutorialController.cs:285-290` 선례). `SaveAt` 은 전체 파일 재작성이다.
- **말풍선·포커스 링·포인터는 `TutorialGuidanceView` 를 무수정 재사용한다.** 챕터당 문구는 1개이며
  포커스와 함께 낸다. 예외는 챕터 A 뿐(문구 2개, 2번째가 포커스 대상을 지목).
- **챕터 순서는 플래그로 보장한다.** 각 `ShouldRun*` 이 앞 스텝의 완료를 전제로 하므로
  A → B1 → B2 → C → E 가 동시에 pending 될 수 없다(`ShouldRunGiftTutorial` 선례와 동형).
  순서를 위한 별도 상태를 두지 않는다.
- **시퀀싱은 패널 왕복이 한다(units 11~13).** B1·B2 는 자기 클릭에서 끝나고, 다음 스텝은
  `ClosePanels(restoreLobby: true)` → `OnLobbyShown` → `TryBeginChapter` 가 집는다. 즉 "일시정지 후
  재개" 같은 상태를 만들지 않는다 — 토큰이 나뉘어 있으면 순서가 저절로 성립하고, 앱을 껐다 켜도
  그 자리에서 재개된다. **`RaiseExclusive` 의 `ClosePanels(false)` 가 이 설계의 전제다**: 패널을
  여는 호출에서 로비 복귀 훅이 돌면 다음 스텝이 열리는 패널 위에 얹힌다.
- **진행 토큰의 JSON 필드명을 좁히지 않는다.** `lobbyLoadoutHintVersion` 은 스텝 B1(스쿼드)의
  저장소로 **재사용**한다 — 이름을 바꾸면 기존 프로필 진행이 0 으로 읽혀 온보딩이 되살아난다.
  의미는 API 이름(`ShouldRunLobbySquadHint`)이 나른다(`awakeningHintVersion` 선례와 동형).
- **레거시 계정은 파생 가드로 막는다(unit 11).** 옛 B·C 를 마친 계정은 신규 토큰이 0 이라
  그대로 두면 B2·E 를 맥락 없이 다시 본다(B1 은 완료라 안 뜨므로 "이번엔" 이 가리킬 앞 단계가
  없다). `키링 완료 && 덱 토큰 0` 은 **레거시에서만** 성립하므로(새 순서는 덱이 키링보다 먼저)
  그 조합을 pending 판정에서 뺀다. 상태를 새로 저장하지 않는 **파생**이다 —
  `ShouldRunAwakeningIntro` 의 "이중 상태 방지" 와 같은 선택.
- **unit 11 은 순수 가산, 개명·체인 재배열은 unit 12 다.** 컨트롤러가 옛 이름을 부르므로
  (`OutgameTutorialController.cs:137`·`:389`) 개명을 먼저 하면 컴파일이 깨지고, 체인만 먼저
  바꾸면 덱 토큰을 채우는 코드가 없어 **챕터 C 가 영원히 발화하지 않는다**. 이 둘은 컨트롤러와
  같은 커밋이어야 한다.
- **Skip 은 그 스텝만 완료한다**(사용자 결정 2026-08-02). 잔여 시퀀스를 함께 소진하지 않으며,
  Skip 직후에는 패널 왕복이 없으므로 **다음 스텝은 다음 로비 도착에서** 뜬다.
- **`startButton` 의 소비자가 둘이다**(A 인트로 · E 재출발). `CompleteAndEnd` 의 case 를 스텝마다
  따로 두지 않으면 뒤 스텝이 앞 스텝의 플래그를 다시 쓰고 자기 토큰은 0 으로 남아 영원히
  pending 이 된다 — 챕터 C 에서 실제로 났던 결함이다.
- **키링 → START 사이에만 in-visit 전이가 있다(unit 13).** 그 구간엔 로비 복귀 이벤트가 없어
  `LobbyKeyringDrag.IsBusy` 폴링으로 **착지까지** 기다린다(놓자마자 dim 을 올리면 낙하 연출을
  덮는다). 표시가 하나도 없는 대기 상태라 멈추면 발견이 늦으므로 **타임아웃 폴백이 필수**다.
- **단, 챕터 D 는 그 체인을 쓰지 않는다(units 8~9).** 게이트는 `matchesPlayed >= 2` 라는 **독립
  신호**다 — 아래 백로그가 지적한 대로 "앞 챕터 완료" 체인은 선행 안내가 fail-open 경로를 타면
  뒤 안내가 영영 발화하지 못하게 만든다. `matchesPlayed` 는 튜토리얼 진행이 아니므로
  `ResetAll` 대상이 **아니다**(리셋 후 두 판을 다시 뛸 필요가 없다).
- **`matchesPlayed` 가 세는 단위는 "히스토리에 남는 판" 이다**(2026-08-01 정정). 종료 경로가
  둘이므로 기록 지점도 둘이다 — `SetPhase(Result)` 와 `MenuPopup.OnExit`(나가기). 나가기는
  `Result` 를 거치지 않지만 `AbandonMatch` 가 0점 마감해 그 판도 히스토리 엔트리를 만든다.
  **한쪽만 세면 챕터 D 가 가르치는 히스토리와 게이트가 서로 다른 것을 센다** — 실제로 두 판을
  뛰고도 카운터가 1 에 머물러 안내가 안 뜨는 결함이 났다. 판당 1회 래치로 이중 카운트를 막는다.
- **챕터 D 는 실계정 전용이다.** `HistoryButton` 은 게스트에게 숨겨지므로(`HasAccount` 게이트,
  `OutgameMenuController.cs:127-131`) 안내가 뜨면 누를 대상이 없는 막다른 길이 된다. 계정 조건은
  `TutorialProgress` 가 아니라 **컨트롤러**가 건다 — 진행 정책 순수 함수에 세션 상태를 끌어들이면
  EditMode 테스트가 전역 상태에 묶인다.
- **챕터 C 는 진행이 버튼 클릭이 아니라 드래그다(unit 6).** 진입은 `ClosePanels()` 말미의
  `OnLobbyShown` 재호출(멱등 · `_step != None` 가드), 완료는 `LobbyKeyringDrag.DragStarted` 다.
  `ShowFocus` 가 홀 대상에서 찾는 `GetComponent<Button>()` 훅은 캐릭터에서 **조용히 no-op** 이므로
  드래그 구독을 별도로 명시해야 한다 — 빠지면 챕터가 끝나지 않는다.
- **`CompleteAndEnd` 의 챕터 분기는 챕터 수만큼 있어야 한다.** 2분기로 남기면 C 가 B 의 플래그를
  다시 쓰고 C 는 영원히 pending 이 된다.
- **신규 진행 토큰은 `ResetAll`/`ResetAllInJson` 의 `changed` 표현식에 반드시 넣는다.** 빠지면 그
  토큰만 다를 때 `ResetTutorialProgressAt` 이 파일 교체를 건너뛰어 리셋이 디스크에 닿지 않는다.
- **fail-open**: 참조 누락·포커스 대상 미발견·저장 실패는 경고 로그만 남기고 로비를 잠그지 않는다.
  저장이 실패하면 다음 진입에 다시 노출되지만 Skip 탈출구가 있으므로 잠금이 아니다.
  **단 "구멍 없이 표시" 폴백(`ShowFocus` 의 `_holes.Count == 0`)은 더 이상 탈출구가 아니다** —
  dim 탭으로 끝나던 단계가 사라져(units 11~13) 이 경로의 유일한 출구는 8초 Skip 이다. 그래서
  포커스 단계는 **진입 전에 대상 활성을 검사해 아예 열지 않는 쪽**이 계약이다(C·D·B1·B2·E).
  챕터 A `IntroFocus` 만 사전 검사가 없어 이 경로가 살아 있다 — 후속 후보.
- **세션 가드 준수**: `PlayerProfileSO.IsLoadedThisSession` 이 false 면 아무것도 하지 않는다.
- **입력 차단의 근거**: 로비의 조작 경로는 전부 EventSystem 기반이다 — `LobbyKeyringDrag`(`IBeginDragHandler`),
  `WorldLobbyCharacter`/`HelloLobbyRoamer`(`IPointerClickHandler`). 씬에 Physics(2D)Raycaster 가 없고,
  `LobbyBackgroundParallax` 는 `Pointer.current` 를 직접 폴링하지만 `LobbyKeyringDrag.AnyDragging`
  게이트라 드래그가 막히면 함께 멈춘다. 따라서 dim 조각만으로 로비 입력이 전부 차단된다.
- ECS 변경 없음. UI·프로필 MonoBehaviour 계층만 다룬다.

## 파이프라인 커버리지

N/A — 신규 플레이 오브젝트나 생성→렌더 경로가 아니다. ScreenSpace 안내 UI만 추가한다.

## 비목표

이 spec 이 **하지 않기로 한 것** — 다시 논의하려면 별도 spec 으로.

- 스쿼드/드림캐쳐 패널 **내부** 편집 방법 안내 (패널을 여는 데까지만 책임진다)
- ~~프리셋·히스토리 안내~~ → **히스토리는 2026-08-01 사용자 재승인으로 챕터 D(units 8~9)가 됐다.**
  원래 근거("`HistoryButton` 은 게스트에게 아예 숨겨진다")는 지금도 유효하므로 폐기하지 않고
  **게이트로 흡수**했다 — 게스트에겐 챕터가 열리지 않는다. **프리셋 안내는 비목표로 남는다**:
  스쿼드/덱이 이미 있어야 의미가 생기는 파생 기능이다.
- 튜토리얼 다시 보기 메뉴, 단계 해금
- 홀 모양 확장(원형·라운드·비사각형). 현재는 축정렬 사각형만

## 후속 후보

`docs/spec/README.md` → Follow-up Backlog → **아웃게임 튜토리얼** 로 전부 이관했다 —
units 0~9 종료분 4건(실기기 QA · 챕터 B 게이트 교체 · 버튼 라벨 한글 통일 · 인지 지표 관측)과
units 11~13 종료분 3건(챕터 A 사전 검사 · 패널 닫기 배선 경로 · 로비 온보딩 총량).
