# 3 — 스쿼드 페이지 배선 (작업본 도입)

## 목적

스쿼드 페이지에 프리셋 바를 붙이고, **프로필을 직접 고치던 편집을 작업본 편집으로 바꾼다.** 이 spec 의 실질 작업이 여기 있다 — 현재 컨트롤러는 `squad.unitIds[idx] = ...` 로 프로필 객체를 in-place 변이하고 매 탭마다 디스크에 쓴다. 그 두 성질을 모두 제거한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPageController.cs` (전면)
- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPage.cs` — 레이아웃 상수 + 바 생성/주입
- `Assets/_Project/Scripts/UI/Outgame/SquadHeaderStrip.cs` — 작업본을 받도록 시그니처 변경:
  `Refresh(SquadSave squad)` → `Refresh(IReadOnlyList<string> unitIds, IReadOnlyList<string> stoneIds)`.
  작업본은 프리셋 객체가 아니라 두 개의 `List<string>` 이므로 프리셋 타입을 받지 않는다 — 이 변경으로 헤더 스트립은 프로필 타입 의존이 사라져 순수 뷰가 된다

## 구현

**레이아웃** (`SquadCharacterPage`): 우측 컬럼을 3분할. `presetBarHeight = 0.10f` 신설, `headerHeight` 0.14 유지, 브라우저는 `1 - 0.10 - 0.14 = 0.76`.
- presetBar: `(detailWidth, 0.90) ~ (1, 1)`
- headerStrip: `(detailWidth, 0.76) ~ (1, 0.90)`
- browser: `(detailWidth, 0) ~ (1, 0.76)`

`ConfirmPopup` 도 페이지 루트에 생성해 컨트롤러에 주입한다(미저장 경고용).

**작업본** (`SquadCharacterPageController`):

```csharp
private string _viewingPresetId;
private readonly List<string> _workingUnits = new();  // 7칸
private readonly List<string> _workingStones = new(); // 4칸
private string _workingName;
```

- `OnEnable`: `NormalizePresets()` → `_viewingPresetId = profile.selectedSquadId` (**확정 프리셋을 디폴트로** — 요구사항) → `LoadWorking(_viewingPresetId)`
- `LoadWorking`: 저장본을 `_working*` 로 **복제**. 이후 모든 편집은 `_working*` 만 건드린다
- `ToggleUnit`/`ToggleStone`: 기존 규칙(첫 빈칸 append · dedup · one item one slot) 그대로, 대상만 `_working*` 로. **`Save()` 호출 제거** — 편집이 디스크에 닿지 않는다
- 편집 후 **`RefreshBarState()`** 로 dirty 재계산(`PresetDiff.IsSquadDirty`) → 배지·버튼 활성 갱신. (구현 정정: 초안의 단일 `RefreshBar()` 는 목록 재구성까지 해서 유닛 토글마다 30셀×썸네일7 을 다시 만들었다. `RefreshBarState`(가벼움) / `RefreshBarEntries`(목록 재구성) 로 분리했고 후자는 구조 변경에서만 부른다)
- `SortedUnits()`/`FirstSquadUnitOrDefault()`/`RefreshUnitMode()`/`RefreshStoneMode()` 가 읽는 소스를 `Squad` 프로퍼티에서 `_working*` 로 교체. `SquadHeaderStrip.Refresh` 는 작업본을 그린다

**4조작**:

| 조작 | 구현 |
|---|---|
| 선택 | `profile.selectedSquadId = _viewingPresetId` → `ProfileStore.Save`. **작업본을 기록하지 않는다**(계약 3) |
| 저장 | 저장본 ← `_working*` 복제 + `_workingName` → `ProfileStore.Save` |
| 되돌리기 | `LoadWorking(_viewingPresetId)` 로 **저장본 기준 복원**. 저장 안 함 → dirty 꺼짐 (rev 2026-07-30) |
| 삭제 | 차단 사유 안내 또는 **확인 팝업** → 확인 시 `DeletePresetConfirmed(id)` 가 가드 재검증 후 엔트리 제거 → `NormalizePresets()` → `ProfileStore.Save` → 확정분으로 복귀 (rev 2026-07-30, 아래 참조) |
| `[+]` | `PresetIds.NextId` 로 id 발급, 이름 `"스쿼드 N"`, 빈 7/4칸 엔트리 추가 → `NormalizePresets()` → `ProfileStore.Save` → 그 프리셋으로 전환 |

버튼 dim 조건(컨트롤러가 판단해 `SetButtonEnabled` 로 전달):
- 선택: `_viewingPresetId == profile.selectedSquadId` 면 dim
- 저장: `!dirty` 면 dim
- 되돌리기: `!dirty` 면 dim (=[저장]과 같은 조건, 한 쌍으로 읽힌다)
- 삭제: **항상 활성**(`delete: true`). dim 하지 않는 이유는 죽은 버튼이 사유를 말해주지 못해서다 — 누르게 하고 `OnDeletePreset` 이 안내한다 (rev 2026-07-30, 사용자 결정)
- `[+]`: `squads.Count >= PlayerProfile.MaxPresets` 면 dim

**`SetEntries` 호출 시점**: 목록 셀은 **저장본**을 그린다(작업본이 아니다 — 목록은 "저장된 프리셋들" 이다). 따라서 `SetEntries` 는 **구조 변경 시에만** 부른다 — 생성 · 삭제 · 확정 · 전환. 유닛/스톤 토글마다 부르면 30셀 × 초상 7개를 매 탭 재구성하고, 아직 저장하지 않은 내용이 목록에 새는 두 문제가 겹친다. 내용 편집 시에는 `SetDirty`/`SetButtonEnabled` 만 갱신한다.

**삭제 3단 게이트** (rev 2026-07-30):
1. **차단 사유 안내** — 확정분이면 `NoticePopup.ShowAlert` 로 *"확정 상태입니다 / 다른 프리셋을 [선택]으로 확정한 뒤 삭제하세요"*(해결 방법 포함), 마지막 1개면 *"반입할 편성이 없어지므로…"*. `NoticePopup` 은 자기 부트스트랩(`RuntimeInitializeOnLoadMethod`+`DontDestroyOnLoad`)이라 씬 배선이 필요 없고 `sortingOrder 3000` 으로 프리셋 팝업 위에 뜬다.
2. **확인 팝업** — 삭제는 되돌릴 수 없다([되돌리기]는 작업본 대상이라 지워진 프리셋을 살리지 못한다). `ConfirmPopup` 으로 *"'{이름}' 프리셋을 삭제합니다 / 저장된 편성이 사라지며 되돌릴 수 없습니다"*, 확인 라벨 `삭제`. **프리셋 이름을 문구에 넣는다.**
3. **콜백 가드 재검증** — 팝업 콜백은 나중에 오므로 `DeletePresetConfirmed(id)` 가 `CanPersist`·확정분·마지막 1개를 **다시** 확인한다. 대상 id 는 캡처해 콜백 시점의 `_viewingPresetId` 에 의존하지 않는다.

**전환 가드**: `PresetPicked(id)` 수신 시 dirty 면 `ConfirmPopup.Show("저장하지 않은 변경이 있습니다.\n이동하면 변경은 사라집니다.", () => Switch(id), "이동")`. dirty 아니면 즉시 전환.

기본 이름은 `"스쿼드 N"` (계약: 드캐는 `"덱 N"` — `"프리셋 2"` 가 두 페이지의 짝이라는 오해 차단).

## 완료 기준

- [ ] 컴파일 그린
- [ ] **편집이 디스크에 닿지 않음**: 유닛/스톤을 바꾸고 [저장] 없이 페이지를 닫고 다시 열면 이전 저장 상태가 보인다
- [ ] [저장] 후 재진입 시 저장 내용이 보인다
- [ ] [선택]만 누르고 미저장 변경을 버린 뒤 START → **저장분이 반입된다**(계약 3 실증)
- [ ] dirty 배지: 편집 시 나타나고 [저장] 시 사라지며, 뺐다 되넣으면 사라진다
- [ ] [되돌리기] → 저장본 내용으로 복원 + dirty 꺼짐. 신규 빈 프리셋에서는 완전 비움
- [ ] 삭제: 확정분/마지막 1개에서 [삭제]를 눌러도 지워지지 않고 **사유 팝업**이 뜬다 · 삭제 가능한 프리셋은 **확인 팝업**을 거친다 · `confirmPopup` 미주입이면 삭제 차단(fail-closed)
- [ ] `[+]` 로 30개까지 생성 후 dim, 31번째 불가
- [ ] dirty 상태로 목록 전환 시 경고 팝업 → [취소]는 머물고 [이동]은 버리고 이동
- [ ] Play 콘솔 에러 0. 헤더 스트립·브라우저 정렬·상세 패널이 작업본을 반영

---

**검증 기록 2026-07-30 · `5de0f258`** — 컴파일 errors=0 · EditMode 로 **편집 비저장 · 저장/확정 분리 · 되돌리기(저장본 복원) · 삭제 가드/확인** 전부 자동 검증(`PresetCommitSemanticsTests` 컨트롤러 직접 구동) · Play 스크린샷으로 바 레이아웃과 버튼 dim 계약 확인. **미검증 Play 조작**: 30개 상한 dim · dirty 전환 경고의 [취소]/[이동] · 삭제 확인 팝업 육안.
