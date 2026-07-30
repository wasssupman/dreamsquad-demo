# unit 3 — projectile launch projection

## 목적

모든 투사체의 몸체 높이, arc, drop을 월드 `+Y`가 아니라 카메라 평면의 `up`으로 표시한다.
원근 카메라에서 맵 상단·하단·좌우 외곽으로 갈수록 탄환이 유닛 몸체에서 바깥으로 밀리는
왜곡을 제거하면서 ECS 발사 원점, 이동 거리, sweep hit 좌표는 그대로 유지한다.

이 unit은 공통 `ProjectileViewPool`의 표시 좌표만 바꾼다. 유닛별 muzzle bone 추적,
ProjectileData 수치 재튜닝, 시뮬 궤적 변경은 범위가 아니다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Data/ProjectileData.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs`
- `Assets/_Project/Tests/EditMode/HeadAnchorTests.cs`
- `docs/spec/projectile-shot-sequence/README.md`
- `docs/spec/projectile-shot-sequence/4_handoff_summary.md`

## 구현

- `BoardSpace.ToView(simPos)` 결과를 공통 base position으로 유지한다.
- `BattleBridge`가 활성 투사체의 `LocalTransform`과 표시용 `ProjectileState` 필드만 plain
  snapshot으로 복사해 Pool에 전달한다. Pool의 기존 `EntityManager` 직접 접근은 제거한다.
- `visualHeightOffset`, Ballistic/Bezier arc, SkyFall drop, Grenade bounce를 합산한 표시 높이는
  기존 `HeadAnchor.Lift(basePos, Vector3.up * height, Camera.main)`으로 카메라 평면에 투영한다.
- `ProjectileViewPool`은 `Camera.main`을 캐시하되 카메라가 없으면 `HeadAnchor`의 기존 월드
  fallback을 사용한다. 신규 SerializeField와 씬 배선은 추가하지 않는다.
- `lastPosition`은 static body height를 제외한 투영 궤적을 보존해 AlongVelocity가 arc/drop을
  따라 회전하게 한다. RollAlongPath는 별도의 ground position delta를 사용해 bounce 높이가
  구름 축에 섞이지 않게 기존 동작을 보존한다.
- ECS `LocalTransform`, `ProjectileState.origin/impact/prevPos/maxDistance`와
  `ProjectileMoveSystem`/`ProjectileHitSystem`은 수정하지 않는다.
- `ProjectileData.visualHeightOffset`, `arcHeight`, `dropHeight` 수치는 변경하지 않는다.

## 완료 기준

- Unity 컴파일 오류가 없다.
- EditMode 테스트가 카메라 평면 lift 후 카메라 depth와 screen X가 유지되고, null camera
  fallback이 기존 월드 offset과 같은지 검증한다.
- 기존 projectile emitter/trajectory/visual 테스트가 회귀 없이 통과한다.
- Play에서 맵 중앙과 상·하단/좌·우 외곽의 샷건너·머신거너 발사점이 같은 몸체 높이로 보이고,
  Ballistic/SkyFall/Grenade 표시가 유지된다.
- ECS 리뷰에서 sim/Presentation 경계, Component 소유권, 구조 변경, lifecycle 회귀가 없다.
