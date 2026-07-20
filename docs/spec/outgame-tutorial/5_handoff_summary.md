# 5 — 인계 요약

## Commit

| 해시 | 내용 |
|---|---|
| `81fdbb59` | docs — 스펙 6문서 (critic 리뷰 3종 반영본) |
| `251705d8` | unit 0 — 진행 상태 플래그 2개 + TutorialProgress 확장 |
| `3dc303c8` | unit 1 — 통과구멍 dim 레이어 (순수 함수 + 뷰) |
| `815b38c4` | units 2~4 — 두 챕터 컨트롤러 + OutgameScene 배선 |
| `1dfc22a3` | chore — 누락 `.meta` 2건 |

## Implemented

- 챕터 A(로그인 후 로비 최초 노출)와 챕터 B(첫 판 복귀)가 각각 2탭으로 동작한다.
- dim 조각만 `raycastTarget` 을 갖고 홀은 비어 있어, 포커스된 버튼이 **실제로** 눌린다.
  전투 시작·패널 열기는 인스펙터 배선이 수행하고 컨트롤러는 완료만 기록한다.
- `OutgameTutorialDimLayout.Subtract` 는 축 전제 없는 y 스캔라인 사각형 차집합.
- 챕터 B 는 두 버튼을 합집합 링 하나로 감싸고 사이 12px dim 스트립을 남긴다.
- 탈출구: 포커스 8초 무진행 시 건너뛰기 지연 노출 + Android 백키. 단계 진입 0.5초 입력 무시.
- 로그아웃은 `AbortChapter` 로 `_step` 까지 되돌려 재로그인에 챕터가 재생된다.

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs` — 상태머신·완료 저장
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialOverlay.cs` — dim 조각·홀·탭
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialDimLayout.cs` — 순수 차집합
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs` — 플래그 정책
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs:70,95` — 진입 훅 2곳
- `Assets/_Project/Scenes/OutgameScene.unity` — 루트 `OutgameTutorial` + 자식 `Dim`/`Guidance`

## Verified

- EditMode 1133개 중 1131 통과 · 0 실패 (2 skip 은 기존 의도적 Ignore).
  신규 `OutgameTutorialDimLayoutTests` 13, `TutorialProgressTests` 12 → 16.
- 씬 diff 147줄 추가 · **삭제 0줄**. `OutgameTutorial` 이 씬 루트임을 확인(계약).
- Play 실측(에디터, reflection 주입):
  - 챕터 A: `IntroMessage`(1조각·홀0) → 탭 → `IntroFocus`(4조각·홀 `(672,-492,240×292)` = StartButton)
    → 포커스 단계 dim 탭 **무반응** → 실제 START 클릭 시 `step=None`·메모리 1·**디스크 1**·씬 전환 시작
  - 챕터 B: `LoadoutMessage` → 탭 → `LoadoutFocus`(7조각·홀 `(-912,12)`/`(-912,-240)`,
    합집합 링 `(-822,0) 180×480`, 사이 12px 스트립, 버튼 2개 훅)
  - 종료 2경로: dim 탭 → 패널 없이 종료·디스크 1 / 실제 SquadButton 클릭 → **패널 열림** + 종료·디스크 1
  - 진행 중 로그아웃 → `step=None`·훅 해제·union 파괴 → 재로그인에 챕터 처음부터 재생
- 에디터 실측 홀 좌표가 EditMode 테스트의 계산값과 일치.

## Notes

되돌리면 안 되는 것:

- **진입 훅은 두 곳**(`Awake` 말미 + `ApplyAuthGate` 말미). `ApplyAuthGate` 는 `Awake` 첫 줄이라
  그 시점 `profileSO.profile` 은 곧 교체될 인스턴스다. 전투 복귀는 `UserSession` 이 이미
  signed-in 이라 `onSignedIn` 이 재발화하지 않아 `Awake` 말미가 챕터 B 의 유일한 진입점이다.
- **`Awake` 프레임에 뷰를 만지지 않는다.** `TutorialGuidanceView.Awake` 의 무조건 `Hide()` 가
  먼저 띄운 문구를 끈다. 래치 후 `Start` 에서 실행. `[DefaultExecutionOrder]` 로는 못 고친다.
- **`PlayerProfile` 을 캐시하지 않는다.** 캐시본에 완료를 쓰면 플레이어가 안내대로 스쿼드를
  저장하는 순간 라이브 인스턴스가 디스크를 되돌려 챕터 B 가 부활한다.
- **버튼 클릭 경로는 `minStepSeconds` 로 게이팅하지 않는다.** 클릭은 이미 씬 전환/패널 열기를
  일으켰으므로 저장을 건너뛰면 안내가 영원히 반복된다.
- `holePadding` 은 두 포커스 버튼 간격(24px)의 절반 미만. 12 면 홀이 맞닿아 스트립이 사라진다.
- 두 뷰는 **서로 다른 GameObject**, `OutgameTutorial` 은 **씬 루트**여야 한다.

알려진 한계: `FocusUi` 는 safe rect 안쪽 20px 로 클램프하는데 dim 홀은 FullBleed 기준이라,
노치 기기에서 링과 홀이 어긋날 수 있다(`3_loadout_chapter.md`).

## Follow-up

**spec 은 종료됐다** — 사용자 동작 확인 완료(2026-07-21), 모바일 실기기 QA 는 보류 결정.

실기기를 잡을 때 한 번에:

- 노치 dim 커버리지, Android 백키(`Keyboard.current` null 기기에서 미동작 가능),
  dim 톤 0.92 가 과한지, 12px 스트립 분리감. 링/홀 정렬은 실측 완료라 불필요.

별도 spec 후보:

- 챕터 B 게이트를 "실제로 판을 끝냈다"는 독립 신호로 교체 (현재는 인게임 core 완료 재사용 —
  그 fail-open 경로를 탄 플레이어는 챕터 B 를 영원히 못 본다).
- `SQUAD`/`DREAMCATCHER` 버튼 라벨 한글 통일 (로비 레이아웃 스펙 범위).

> 검증용으로 로컬 `profile.json` 의 튜토리얼 플래그 5개를 모두 0 으로 리셋해 두었다
> (스쿼드·덱 선택은 보존). 다음 Play 는 인게임 튜토리얼부터 처음 흐름으로 시작한다.
