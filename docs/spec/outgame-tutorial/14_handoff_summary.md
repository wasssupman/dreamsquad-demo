# 14 — 인계 요약 (units 11~13: 로드아웃 시퀀스 재편)

`10_handoff_summary.md`(units 8~9) 이후분. 최신 계약은 README + 번호 문서 우선.

## Commit

| 해시 | 내용 |
|---|---|
| `10fad0c2` | units 11~13 — 스펙 + 토큰 + 챕터 B 분리 + 재출발 스텝 |

세 unit 이 한 커밋인 이유: 11 의 개명·체인 재배열은 컨트롤러가 옛 이름을 부르는 탓에 12 와
분리하면 컴파일이 깨지고, 12·13 은 진행 순서가 서로 물려 있다. 11 의 "순수 가산" 성질은
문서로만 남고 커밋으로는 나뉘지 않았다.

## Implemented

옛 챕터 B(스쿼드·드림캐쳐 한 덩어리, 2탭, dim 탭으로도 종료)를 **행동 4스텝 시퀀스**로 재편했다.

- **B1 스쿼드** → 실제 클릭 → 페이지 → 닫기 → **B2 드림캐쳐** → 실제 클릭 → 페이지 → 닫기 →
  **C 키링** 드래그 → 착지 → **E 재출발 START**
- 2탭 프리앰블 제거. A 를 제외한 모든 스텝이 1단계 포커스(문구+포커스 동시)
- 진행 토큰 2개 신설(`lobbyDeckHintVersion`·`lobbyStartHintVersion`).
  스쿼드는 기존 `lobbyLoadoutHintVersion` 재사용(JSON 호환)
- 레거시 계정(옛 B·C 완료)은 **파생 가드**로 B2·E 재노출을 막는다
- `BuildUnionRect`/`_unionRect`/`DestroyUnionRect` 제거(2대상 스텝 소멸), `ShowFocus` 단일 대상화

## Key Files

- `Core/Profile/TutorialProgress.cs` — 토큰 4개 사슬 · `IsLegacyLobbySequenceDone`
- `Core/Profile/PlayerProfile.cs` — 신규 필드 2개
- `UI/Outgame/Tutorial/OutgameTutorialController.cs` — `Step` 9개 · `TryEnterFocusStep` ·
  `TickKeyringSettle`
- `Tests/EditMode/TutorialProgressTests.cs` · `OutgameTutorialChapterCTests.cs`

**씬 변경 없음** — `squadButton`·`dreamcatcherButton`·`startButton` 은 이미 배선돼 있었다(실측).
신규 `keyringSettleTimeoutSeconds` 는 YAML 에 없어 코드 기본값 4초로 동작한다.

## Verified

- 컴파일 0. EditMode **1809 중 실패 1** — 실패는 `DcApplicabilityTests`(`UltimateLeap × None ×
  Standard` 미분류)로 **다른 세션이 같은 워크트리에서 진행 중인 작업**이다(`DcMechanic.cs` 등
  미커밋 dirty). 이 spec 과 무관.
- 이 spec 의 회귀 11건 개별 재실행 전부 Passed — dim 탭 no-op 4건(Squad·Deck·Keyring·Start),
  토큰 격리 4건, `DragStarted → KeyringSettling` 전이, 늦은 드래그 신호 무시, 키링 선행 순서.
- `TutorialProgressTests` 신규 6건(선행 관계 2 · 레거시 가드 경계 2 · 멱등/독립 · 리셋) Passed.
- **사용자 Play 확인 통과 2026-08-02** — 스쿼드 포커스→클릭→페이지, 복귀 시 드림캐쳐 포커스,
  키링 4초 초과 홀드 중 dim 미노출, 착지 후 START 포커스·문구, 콘솔 경고 0.

## Notes (되돌리면 안 되는 것)

- **B1·B2 의 dim 탭은 무반응이다.** 옛 `LoadoutFocus` 는 dim 탭으로도 완료됐다("여기 있다"만
  알리는 정보 단계였다). 그 case 를 복붙하면 페이지를 한 번도 열지 않고 시퀀스가 통과한다.
- **시퀀싱은 패널 왕복이 한다.** `RaiseExclusive` 의 `ClosePanels(false)` 가 전제다 — 패널을
  여는 호출에서 로비 복귀 훅이 돌면 다음 스텝이 열리는 패널 위에 얹힌다.
- **`KeyringSettling` 의 타임아웃은 `_settleStartedAt` 이 소유한다.** `_stepEnteredAt` 은
  `EnterStep` 에서만 갱신되므로 그걸 쓰면 기준이 KeyringFocus 진입 시각이 되고, 안내를 읽고
  4초 넘게 지난 뒤 잡으면 **드래그 중에 dim 이 올라온다**.
- **드래그 중에는 폴백 타이머를 리셋한다**(`LobbyKeyringDrag.AnyDragging`). 키링은 만지작거리는
  장난감이라 4초 초과 홀드가 예외가 아니다.
- **착지 폴링은 `Update` 의 포커스 단계 가드보다 앞**이다. 뒤에 두면 한 번도 실행되지 않고,
  대기 목록에 넣으면 말풍선 없는 구간에 Skip 만 뜬다.
- **`CompleteAndEnd` 의 `StartFocus` case 를 지우지 말 것.** 챕터 A 가 같은 `startButton` 을
  쓰므로 빠지면 E 가 A 의 플래그를 다시 쓰고 자기 토큰은 영원히 0 이다.
- **레거시 가드의 조합(`키링 완료 && 덱 0`)은 새 순서에서 나올 수 없다** — 덱이 키링보다 먼저
  완료되기 때문이다. 이 성질이 깨지면(체인 순서를 바꾸면) 가드가 정상 진행을 삼킨다.
  `OutgameTutorialChapterCTests` SetUp 이 덱을 채워두는 이유도 이것이다.

## Follow-up

후속 후보 3건(챕터 A 사전 검사 · 패널 닫기 배선 경로 · 온보딩 총량)은
`docs/spec/README.md` → Follow-up Backlog → **아웃게임 튜토리얼** 로 이관했다.

- `TryBeginChapter` 의 스텝 선택과 착지 폴링 진입은 **EditMode 로 관측 불가**다
  (`overlay`/`guidance` 미배선이면 즉시 return · `EnterStep` 이 `overlay.Show()` 를 탄다).
  선물 홀드(unit 7)·리빌 홀드(first-session unit 24)와 같은 계열의 구조적 한계.
- 실기기 QA 는 units 0~4 시절부터 계속 보류다. 이번 재편으로 포커스 대상이 2개 늘었으므로
  노치·safe area 확인 대상도 함께 늘었다.
