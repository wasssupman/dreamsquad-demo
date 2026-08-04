# 2. 공성 게이트 + 근접 개통 — 유출 억제와 goal 최후순위 타겟팅

## 목적

살아있는 골에서 유출을 봉인하고, 적이 골을 최후순위 타겟으로 공격하게 한다. 이 unit 으로 근접 적(Melee 5종)의 공성이 개통된다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (적 스폰 targetMask)
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/EnemyAiStateSystem.cs`

## 구현

1. **공성 게이트**: `MovementSystem` 프레임 초에 `GoalPoint` 쿼리로 살아있는 골 셀 집합(`FixedList` 또는 소용량 NativeHashSet, 맵당 ≤4)을 구축. 골 도달 판정 `!hunting && !patrolling && field.IsGoalCell(cell)` 에 `&& !IsAliveGoalCell(cell)` 추가 — 살아있는 골 셀에서는 `PastGoalTag` 를 붙이지 않는다. 골 엔티티가 파괴되면(붕괴) 다음 프레임부터 자동으로 현행 유출 경로가 재개된다. hunting(보스)·patrolling(순찰 아군) 게이트 의미는 불변.
2. **targetMask 개통**: 적 스폰 시 `targetMask = Defender | BlockingHazard | Goal` (`BattleBridge` 적 스폰 지점). 방어유닛/가디언 마스크는 무변경.
3. **goal 최후순위**: `AttackSystem` nearest 스캔에서 `Faction.Goal` 후보는 별도 슬롯으로 추적하고, **사거리 내 non-Goal 유효 후보가 하나도 없을 때만** 골을 채택한다.
   - 기존 오버라이드 계층 교차(리뷰 확인): healer-rankByHealth·priorityClass·aggro-sticky·frontmost·facing 5개는 Defender/DefenderUnitTag 게이트로 골 자연 배제 — 접점 없음.
   - **FocusUntilDead 는 골을 잠그지 않는다(리뷰 M3)**: 잠금 채택(focus 기록) 시 `GoalPoint` 보유 대상은 제외한다. 골을 잠그면 이후 배치된 방어유닛을 사거리에 두고도 골만 계속 때려 "최후순위" 계약이 깨진다. 골은 항상 매 프레임 최후순위 재평가 대상.
4. **AI 미러 동기화**: `EnemyAiStateSystem.HasFireTarget` 은 targetMask OR 에 따라 골을 후보로 포함하기만 하면 된다(boolean "때릴 대상이 있는가" 판정 — 최후순위 선택 로직은 AttackSystem 전용, 리뷰 L1). 골만 사거리에 있어도 Engaging 이 성립해 골 앞 정지가 성립한다. 골 셀 위/인근에서 flow=0 정지 + Engaging(Halt) 자연 성립 확인.

## 완료 기준

- [x] compile + 기존 EditMode green (관련 스위트 96/96).
- [x] Play(M>0 맵): 근접 적이 골 사거리에서 멈춰 공격, 유출/스트레스 0 유지 — 풀 맵 임시 M=300 주입으로 사용자 Play 확인. EditMode 로도 고정: `GoalSiegeGateTests` 4건(공성/DeadTag 당프레임 복귀/파괴 후 복귀/멀티골 셀 단위).
- [x] 방어유닛 교전 현행 동일 + 골 최후순위 — `GoalTargetingPriorityTests` 3건(근접골보다 원거리 방어유닛 우선/골만 있을 때 채택/Focus 잠금 금지·전환).
- [x] 붕괴 후 유출 재개 — 게이트 테스트(DeadTag/파괴) + Play 확인.
- 주의: walk-only 적(Runner/Swift)·원거리 헛사격은 unit 3 전까지 의도된 중간 상태.

2026-08-04 사용자 확인 완료. (검증용 임시 맵 M=300 은 미커밋 — 콘텐츠 값은 별도 결정)
