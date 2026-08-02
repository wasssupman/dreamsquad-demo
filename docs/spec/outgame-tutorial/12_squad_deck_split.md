# 12 — 챕터 B 분리 (스쿼드 → 드림캐쳐)

## 목적

한 덩어리였던 챕터 B 를 **스쿼드 스텝 → 페이지 진입 → 로비 복귀 → 드림캐쳐 스텝 → 페이지 진입 →
로비 복귀** 로 쪼갠다. 두 페이지를 "있다" 고 알리는 정보 단계에서, **실제로 한 번씩 열어보는**
행동 단계로 바뀐다.

선행: unit 11(토큰). 개명과 체인 재배열도 여기서 함께 한다 — 컨트롤러와 같은 커밋이어야
컴파일과 동작이 모두 성립한다(unit 11 문서 참조).

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs` (개명 + 체인)
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs`
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs`
- `Assets/_Project/Tests/EditMode/OutgameTutorialChapterCTests.cs`

**씬 배선 변경 없음** — `squadButton`·`dreamcatcherButton` 은 이미 SerializeField 다.

## 구현

**개명**: `ShouldRunLobbyLoadoutHint`/`IsLobbyLoadoutHintPending`/`CompleteLobbyLoadoutHint`
→ `...LobbySquadHint`. const `LobbyLoadoutHintVersion` 과 JSON 필드는 **그대로** 둔다(unit 11).

**체인 재배열**: `ShouldRunLobbyKeyringHint` 의 선행을 `!IsLobbyLoadoutHintPending` →
**`!IsLobbyDeckHintPending`** 으로 바꾼다. 이 한 줄을 빠뜨리면 스쿼드만 끝낸 상태에서 키링이
드림캐쳐를 앞지른다. `TutorialProgressTests.LobbyKeyringHint_RunsOnlyAfterLoadoutHintComplete`
가 이 교체로 **깨지므로 함께 갱신한다**(덱 완료까지 요구하도록).

**컨트롤러** — `Step` 의 `LoadoutMessage`·`LoadoutFocus` → **`SquadFocus`·`DeckFocus`**.
2탭 프리앰블은 제거하고 C·D 와 같은 **1단계 포커스**로 통일한다(사용자 결정 2026-08-02) —
스텝이 늘어나는 만큼 게임플레이를 만들지 않는 순수 해제 탭을 늘리지 않는다.

손대는 곳은 **5군데로 고정**된다(새 챕터를 더할 때마다 같은 다섯 곳이다):
`TryBeginChapter` 사슬 · `EnterStep` case · `OnOverlayTapped` case · `CompleteAndEnd` case ·
`Update` 의 포커스 단계 목록.

**⚠️ dim 탭 정책이 뒤집힌다.** 옛 `LoadoutFocus` 는 dim 탭으로도 완료됐다 — "여기 있다"만
알리는 정보 단계였기 때문이다. 새 스텝은 **실제 진입을 요구**하므로 `KeyringFocus`/`HistoryFocus`
편(무반응)이고, `EnterStep` case 도 그 둘을 복제한다(사전 활성 검사 포함). **옛 `LoadoutFocus`
case 를 복붙하면 아무 데나 탭해도 완료가 저장돼 페이지를 한 번도 열지 않고 시퀀스가 통과한다.**

**fail-open**: 대상 버튼이 null/비활성이면 그 스텝을 **아예 열지 않는다**(`_step = None`, 완료
미저장). dim 탭이 무반응인 단계에서 구멍 없이 dim 만 띄우면 8초 Skip 까지 로비가 통째로 잠긴다.

**죽는 코드**: `BuildUnionRect()`·`_unionRect`·`DestroyUnionRect()` 는 두 버튼을 링 하나로
감싸던 `LoadoutFocus` 가 유일한 소비자였다(2대상 스텝이 사라진다). 함께 제거하고 호출처
2곳(`OnDestroy`·`EndChapter`)도 정리한다. `OutgameTutorialOverlay.TryGetHoleBounds`/
`EnsureHostRoot` 는 호출처가 0 이 되지만 오버레이는 이 unit 의 변경 대상이 아니므로 남긴다.

**문구(사용자 확정 2026-08-02 — 임의로 고치지 말 것)**:

```
SquadFocus  "더 잘 막고 싶다면, 함께 싸울 유닛부터!\n스쿼드를 눌러보세요."
DeckFocus   "이번엔 드림캐쳐 덱 차례!\n드림캐쳐를 눌러보세요."
```

프리앰블이 없으므로 **"왜 손봐야 하는가"가 B1 첫 줄에 들어간다**(옛 B 문구의 `더 잘 막고
싶다면` 회수). B2 의 `덱` 은 마지막 스텝 `새로 구성한 덱으로 다시 게임시작!` 에서 회수된다.
**정보형("~에서 바꿀 수 있어요")으로 되돌리지 말 것** — dim 탭이 무반응이라 누르라는 지시가
약하면 8초 Skip 까지 멈춘다(옛 B 는 dim 탭으로도 끝나서 정보형으로 충분했다).
버튼 실제 라벨은 `SQUAD`/`DREAMCATCHER` 영문이지만 포커스 링이 지목하므로 한글 표기를 유지한다.

## 완료 기준

- 컴파일 0 (Runtime · Tests.EditMode)
- EditMode — **기존 챕터 C 하네스로 작성 가능한 것만** 넣는다(하네스는 overlay/guidance/버튼을
  배선하지 않고 reflection 으로 `_step` 을 심는다 · `ProfileSaver` seam 으로 저장을 가로챈다):
  - `SquadFocus`·`DeckFocus` 의 dim 탭이 완료를 저장하지 않는다
  - 포커스 버튼 클릭이 **자기 토큰만** 저장한다(각 스텝별로, `_saved.Count == 1` 동반 단언)
  - 갱신된 `LobbyKeyringHint_RunsOnlyAfterLoadoutHintComplete` 통과
  - **`TryBeginChapter` 의 스텝 선택은 EditMode 로 관측할 수 없다** — 그 함수가
    `overlay == null || guidance == null` 에서 즉시 return 하기 때문이다
    (`OutgameTutorialController.cs:118-122`). 진짜 컴포넌트를 붙이면 `overlay.Show()` 가
    `UiCanvasSetup`+PrimeTween 을 EditMode 에서 돌린다. **Play 검증으로 대체한다** —
    선물 홀드(unit 7)·리빌 홀드(unit 24)와 같은 계열의 구조적 한계다
- Play 확인(로비 `RESET TUTORIAL` 후 1판 소화 → 복귀): 스쿼드 포커스 → 클릭 시 dim 이 같은
  프레임에 걷히고 페이지가 열린다 → 닫으면 로비 복귀와 함께 드림캐쳐 포커스 → 클릭 → 닫으면
  기존 키링 스텝. 콘솔 경고 0.
