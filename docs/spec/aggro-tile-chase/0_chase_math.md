# 0. 순수함수 — 유효 사거리 해석 + chase field 빌드 (재사용 조합)

## 목적

어그로 추격의 목적지/도달가능 판정에 필요한 순수 계산을 확정한다. multi-source BFS 와 Chebyshev 디스크 소스 수집은 **기존 `FlowFieldBuilder.BuildFromSources`/`CollectDefenderSources`(boss-defender-field, 기 테스트됨)를 재사용** — 신규 로직은 (a) 적의 유효 tileRange 해석, (b) 재사용 조합 wrapper, (c) 버그 기하 회귀 테스트다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AggroChaseMath.cs` (신규 — 정의 계층, AggroPolicy 형제)
- `Assets/_Project/Tests/EditMode/AggroChaseMathTests.cs` (신규)

## 구현

- `ResolveTileRange(hasAttack, attackRange, hasProfile, profileRange)` → int. AttackState 우선, 없으면 AggroAttackProfile 폴백, 둘 다 없으면 **-1 (전투 불능 — 어그로 획득 거부 신호)**. `GridMath.RangeToTiles` 와 동일 변환.
- `BuildChaseField(walkMask, gridSize, guardianCell, tileRange, tempFlow, outDist)` → int sourceCount. `CollectDefenderSources`(단일 가디언 셀, Chebyshev ≤ tileRange, 가디언 자신 셀 제외) + `BuildFromSources` 조합. sourceCount 0 = 목적지 후보 없음(거부), `outDist[enemyCell] == int.MaxValue` = 도달 불가(거부).
- 이동 하강은 신규 코드 없음 — 기존 `FlowRecovery.RecoveryDir`(dist 하강, 동일 타이브레이크) 재사용. dist 0 셀에서는 zero 반환 → 자연 정지.

## 완료 기준

- compile 0 errors.
- EditMode 신규 테스트 green: 사거리 해석 3케이스 / **수선 pin 기하**(가디언 통로 2칸 밖 + range1 → sourceCount 0) / 같은 기하 range2 → 도달 가능 / **코너 기하**(직선 불가·우회 가능 → dist 유한) / 고립 walk 섬 → MaxValue / RecoveryDir 하강으로 dist-0 도달.
- 기존 EditMode 무회귀.
