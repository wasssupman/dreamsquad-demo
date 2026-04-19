# Phase 6 — 코스트 관리 시스템

> 본 문서는 `PRD.md`, `TRD.md`, `PHASE0~5.md`를 전제로 작성되었다. Phase 0~5에서 확립된 아키텍처 경계, 맥락 분리, 추상화 규칙, 금지 패턴은 Phase 6에서도 그대로 유지된다.

---

## 0. Phase 6의 존재 이유

### 검증 목표

> **H2 (3분 루프의 긴장감)** 의 핵심 축인 "코스트 제약 하에서의 결정"을 도입한다 — 플레이어가 "지금 쓸까 아낄까"를 매 순간 고민하도록 만드는 **리소스 희소성**.

Phase 5까지 게임은 "무한 자원" 상태였다. 방어 유닛을 무제한 배치할 수 있고 스킬을 쿨다운만 돌면 언제든 쓸 수 있었다. 이는 배치/스킬 결정의 **기회비용**을 없앤다 — 플레이어가 "고민"할 이유가 없음. Phase 6의 코스트 시스템은 모든 행동에 가격표를 붙여 긴장감을 복원한다.

### Phase 6가 하는 것 / 안 하는 것

**하는 것:**
- 유닛별 배치 비용 (SO 기반, 튜닝 가능)
- 스킬별 사용 비용 (SO 기반, 튜닝 가능)
- 10 시작 코스트 + 1/sec 충전 + 15 상한
- **배치 페이즈** 신규 — 드래프트 후 30초 카운트다운 동안 10코스트로 사전 배치. 스킬/충전 비활성.
- 좌하단 CostDisplay UI (숫자 + 충전 바)
- 코스트 부족 시 시각 피드백 (배치 실패 빨간 플래시, 스킬 슬롯 회색)
- 로그 확장 (placements/skill_usages에 cost_spent)
- Restart/Redraft 시 10코스트 리셋 + 배치 페이즈 재진행

**안 하는 것:**
- 코스트 보너스 이벤트 (특수 유닛 처치 시 +N 등) — Phase 7 이후.
- 스킬별 쿨다운 감소 코스트 투자 등 고급 경제.
- 신규 유닛/스킬/맵.
- 새 맥락 폴더 추가.

---

## 1. 게임 흐름 변화

```
[빌드 실행]
  ↓
[브리핑 — 공격 타임라인 그래프]
  ↓
[드래프트 — 10종 → 7종 픽]
  ↓
[배치 페이즈 — 신규]
  - 30초 카운트다운 시작
  - startingCost=10 지급, regen 정지, 스킬 비활성
  - 플레이어가 전략적으로 사전 배치 (비싼 유닛 = 코어 위치에)
  - 카운트다운 종료 또는 "START BATTLE" 클릭 → 전투
  ↓
[전투 — 180초 타이머]
  - 코스트 regen 1/sec 시작 (maxCost=15 상한)
  - 방어 배치 시 unit.cost 소모
  - 스킬 사용 시 skill.cost 소모
  - 코스트 부족 시 배치/스킬 거부 + 시각 피드백
  ↓
[결과 — VICTORY/DEFEAT + 리더보드]
  ↓
[재시작 | 다른 픽으로]
  - Restart: 같은 픽 유지 → 배치 페이즈 재진행
  - Redraft: 드래프트 재오픈 → 배치 페이즈 → 전투
```

---

## 2. 콘텐츠 스펙

### 2.1 코스트 데이터 (SO 기반 튜닝)

**DefenderUnitData 확장:**
```csharp
public int cost = 1;
```

**SkillData 확장:**
```csharp
public int cost = 2;
```

**신규 CostConfig ScriptableObject** (또는 AttackDeck에 필드 추가 — 자율):
```csharp
[CreateAssetMenu(fileName = "CostConfig", menuName = "Wassup/CostConfig", order = 14)]
public class CostConfig : ScriptableObject
{
    public int startingCost = 10;
    public int maxCost = 15;
    public float regenPerSec = 1f;
    public float placementPhaseDuration = 30f;
}
```
BattleBridge 또는 GameManager가 이 SO 참조. Inspector 튜닝.

**초기 밸런스 값 (변경 가능):**
- Scout:1, Archer:2, Ranger:2, Guardian:2, Bruiser:3, Marksman:3, Piercer:3, Bastion:3, Cannon:4, Sniper:4.
- Slow Field:2, Rapid Fire:2, Power Surge:3.
- starting=10, max=15, regen=1.0/s, placement=30s.

### 2.2 CostRuntime (MonoBehaviour, 싱글톤 금지)

GameManager 자식 GameObject + MonoBehaviour. SkillRuntime 패턴 재사용.

```csharp
public class CostRuntime : MonoBehaviour
{
    private float _current;
    private float _max;
    private float _regenPerSec;
    private bool _regenActive;

    public float Current => _current;
    public float Max => _max;
    public int CurrentInt => Mathf.FloorToInt(_current);
    public bool RegenActive => _regenActive;

    public void Configure(float startingCost, float max, float regenPerSec);
    public void ResetToStart();          // back to startingCost, stop regen
    public void BeginRegen();            // during battle start
    public void StopRegen();             // teardown/result

    public bool CanAfford(int amount) => _current >= amount;
    public bool TrySpend(int amount);    // returns false if insufficient
    public void RefundSpend(int amount); // if operation fails after spend

    private void Update() { if (_regenActive && _current < _max) _current += _regenPerSec * Time.deltaTime; if (_current > _max) _current = _max; }
}
```

### 2.3 게임 페이즈 상태 머신

`GameManager`에 `GamePhase` enum 추가 — 현재는 암묵적 상태를 명시화:

```csharp
public enum GamePhase { None, Briefing, Draft, Placement, Battle, Result }
```

**전환 규약:**
- `None → Briefing` : GameManager.Start (TimelineBriefing.Show)
- `Briefing → Draft` : TimelineBriefing 확정 콜백
- `Draft → Placement` : DraftController.TryConfirm (기존에 바로 Battle로 갔던 것이 Placement로)
- `Placement → Battle` : Placement 카운트다운 종료 또는 Start Battle 버튼
- `Battle → Result` : VICTORY/DEFEAT
- `Result → Placement` : RestartRequested (같은 픽 유지, 10코스트 리셋)
- `Result → Draft` : RedraftRequested → Draft 완료 시 Placement

GameManager가 enum property `CurrentPhase` 노출. 다른 시스템(CostRuntime, SkillBar, PlacementInput)은 이 값으로 자기 행동 결정.

### 2.4 PlacementPhaseView UI (신규)

- 신규 MonoBehaviour `UI/PlacementPhaseView`. 런타임 빌드 Canvas.
- **상단 중앙**에 "배치 페이즈 · 0:30" 카운트다운 (기존 TimerDisplay는 전투 타이머 전용 유지).
- **중앙 하단**에 "START BATTLE" 버튼 (즉시 전투 시작).
- 배치 페이즈 종료 시 Panel 자동 Hide.
- 시간 만료 또는 버튼 클릭 → GameManager.CurrentPhase = Battle + BattleBridge.StartBattle.

### 2.5 CostDisplay UI (신규)

- 좌하단 (DefenderSelector 위/옆 자율). 런타임 빌드.
- 표시: `"Cost 8 / 15"` 형태 + **진행 바** (현재/최대 비율).
- 코스트 증가 시 부드럽게 증가 애니메이션은 Phase 6 스코프 외 (수치만 실시간 반영).
- Phase != Placement && != Battle일 때는 숨김.

### 2.6 PlacementInput 코스트 연동

- `bridge.PlaceDefenderAs` 진입 직전에 `costRuntime.CanAfford(unitData.cost)` 체크.
- 실패 시: `bridge.OnPlacementRejected(tile)` 호출 → **빨간 플래시** 시각 피드백 (C7=b).
- 성공 시: `costRuntime.TrySpend(unitData.cost)` 후 배치.
- 배치 페이즈/전투 페이즈 양쪽에서 동일 규칙.

**빨간 플래시 구현:**
- `MapView`가 해당 타일 큐브 material 색상을 일시적으로 붉게 덧칠 후 복원 (0.2s).
- 또는 신규 `PlacementRejectFlash` System. MapView 레이어가 자연스러움.

### 2.7 SkillBar 코스트 게이트

- 슬롯 interactable 조건: `skillRuntime.IsReady(skill) && costRuntime.CanAfford(skill.cost) && GameManager.CurrentPhase == Battle`.
- `CanAfford==false`면 **회색**으로 표시 (기존 어두워진 tint 재활용, 혹은 별도 색).
- 슬롯 클릭 시 재검증 (페이즈 전환 레이스 대비). 실패 시 조용히 무시.
- 스킬 확정 시 `costRuntime.TrySpend(skill.cost)` 먼저 → 실패면 `BattleBridge.CastSkill*` 호출하지 않음.

### 2.8 로깅 확장 (v5)

```csharp
[Serializable]
public class PlacementLog {
    public string unit_type;
    public Vector2Int tile;
    public float time;
    public int cost_spent;   // 신규
}

[Serializable]
public class SkillUsageLog {
    public string skill_id;
    public float time;
    public Vector2Int target_tile;
    public int affected_count;
    public int cost_spent;   // 신규
}
```

- BattleLogger 메서드 시그니처는 유지하되 파라미터에 cost_spent 추가.
- BattleBridge.PlaceDefenderAs / CastSkill*이 소모 금액을 로그에 전달.

### 2.9 Restart / Redraft 통합

**Restart (기존 같은 픽 유지):**
1. BattleBridge.TeardownCurrentBattle (모든 유닛/투사체/체력바 파괴)
2. CostRuntime.ResetToStart()
3. GameManager.CurrentPhase = Placement
4. PlacementPhaseView.Show (30초 카운트다운 재시작)

**Redraft:**
1. 동일한 teardown
2. DraftController.BeginDraft → DraftView 표시
3. TryConfirm → Placement → Battle

코스트 리셋은 항상 `startingCost`로 복귀. 최대 상한 미반영.

### 2.10 기존 코드 영향 요약

| 파일 | 변경 |
|---|---|
| `DefenderUnitData.cs` | `cost` 필드 추가 |
| `SkillData.cs` | `cost` 필드 추가 |
| `Data/CostConfig.cs` (신규) | ScriptableObject — starting/max/regen/placementDuration |
| `Core/CostRuntime.cs` (신규) | MonoBehaviour — 상태/API |
| `Core/GameManager.cs` | `GamePhase` enum + `CurrentPhase` property, flow rewire (Briefing→Draft→Placement→Battle→Result) |
| `UI/PlacementPhaseView.cs` (신규) | 카운트다운 + Start 버튼 |
| `UI/CostDisplay.cs` (신규) | 좌하단 숫자 + 바 |
| `Core/PlacementInput.cs` | 코스트 체크 + 실패 시 flash 호출 |
| `Core/MapView.cs` | `FlashTileReject(Vector2Int)` 메서드 추가 |
| `UI/SkillBar.cs` | 코스트 게이트 추가, 슬롯 색상 로직 확장 |
| `Bridge/BattleBridge.cs` | `PlaceDefenderAs`/`CastSkill*`에 cost 인자 + CostRuntime 경유 소모 + 로그 cost_spent 전달. StartBattle 시 CostRuntime.BeginRegen. Teardown 시 StopRegen. |
| `Logging/BattleLogSchema.cs` | PlacementLog/SkillUsageLog에 cost_spent 필드 |
| `Logging/BattleLogger.cs` | 메서드 시그니처 확장 |
| `Data/Defenders/*.asset` | 각 SO에 cost 할당 |
| `Data/Skills/*.asset` | 각 SO에 cost 할당 |

---

## 3. 종료 조건 (Done Criteria)

### 3.1 기능 이진 체크

**[P6-01] 데이터 스키마 + CostConfig SO + 수치 할당**
- [x] `DefenderUnitData.cost` 필드 + 10종 SO에 할당
- [x] `SkillData.cost` 필드 + 3종 SO에 할당
- [x] `Data/CostConfig.cs` SO + 기본값 에셋 1개 생성
- 선행: Phase 5 완료
- 완료 확인: Inspector에서 모든 cost 필드 읽힘

**[P6-02] CostRuntime MonoBehaviour**
- [x] `Core/CostRuntime.cs` — Current/Max/CanAfford/TrySpend/BeginRegen/StopRegen/ResetToStart
- [x] Update에서 regen (float, maxCost 상한)
- [x] GameManager 자식 GameObject로 씬 배치
- 선행: P6-01
- 완료 확인: EditMode 테스트 — Configure(10,15,1) 후 Update tick 5초 시뮬 → Current=10 유지(regen 비활성), BeginRegen 호출 후 5초 → 15로 상한 고정

**[P6-03] GamePhase 상태 머신 (GameManager)**
- [x] `GamePhase` enum 추가
- [x] `GameManager.CurrentPhase` property + SetPhase 내부 전환 메서드
- [x] Start 플로우: Briefing → Draft → Placement → Battle 전이
- [x] 기존 TimelineBriefing/DraftController/BattleBridge 콜백이 SetPhase 호출
- 선행: P6-01
- 완료 확인: execute_code로 각 페이즈 전이 후 CurrentPhase 값 확인

**[P6-04] PlacementPhaseView + 배치 페이즈 진입**
- [x] `UI/PlacementPhaseView` 런타임 빌드 — 상단 카운트다운 + 중앙 START BATTLE 버튼
- [x] DraftController.TryConfirm이 Phase=Placement 전환 + PlacementPhaseView.Show + CostRuntime.ResetToStart (regen OFF)
- [x] 카운트다운 종료 또는 Start 버튼 → Phase=Battle + BattleBridge.StartBattle + CostRuntime.BeginRegen + TimerDisplay/SkillBar 활성
- 선행: P6-03
- 완료 확인: Play — Draft Confirm 후 30s 카운트다운 뜸, 배치 가능, 코스트 10에서 감소, 전투 시작 후 regen 시작

**[P6-05] CostDisplay UI**
- [x] 좌하단 런타임 빌드, `"N / Max"` + 진행 바
- [x] Phase == Placement 또는 Battle일 때만 표시
- 선행: P6-02
- 완료 확인: Play 전 과정에서 숫자/바가 정확히 현재 코스트 반영

**[P6-06] PlacementInput + 거부 피드백 + CostRuntime 소모**
- [x] PlaceDefender 클릭 시 CostRuntime.CanAfford 체크
- [x] 실패 시 MapView.FlashTileReject(cell) — 0.2s 빨간 플래시 후 복원
- [x] 성공 시 CostRuntime.TrySpend(unitData.cost) 후 배치
- [x] BattleBridge.PlaceDefenderAs 서명에 cost 매개변수 추가, 로그 cost_spent 전달
- 선행: P6-04, P6-05
- 완료 확인: Play에서 코스트 부족 시 타일 빨강 플래시 + 배치 실패, 충분하면 배치+코스트 감소

**[P6-07] SkillBar 코스트 게이트**
- [x] 슬롯 interactable = (IsReady && CanAfford && Phase==Battle)
- [x] CanAfford==false 시 회색 슬롯 (C8=a)
- [x] 캐스트 성공 시 CostRuntime.TrySpend(skill.cost) + CastSkill 호출
- [x] BattleBridge.CastSkill*에 cost 매개변수 + 로그 cost_spent 전달
- 선행: P6-04, P6-05
- 완료 확인: 코스트 3 미만일 때 Power Surge 슬롯 회색, 충분하면 발동 후 코스트 감소

**[P6-08] 로깅 v5**
- [x] PlacementLog.cost_spent 필드
- [x] SkillUsageLog.cost_spent 필드
- [x] BattleLogger 메서드 시그니처 확장
- [x] 세션 JSON에 cost_spent 정상 기록
- 선행: P6-06, P6-07
- 완료 확인: `/GameLogs/session-*.json` 최신 파일에 cost_spent 필드 확인

**[P6-09] Restart / Redraft 통합**
- [x] Result → Restart 버튼 → Placement 페이즈 재진행 (같은 픽, 코스트 10 리셋, 유닛 teardown)
- [x] Result → Redraft 버튼 → Draft → Placement → Battle
- [x] 기존 `_defenderByTile`/`_occupiedTiles`/체력바 teardown 경로 정상 유지
- 선행: P6-04
- 완료 확인: 2판 이상 플레이 — Restart/Redraft 양쪽에서 Placement 페이즈 진입 확인

**[P6-10] EditMode 테스트 확장**
- [x] `CostRuntimeTests` — Configure/TrySpend/Refund/BeginRegen tick/Max clamp
- [x] 기존 26개 회귀 없음
- 선행: P6-02
- 완료 확인: run_tests 전부 pass (목표: 기존 + 신규 3~5건)

**[P6-11] Phase 0~5 회귀 체크**
- [x] 브리핑 → 드래프트 → 배치 페이즈 → 전투 → 결과 → Restart/Redraft 전 플로우 정상
- [x] 로그에 cost_spent + 기존 필드 모두 적재
- [x] onPlace/시너지/Splash/Timer 등 기존 기능 모두 동작
- 선행: P6-09, P6-10
- 완료 확인: 한 판 수동 플레이 완주

---

### 3.2 아키텍처 이진 체크

**Phase 0~5 재확인:**
- [x] BattleBridge 유일 MonoBehaviour ↔ ECS 창구
- [x] 맥락 4종 유지, 새 폴더 0개
- [x] GameManager 유일 싱글톤

**Phase 6 전용:**
- [x] CostRuntime이 싱글톤 아님 (GameManager.costRuntime 레퍼런스로만 접근)
- [x] 코스트 수치 전부 SO 필드 (하드코딩 0건)
- [x] GamePhase 상태 전이는 GameManager가 단일 주관 (다른 코드가 CurrentPhase 직접 쓰기 금지)
- [x] PlacementInput/SkillBar가 직접 CostRuntime을 쓰되, 수치 결정은 SO에서 읽음
- [x] Assembly Definition 2개 체제 유지

---

### 3.3 주관 평가 게이트

Phase 6의 핵심 질문: **코스트 제약이 "지금 쓸까 아낄까" 긴장을 만들어내는가.**

- 3~5명 플레이어가 배치 페이즈 30초 + 전투 180초를 3판 이상 플레이.
- 수집 지표:
  - 배치 페이즈 카운트다운 마지막 5초에 잔여 코스트 분포 (평균적으로 0에 가까운가? 많이 남기는가?)
  - 전투 중 코스트 maxCost 도달 빈도 (15에 오래 머무르면 여유 있음 = 희소성 부족)
  - 스킬 사용 시점 평균 (코스트 충전 대기 시점에 몰리는지)
  - 자가 보고: "코스트 때문에 스킬을 아꼈거나 배치를 미룬 적 있는가?" (Y/N)
- 통과 기준: 2문항 이상 Y 다수 + maxCost 도달 비율 < 30%.

---

## 4. 에이전트 자율 결정 영역

- CostConfig 단일 SO vs AttackDeck 필드 추가 중 어느 쪽에 코스트 파라미터를 둘지
- CostDisplay 디자인 세부 (폰트/바 색상)
- PlacementPhaseView 카운트다운 형식 (분:초 vs 초만)
- 빨간 플래시 지속 시간 (0.15 ~ 0.3s)
- 회색 비활성 슬롯의 정확한 tint 값
- MapView.FlashTileReject 구현 — 런타임 color 덧칠 vs 임시 이벤트 엔티티 생성 (전자 단순)
- CostRuntime.RefundSpend 호출 시점 — Phase 6 초기 구현에서는 실제 실패 경로 없으면 미사용 가능

**고정(자율 결정 아님):**
- 유닛/스킬 코스트 수치는 §2.1 표 초기값. 튜닝은 SO에서 Inspector로.
- startingCost=10, maxCost=15, regenPerSec=1.0, placementPhaseDuration=30s 초기값 동일.
- 배치 페이즈: 코스트 regen 정지 + 스킬 비활성 (B5=a, B6=a).
- 코스트 부족 시 배치 visual feedback(C7=b), 스킬 슬롯 회색(C8=a).
- Restart/Redraft 양쪽 배치 페이즈 재진행 (D9=a, D10=a).
- CostDisplay 좌하단.
- 충전 연속 소수점 (E12).
- 로그에 cost_spent 필드 추가 (E13).

---

## 5. 산출물

- 동작하는 Unity 6 프로젝트 (Briefing → Draft → Placement → Battle → Result 전 플로우)
- EditMode 테스트 기존+신규 pass
- `phase6-decisions.md` 누적 기록
- cost_spent 필드가 들어간 JSON 로그 샘플 3개 이상
- Phase 7 이후에서 재활용될 핵심 타입: CostRuntime, CostConfig, GamePhase enum

---

## 6. Phase 순서 (현재)

| Phase | 내용 | 상태 |
|---|---|---|
| 0 | 실시간 디펜스 루프 | ✅ |
| 1 | 드래프트 | ✅ |
| 2 | 스킬 | ✅ |
| 3 | 전투 비주얼 | ✅ |
| 4 | 배치/시너지/적 공격/Splash | ✅ |
| 5 | 비주얼 업그레이드 + 타이머 + 브리핑 + 봇 + 측정 스크립트 | ✅ |
| **6** | **코스트 시스템** | **현재** |
| 7+ | 미정 — 난이도 튜닝, 추가 유닛, 스프라이트 애니메이션, Spine 등 | 대기 |

---

## 7. TRD 금지 패턴의 Phase 6 재적용

- **새 싱글톤 금지** — CostRuntime은 MonoBehaviour, public static Instance 없음. GameManager가 ref 보유.
- **수치 하드코딩 금지** — 모든 cost 값 SO 필드. starting/max/regen/duration 전부 CostConfig에서.
- **새 맥락 폴더 금지** — CostRuntime은 Core. 배치 거부 플래시는 MapView(Core) 내부.
- **맥락 경계 유지** — ECS Component 직접 수정 없음. 코스트는 MonoBehaviour 레이어 전용.
- **GameManager 단일 싱글톤 유지** — CurrentPhase property는 GameManager에.
- **"나중을 위한" 인터페이스 금지** — CostRuntime은 concrete class. ICostSource 등 추상 금지.
- **Assembly Definition 2개 체제 유지**.
- **기존 원본 Component 불변 원칙** — AttackState.damage, PathFollowState.speed 등 여전히 쓰기 금지.

---

## 8. 구현 결과 스냅샷 (2026-04-19)

Phase 6은 현재 구현 완료 상태다. 확정/구현된 세부 결정은 과거 `phase6-decisions.md` 에 기록되었고, 본 문서가 Phase 6 스펙의 단일 출처다.

- `CostConfig` 와 `CostRuntime` 이 starting/max/regen/placement duration을 관리한다.
- `GameManager.GamePhase` 가 Draft / Placement / Battle / Result 전이를 명시한다.
- Draft 확정 후 30초 Placement 페이즈에 진입하고, 전투 시작 후 cost regen이 시작된다.
- defender 배치와 skill cast는 cost를 지불하며, 부족 시 UI 비활성 또는 tile reject flash로 피드백한다.
- Restart 는 같은 pick/loadout으로 Placement를 재진행하고, Redraft 는 Draft부터 다시 시작한다.
- JSON 로그에는 `cost_spent` 가 포함된다.

**문서 버전**: v1.1 (구현 스펙 통합)
**상태**: 구현 완료. 기존 EditMode 테스트 35/35 통과 기록.
