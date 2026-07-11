# 5 — 소스 규칙 수정: 4-이웃 → 공격 가능 반경 (버그픽스)

## 목적

실플레이 결함(2026-07-11): **레인에 인접하지 않은 셀에 배치한 방어유닛을 보스가 무시하고 goal 로 마칭.** 원인 = unit 0 의 소스 규칙("방어유닛 셀의 walkable 4-이웃")이 실전 맵에서 너무 좁다 — 걷는 셀이 레인 2줄+커넥터뿐이라 레인에서 1칸이라도 떨어진 배치 셀은 walkable 이웃 0 → 소스 미기여 → dist=MAX → fallback(goal) 발동.

옳은 계약: **소스 = "보스가 그 방어유닛을 공격할 수 있는 walkable 셀" = Chebyshev 거리 ≤ 보스 공격 사거리(타일)**. FSM 교전 판정(`HasFireTarget`)이 Chebyshev 기반이므로 이 정의와 정확히 일치 — 보스가 소스에 도달하면 자연히 Engaging 전이.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/FlowFieldBuilder.cs` — `CollectDefenderSources` 에 `rangeTiles` 파라미터
- `Assets/_Project/Scripts/Battle/Effects/DefenderFieldSystem.cs` — R 산출 + 보스 부재 early-out
- `Assets/_Project/Tests/EditMode/FlowFieldBuilderTests.cs` — 소스 수집 테스트 개정

## 구현

1. `CollectDefenderSources(walkMask, gridSize, defenderCells, rangeTiles, outSources)` — 각 방어유닛 셀 중심 Chebyshev ≤ rangeTiles 디스크의 walkable 셀 전부(자기 셀 제외). 대각 포함(FSM 사거리 판정과 동일 메트릭).
2. `DefenderFieldSystem`: `R = min(GridMath.RangeToTiles(AttackState.range))` over `BossTag` 엔티티, `max(1, R)` 클램프. AttackState(Combat) RO 읽기. **min fold 인 이유**: 소스는 "모든 동시 헌터가 발사 가능한 셀"이어야 사거리 짧은 보스가 dist-0 셀에서 발사 불가 정지(스톨)하는 경우가 구조적으로 불가능. 사거리 긴 보스는 FSM 이 소스 도달 전에 Engaging 으로 먼저 멈춘다. 현 콘텐츠는 보스 1종 → min==max, 동작 차이 0.
3. **보스 부재 early-out**: `BossTag` 쿼리가 비면 재빌드 skip — 필드 소비자가 보스뿐이므로 안전(보스 스폰 프레임엔 시스템이 Movement 앞에서 재빌드). 보스 없는 구간의 매 프레임 BFS 비용 제거(ecs-review M2 부분 해소).
4. 한계(의도된 동작): 모든 walkable 셀에서 Chebyshev R 밖인 초심층 배치는 여전히 소스 0 → goal 마칭. 보스가 물리적으로 공격 불가능한 대상이라 "사냥"의 정의 밖 — 정지 대기는 스톨이므로 채택 안 함.

## 완료 기준

- compile + EditMode 무회귀, 신규/개정 테스트: R=1 디스크(대각 포함) / R=2 심층 방어유닛 소스화(R=1 에선 0) / 전부-벽 0.
- Play: 레인 비인접(1칸 떨어진) 셀에 방어유닛 배치 → 보스가 사냥(역주행 포함)·교전. 보스 부재 시 필드 재빌드 skip 확인.

확인 2026-07-11 — EditMode 654(652 pass/0 fail, 구 결함 재현 테스트 포함) + **사용자 실플레이 확인("이제 잘 쫓아온다")**. R fold 는 max→min 정정(이질 사거리 동시 헌터 스톨 구조적 차단, 현 콘텐츠 min==max 로 동작 차이 0).
