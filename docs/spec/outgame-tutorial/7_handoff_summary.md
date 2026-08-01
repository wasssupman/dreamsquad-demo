# 7 — 인계 요약 (챕터 C)

`5_handoff_summary.md`(units 0~4) 이후분. 최신 계약은 README + 번호 문서 우선.

## Commit

| 해시 | 내용 |
|---|---|
| `ec1f62ed` | docs — 신규 스텝 3종 스펙 + README 2개 |
| `f0d05bd7` | unit 6 — 챕터 C(로비 배경 캐릭터 드래그) |

## Implemented

- **챕터 C**: 챕터 B 완료 후 로비로 **복귀할 때** World 캐릭터에 포커스 링 + `배경에 있는 캐릭터를 끌고 드래그 해보세요`.
- **완료 조건은 드래그 자체**다. dim 탭은 **의도적 no-op** — 바로 앞 `LoadoutFocus` 는 dim 탭으로 완료를 저장하므로 그 코드를 복붙하면 안 된다.
- 탈출구는 형제 챕터와 동일: 8초 무진행 시 건너뛰기 지연 노출.
- 챕터 종료 후 늦게 도착한 드래그 신호는 아무것도 쓰지 않는다.

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs` — 챕터 C 상태·완료 저장
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `ClosePanels(bool restoreLobby)` 진입 훅
- `Assets/_Project/Scripts/UI/Outgame/LobbyKeyringDrag.cs` — 드래그 신호 소스
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs` — 신규 진행 토큰
- `Assets/_Project/Scenes/OutgameScene.unity` — `keyringCharacter → World` 배선

## Verified

- 컴파일 0 (Runtime · Tests.EditMode · Tests.PlayMode).
- EditMode **1777 중 실패 0**(2026-08-01 머지 기준 재실행). 신규 `OutgameTutorialChapterCTests`.
- 씬 배선 실측: `keyringCharacter → World`.
- **사용자 Play 확인 2026-08-01 통과** — 리셋 → A → 첫 판 → 복귀 B → 패널 열고 닫기 → C 노출 → 드래그 → 즉시 종료 → 재진입 미노출. dim 탭 무반응, 8초 건너뛰기 확인.
- **`restoreLobby` 회귀 확인**: 스쿼드·드림캐쳐·테스트모드·히스토리를 **여는 순간** C 가 뜨지 않는다(4개 모두).

## Notes (되돌리면 안 되는 것)

- **`ClosePanels(bool restoreLobby)`** — `RaiseExclusive`·`OnResetAccount`·`Awake` 는 반드시 `false`. `true` 로 되돌리면 **패널을 여는 순간** 챕터 C 가 뜬다. 이게 이 유닛의 유일한 구조적 함정이다.
- **챕터 C 의 dim 탭은 no-op 이 사양이다.** 형제 `LoadoutFocus` 와 동작이 다르다 — 복붙 금지.
- **신규 진행 토큰은 `ResetAll`/`ResetAllInJson` 양쪽 `changed` 식에** 넣어야 한다. 한쪽만 넣으면 `TutorialReset` 이 조용히 반쪽만 지운다.

## Follow-up

- 캐릭터 스프라이트의 투명 여백 때문에 포커스 홀이 실제 그림보다 커 보일 수 있다 — 실기기에서 거슬리면 홀 여백 보정.
- 모바일 실기기 QA 는 units 0~4 시절부터 보류 상태(사용자 결정)로 이어진다.
