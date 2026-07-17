# 2. ECS 투사체 — DirectionalLinear 궤적 + PathHit 페이로드

## 목적

방향 벡터로 직선 비행하며 경로상 적을 스윕 히트(관통 예산 N)하는 투사체를 기존 궤적×페이로드 2축에 arm 추가로 붙인다. 새 System/큐 없음.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileMoveSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/` 히트 기록 버퍼 element (신규 파일)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` `SpawnProjectile` (방향 request 분기)

## 구현

**ProjectileState 확장**: `direction`(정규화 발사 벡터) · `maxDistance` · `prevPos`(스윕 세그먼트 시작점, move arm 이 매 프레임 기입) · `pierceRemaining`. `origin`/`speed`/`hitThreshold`(=스윕 반경) 는 기존 필드 재활용.

**ProjectileMoveSystem — DirectionalLinear arm**:
- `position += direction * speed * dt` (sim 평면. arc/sim-Y 없음 — BoardSpace 계약 준수).
- 누적 비행 거리 ≥ maxDistance 면 위치를 사거리 끝에 클램프하고 `impactReached = true`.
- **rev1 계약(구현 시 확정)**: 이 arm 에서 `impactReached` 의 뜻은 "타겟 명중"이 아니라 **"비행 종료(최대 사거리 도달)"**. 초안은 "impactReached 를 세우지 않는다"였으나, MoveSystem 이 직접 파괴하면 **마지막 프레임 스윕이 소실**되어 사거리 끝 적이 그냥 통과된다. 소멸 소유권은 HitSystem 단독으로 두고(파괴 지점 이원화 금지), 이 플래그를 "이번 스윕 후 소멸" 신호로 쓴다. 기존 doc comment("trajectory reaches its endpoint")와도 정합.

**PathHit 히트 기록 버퍼**: 투사체 엔티티에 `DynamicBuffer<PathHitRecordElement>`(맞힌 Entity 목록). `BattleBridge.SpawnProjectile` 이 방향 투사체 생성 시 부착(IncomingHeal 사전 부착 선례).

**ProjectileHitSystem — PathHit arm**:
- **도착 게이트 우회**: 기존 루프 진입 조건 `if (!impactReached) continue;` 를 `if (!impactReached && payload != PathHit) continue;` 로 확장 — PathHit 은 비행 중 매 프레임 해결한다.
- 매 프레임 `prevPos`→현위치 세그먼트에 대해 적 스냅샷을 `SweepHitMath.SegmentHits`(반경 = `hitThreshold` 재사용) 로 판정.
- 히트 시: hit-set(`PathHitRecord`)에 없으면 → `IncomingDamage` append + ThreatTable 귀속 + `ProjectileHitEvent` enqueue(히트당 1건, 기존 큐 재사용) + HitFlash → 기록 append → pierce 예산 차감.
- **같은 프레임 다중 히트는 경로 앞쪽부터**: 스냅샷 인덱스 순서는 의미가 없으므로 방향 투영 거리로 정렬해 예산을 소비(1관통 탄이 항상 최근접 적에서 멈춤 — 결정론).
- 예산 소진 또는 `impactReached`(사거리 종료) 시 소멸. 그 외에는 생존(기존 `bounced` 플래그를 `survives` 로 일반화 — bounce·PathHit 공용).
- 데미지 소스는 `state.damage`(Damage 합산 스냅샷) — TileAoe 선례와 동일. 비-Damage output 은 후속(v1 Damage-only). 적 pool 한정(splash/bounce 선례).

**BattleBridge.SpawnProjectile**: 방향 request 는 타겟 엔티티/착탄 셀이 없다 — direction(여기서 1회 정규화)·maxDistance 를 ProjectileState 에 복사하고 `PathHitRecord` 버퍼 부착. `pierceCount` 는 SO 소유이므로 drain 이 번역해 채운다(SkyFall 의 dropHeight 보충 선례). **퇴화 방향(zero) request 는 경고 후 스폰 폐기** — 정지한 투사체가 사거리 소진 없이 영구 잔류하는 것을 구조적으로 차단. `ProjectileViewPool.Spawn` 은 기존 그대로(평면 비행이라 SyncTransforms 무변경 동작).

## 완료 기준

- [ ] compile 통과, 기존 Homing/Ballistic/SkyFall 경로 회귀 없음 (기존 테스트 green)
- [ ] execute_code 로 방향 request 1건 스테이징 스모크: 직선 비행 + 경로상 적 히트 + pierceCount=1 소멸 / pierceCount=3 관통을 콘솔·데미지 넘버로 확인
- [ ] 스윕/pierce 순수 판정은 unit 0 테스트가 커버 — 시스템 쪽은 통합 스모크로 충분
