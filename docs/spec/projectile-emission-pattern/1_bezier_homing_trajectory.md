# 1 — 베지어 호밍 궤적 arm (확장 seam 실증)

## 목적

"곡선으로 날면서 타겟을 추적" 조합을 개통한다. 현 `MovementKind` 는 `HomingToEntity`(직진·추적)와 `BallisticArcToPoint`(곡선·셀고정)가 배타적이라 이 조합이 표현 불가다. 궤적 하나 추가 비용이 **위치 순수함수 + Move arm 1 + view Y arm 1**(시스템·드레인·태그 0)임을 실증한다 — projectile-trajectory-payload 계약 8 의 리트머스. 베지어는 이 레시피의 실증이지 산출물의 전부가 아니다(README 계약 11 — 궤적은 열린 어휘).

**이동 수학의 거주지 계약**: 수학 본체는 로직 계층 순수 static 이다 — `BallisticArc`/`SkyFall` 이 `using Unity.Mathematics` 만으로 이미 그 형태이고, 같은 함수를 ECS sim(XZ)과 Mono view(Y)가 나눠 소비한다(아키텍처 종속이면 불가능한 공유). arm 은 "상태 읽기 → 순수함수 호출 → 결과 쓰기" 3줄 소비자다. `elapsed += dt`·`impactReached` 같은 자명한 1줄 전진만 arm 인라인(제약 10 후반부 — 과잉 추상화 금지).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/Projectile/Bezier3.cs` (순수)
- 신규 `Assets/_Project/Tests/EditMode/Bezier3Tests.cs`
- `Projectile/MovementKind.cs` — `BezierHomingToEntity = 5` **append**
- `Data/ProjectileData.cs` — `bezierLateral`·`bezierForwardBias` 추가 (탄의 성질 = barrel 소유, 계약 3)
- `Data/ProjectileData.cs` — `ProjectileFlightMode.BezierHoming` **append**
- `Bridge/BattleBridge.cs` — `ResolveProjectileAxes` 에 `BezierHoming → (BezierHomingToEntity, SingleSplash)` 매핑
- `Projectile/ProjectileState.cs` — `control1`·`control2`(float3 ×2) 추가
- `Projectile/ProjectileSpawnRequest.cs` — `swingIndex`(int) 추가 (제어점 자체는 싣지 않는다 — 아래 참조)
- `Bridge/BattleBridge.cs` — `SpawnProjectile` 에서 제어점 산출(드레인 = SO 접근 유일 seam)
- `Projectile/ProjectileMoveSystem.cs` — arm 1개
- `Presentation/ProjectileViewPool.cs` — view Y switch 에 arm 1개

## 구현

### `Bezier3` (순수)

```
static float3 Position(float3 p0, float3 p1, float3 p2, float3 p3, float t)
    // 3차 베지어. t 는 호출 측에서 saturate.

static void ControlPoints(float3 origin, float3 dest, int swingIndex,
                          float lateral, float forwardBias,
                          out float3 c1, out float3 c2)
```

제어점은 **결정론**(계약 6): 진행 방향 `dir`(XZ 정규화)의 수직 `perp` 로 좌우 교대 스윙.

```
sign = (swingIndex % 2 == 0) ? +1 : -1
mag  = lateral * (1 + (swingIndex / 2) * 0.35)     // 발수 늘면 더 크게 벌어짐
c1 = origin + dir*len*forwardBias + perp*sign*mag
c2 = dest   - dir*len*forwardBias + perp*sign*mag*0.5
```

`swingIndex` = `ShotOrder.shotIndex`. 그래서 `shotCount` 를 3 으로 올리면 **같은 타겟으로 세 발이 각각 다른 곡선을 그리며 갈라진다** — authoring 값 하나로 살포가 나온다.

**제어점은 요청이 아니라 드레인에서 산출한다.** `lateral`/`forwardBias` 는 barrel SO 값이고 ISystem 은 SO 를 읽을 수 없다. 그래서 요청은 `swingIndex` 만 싣고, `BattleBridge.SpawnProjectile` 이 `dataIndex` → `ProjectileData` 를 해석해 `ControlPoints` 를 호출한 뒤 `ProjectileState.control1/2` 를 채운다 — `SkyFall` 의 `dropHeight` 를 드레인이 채우는 기존 선례와 같은 seam 이다. 덕분에 요청 struct 에 궤적 파라미터가 늘지 않고, 발사 주체(AttackSystem·emitter·캐스트)는 어느 쪽도 SO 를 알 필요가 없다.

퇴화 입력 가드: `origin ≈ dest`(len < 1e-6) 이면 `perp` 가 정의되지 않으므로 `c1 = c2 = dest`(직선으로 붕괴). `BlinkMath.FallbackAxis` 와 같은 결 — 런타임 파생 축으로 NaN 을 재도입하지 않는다.

### Move arm

```
case MovementKind.BezierHomingToEntity:
    // P3 = 타겟 live 위치 → 호밍. 타겟 소실 정책은 HomingToEntity 상속:
    //   retargetTileRange > 0 이면 BounceRetarget 재조준, 아니면 파괴 (기존 필드 재사용)
    elapsed += dt;
    t = saturate(elapsed / flightTime);
    pos = Bezier3.Position(origin, control1, control2, targetPosXZ, t);
    impactReached = t >= 1f || distance(pos, targetPosXZ) < hitThreshold;
```

`flightTime` 은 발사 시 `distance/speed` 로 산출하고 `minFlightTime` 으로 클램프한다 — `BallisticArcToPoint` 가 드레인에서 쓰는 산출 경로를 그대로 공유한다(신규 산식 0). 비행 중 타겟이 움직여도 `flightTime` 은 고정 = 근접할수록 곡선이 압축되며 파고든다.

**sim 은 XZ 만 갱신한다.**

### view Y arm

```
case MovementKind.BezierHomingToEntity:
    pos.y += BallisticArc.ArcHeight(ps.arcHeight, math.saturate(ps.elapsed / ps.flightTime));
```

3축의 Y 성분은 여기서만 생긴다(계약 9 — `BoardSpace.ToView` 가 sim-Y 를 drop 하므로 sim 에 실으면 화면에 안 보인다). 기존 `BallisticArc.ArcHeight` 재사용이라 **신규 코드 0줄**이고, `arcHeight` 슬롯도 그대로 쓴다. 결과적으로 XZ 곡선 × Y 아치 = 3축 이동.

`facing = AlongVelocity` 면 뷰가 이미 프레임 간 위치차로 피칭하므로 미사일 노즈가 곡선을 따라 돈다 — 추가 코드 없다.

## 완료 기준

- 컴파일 클린 (`refresh_unity scope=all`).
- EditMode 신규 ≥ 7:
  - `Position`: t=0 → p0 · t=1 → p3 · 제어점 4개가 일직선이면 직선 lerp 와 일치 · t=0.5 대칭성
  - `ControlPoints`: 좌우 교대(swingIndex 0/1 의 perp 부호 반대) · `swingIndex` 증가 시 `mag` 단조 증가 · 퇴화 입력(origin≈dest) NaN-free
- **무회귀**: 기존 5 궤적 arm 의 EditMode/PlayMode 그린. `ProjectileState`/`SpawnRequest` 필드 추가는 default 0 이라 기존 스폰 전부 inert.
- 시각 검증은 unit 5(authoring)에서 — 이 unit 은 arm 만 열고 소비자를 만들지 않는다. `MovementKind` append 만으로는 어떤 SO 도 이 궤적을 고르지 않는다(미사용 라이브 경로 아님 — unit 5 가 같은 spec 안에서 소비자를 붙인다).
