# 2. 기믹 프레임 — GimmickData SO + SeasonData.gimmick + BattleBridge 주입 seam

## 목적

시즌에 기믹을 묶는 데이터 모델과, 매치 인프라 구축 시 기믹 config 를 ECS 로 주입하는 유일한 seam 을 만든다. 이 unit 이 끝나면 룰 시스템(unit 3~5)은 `OverworkGimmickConfig` 싱글턴 유무로 self-gate 만 하면 된다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Gimmick/GimmickData.cs` — 신규 base SO (gimmickId / displayName)
- `Assets/_Project/Scripts/Data/Gimmick/OverworkGimmickData.cs` — 신규 야근 기믹 SO (모든 룰 수치)
- `Assets/_Project/Scripts/Data/Season/SeasonData.cs` — `gimmick` 필드 (nullable)
- `Assets/_Project/Scripts/Battle/Effects/OverworkGimmickConfig.cs` — 신규 blittable config 싱글턴 컴포넌트
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 주입/파괴 (EnsureQueriesAndQueues / DestroyEcsInfrastructureEntities)
- `Assets/_Project/Data/Gimmick/Gimmick_Overwork.asset` — 야근 기믹 데이터 (아직 어느 시즌에도 미연결)

## 구현

1. **GimmickData (base)**: abstract SO — `gimmickId`, `displayName` 만. base 클래스는 `SeasonData.gimmick` 필드 슬롯을 위해 필요 (상속 SO → base → concrete = 2단계, 제약 7 준수).
2. **OverworkGimmickData**: `fatigueStack`(StackModifierSO 참조 — maxStack/perAppDuration 원천), `fatigueInterval=10`, `fatigueAmount=1`, `redbullSpawnInterval=5`, `lastRunAttackSpeedMul=1.5`, `lastRunDuration=5`, `lastRunMaxHealthMul=0.1`. 하드코딩 금지 계약의 수치 원천.
3. **SeasonData.gimmick**: null 허용. null = 기믹 시스템 전체 비활성 (config 미생성).
4. **OverworkGimmickConfig** (Effects): SO 수치의 blittable 사본. Burst 시스템이 SO 를 직접 만지지 않도록 주입 시점에 복사.
5. **BattleBridge seam**: `EnsureQueriesAndQueues` 끝(BuildStackThresholdRegistry 다음)에서 `SeasonRuntime.Active?.gimmick is OverworkGimmickData` 일 때만 config 엔티티 생성. `DestroyEcsInfrastructureEntities` 에 `DestroyEntitiesByType<OverworkGimmickConfig>()` 대칭 추가 (BattleTimeScale orphan 교훈 준수 — 누락 시 재입장마다 중복 엔티티).

## 완료 기준

- compile 통과 + 콘솔 클린.
- 현 시즌(gimmick=null) BattleScene Play smoke — 기존 플레이 무변화, config 엔티티 미생성.
- Gimmick_Overwork.asset 생성 확인 (시즌 연결은 unit 3 검증에서 임시, 정식은 unit 7).

확인 2026-07-15 · 커밋 `de6068e5`
