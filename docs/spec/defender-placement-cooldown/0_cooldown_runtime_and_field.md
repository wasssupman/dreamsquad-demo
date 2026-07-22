# 0 — 쿨타임 런타임 + SO 필드 (토대)

## 목적

배치 쿨타임의 상태 토대를 세운다: 유닛 SO 에 쿨타임 값 필드, 유닛 타입별 남은 시간을 소유·tick 하는 Mono 런타임, `GameManager` 노출, 매치 리셋 훅, 순수 로직 EditMode 테스트. 이 unit 만으로는 아무 유닛도 쿨타임에 들어가지 않는다(시작·차단은 unit 1) — 여기서는 **상태 그릇과 시계**만 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — 필드 추가
- `Assets/_Project/Scripts/Core/PlacementCooldownRuntime.cs` — **신규**
- `Assets/_Project/Scripts/Core/GameManager.cs` — SerializeField + accessor + fallback 획득
- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` — 배치 페이즈 진입 리셋 훅
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 매치 teardown 방어적 리셋(critic m5, 선택이지만 일관성)
- `Assets/_Project/Tests/EditMode/PlacementCooldownRuntimeTests.cs` — **신규**
- 씬 배선: `GameManager` GO 에 `PlacementCooldownRuntime` 컴포넌트 부착 + SerializeField 할당 (UnityMCP)

## 구현

**DefenderUnitData** — `cost`(현 57행) 바로 뒤에 추가:
```csharp
// defender-placement-cooldown 0 — 배치 성공 후 이 타입 재배치가 막히는 시간(초).
// 0 = 쿨타임 없음(기존 동작 유지, opt-in). 유닛 타입 단위.
public float placementCooldown = 0f;
```

**PlacementCooldownRuntime** (`Wassup.Core`, MonoBehaviour, **비싱글턴** — `CostRuntime` 미러):
- 내부: `Dictionary<DefenderUnitData, Entry>` where `struct Entry { float remaining; float total; }`.
- API:
  - `void StartCooldown(DefenderUnitData unit, float seconds)` — `unit == null || seconds <= 0f` 이면 **no-op**(등록 안 함). 아니면 `_map[unit] = {remaining=seconds, total=seconds}`(재배치 시 full 리셋).
  - `float RemainingFor(DefenderUnitData unit)` — 없으면 `0f`.
  - `bool IsReady(DefenderUnitData unit)` — `RemainingFor <= 0f`.
  - `float Fraction(DefenderUnitData unit)` — `remaining/total` (1→0), 없으면 `0f`. 오버레이 fillAmount 용.
  - `bool AnyActive => _map.Count > 0`.
  - `void Tick(float dt)` — 각 entry `remaining -= dt`, `<= 0` 은 제거. 만료 키는 **필드로 둔 재사용 List** 에 수집 후 제거(매 tick 새 할당 금지; 각 tick 시작에 `Clear()`).
  - `void ResetAll()` — `_map.Clear()`.
- `Update`: `if (_map.Count == 0) return;` **조기 반환 후** `Tick(TimeManager.Instance.DeltaTime(TimeDomain.Battle))` — CostRuntime 과 동일 시계(슬로모 감속·정지 동결). 조기 반환으로 쿨타임 전무 시 `ScaleOf` 루프·빈 dict 순회조차 안 돈다(critic n1, "0 = inert" 를 O(1)→실질 0 에 근접).

**GameManager** (CostRuntime 패턴 미러, 27·48·131행 참조):
```csharp
[SerializeField] private PlacementCooldownRuntime cooldownRuntime;
public PlacementCooldownRuntime CooldownRuntime => cooldownRuntime;
// Awake 초기화 블록에서: if (cooldownRuntime == null) cooldownRuntime = GetComponentInChildren<PlacementCooldownRuntime>();
```

**PlacementPhaseView** (96행 `CostRuntime.ResetToStart()` 옆) — 정상 경로 리셋(배치 페이즈 진입마다 = 매치 시작·재시작·리드로우 전부 커버):
```csharp
if (gameManager != null && gameManager.CooldownRuntime != null) gameManager.CooldownRuntime.ResetAll();
```

**BattleBridge** (teardown, 454-463행 `TimeManager.ResetAll()`/`CostRuntime.StopRegen()` 옆) — 매치 경계 방어적 리셋(critic m5). 정상 경로가 이미 커버하므로 correctness 필수는 아니나, teardown 에서 stale 쿨타임을 확실히 비워 일관성 유지:
```csharp
if (GameManager.Instance != null && GameManager.Instance.CooldownRuntime != null)
    GameManager.Instance.CooldownRuntime.ResetAll();
```

## 완료 기준

- [ ] 컴파일 클린 (Unity `read_console` 또는 `dotnet build` 대상 asmdef).
- [ ] EditMode 테스트 green (`CostRuntimeTests` 미러):
  - `StartCooldown(u, 5)` → `RemainingFor(u)==5`, `IsReady==false`, `Fraction==1`.
  - `Tick(2)` → `RemainingFor==3`, `Fraction==0.6`.
  - `Tick(3)` 이상 → 제거됨, `RemainingFor==0`, `IsReady==true`, `AnyActive==false`.
  - `StartCooldown(u, 0)` 및 `StartCooldown(null, 5)` → **no-op**(`AnyActive==false`).
  - `StartCooldown(u,5); StartCooldown(u,5)` 재호출 → full 리셋(remaining==5).
  - `ResetAll()` → 전부 소거.
- [ ] 씬: `GameManager` GO 에 `PlacementCooldownRuntime` 컴포넌트 존재 + `cooldownRuntime` SerializeField 할당됨(MCP 확인).
- [ ] Play 진입 시 콘솔 에러 없음.

✅ 확인: 2026-07-22 · commit `76067285` — EditMode 7/7 통과, 컴파일 클린, `GameManager/PlacementCooldownRuntime` GO + `cooldownRuntime` SerializeField 배선(BattleScene). Play 진입 검증은 시작 경로가 없어 unit 1·2로 이연.
