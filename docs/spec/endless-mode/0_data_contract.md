# 0 — 데이터 계약 (enum + 간격 필드)

## 목적

모드 분기의 **데이터 토대**만 놓는다. **동작 변경 없음** — 기본값이 기존 동작을 그대로 재현.

> critic 반영(2026-07-24): `stressScoreBudget` int·`endlessNoDefeat` bool·`ScoreRulesData` Range 수정
> **전부 제거**. 무제한-only v1 은 코드 쪽 `!IsEndless` 로 처리(unit 2)라 신규 덱 필드는 2개뿐.

## 변경 대상

- `Assets/_Project/Scripts/Data/BattleMode.cs` (신규 enum)
- `Assets/_Project/Scripts/Data/AttackDeck.cs`

## 구현

1. **`BattleMode` enum** (신규 파일, `Wassup.Data`):
   ```csharp
   public enum BattleMode { Main = 0, Endless = 1 }
   ```
2. **`AttackDeck` 필드 추가 (2개만)**:
   - `public BattleMode battleMode = BattleMode.Main;`
   - `public float fixedWaveIntervalSec = 0f;` — `0`=기존 `duration/waveCount` 파생,
     `>0`=고정 간격. Tooltip 명시.
3. **누수/점수 관련 신규 필드 없음.** 엔드리스는 `defeatGoalReachedCount`(기존 필드)를 스트레스
   점수 예산으로 **재사용**하되 authoring 에서 **높게** 잡는다(saturation 방지 — README §누수 예산).
   패배 비활성은 코드(`!IsEndless`, unit 2)라 필드 불필요.

## 완료 기준

- 컴파일 통과 (`dotnet build` 또는 Unity 리컴파일).
- **기존 덱 동작 불변**: `battleMode` 기본 `Main`, `fixedWaveIntervalSec=0` → 메인 5덱 + Hook
  직렬화/런타임 그대로.
- 인스펙터에서 신규 2필드 노출 확인.
