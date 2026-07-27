# 드림캐쳐 덱 즉시 저장

상태: 설계 승인 2026-07-27 · 구현 대기

## 목표

드림캐쳐 덱 페이지의 편집이 스쿼드 페이지와 같이 **즉시 디스크에 저장**되게 한다. 명시적 `[저장]` 버튼을 제거하고, 덱 유효성 판정은 출전 게이트(`LoadoutGate`) 단독 책임으로 남긴다.

## 배경

같은 로비 안에서 같은 "편성" 행위가 다르게 동작한다.

- 스쿼드 (`SquadCharacterPageController`): `ToggleUnit` / `ToggleStone` 직후 `ProfileStore.Save`. 헤더 주석 그대로 *"Every edit mutates the selected SquadSave in place and auto-saves."*
- 드림캐쳐 (`DreamcatcherDeckPageController`): `_working` 초안에만 반영, `SaveClicked` + `DeckRules.Validate` 통과 시에만 저장.

덱의 저장 게이트는 **중복 방어**였다. `LoadoutGate.cs:68` 이 이미 같은 `DeckRules.Validate` 로 저장된 덱을 재검증하고, 미달이면 팝업에서 "드림캐쳐 덱" 버튼으로 되돌려보낸다. 저장 게이트를 없애도 "무효 덱으로 출전"은 성립하지 않는다.

포기하는 것: 명시적 저장이 덱을 스크래치패드로 만들어 주던 성질. 깨진 상태로 페이지를 나가면 그대로 저장되고, 다음 출전에서 게이트를 맞는다. 교체(A 빼고 B 넣기)는 시작도 끝도 유효하고 무효는 중간 순간뿐이라 실사용 노출은 낮다고 판단했다.

## 작업 단위

| 파일번호 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 동작 + 테스트 | `0_autosave_on_edit.md` | 편집 즉시 저장, 저장 버튼 제거, saver seam + EditMode 회귀 |

한 단위로 묶는다. 저장 버튼을 먼저 지우면 저장 경로가 사라져 중간 상태가 깨진다.

## feature-wide 계약

1. **저장 트리거 = `_working` 을 바꾸는 모든 경로.** 현재 `AddCard` / `RemoveOccurrence` 2곳. 새 편집 경로를 추가하면 반드시 같이 태운다.
2. **저장은 `DeckRules.Validate` 를 검사하지 않는다.** 무효 덱(예: 9/10)도 그대로 디스크에 남는다.
3. **무효 덱의 출전 차단은 `LoadoutGate` 단독 책임** (`Assets/_Project/Scripts/Core/Profile/LoadoutGate.cs:68`). 이 spec 은 `LoadoutGate` 와 `DeckRules` 를 건드리지 않는다.
4. **`profileSO.IsLoadedThisSession` 이 false 면 저장하지 않는다.** `PlayerProfileSO.cs:17-18` 의 계약("only OutgameMenuController's ProfileStore path may arm disk writes")이 근거다. 로드되지 않은 프로필 위에 쓰면 스쿼드 등 다른 섹션이 통째로 날아간다 — `DefaultLoadoutButton.cs:56`, `HiddenCardDeckPruner.cs:57` 이 경고하는 사고. 명시적 버튼 시절엔 노출이 적었으나 자동 저장은 노출이 크다.
5. **페이지 진입은 아무것도 저장하지 않는다.** `OnEnable` / `LoadWorking` 은 읽기 전용. 숨김 카드 자동 해제는 계속 로그인 prune(`HiddenCardDeckPruner`) 담당이다.
6. **덱 스트립의 상태 라인은 유지한다.** `count/deckSize · reason` 계산식과 문구 그대로. 의미만 "저장 가능" → "출전 가능" 으로 바뀐다.
7. **`selectedDeckId` 는 첫 저장 시 `deck_1` 로 고정**된다 (기존 `OnSave` 동작 유지).

## 비목표

- `DreamcatcherDeckBuilderView` — `OutgameScene.unity` 에서 `m_Enabled: 0` 인 레거시. 손대지 않는다.
- 교체(swap) 상호작용 도입 — 무효 중간 상태를 원천 제거하는 대안이었으나 상호작용 재설계 규모라 채택하지 않았다.
- `LoadoutGate` / `DeckRules` 규칙 변경.

## 후속 후보

- **`SquadCharacterPageController.Save()` 의 `IsLoadedThisSession` 가드 부재** [S] — 계약 4와 같은 노출이 스쿼드 쪽에도 있다. 이 spec 범위 밖.
- **덱 스트립 상태 라인의 문구 재검토** [S] — "출전 가능" 의미로 바뀌었으므로 `need exactly 10 (have 9)` 같은 영문 reason 이 플레이어 문구로 적절한지. `DeckRules.Validate` 의 reason 은 진단용이었다.

파이프라인 커버리지: **N/A** — 플레이 오브젝트를 신설하거나 생성→렌더 경로를 바꾸지 않는 아웃게임 UI·영속화 변경이다.
