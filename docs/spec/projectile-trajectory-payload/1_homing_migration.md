# 1 — Homing Migration (홈잉 이관)

## 목적

기존 홈잉 투사체를 궤적/페이로드 **switch seam** 으로 이관한다. **동작 보존**. 핵심 전략: 홈잉을 이 unit 에서 **한 번만** 건드리고 완전히 봉인 → 이후 unit 3(ballistic)/4(TileAoe)/5(wiring)은 각 switch 에 arm 만 추가하고 홈잉 코드는 다시 안 건드린다. 회귀 위험을 이 게이트 하나에 집중.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs` (+ `impactReached` 필드)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileMoveSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs`

`BattleBridge.cs` / `AttackSystem.cs` **무변경** (impactReached default false 라 spawn 무수정).

## 구현

- **ProjectileState**: 런타임 arrival 플래그 `bool impactReached` 추가. spawn 시 false, MoveSystem 이 도착 프레임에 set, HitSystem 이 소비. (구조 변경 아님, 직접 값 write.)
- **ProjectileMoveSystem** (`RefRO`→`RefRW<ProjectileState>`): `switch(movement)`.
  - `HomingToEntity` arm = 기존 추적/스냅/타겟-null 파괴 로직 그대로.
  - **arrival 판정 이동**: 기존엔 HitSystem 이 거리 판정했으나, "각 궤적이 자기 도착을 안다" 원칙에 따라 여기로 이동. post-move XZ `distSq ≤ hitThreshold²` 이면 `impactReached=true`. 조건식은 legacy HitSystem 과 **동일**.
  - `BallisticArcToPoint` arm 은 unit 3 에서 추가.
- **ProjectileHitSystem** (payload 해결자): arrival 을 `if (!impactReached) continue;` 게이트로 전환(거리 판정 제거). `switch(payload)`.
  - `SingleSplash` arm = 기존 outputs + fallback damage + hit VFX + splash + HitFlash 그대로. 타겟-null 가드를 arm **안으로** (TileAoe 는 타겟 없음).
  - `LocalTransform` 쿼리 제거 (SingleSplash 는 `targetPos` 만 사용). `TileAoe` arm 은 unit 4 에서 추가.
- **클래스명 유지** (`ProjectileMoveSystem`/`ProjectileHitSystem`). README 의 "Move/Impact" 는 개념명 — 리네임은 meta/참조 churn 이라 회귀 위험 unit 에서 안 함(원하면 후속 cosmetic).

## 완료 기준

- [x] refresh scope=all → 컴파일 0 에러.
- [x] **홈잉 무회귀**: EditMode 프로젝타일 테스트 6개(기존 5 + 신규 다프레임 arrival) green, 483/484 pass(1 실패=무관 ObstaclePlacer 기존 결함). Play smoke 는 포커스 필요로 생략(EditMode+리뷰로 대체).
- [x] 두 트랙 리뷰 게이트 통과(ecs-reviewer + code-reviewer 둘 다 Pass, minor 2건 반영: MoveSystem default arm + 다프레임 arrival 테스트).
- [x] diff = 3파일만 (BattleBridge/AttackSystem 무변경).

완료 확인: 2026-07-06 — 양트랙 리뷰 Pass, EditMode 483/484.
