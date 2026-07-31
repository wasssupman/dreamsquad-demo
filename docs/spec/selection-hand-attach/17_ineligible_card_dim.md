# 17. 선택 유닛에 못 붙는 카드 딤

## 목적

선택 상태에서 손패의 딤은 **각성치만** 말한다 (`RefreshUsability()` = `CanUse` 단독). 선택 유닛에
부착 불가한 카드는 밝게 남아 있다가, 탭해야 비로소 움찔 + "이 유닛에는 부착할 수 없습니다" 가
뜬다 — **시도해야 알 수 있는** UX 다. 손패 5장 중 어느 것이 지금 쓸 수 있는 카드인지가 화면에
없다.

계약 5 의 3판정(`CanUse` · `CanAttachMore` · `WouldDreamcatcherCardApply`)은 이미 탭 즉발과 D&D
`_attachable` 스냅샷이 공유한다. **판정은 그대로 두고 딤에 연결만** 해서 "못 쓰는 카드는 눌러보기
전에 보인다"로 바꾼다.

## 사용자 결정 (2026-07-31)

1. 딤 = **완전 차단**(탭·드래그 모두). 각성치 딤과 같은 의미로 통일한다. 부착 카드를 다른
   유닛에 붙이려면 **그 유닛을 탭해 선택을 전환**하면 딤이 풀린다 (계약 2: 선택 전환 = 손패 유지).
2. **딤 대상은 `Defender` 조준(Unit/Squad 부착) 카드뿐이다.** 나머지 조준(Active 타일 · 제물 표식
   적 지정)은 겨누는 대상이 선택 유닛이 아니므로, 막는 대신 **선택을 놓고 조준으로 나온다** —
   active-ally-zone unit 3 의 `NotifySelectionReleasedForAim` 문법을 **비-Defender 조준 전체로
   확장**한다(rev 2, 사용자 결정 2026-07-31).
   - rev 1 은 제물 표식을 딤+차단했다. "그 경로가 없으니 막자" 였는데, 답은 **경로를 주는 것**
     이었다. 막으면 선택 중 제물 표식을 쓰려고 손패를 닫았다 다시 열어야 했다.
   - 조건은 카드 **타입**이 아니라 조준 **모드**로 쓴다(`_mode != AimMode.Defender`). 새 비-Defender
     조준이 생겨도 조건에 타입을 더할 일이 없다.
   - `IsAiming`/`SelectedDefender = null` 은 **옮기지 않는다** — 배치 입력 상호배제라는 Active
     고유 사정이고 선택 해제와 무관하다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `AimMode`/`Classify` 가시성만

## 구현

- `CardSlot` 에 `attachBlocked` 추가. **`usable` 의 뜻은 각성치 그대로 둔다** — 탭 거절 문구가
  `!usable → "각성치가 부족합니다"` → 부착 판정 → `"이 유닛에는 부착할 수 없습니다"` 순서로
  갈리므로, 두 사유를 한 필드에 합치면 사유 문구가 어긋난다.
- `CardSlot.Playable => usable && !attachBlocked` 파생 프로퍼티. 딤 표현 · `CanStartDrag` ·
  focus 확대(`ApplyFocusTargets`)가 **이 하나**를 읽는다.
- **딤 = 붉은 계열** (rev 3, 사용자 결정 2026-07-31). 표면 **네 개**를 함께 붉힌다:
  face 곱 틴트 · 이름 · 본문 · 코스트 배지.
  - ⚠ `face.color` 하나로는 성립하지 않는다. 그건 face 스프라이트에 대한 **곱연산**이라 R 이
    낮은 헤더색(Squad 파랑 · Active 청록)은 붉어지는 게 아니라 검어진다 — 카드 타입마다 딤이
    다른 색으로 읽힌다. 텍스트·배지는 직접 지정이라 타입과 무관하게 같은 붉기가 나온다.
  - 붉은색은 브리핑 실패 문안과 **같은 `#FF9B8A`**. "빨강 = 지금 안 된다"를 카드와 안내가
    한 어휘로 말한다.
  - 텍스트는 어둡게 죽이지 않고 **밝은 살몬**으로 간다 — hand-card-face 계약 8(딤 상태에서도
    본문 가독 유지)이 여기서도 우선한다.
  - 색 상수는 `DreamcatcherHandView` 의 `FaceNormal`/`FaceDim`/`TextDim`/`CostBadge*` 한 곳에
    모으고 빌드(`EnsureSlots`)와 갱신(`RefreshUsability`)이 같은 값을 읽는다 — 두 곳에 리터럴을
    흩으면 정상 색이 조용히 어긋난다.
- `RefreshUsability()` 가 `attachBlocked` 를 함께 계산한다:
  - `!InSelectionMode` → 항상 `false` (항아리 단독 오픈 무회귀).
  - `Classify(card) != AimMode.Defender` → `false` (Active·제물 표식·미분류 — 결정 2).
  - 그 외 → `!(Controller.CanAttachMore(SelectionTarget) &&
    Bridge.WouldDreamcatcherCardApply(SelectionTarget, card))` — 계약 5 의 나머지 2판정 그대로.
- `DreamcatcherCardDragSlot.OnBeginDrag` 의 선택 해제 조건을 `card.type == Active` →
  **`_mode != AimMode.Defender`** 로 확장(결정 2). 호출 위치는 `BeginFocus(slot)` **앞** 유지 —
  뒤로 가면 `ReleaseSelectionKeepHand` 의 `focus.End()` 가 방금 시작한 조준 세션을 지운다.
  - 검증됨: `ReleaseSelectionKeepHand` 는 조준 종류에 중립이다. 줌 복귀가 `IsAiming` 에
    의존하는 것처럼 주석(`DcInspectController.cs:487`)에 적혀 있으나, 실제 기전은
    `_selected = Entity.Null` → `TickSelectionAnchor` 조기 return → `SetInspectFocus` 피드 중단
    → `CameraDirector` staleness 페이드다. `IsAiming` 없이도 성립한다.
- 부착 카드 판별은 `DreamcatcherCardDragSlot.Classify` 재사용 (`private` → `internal`, `AimMode` 도
  `internal`). 조준 라우팅과 딤이 **한 판별**을 봐야 BountyMark 같은 라우팅 변경이 딤에 자동으로
  따라온다 — 뷰에 판별을 복제하지 않는다.
- 재계산은 **이벤트 시점만**. `WouldDreamcatcherCardApply` 는 managed SO(mechanics 배열)를 읽어
  per-frame 호출이 금지돼 있다(`DreamcatcherAttachEval` 주석). 추가 훅은 하나:
  `SetSelectionTarget` / `ClearSelectionTarget` 에서 `RefreshUsability()` 호출 — 선택 전환(A→B)은
  `OpenForSelection()` 이 `State == Hand` 라 no-op 이므로 **여기서만** 잡힌다. 게이지 변동
  (`OnGaugeChangedRefreshDim`)·사용/회수/오픈(`Refresh()`)은 기존 경로가 이미 통과한다.
- 딤이 "안 된다"를 말하고, 탭 움찔 + 브리핑 사유가 "왜 안 되는지"를 말한다 — 둘은 대체가 아니라
  층이 다르다. press 브리핑 사유는 `PressStatus` 단일 소스에서 두 갈래로 갈린다: 각성치를
  모아라 / 다른 유닛을 골라라. 비-Defender 카드의 탭 사유는 `"끌어서 사용하세요"` 하나로
  통일된다 — rev 2 이후 Active·제물 표식 **둘 다 실제로 끌리기** 때문이다.
- `CanPeek` 무변경 — 딤 카드도 press 툴팁으로 읽힌다 (hand-card-face 계약 8: 테스터는 못 내는
  카드도 읽는다).

## 완료 기준

- compile 통과 · EditMode 전량 green (`HandViewSelectionSignalTests` 포함 — `SetSelectionTarget` 에
  refresh 가 붙어도 그 픽스처는 슬롯 0개라 무영향).
- Play: 가디언 선택 → `레인저 전용` 카드와 투사체 전용 카드(통통구슬)가 **즉시 붉어지고**,
  탭·드래그 모두 시작되지 않는다 (탭 시 움찔 + 사유는 그대로 뜬다).
- Play: 붉은 딤이 **카드 타입 3종 모두에서** 붉게 읽히는지 — 특히 Squad(파랑)·Active(청록)
  헤더 카드가 "그냥 어두운 카드" 로 보이지 않는지. 그렇게 보이면 face 곱 틴트로는 한계이므로
  딤 전용 face 스프라이트(붉은 헤더/본문 베이크)로 올린다.
- Play: 붉은 딤 상태에서 이름·본문이 여전히 읽히는지(계약 8).
- Play: 그 상태에서 레인저 유닛을 탭해 선택 전환 → 같은 카드가 **밝아지고** 탭 즉발 부착이 된다.
- Play: 선택 중 **Active·제물 표식 모두 밝게** 유지된다. 각각 끌면 그 순간 선택만 풀리고
  (패널·리티클·줌 해제) **손패는 열린 채 조준이 이어진다** — 손을 떼지 않고 그대로 대상까지 간다.
- Play: 제물 표식 드래그 해제 후 적을 겨눠 커밋하면 표식이 정상 부여되고, 취소해도 차감 0.
- Play: 선택 중 Active·제물 표식을 **탭**(드래그 아님)하면 "이 카드는 끌어서 사용하세요" + 움찔.
- Play: 한 유닛에 부착 상한(`MaxAttachPerUnit`)까지 붙이면 남은 부착 카드가 전부 딤으로 바뀐다.
- Play: 선택 없이 항아리로 연 손패의 딤 분포가 종전과 동일(각성치 기준만).

---

**확인 2026-07-31** — 사용자 Play 확인 통과(딤 붉은 계열 rev 3 포함). compile: `Wassup.Runtime` ·
`Wassup.Tests.EditMode` 모두 error 0. ⚠ EditMode 테스트는 **컴파일만** 확인했고 실행하지 않았다
(이 세션에 Unity MCP 미연결).

커밋 `b7bfa5c7` — `feat(selection-hand-attach, dreamcatcher-hand-card-face): 선택 중 부착 불가
카드 붉은 딤 + 손패 카드 이름 확대`. hand-card-face unit 5 와 한 커밋이다: 둘 다
`DreamcatcherHandView.cs` 의 같은 영역(카드 면 색·헤더 상수·`CardSlot` 필드)을 건드려 hunk 를
가르면 어느 한쪽이 컴파일되지 않는다.
