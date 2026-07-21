# 0. 기믹 데이터 + config + 주입 seam

## 목적

온천 기믹의 토대를 놓는다: 룰 수치를 담는 `OnsenGimmickData`(SO) + Burst 시스템이 소비할 blittable `OnsenGimmickConfig`(ECS 싱글턴) + BattleBridge 가 배정 기믹에 맞춰 config 를 주입하는 seam. **Burnout 3종(Data/Config/inject) 을 그대로 미러**한다.

## 변경 대상

- **신규**: `Assets/_Project/Scripts/Data/Gimmick/OnsenGimmickData.cs`
- **신규**: `Assets/_Project/Scripts/Battle/Effects/OnsenGimmickConfig.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 2곳:
  - `CreateGimmickConfigIfActive()` — pre-clear 에 `DestroyEntitiesByType<OnsenGimmickConfig>()` 추가 + else-if 분기로 `OnsenGimmickConfig` 주입 (ClockOut 분기 다음).
  - `DestroyEcsInfrastructureEntities()` — 인프라 파괴 대칭 목록에 `DestroyEntitiesByType<OnsenGimmickConfig>()` 추가 (ClockOut 다음).

## 구현

1. **`OnsenGimmickData : GimmickData`** (`[CreateAssetMenu(... menuName="Wassup/Gimmick/Onsen", order=42)]`, `sealed`):
   - `float heatInterval = 5f` — 열기 누적 주기(초).
   - `byte flipThreshold = 5` — 이 스택 **이하** = 회복, **초과** = 손실.
   - `float healPercent = 0.1f` — 스택 획득 시 회복 = maxHP × 비율.
   - `float lossPercent = 0.1f` — 과열 손실 = maxHP × 비율(HP 1 바닥).
   - `byte heatMaxStack = 6` — 카운터 상한(flipThreshold+1 이면 충분; 이후 효과 동일).
2. **`OnsenGimmickConfig : IComponentData`** (blittable 사본 — 위 5개 필드). 존재 = 기믹 활성 → `HeatAccrualSystem`(unit 2)이 `RequireForUpdate` self-gate.
3. **BattleBridge 주입 분기**: `else if (_assignedGimmick is Wassup.Data.OnsenGimmickData od)` → `_em.CreateEntity()` + `AddComponentData(OnsenGimmickConfig{...})`. 다섯 필드 1:1 복사. Debug.Log 미러. (rng 불필요 — 결정론 셀 선택 없음.)

## 완료 기준

- Unity 재컴파일 CS 에러 0.
- `Wassup/Gimmick/Onsen` 메뉴로 SO 생성 가능(에셋 생성은 unit 3).
- config 주입/파괴가 BattleBridge 양쪽(재빌드 pre-clear + 인프라 teardown)에 대칭으로 걸림 — 재진입 시 orphan/중복 없음(BattleTimeScale 교훈).
- 이 단계에선 아직 열기 효과 없음(HeatAccrualSystem 은 unit 2). config 만 주입돼도 무동작이 정상.
