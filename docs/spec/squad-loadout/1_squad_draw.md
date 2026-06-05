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

---

## rev 2026-06-05 — 결정적 편성 (랜덤 제거)

**이유**: 매 게임 시작마다 라인업이 달라져 "아웃게임에서 저장한 스쿼드가 안 지켜진다"는 사용자 혼란. 저장/로드는 정상이었고, 변동의 원인은 본 함수의 가변 랜덤(스쿼드 + 랜덤3 → 랜덤7, time-seed)이었음. 사용자 결정: **저장 스쿼드를 그대로 반입**.

**변경 후 계약**:

```csharp
public static class SquadDraw
{
    public const int FieldCount = 7;
    // 저장 스쿼드의 비어있지 않은 id 를 중복 제거 + 슬롯 순서 보존 + 최대 7 로 반환.
    public static List<string> Resolve(IReadOnlyList<string> squadUnitIds);
}
```

- `VariableCount`, `ownedUnitIds`, `seed`, `Shuffle` **제거**.
- 호출측 `GameManager.StartSquadMatch`: `SquadDraw.Resolve(squad.unitIds)` 로 단순화, 미사용 `GenerateSeed()` 제거.
- 규칙: 빈 슬롯("") 제외 · 중복 첫 등장만 · 순서 보존 · 7 초과면 앞 7. candidates 0 → 빈 리스트(호출측 드래프트 폴백).

**완료 기준 (rev)**:
- EditMode `SquadDrawTests` 갱신: 순서보존/결정성/빈슬롯제외/중복제거/7캡/빈·null→빈.
- compile + EditMode 통과.
- Play: 같은 저장 스쿼드로 두 번 시작 → 동일 유닛 반입.
