# Unit 6 — 적 근접 AoE (attackTargetCount)

## 목적

적 근접 유닛이 단일 타겟이 아니라 주변 N명을 동시 타격(AoE)하도록 `attackTargetCount` 를 SO 에서 노출. 기획상 "적 브루저 AoE 2+ → 가디언 인접 파이터 splash" 자리싸움의 토대.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs` (attackTargetCount 필드)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (bake 하드코딩 1 → SO 값)
- `Assets/_Project/Data/Enemies/Enemy_Basic.asset`, `Enemy_Tanker.asset` (근접 적 값)

## 구현

- `AttackUnitData.attackTargetCount`(기본 1) 추가.
- 적 스폰 bake: `attackTargetCount = Mathf.Max(1, entry.unitType.attackTargetCount)`.
- 광역 로직은 AttackSystem melee outputs 경로(가장 가까운 N명)를 그대로 사용 — 디펜더 Bastion/Fighter 와 동일 경로, 적도 같은 루프.
- 근접 적 값: Basic=2, Tanker=2 (placeholder, 밸런싱 위임).

## 상호작용 (기존 처리)

- **어그로 시**: AttackSystem 이 어그로 적의 desiredCount=1 강제 → 가디언만(주변 splash 없음). 어그로 계약 유지.
- **FocusUntilDead**: 잠근 주 타겟 기준으로 N명 확장(primary 고정 + AoE).
- 투사체 적(Shooter)은 ProjectileData 의 splash 로 별도 처리 — attackTargetCount 는 melee 경로 전용.

## 완료 기준

- [x] 컴파일 + Play reflection: Basic AttackState.attackTargetCount==2.
- [x] Play: tc=2 공격자가 근접 3명 중 가장 가까운 2명에 피해(d1·d2=990, d3=1000).
- [x] 어그로된 근접 적 desiredCount=1(aggro-targeting unit 8 계약 — 가디언만).
- [x] EditMode 전체 회귀 없음(342 중 340 pass; Play 잔류 6건은 RequestScriptReload 후 해소).

완료: 2026-06-18 / 커밋 해시 `19f03c4`
