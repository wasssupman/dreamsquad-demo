# 0 — 데이터 계약 (enum + 필드)

## 목적

모드 분기의 **데이터 토대**만 놓는다. 이 단계는 **동작 변경 없음** — 기본값이 기존 동작을 그대로
재현해야 한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/BattleMode.cs` (신규 enum)
- `Assets/_Project/Scripts/Data/AttackDeck.cs`
- `Assets/_Project/Scripts/Data/ScoreRulesData.cs`

## 구현

1. **`BattleMode` enum** (신규 파일, `Wassup.Data`):
   ```csharp
   public enum BattleMode { Main = 0, Endless = 1 }
   ```
2. **`AttackDeck` 필드 추가**:
   - `public BattleMode battleMode = BattleMode.Main;`
   - `public float fixedWaveIntervalSec = 0f;` — `0`=기존 `duration/waveCount` 파생, `>0`=고정 간격.
   - `public int stressScoreBudget = 0;` — 스트레스 점수 전용 예산. `0`=`defeatGoalReachedCount` 재사용
     (기존 동작), `>0`=패배한계와 분리된 예산.
   - Tooltip 으로 sentinel 의미 명시. **`defeatGoalReachedCount<=0` = 무제한(패배 없음)** 은 여기
     문서화만; 실제 게이트는 unit 2.
3. **`ScoreRulesData.timeScorePerSecond`**: `[Range(1, 10000)]` → `[Range(0, 10000)]`.
   0 을 허용해야 엔드리스 ScoreRules 가 시간축을 끌 수 있다. 주석에 "0 = 시간점수 비활성(엔드리스)" 추가.

## 완료 기준

- 컴파일 통과 (`dotnet build` 또는 Unity 리컴파일).
- **기존 덱 동작 불변**: `battleMode` 기본 `Main`, `fixedWaveIntervalSec=0`, `stressScoreBudget=0`
  → 메인 5개 덱 + Hook 은 직렬화/런타임 동작이 그대로.
- 인스펙터에서 신규 필드 노출 확인.
