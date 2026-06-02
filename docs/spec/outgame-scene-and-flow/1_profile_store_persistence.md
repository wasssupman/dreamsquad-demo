# 1 — ProfileStore JSON 영속 + 테스트

## 목적

`PlayerProfile` 를 디스크 JSON 으로 round-trip 한다. 매니저 싱글톤 없이 static 유틸로.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/Profile/ProfileStore.cs`
- 신규 `Assets/_Project/Tests/EditMode/ProfileStoreTests.cs`

## 구현

`ProfileStore` — static 클래스 (MonoBehaviour 아님, 싱글톤 아님):
```csharp
public static class ProfileStore
{
    public static string Path => System.IO.Path.Combine(
        Application.persistentDataPath, "profile.json");

    // 파일 없으면 catalog 기반 기본 프로필 생성 후 저장하여 반환.
    public static PlayerProfile LoadOrCreate(DefenderCatalog catalog);

    public static void Save(PlayerProfile profile);

    // 경로 주입 오버로드 (persistentDataPath 비의존). EditMode 테스트가 별도
    // 어셈블리라 internal 대신 public (InternalsVisibleTo 플러밍 회피).
    public static PlayerProfile LoadOrCreateAt(string path, DefenderCatalog catalog);
    public static void SaveAt(string path, PlayerProfile profile);
}
```
- 직렬화는 `JsonUtility.ToJson(profile, prettyPrint:true)`.
- 기본 프로필: `ownedUnitIds = catalog.AllIds()`, squads/decks 빈, selected null.
- `schemaVersion` 불일치 시: 지금은 최신만 존재하므로 그대로 로드. 마이그레이션 훅 자리만 주석으로 표시(구현 X).
- 손상 JSON: 파싱 실패 시 경고 로그 + 기본 프로필 생성(데이터 날림 방지 위해 `profile.bak` 으로 백업 후).

`OutgameMenuController`(Unit 2) 가 씬 진입 시 `LoadOrCreate` 호출 → `PlayerProfileSO.profile` 에 대입. 변경 시 `Save`.

## 완료 기준

- EditMode 테스트 통과 (`mcp__UnityMCP__run_tests` EditMode):
  - 빈 경로 `LoadOrCreateAt` → 기본 프로필(ownedUnitIds == 카탈로그 전체) + 파일 생성됨.
  - `SaveAt` → `LoadOrCreateAt` round-trip 시 schemaVersion/ownedUnitIds/selected 동일.
  - 손상 문자열 기록 후 `LoadOrCreateAt` → 예외 없이 기본 프로필 + `.bak` 생성.
- 테스트는 `Path.GetTempPath()` 하위 임시 파일 사용, 끝나면 정리.
- compile + read_console clean.

> 완료 확인 2026-06-02 — EditMode `ProfileStoreTests` 3/3 통과(기본생성/round-trip/손상복구+백업), 컴파일 클린.
