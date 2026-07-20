# outgame-tutorial — 로비 통과구멍 2챕터 온보딩

> 상태: **작성 2026-07-21 · critic 리뷰 3종 반영 개정 · 미구현**
> 선행: `first-session-tutorial` (완료) · `outgame-lobby-layout` (완료)

## 검증 질문

신규 플레이어가 첫 로비 진입과 첫 복귀에서 각각 **다음에 눌러야 할 버튼을 실제로 눌러보고** 넘어가는가?

인지 여부는 이 spec 이 검증하지 않는다 — 노출과 실행만 보장한다. 인지 지표는 "비목표" 참조.

## 상위 목표

인게임 튜토리얼(`first-session-tutorial`)의 **행동 성공 신호로 진행한다**는 철학을 로비에서도 유지한다.
dim 은 주의를 좁히는 수단일 뿐이고, 진행시키는 것은 언제나 **플레이어가 실제 버튼을 누른 사건**이다.

| 챕터 | 시점 | 1탭째 문구 | 2탭째 문구 | 포커스 |
|---|---|---|---|---|
| A 인트로 | 로그인 후 로비 최초 노출 | `악몽이 몰려옵니다. 꿈결특공대, 출동!` | `이 버튼을 눌러 출발!` | `StartButton` |
| B 로드아웃 | 첫 판 이후 로비 복귀 | `더 잘 막고 싶다면, 함께 싸울 유닛과 카드를 손봐보세요.` | `스쿼드와 드림캐쳐에서 바꿀 수 있어요!` | `SquadButton` + `DreamcatcherButton` |

`문구` → **어디든 탭** → `문구 + 포커스` → **포커스된 버튼 탭** → 종료.

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_progress_state.md` | 프로필 상태 | 챕터 A/B 버전 플래그와 기존 리셋 경로 편입 |
| 1 | `1_dim_overlay.md` | 통과구멍 레이어 | 사각형 차집합 dim + 홀 통과. 차집합은 순수 함수 |
| 2 | `2_intro_chapter.md` | 챕터 A | 로비 최초 노출 → StartButton 실제 클릭 |
| 3 | `3_loadout_chapter.md` | 챕터 B | 첫 판 복귀 → 스쿼드/드림캐쳐 실제 클릭 |
| 4 | `4_scene_wiring_and_qa.md` | 통합/검증 | OutgameScene 배선 + 진입 훅 + Play QA |

## Feature-wide 계약

- **차단은 dim 조각이, 진행은 실제 대상이 담당한다.** dim 조각은 `raycastTarget = true` 로 로비 입력을
  먹고, **홀 영역에는 그래픽이 없어 레이캐스트가 아래 `MenuCanvas`(order 0)로 떨어진다.** 즉 포커스된
  버튼은 진짜로 눌린다. 완료 저장은 그 버튼 `onClick` 을 런타임 한정으로 임시 구독해 수행하며,
  인스펙터 persistent call 은 건드리지 않는다. 풀스크린 탭 캐처는 쓰지 않는다.
- **1탭째(홀 0개)는 dim 한 장이 전체를 덮고 어디를 눌러도 진행한다. 2탭째부터 dim 탭은 무반응**이며
  포커스된 버튼만 진행시킨다 — 버튼 위치를 학습시키는 것이 이 단계의 목적이다.
- **탈출구는 지연 노출 Skip 이다.** 포커스 단계 진입 후 `escapeDelaySeconds`(기본 8초) 동안 진행이
  없으면 `ShowMessage(같은 문구, showSkip: true)` 로 Skip 을 노출한다. Android 백키(`escapeKey`)도
  동일 취급한다. Skip 은 완료 저장 후 안내만 종료하며 **챕터 A 라도 전투를 시작하지 않는다**.
- **최소 표시 시간**: 각 단계는 진입 후 `minStepSeconds`(기본 0.5초) 동안 입력을 무시한다. 씬 전환
  직후의 잔여 탭이나 연타로 문구를 읽기 전에 챕터가 소진되는 것을 막는다.
- **기존 UI 레이어를 수정하지 않는다.** 별도 Canvas 로 얹는다. `MenuCanvas`(0) 위,
  `TutorialGuidanceView`(10) 아래, `SceneTransition`(10000) 아래.
- **두 뷰의 GameObject 는 씬 루트여야 한다** — 어떤 Canvas 의 자식도 아니어야 한다. 중첩 캔버스에서는
  `overrideSorting` 이 렌더 순서만 올리고 레이캐스트 우선순위는 마지막 sibling 이 이긴다
  (`LoadoutGatePopup.cs:44-47`, `SquadBuilderView.cs:392-399` 의 실패 기록). 루트 형제 캔버스여야
  `GraphicRaycaster.sortOrderPriority == canvas.sortingOrder` 가 성립해 이 설계가 동작한다.
- **`PlayerProfile` 참조를 캐시하지 않는다.** 판정·완료·저장 시점마다 `profileSO.profile` 을 다시 읽는다.
  `OutgameMenuController.Awake` 는 `ApplyAuthGate`(L52) 뒤 L68 에서 프로필 인스턴스를 교체하므로,
  캐시하면 곧 버려질 객체에 완료 플래그를 쓰게 된다.
- **완료 저장은 `Complete*` 반환값이 true 일 때만 `ProfileStore.Save` 를 호출한다**
  (`FirstSessionTutorialController.cs:285-290` 선례). `SaveAt` 은 전체 파일 재작성이다.
- **말풍선·포커스 링·포인터는 `TutorialGuidanceView` 를 무수정 재사용한다.** 챕터당 문구는 최대 2개이며
  2번째는 포커스 대상을 지목한다.
- **챕터 순서는 플래그로 보장한다.** B 는 인게임 core 튜토리얼 완료를 전제로 하므로 A 와 동시에
  pending 될 수 없다(`ShouldRunGiftTutorial` 선례와 동형).
- **fail-open**: 참조 누락·포커스 대상 미발견·저장 실패는 경고 로그만 남기고 로비를 잠그지 않는다.
  대상을 못 찾으면 구멍 없이 표시하고 dim 탭으로 종료한다. 저장이 실패하면 다음 진입에 다시 노출되지만
  Skip 탈출구가 있으므로 잠금이 아니다.
- **세션 가드 준수**: `PlayerProfileSO.IsLoadedThisSession` 이 false 면 아무것도 하지 않는다.
- **입력 차단의 근거**: 로비의 조작 경로는 전부 EventSystem 기반이다 — `LobbyKeyringDrag`(`IBeginDragHandler`),
  `WorldLobbyCharacter`/`HelloLobbyRoamer`(`IPointerClickHandler`). 씬에 Physics(2D)Raycaster 가 없고,
  `LobbyBackgroundParallax` 는 `Pointer.current` 를 직접 폴링하지만 `LobbyKeyringDrag.AnyDragging`
  게이트라 드래그가 막히면 함께 멈춘다. 따라서 dim 조각만으로 로비 입력이 전부 차단된다.
- ECS 변경 없음. UI·프로필 MonoBehaviour 계층만 다룬다.

## 파이프라인 커버리지

N/A — 신규 플레이 오브젝트나 생성→렌더 경로가 아니다. ScreenSpace 안내 UI만 추가한다.

## 비목표 / 후속 후보

- 스쿼드/드림캐쳐 패널 **내부** 편집 방법 안내 (패널을 여는 데까지만 책임진다)
- 프리셋·히스토리 안내. 근거: `HistoryButton` 은 게스트에게 아예 숨겨지고(`OutgameMenuController.cs:89-94`),
  프리셋은 스쿼드/덱이 이미 있어야 의미가 생기는 파생 기능이다
- 튜토리얼 다시 보기 메뉴, 단계 해금
- 홀 모양 확장(원형·라운드·비사각형). 현재는 축정렬 사각형만
- **인지 지표 관측** — `2번째 판 시작 시 안내 없이 START 도달`, `첫 복귀 이후 세션에서 스쿼드/덱 패널
  1회 이상 열기`. 이 spec 은 노출과 실행만 보장하고 인지를 검증하지 않는다
- 챕터 B 게이트를 "실제로 판을 끝냈다"는 독립 신호로 교체 (현재는 core 튜토리얼 완료 재사용 — `3_loadout_chapter.md` 참조)
- `SQUAD`/`DREAMCATCHER` 버튼 라벨의 한글 통일 (로비 레이아웃 스펙 범위)
