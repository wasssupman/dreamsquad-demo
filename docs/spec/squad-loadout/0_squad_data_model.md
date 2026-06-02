# 0 — 스쿼드 데이터 모델

## 목적

`SquadSave` stub 을 실제 7슬롯 스쿼드로 확장하고, 신규 프로필에 기본 스쿼드 1개를 보장한다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` — `SquadSave` 확장
- 수정 `Assets/_Project/Scripts/Core/Profile/ProfileStore.cs` — 기본 스쿼드 생성 + 보강
- (선택) 수정 `Assets/_Project/Tests/EditMode/ProfileStoreTests.cs` — 기본 스쿼드 검증 추가

## 구현

`SquadSave` 확장 (JsonUtility 직렬화 유지):
```csharp
[Serializable]
public class SquadSave
{
    public string id;                 // 안정 id (예: "squad_1")
    public string name = "Squad 1";
    public const int SlotCount = 7;
    // 길이 7 고정. 빈 슬롯 = "" (null 대신 빈 문자열로 직렬화 일관).
    public List<string> unitIds = new List<string>();  // 7개, 빈칸 ""
}
```
- 헬퍼: `bool IsEmpty()` (모든 슬롯 "" ), `int FilledCount()`.
- 슬롯 길이 정규화 헬퍼: 7개로 pad/trim (로드 후 `EnsureNonNull` 에서 호출).

`ProfileStore`:
- `CreateDefault`: `ownedUnitIds` 채운 뒤 **빈 스쿼드 1개**(`id="squad_1"`, unitIds = 7×"") 추가하고 `selectedSquadId="squad_1"`.
- `EnsureNonNull`: squads null 가드 + 각 SquadSave.unitIds 7개로 정규화. squads 비어있으면 기본 스쿼드 1개 주입(+select).
- 헬퍼 `PlayerProfile.SelectedSquad()` (selectedSquadId 로 조회, 없으면 null).

unitIds 의 유효성(보유/카탈로그 존재)은 여기서 강제하지 않음 — 배정 시 UI(Unit 2)가 보장, 반입 시 SquadDraw(Unit 1)가 무효 id 스킵.

## 완료 기준

- compile + read_console clean.
- EditMode: 신규(빈 경로) 프로필 → `squads.Count==1`, `unitIds.Count==7` 전부 "", `selectedSquadId=="squad_1"`.
- round-trip: 슬롯 일부 배정 후 save→load 동일.
- 기존 ProfileStoreTests 3건 여전히 통과.
