# 1 — SquadDraw (순수 출전 로직)

## 목적

스쿼드 + 가변 랜덤에서 출전 7장을 결정하는 **순수·결정적** 함수. 테스트 가능.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/Squad/SquadDraw.cs`
- 신규 `Assets/_Project/Tests/EditMode/SquadDrawTests.cs`

## 구현

```csharp
public static class SquadDraw
{
    public const int VariableCount = 3;
    public const int FieldCount = 7;

    // squadUnitIds: 스쿼드 슬롯의 비어있지 않은 id (중복 제거 전 그대로)
    // ownedUnitIds:  프로필 보유 전체
    // 반환: 출전 유닛 id 리스트 (최대 7).
    public static List<string> Resolve(
        IReadOnlyList<string> squadUnitIds,
        IReadOnlyList<string> ownedUnitIds,
        int seed);
}
```
규칙:
1. `squad` = squadUnitIds 중 비어있지 않은 것.
2. `varPool` = ownedUnitIds − squad (집합 차). 거기서 seed 기반 셔플로 최대 `VariableCount`(3) 추출.
3. `candidates` = squad ∪ variable (순서 보존; 중복 id 는 1회만).
4. candidates 를 seed 기반 셔플 후 앞 `min(FieldCount, candidates.Count)` 반환.

- RNG: `Unity.Mathematics.Random`(seed) 또는 `System.Random`(seed) — Burst 불필요(MonoBehaviour 맥락). 결정성만 보장.
- 무효/빈 id 는 1단계에서 제거. candidates 0개면 빈 리스트(호출측이 폴백 판단).
- catalog 해석(id→SO)은 호출측(GameManager, Unit 3)에서 `DefenderCatalog.ById`.

## 완료 기준

- EditMode `SquadDrawTests`:
  - 같은 seed → 같은 결과(결정성).
  - 가득 찬 스쿼드(7) + 보유 충분 → 정확히 7 반환, 모두 candidates 소속.
  - variable 은 스쿼드에 없는 것만(겹침 없음), 최대 3.
  - 빈 스쿼드 → variable 만(≤3) 반환.
  - 무효 id 섞임 → 결과에서 제외.
- compile + read_console clean.

> 완료 확인 2026-06-02 — EditMode SquadDrawTests 5/5(결정성/full7/variable≤3·겹침없음/빈스쿼드/빈슬롯 제외).
