# 3 — BallisticArc Trajectory (곡사 궤적 arm)

## 목적

궤적 축의 **두 번째 arm** 을 MoveSystem switch 에 추가한다: `BallisticArcToPoint` — 타겟 엔티티 없이 발사 시 고정된 셀로 포물선 비행. 이로써 궤적 축이 실제 2 구현체(Homing + Ballistic)가 되어 seam 이 실증되고(프로젝트 규칙 "구현체 2개 이상" 충족), 이후 베지어는 arm 하나로 붙는다. **홈잉 arm 무수정.**

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/Projectile/BallisticArc.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileMoveSystem.cs` (switch 에 arm 추가)
- 신규 `Assets/_Project/Tests/EditMode/BallisticArcTests.cs`
- `Assets/_Project/Tests/EditMode/ProjectileSystemTests.cs` (arm 통합 테스트 1개)

## 구현

- `BallisticArc.ArcPosition(origin, impact, arcHeight, t)`: `lerp(origin,impact,t)` 후 Y 에 `sin(t·π)·arcHeight` 가산. t=0→origin, t=1→impact(양 끝 arc항=0), t=0.5→apex.
- `BallisticArc.FlightTime(origin, impact, speed, minTime)`: XZ 거리/speed, `speed>0` 가드, `minTime` floor(point-blank 도 arc 가 보이게 — 즉시 착탄 방지).
- MoveSystem `BallisticArcToPoint` arm: `elapsed += dt`(RefRW), `t = saturate(elapsed/flightTime)`, `Position = ArcPosition(...)`, `elapsed ≥ flightTime → impactReached`. 타겟/`transformLookup` 불사용.
- `flightTime`/`origin`/`impact`/`arcHeight` 는 spawn(unit 5)에서 세팅 → unit 3 에선 아직 **발사 경로 없음**, 런타임 inert.
- `minTime` 출처(상수 vs authored)는 unit 5 배선 결정. 순수함수는 파라미터로 받아 테스트 가능.

## 완료 기준

- [x] refresh **scope=all** → 컴파일 0 에러.
- [x] `BallisticArcTests` green: 끝점(t=0/1), apex(t=0.5=+arcHeight), Y 대칭, `FlightTime` dist/speed·min floor·zero-speed. (6개)
- [x] `ProjectileSystemTests` 통합 테스트 green: 곡사 투사체가 arc 중간위치 경유 → flightTime 에 arrival·소비. **홈잉 회귀 유지** (EditMode 496/497, 1 실패=무관 ObstaclePlacer).
- [ ] 리뷰는 unit 4(payload arm) 게이트에 흡수.

완료 확인: 2026-07-06 — 컴파일 0, 신규 7 테스트 green, 홈잉 회귀 유지.
