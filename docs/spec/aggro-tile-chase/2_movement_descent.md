# 2. Movement — Chasing 을 chase field 하강으로 교체 + 프레임 변위 클램프

## 목적

직선 greedy 추격(수선/코너 고착의 원인)을 chase field 하강(cardinal step)으로 교체한다. 경로가 있으면 사거리 타일 도달이 보장된다. 동시에 프레임당 변위 상한(계약 6)으로 터널링을 차단한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — Chasing 분기 재작성, guardianPos 스냅샷/aggroLookup 제거
- `Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs` — `ClampDisplacement` 추가 (unit 3 tornado 도 사용)

## 구현

- Chasing: `AggroChaseCell` 버퍼(RO)를 `Reinterpret<int>` 로 dist 배열화 → `FlowRecovery.RecoveryDir` 하강(기존 재사용, 동일 타이브레이크). dir zero = 목적지(dist 0) 도착 또는 고립 — 정지. 버퍼 없음(합성 테스트 월드) = 정지.
  - 도착 셀은 정의상 발사 조건 충족 → 다음 틱 EnemyAiStateSystem 이 Standoff 전이(기존 계약 5 — 사거리 조기 진입 시 더 일찍 전이).
- 스텝은 flow 이동과 동일 형식(speed×moveSpeedMul×dt), cell-trim 은 안전망으로 유지. 임펄스는 Chasing 중 적용하지 않음(기존 동작 유지 — Chasing 은 flow 분기 이전에 continue).
- `ClampDisplacement(current, desired, tileSize)`: XZ 변위를 0.9×tileSize 로 상한 — 단일 목적 셀 검사(cell-trim)의 전제("한 프레임에 최대 인접 셀")를 불변식으로 만든다. flow 분기(임펄스 합산 후)와 Chasing 분기 모두 trim 직전 적용.
- guardianPos NativeHashMap·aggroLookup 제거 (Chasing 상태 자체가 어그로 함의 — Evaluate 계약).

## 완료 기준

- compile 0 · EditMode 전체 green (ClampDisplacement 단위 테스트 포함).
- PlayMode 검증은 unit 4 에서 (고착 재현 지형 스모크).
