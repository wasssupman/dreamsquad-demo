# 1 — BattleSimGroup 부모 그룹 + 24 시스템 재타겟 (ECS 구조)

## 목적

RateManager 를 붙일 단일 제어 지점을 만든다. 현재 전투 시스템 24개는 `SimulationSystemGroup` 에 평평하게 흩어져 있어 그룹 단위 시간 제어가 불가능하다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/BattleSimGroup.cs`
- 재타겟: `Scripts/Battle/{Units,Movement,Combat,Effects}/**` 중 `[UpdateInGroup(typeof(SimulationSystemGroup))]` 을 가진 전 시스템 (critic 집계 24개)

## 구현

1. `BattleSimGroup`:
```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]   // 이동/투사체가 LocalTransform write → 렌더 전 반영 보존
public partial class BattleSimGroup : ComponentSystemGroup { }
```
   - 구현 전 **현재 시스템들의 TransformSystemGroup 대비 실효 순서를 확인**해 정확히 재현. 흩어진 시스템이 암묵적으로 TransformSystemGroup 앞/뒤 어디였는지 검증.

2. 24개 시스템의 `[UpdateInGroup(typeof(SimulationSystemGroup))]` → `[UpdateInGroup(typeof(BattleSimGroup))]`.
   - **그룹 내부 `[UpdateBefore]/[UpdateAfter]` 관계는 그대로 유지** (전부 다른 전투 시스템 대상이라 유효).
   - 4+ 파일 동시 편집 위험 → A/B/C 서브태스크 분할 가능(Units/Movement / Combat / Effects). 각 배치 후 컴파일 확인.

## 완료 기준

- [ ] 컴파일 통과. `read_console` 에러 0.
- [ ] `SimulationSystemGroup` 직속 전투 시스템 0개 (전부 BattleSimGroup 하위). grep 으로 확인.
- [ ] 이 단위만으로는 **동작 회귀가 없어야 함** — RateManager 미부착 상태(scale=1 등가)에서 Play 스모크: 웨이브 진행·유닛 이동·전투가 재타겟 전과 동일.
- [ ] 유닛 위치 렌더가 1프레임 stale 하지 않음(TransformSystemGroup 순서 보존 확인).

## 주의

- 이 단위는 **구조만** 바꾸고 시간 스케일은 아직 도입하지 않는다. 회귀 격리를 위해 RateManager(단위 2)와 분리한다.
