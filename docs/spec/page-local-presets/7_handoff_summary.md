# 7 — handoff summary

## Commits

| 해시 | 내용 |
|---|---|
| `85d5d0d7` | docs — spec A·B 작성 |
| `5592b676` | feat — spec A 3 units + spec B 0~6 구현 |
| `fe7ce977` | fix — 코드리뷰 반영 (CRITICAL 팝업 가려짐 · HIGH 항진 테스트 · MEDIUM 3) |
| `f5f7608f` | test — CRITICAL 회귀 테스트 + `[+]` 두부 글리프 |
| `80ba77e6` | feat! — **리셋 → 되돌리기**(저장본 기준 복원) |
| `09d7e527` | feat — `[삭제]` 항상 활성 + 누르면 차단 사유 안내 |
| `5de0f258` | feat — 삭제 확인 팝업(fail-closed + 콜백 가드 재검증) |
| `2e4f4c63` | test — fail-closed 가 LogError 를 기대값으로 |
| `9d9b70e6` | docs — handoff 2건 + 상태 라인 + 작업 단위별 검증 기록 |
| `be9927bf` | docs — 진행 중 구현이 스펙과 달라진 5건 반영 |

병합 `51aa8245` (타 세션 `tournament-deck-info` 편입) 이후 전체 재검증을 수행했다 — 아래 Verified 참조.

## Implemented

- **스키마 변경 0.** `PlayerProfile` 이 이미 이 기능의 저장 구조였다 — `squads` 리스트 · `name` · `stoneIds`(이미 스쿼드 소속) · `selectedSquadId`. 타입명만 `SquadSave`→`SquadPreset`, `DeckSave`→`DreamcatcherPreset` 으로 개명하고 **JSON 필드명은 유지**했다. 마이그레이션 없음, `schemaVersion` 1 유지. 기존 편성이 그대로 프리셋 #1 이 된다.
- `NormalizePresets()`(로드·생성·삭제 3곳) · `PlayerProfile.MaxPresets = 30` · `PresetIds.NextId`(접미 max+1).
- `PresetDiff` — dirty 판정 순수 함수. 빈칸 `null`≡`""` 정규화, 슬롯 **위치**·덱 **순서** 유의. 플래그가 아니라 내용 비교라 "뺐다 되넣기"에서 dirty 가 정확히 꺼진다.
- `PresetBarView` — 커스텀 목록 팝업(리치 셀이 필요해 `TMP_Dropdown` 불가) + 이름 `TMP_InputField`(**`onEndEdit` 전용**, IME 대응) + 버튼 4 + dirty 배지. **프로필 타입 참조 0**.
- 두 페이지: **in-place 편집 → 작업본**. 드캐는 자동 저장과 `DeckId="deck_1"` 하드코딩·`selectedDeckId` 강제 대입을 제거.
- 4조작: **선택**(확정 포인터만) · **저장**(저장본 ← 작업본) · **되돌리기**(작업본 ← 저장본) · **삭제**(확인 후).
- 시드/프루너: 기본 이름 한글화, `DeckPrune` 이 이미 프리셋 전체를 훑음을 확인, `GameManager` 빈-스쿼드 draft 폴백에 진단 로그.

## Key Files

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` — 타입·`NormalizePresets`·`MaxPresets`·`CommittedSquad/Deck`
- `Assets/_Project/Scripts/Core/Profile/PresetDiff.cs` · `PresetIds.cs`
- `Assets/_Project/Scripts/UI/Outgame/PresetBarView.cs`
- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPageController.cs` · `DreamcatcherDeckPageController.cs`
- `Assets/_Project/Tests/EditMode/Profile/PresetCommitSemanticsTests.cs` (의미론 회귀의 중심)
- `Assets/_Project/Tests/PlayMode/PresetBarPopupLayerTest.cs` (CRITICAL 회귀 고정)

## Verified

- 컴파일 **errors=0** — 4 어셈블리, `dotnet build` 로 Unity 지연 컴파일과 독립 확인(csproj 최신 여부 먼저 확인해 stale 오탐 배제).
- **EditMode 1660 / 1658 pass / 0 fail / 2 skip**(병합 전 기준. 사전 Ignored 2건 동일). 베이스라인 1617 → 삭제 21 + 신규 64. **병합 후 1700 / 1698 pass / 0 fail**.
- **PlayMode 프리셋 3건 통과** — `PresetBarPopupLayerTest` 2(두 페이지) + `PresetCarryInTest`. `f5f7608f` 시점 실행.
- **마이그레이션 0 을 실제 데이터로 확인** — 디스크 `profile.json` 의 `squads[0]`={id,name,unitIds,stoneIds}, `dreamcatcherDecks[0]`={id,name,cardIds} 가 새 타입과 필드 일치.
- **Play 육안** — 프리셋 바 6컨트롤이 밴드에 들어가고, 팝업이 헤더·브라우저 위에 렌더되며 확정 뱃지·초상 7개 표시. 버튼 dim 이 계약대로(확정분→선택 dim / 미변경→저장·되돌리기 dim / 1개뿐→삭제는 눌리고 사유 안내).
- **PlayMode 전체 86건 / 13 실패 — 13건 전부 사전 실패**(`docs/spec/README.md` 기재 목록과 정확히 일치). 이번 작업 귀속 실패 0. `PresetCarryInTest` · `PresetBarPopupLayerTest` 2건 통과.
- 병합(`51aa8245`) 이후 재검증 완료 — 타 세션의 `tournament-deck-info` 와 내 접근자 개명이 함께 성립함을 확인(EditMode 1700/1698 pass).

## Notes — 되돌리면 안 되는 의도

1. **JSON 필드명(`squads`/`dreamcatcherDecks`/`selectedSquadId`/`selectedDeckId`)을 바꾸지 말 것.** 타입명과 어긋나 보여도, 바꾸면 실기기 프로필 마이그레이션이 부활한다. 얻는 건 정돈뿐이다.
2. **저장과 확정은 완전 분리.** `[선택]`은 **저장본**을 확정할 뿐 작업본을 기록하지 않는다. 그래서 "확정 ≠ 화면"이 가능하고, **dirty 배지가 그 짝 계약**이다 — 배지를 빼면 조용한 오적재가 된다.
3. **`SetEntries` 는 구조 변경에서만.** 내용 편집 경로는 `RefreshBarState()` 만. 합치면 매 탭 30셀×썸네일7 을 재구성하고, 목록이 **저장본**을 그린다는 성질이 흐려진다.
4. **팝업을 열 때 바 자체를 페이지 루트 마지막 형제로 올린다.** 팝업만 바 안에서 `SetAsLastSibling` 하면 브라우저가 덮는다(= 리뷰 CRITICAL). 중첩 Canvas 우회는 렌더만 살고 탭이 샌다 — `LoadoutGatePopup.cs:43-49` 참조.
5. **`CanPersist()` 는 변이 **전에** 묻는다.** `Save()` 안에서만 걸면 메모리에 프리셋이 생겼는데 디스크엔 없는 상태로 갈린다.
6. **fail-closed 2곳** — `confirmPopup` 미주입 시 (a) dirty 전환 차단 (b) 삭제 차단. 미주입 ref 를 플레이어가 고칠 수 있는 상황으로 위장하지 않는다(`OnStartGame` 정책과 동일).
7. **`DeletePresetConfirmed` 의 가드 재검증을 지우지 말 것.** 팝업 콜백은 나중에 오므로 그 사이 대상이 확정되거나 개수가 줄 수 있다.
8. **`ProfileStore.EnsureDefaultSquad` 의 확정 포인터 교정은 시딩의 선행 조건**이다. `NormalizePresets` 가 같은 일을 하지만 그건 `EnsureNonNull` 말미라 늦다 — 빼면 신규 프로필이 빈 스쿼드로 시작한다.
9. **`DreamstonePreset` 타입은 없다.** 스톤은 `SquadPreset.stoneIds` 에 통합돼 있다. 1:1 병렬 리스트는 짝 어긋남이라는 없어도 될 실패 모드와 그 수리 코드를 요구한다.
10. **`되돌리기`는 완전 비움이 아니다.** 저장본 기준 복원이며, 신규 프리셋에서만 결과적으로 완전 비움이 된다. "백지에서 시작"은 `[+]` 담당.

## Follow-up

- ~~PlayMode 전체 재실행~~ **완료 2026-07-31** — 병합 후 86건 실행, 13 실패 전부 사전 실패.
- ~~`ActiveAllyZoneTest` · `DreamcatcherCombatDamageTest` 격리 확인~~ **해소 2026-07-31** — 정상 실행에서 **둘 다 통과**. 앞선 실패는 `blocked_reason: editor_unfocused` 로 프레임 펌핑이 멈춘 실행의 부산물이었다(둘 다 시간·프레임 누적 단정). 회귀 아님.
- **Play 조작 검증 미완**: 목록 31셀 스크롤 · `[+]` 30개 상한 dim · 한글 IME 이름 입력 · dirty 상태 전환 경고 팝업의 [취소]/[이동] · 삭제 확인 팝업 육안.
- 리뷰 LOW 6건 미반영(리포트는 리뷰 세션 transcript).
- README 후속 후보: 프리셋 복제 · diff 보기 · 로비 확정 요약 뱃지 · 순서 재배열 · `MaxPresets` SO 이관.
