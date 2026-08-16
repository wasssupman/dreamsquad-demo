# 1 — 궤도 궤적 (레인 A · 엔진)

## 목적

투사체 이동 어휘에 **한 점을 도는 원운동**을 추가한다. 카드도 드림캐쳐도 모르는 순수 엔진 작업이며,
`projectile-emission-pattern` 계약 11 이 예고한 표준 레시피 그대로다 —
`MovementKind` append + 위치 순수함수 + Move arm + (필요하면) view-Y arm.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/Orbit.cs` **(신규)**
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileMoveSystem.cs`
- `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` (필요할 때만)
- `Assets/_Project/Tests/EditMode/OrbitTests.cs` **(신규)**

## 구현

### 1) 전제 — 어휘는 이미 서 있다

`MovementKind.OrbitAroundPoint = 6` · `MovementBinding` 분류 · 브리지 드레인의 궤도 분기
(중심/반경/각속도/지속/관통예산)는 **unit 0 이 이미 커밋했다.** 이 unit 은 `MovementKind.cs` 도
브리지도 만지지 않는다 — **수학과 그 수학을 매 프레임 돌리는 arm** 만 갖는다.

### 2) 필드 재사용 (신규 `ProjectileState` 필드 0)

| 슬롯 | 의미 |
|---|---|
| `origin` | 궤도 중심 (발사 시점 host 셀 중심, 고정) |
| `maxDistance` | 궤도 반경 (월드 유닛) |
| `speed` | 각속도 (rad/s, 음수 = 역회전) |
| `flightTime` / `elapsed` | 지속 초 / 누적 |
| `prevPos` | 직전 프레임 위치 — PathHit 스윕이 읽는다(`DirectionalLinear` 과 동일 규약) |
| `direction` | **접선 방향**을 매 프레임 써 준다 (아래 4) |

### 3) 순수함수 `Orbit.cs`

```
public static float3 Position(float3 center, float radius, float angularSpeed, float elapsed)
```
`θ = angularSpeed * elapsed`, `center + (cos θ, 0, sin θ) * radius`.
시작 각도는 **0 고정**(결정론 — seeded RNG 금지, `project_sim_structural_determinism`).
`Unity.Entities` 무참조 · plain 값 in/out (CLAUDE.md 제약 10).

### 4) Move arm

`ProjectileMoveSystem` 의 switch 에 `case MovementKind.OrbitAroundPoint:`
- `prevPos = 현재 위치` 를 **먼저** 기록 → `elapsed += dt` → `Orbit.Position` 으로 위치 갱신.
- `elapsed >= flightTime` 이면 `impactReached = true`.
  ⚠ PathHit 에게 `impactReached` 는 "착탄"이 아니라 **"비행 종료"** 신호다(최종 스윕 후 소멸).
  이 arm 은 그 규약을 `DirectionalLinear` 과 공유한다.
- `direction` 에 **접선**(`(-sin θ, cos θ)`, 각속도 부호 반영)을 쓴다. PathHit 이 한 프레임에
  여러 명을 스쳤을 때 **front-most 우선** 정렬에 이 벡터를 쓰기 때문이다. 화염구는 관통 예산을
  소모하지 않아 결과가 달라지지는 않지만(unit 2 계약 3), 0 벡터를 남기면 정렬이 조용히 무의미해져
  나중에 이 궤적을 관통 예산과 함께 쓰는 사람이 함정을 밟는다.

### 4-1) ⚠ 각속도 저작 상한 — 현(chord) 함정

스윕은 **직전 위치와 현재 위치를 잇는 직선**이다. 원운동에서 그 직선은 호가 아니라 **현**이라,
프레임당 회전각이 크면 현이 원 안쪽으로 파고들어 **정작 궤도 선상에 서 있는 적을 스쳐 지나간다**
(빠를수록 더 많이 놓친다 — 직관과 반대 방향의 버그라 눈으로 잡기 어렵다).

가드는 저작으로 한다: 프레임당 회전각이 작게(대략 `angularSpeed × dt ≲ 0.3 rad`, 60fps 기준
`angularSpeed ≲ 18 rad/s`) + 탄 SO 의 `hitThreshold` 로 여유. unit 3 의 초기값은 이 범위 안에 둔다.
**Burst/프레임률에 따라 dt 가 변하므로 코드로 클램프하지 않는다** — 클램프하면 저프레임에서
궤도가 느려져 결정론이 깨진다.

### 5) 뷰

궤도는 sim XZ 만 도므로 **view-Y arm 은 추가하지 않는 것이 기본값**이다(높이는 탄 SO 의
`visualHeightOffset`). facing 은 기존 `AlongVelocity` 가 접선을 준다.
→ 구현 중 화면에서 확인하고, **필요할 때만** arm 을 넣는다. 넣었다면 그 이유를 주석에 남긴다.

## 완료 기준

- EditMode `OrbitTests` green:
  ① 어느 `elapsed` 에서도 중심과의 거리 = `radius`(부동소수 tolerance)
  ② `elapsed = 0` → 시작점이 `center + (radius, 0, 0)`
  ③ 한 바퀴(`2π/angularSpeed`) 뒤 시작점 복귀
  ④ 음수 각속도 = 반대 방향
  ⑤ `radius = 0` → 항상 중심(퇴화 안전)
- 기존 궤적 5종 EditMode 무회귀.
- **컴파일만 확인하고 커밋하지 않는다**(README 계약 P3). Play 검증은 unit 3 에서 화염구가
  실제로 날아갈 때 한다.

---

확인 완료 2026-08-16 (사용자 Play 확인) — 커밋 `fa3a5eff`
