# 0. sim 순수 로직 — VolleyMath · LaneMath · SweepHitMath

## 목적

방향 레인 판정, 버스트 발사 타이밍, 스프레드 각 분배, 경로 스윕 히트를 아키텍처를 모르는 순수 static 함수로 만든다. 이후 작업 단위(2·3·4)의 ECS 시스템은 이 함수의 결과값만 소비한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/VolleyMath.cs` (신규)
- `Assets/_Project/Scripts/Battle/Combat/LaneMath.cs` (신규)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/SweepHitMath.cs` (신규)
- `Assets/_Project/Tests/EditMode/VolleyMathTests.cs` · `LaneMathTests.cs` · `SweepHitMathTests.cs` (신규)

## 구현

전부 `Unity.Mathematics` plain 값 입출력, Burst 호환(managed 타입·Time·EntityManager 금지). `BlinkMath`/`ModifierMath` 스타일.

**LaneMath** — 레인 멤버십:
- `bool IsInLane(int2 attackerTile, int2 facing, int rangeTiles, int2 targetTile)` — facing 축 투영 델타가 `[1, rangeTiles]`, 수직 델타 == 0 (폭 1타일). facing 은 cardinal 단위 벡터 전제(검증은 호출부).

**VolleyMath** — 발사 타이밍/각도:
- `int TickBurst(float dt, ref int remainingShots, ref float shotTimer, float intervalSec)` — dt 를 소화하며 이번 프레임 발사 수를 반환(느린 프레임에 interval 여러 개가 지나가면 >1). remainingShots 0 이면 0 반환.
- `float2 SpreadDirection(float2 baseDir, int shotIndex, int shotCount, float spreadAngleDeg)` — 총 확산각을 shotCount 발에 균등 분배해 baseDir 를 회전(3발 30° = −15°/0°/+15°). shotCount 1 또는 각도 0 이면 baseDir 그대로.
- `float CooldownAfterVolley(float cooldownDuration, int shotCount, float intervalSec)` — 버스트 완주 시간을 포함한 다음 트리거까지 쿨다운(= cooldownDuration + (shotCount−1)×interval). 계약 8(버스트 종료 후 기산)의 순수 표현.

**SweepHitMath** — 경로 스윕:
- `bool SegmentHits(float2 prevPos, float2 currPos, float2 targetPos, float hitRadius)` — 점-선분 최소 거리 ≤ hitRadius. 이동량 0 프레임(prev==curr)도 점 판정으로 동작.

## 완료 기준

- [ ] compile 통과, 신규 EditMode 테스트 전부 green (기존 테스트 회귀 없음)
- [ ] 테스트 커버: 레인 경계(0/1/range/range+1·수직 오프셋), TickBurst 다중 interval 프레임/잔여 0, 스프레드 홀짝 발수·0각도, 스윕 접선/무이동 프레임
