# 2. ECS 투사체 — DirectionalLinear 궤적 + PathHit 페이로드

## 목적

방향 벡터로 직선 비행하며 경로상 적을 스윕 히트(관통 예산 N)하는 투사체를 기존 궤적×페이로드 2축에 arm 추가로 붙인다. 새 System/큐 없음.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileMoveSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/` 히트 기록 버퍼 element (신규 파일)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` `SpawnProjectile` (방향 request 분기)

## 구현

**ProjectileState 확장(또는 기존 필드 재활용)**: 발사 origin·direction·maxDistance·잔여 pierce 예산. 기존 arm 이 쓰는 필드와 겹치면 재활용하고, 아니면 최소 추가.

**ProjectileMoveSystem — DirectionalLinear arm**:
- `position += direction * speed * dt` (sim 평면. arc/sim-Y 없음 — BoardSpace 계약 준수).
- 누적 비행 거리 ≥ maxDistance 면 소멸 마킹. 점 도착 개념 없음 — `impactReached` 는 세우지 않는다(PathHit 는 도착 이벤트를 쓰지 않음).

**PathHit 히트 기록 버퍼**: 투사체 엔티티에 `DynamicBuffer<PathHitRecordElement>`(맞힌 Entity 목록). `BattleBridge.SpawnProjectile` 이 방향 투사체 생성 시 부착(IncomingHeal 사전 부착 선례).

**ProjectileHitSystem — PathHit arm**:
- 매 프레임 전프레임 위치→현위치 세그먼트에 대해 targetMask 후보를 `SweepHitMath.SegmentHits` 로 판정.
- 히트 시: hit-set 에 없으면 기록 → `IncomingDamage` append + `ProjectileHitEvent` enqueue(히트당 1건, 기존 큐 재사용) → pierce 예산 차감.
- 예산 소진 시 소멸. 예산이 남은 채 maxDistance 도달 시 조용히 소멸(히트 이벤트 없음).
- 같은 프레임 다중 후보 히트 가능(관통 탄이 밀집 대열 통과) — 예산 내에서 전부 처리.

**BattleBridge.SpawnProjectile**: 방향 request 는 타겟 엔티티/착탄 셀이 없다 — direction·maxDistance 를 ProjectileState 에 복사하고 hit-set 버퍼 부착. `ProjectileViewPool.Spawn` 은 기존 그대로(평면 비행이라 SyncTransforms 무변경 동작).

## 완료 기준

- [ ] compile 통과, 기존 Homing/Ballistic/SkyFall 경로 회귀 없음 (기존 테스트 green)
- [ ] execute_code 로 방향 request 1건 스테이징 스모크: 직선 비행 + 경로상 적 히트 + pierceCount=1 소멸 / pierceCount=3 관통을 콘솔·데미지 넘버로 확인
- [ ] 스윕/pierce 순수 판정은 unit 0 테스트가 커버 — 시스템 쪽은 통합 스모크로 충분
