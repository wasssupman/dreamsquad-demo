# Handoff — aggro-targeting

> 상태: 완료 2026-06-18 (Unit 0~6). 자석 디펜스 어그로 핵심 루프 + 적 공격필터.

## Commit (단위별)

- `d559e70` unit 0 — 컴포넌트/SO 필드
- `7db763b` unit 1 — BattleBridge 베이킹
- `665d0e7` unit 2 — AggroAssignmentSystem (획득/해제)
- `70dc5b2` unit 3 — 어그로 보행+stack
- `59ddc7e` unit 4 — 공격필터+우선순위
- `030d484` unit 5 — sticky + 도발공격
- (이 커밋) unit 6 — EditMode 테스트 + 사망 가디언 획득 가드 보강

## Implemented

- 가디언 전용 어그로: `DefenderUnitData.aggroCapacity>0` 인 디펜더만 `AggroProvider` 부착.
- 획득: `AggroAssignmentSystem`(Effects)이 매 틱 근접(가디언 attackRange) 미어그로 적을 capacity 한도로 배정. 선점 고정.
- 해제: 가디언 사망/소멸 시 링크 적 `Aggroed` 제거 → 기본 거동 복귀. 죽은(Health 0) 가디언은 신규 획득도 안 함.
- 이동: 어그로 적은 flow/포탈/토네이도/pause 우회, **자기 moveSpeed로** 가디언 보행 후 겹쳐 정지(토네이도 강제풀 아님).
- 타게팅: 어그로 적은 필터/우선순위/최근접 무시하고 사거리 내 가디언만 sticky 공격. outputs 없는 Runner/Swift 는 `AggroAttackProfile`→임시 `AttackState`+outputs grant(해제 시 strip).
- 적 공격필터: `EnemyTargetFilter{classMask, priorityClass}`. Shooter→Ranger 우선, 그 외 all(최근접). 디펜더 클래스는 `DefenderClassTag`로 ECS 노출.

## Key Files

- `Battle/Effects/AggroProvider.cs`, `Aggroed.cs`, `TauntAttackGranted.cs`, `AggroAssignmentSystem.cs`
- `Battle/Combat/AggroAttackProfile.cs`, `EnemyTargetFilter.cs`, `AttackSystem.cs`(선정 분기)
- `Battle/Units/DefenderClassTag.cs`
- `Battle/Movement/MovementSystem.cs`(어그로 보행 분기)
- `Bridge/BattleBridge.cs`(디펜더/적 베이킹 ~2800, ~3390)
- `Data/DefenderUnitData.cs`, `AttackUnitData.cs`(필드)
- `Tests/EditMode/AggroAssignmentTests.cs`

## Verified

- EditMode: `AggroAssignmentTests` 5/5. 전체 334 중 332 pass / 0 fail / 2 기존 ignore.
- Play(실월드 reflection): capacity 상한, 선점, 사망 해제+재배정, 보행 수렴(dist 4→0), Shooter→Ranger 우선, sticky(가까운 Ranger 무피해), 도발 grant/strip. 콘솔 에러 0.

## Notes (되돌리면 안 되는 의도)

- 어그로 상태 쓰기는 **AggroAssignmentSystem(Effects) 단독**. Movement/Combat 은 `Aggroed` 읽기 전용. (TornadoField 선례)
- count 는 컴포넌트에 저장 안 함 — 매 틱 `Aggroed` 집계로 파생(drift 방지).
- 어그로 이동은 토네이도 pull 재사용 금지(개념 분리: 자기 보행 vs 강제 변위).
- aggroCount/도발공격 수치는 placeholder(Guardian/Bastion 4, Runner/Swift 5/1/1) — 밸런싱 spec 위임.

## Follow-up (미구현)

- **도발(에픽 가디언)**: aggroCount 무한 해제 + 범위 일괄 어그로 + 최근 우선. 별도 spec.
- **"시간당 어그로 수량" rate**: 공격당 대상수×공격속도. 현재는 근접 즉시 배정.
- **적 클래스 거동 정식화**: 탱커/러너 walk-only, 브루저/슈터 focus-fire(죽을때까지)·move-and-shoot. 클래스는 상위 축, 행동은 sub-variant.
- **적 브루저 AoE 2+ ↔ Fighter 인접 시너지 자리싸움**, 점수·드림캐쳐 후크.
