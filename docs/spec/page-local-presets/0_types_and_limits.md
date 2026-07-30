# 0 — 타입 개명 + 정규화 + 상한

## 목적

기존 저장 타입을 프리셋 어휘로 개명하고, 프리셋 리스트가 항상 만족해야 할 불변식(칸 수·상한·확정 포인터 유효성)을 한 함수로 모은다. 신규 id 발급도 순수 함수로 둔다. **JSON 필드명은 바꾸지 않는다** — 마이그레이션 없음.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` — 타입·메서드 개명 + `NormalizePresets` + `MaxPresets` 상수
- 신규: `Assets/_Project/Scripts/Core/Profile/PresetIds.cs`
- 신규: `Assets/_Project/Tests/EditMode/Profile/PresetNormalizeTests.cs`

**타입명을 직접 쓰는 파일** (실측, `authored-preset-removal` 완료 후 잔존분 — 괄호는 참조 건수):

| 파일 | 건수 |
|---|---|
| `Tests/EditMode/ProfileStoreTests.cs` | 11 |
| `Tests/EditMode/LoadoutGateTests.cs` | 9 |
| `Scripts/Core/Profile/ProfileStore.cs` | 9 |
| `Scripts/Core/Profile/PlayerProfile.cs` | 6 |
| `Scripts/Core/GameManager.cs` | 6 |
| `Tests/EditMode/Profile/DeckPruneTests.cs` | 3 |
| `Scripts/UI/Outgame/SquadHeaderStrip.cs` | 3 |
| `Scripts/UI/Outgame/SquadCharacterPageController.cs` | 3 |
| `Scripts/Core/Profile/LoadoutGate.cs` | 3 |
| `Scripts/UI/Outgame/DreamcatcherDeckPageController.cs` | 2 |
| `Tests/PlayMode/DreamcatcherDeckCarryInTest.cs` | 1 |
| `Tests/EditMode/ProfileStoreDefaultDeckTests.cs` | 1 |
| `Tests/EditMode/DreamcatcherDeckAutosaveTests.cs` | 1 |

**접근자만 호출하는 파일** (타입명 미등장 — `SelectedSquad`/`SelectedDeck` 개명만 반영):
`Scripts/Core/SceneTransition.cs`(`:262`) · `Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`(`:197`, `:491`) · `Scripts/UI/Outgame/DefaultLoadoutButton.cs`(`:61~63`)

**개명 불필요, 확인만**: `Scripts/Core/Profile/DeckPrune.cs` — `var` 로 순회해 타입명이 등장하지 않는다. 컴파일은 자동 통과하므로 **읽어서 의미가 여전히 맞는지만** 확인한다(unit 5 에서 프리셋 30개 순회를 다룬다).

## 구현

개명 (필드명·직렬화 형태 불변):

| 기존 | 신규 |
|---|---|
| `SquadSave` | `SquadPreset` |
| `DeckSave` | `DreamcatcherPreset` |
| `SelectedSquad()` | `CommittedSquad()` |
| `SelectedDeck()` | `CommittedDeck()` |

`SquadSave.SlotCount`/`StoneSlotCount`/`NormalizeSlots`/`SetStoneSlot`/`IsEmpty`/`FilledCount` 는 이름·동작 그대로 `SquadPreset` 으로 옮긴다. `DeckSave.Count()` 도 동일.

`CommittedSquad()`/`CommittedDeck()` 에 주석으로 계약 6 을 못박는다 — 살아있는 참조이며, 이 리스트에 쓰는 것은 페이지 컨트롤러의 저장 경로뿐이다.

신규 — 상한은 `PlayerProfile` 의 상수로 둔다. 1개 const 를 담는 `PresetLimits` 전용 클래스는 제약 8 이 금지하는 얇은 층이고, `SquadPreset.SlotCount = 7` 이 타입에 붙어 있는 기존 선례와도 어긋난다:

```csharp
public class PlayerProfile
{
    // UI 용량 한계(목록 팝업 스크롤 예산)라서 const 다 — SquadPreset.SlotCount 와 같은 급.
    // 재화/과금 knob 이 되면 SO 로 이관(README 후속 후보).
    public const int MaxPresets = 30;
    ...
}
```

```csharp
// PresetIds.cs — 순수 함수 1개라 파일을 따로 둔다(EditMode 테스트 대상, 제약 10)
public static class PresetIds
{
    // 기존 id 들의 숫자 접미 최대값 + 1. 삭제 후 재생성에서도 충돌하지 않는다.
    // "preset_" 접두 없는 레거시 id(squad_1/deck_1)도 입력으로 받아 접미만 센다.
    public static string NextId(IReadOnlyList<string> existingIds, string prefix);
}
```

`PlayerProfile.NormalizePresets()` — **로드 · 프리셋 생성 · 프리셋 삭제 3곳에서만** 호출한다(내용 편집은 호출하지 않는다):
1. `squads`/`dreamcatcherDecks` null 이면 생성
2. 각 `SquadPreset.NormalizeSlots()` (7/4 패딩·트림)
3. 두 리스트를 `MaxPresets` 로 트림
4. `selectedSquadId`/`selectedDeckId` 가 실존 엔트리를 가리키지 않으면 첫 엔트리로 교정(리스트가 비면 빈 문자열)

"로드" 호출 지점은 **`ProfileStore.EnsureNonNull` 과 `CreateDefault` 각각의 말미 1곳**이다(unit 5). 세 `EnsureDefault*` 함수 안에 각각 넣지 않는다 — 3중 호출이 되고 어느 것이 불변식의 소유자인지 흐려진다.

**짝 수리 로직은 없다** — 계약 1 로 스톤이 `SquadPreset` 안에 있어 짝 개념 자체가 존재하지 않는다.

## 완료 기준

- [ ] 컴파일 그린. 개명 누락 0 (`SquadSave|DeckSave|SelectedSquad|SelectedDeck` 검색 0건)
- [ ] 기존 EditMode 전체 그린 — 개명만 했으므로 **어서션 내용은 하나도 바뀌지 않아야 한다.** 값이 바뀐 테스트가 있으면 개명이 아니라 동작 변경을 한 것
- [ ] `PresetNormalizeTests` 신규 그린: 7/4 패딩 · 31개 입력 → 30 트림 · 깨진 확정 포인터 교정 · 빈 리스트에서 no-throw · `NextId` 가 삭제 후에도 충돌 없음 · 레거시 id(`squad_1`) 혼재 입력 처리
- [ ] 기존 `profile.json` 을 그대로 로드해 편성이 **한 칸도** 바뀌지 않음 (마이그레이션 없음 증명 — 로드 전후 JSON diff 없음)
- [ ] Play — 로비 → 스쿼드/드캐 페이지 → START 반입까지 기존과 동일 동작

---

**검증 기록 2026-07-30 · `5592b676`** — 컴파일 errors=0, 개명 누락 0건 · 기존 EditMode 그린이며 **어서션 값 변경 0**(개명만 했음의 증거) · `PresetNormalizeTests` 14건 그린 · **실기기 `profile.json` 필드 대조로 마이그레이션 0 실증**(`squads[0]`={id,name,unitIds,stoneIds}, `dreamcatcherDecks[0]`={id,name,cardIds}). **미검증**: START 반입 Play e2e(단 `PresetCarryInTest` 가 자동 검증).
