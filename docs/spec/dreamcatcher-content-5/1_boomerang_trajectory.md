# 1 — 왕복 궤적 (`MovementKind.BoomerangReturn`)

## 목적

날아갔다 **돌아오는** 궤적을 엔진에 추가한다. 카드가 아니라 이동 수학 한 종이며,
`Orbit`(팽이)과 같은 계층 — 아키텍처 없이 도는 순수 함수 + 이동 arm 하나.

## 변경 대상

- `Battle/Combat/Projectile/Boomerang.cs` — **신규**(순수 static, Burst 호환)
- `Battle/Combat/Projectile/ProjectileMoveSystem.cs` — arm 1개
- `Bridge/BattleBridge.cs` — **`SpawnProjectile` 왕복 분기**(아래 §드레인)
- `Tests/EditMode/BoomerangTests.cs` — **신규**

뷰(`ProjectileViewPool`)는 **무변경**. 화면상 향하는 쪽은 뷰가 **직전 프레임 위치와의 차이**로
계산하므로 왕복이 자동으로 반영된다(`AlongVelocity`). 회전은 탄 SO 의 `spinSpeed`.

## 구현

**순수 함수**

```
Position(origin, axis, maxDistance, speed, elapsed, out bool returning)
TotalTime(maxDistance, speed) = 2 * maxDistance / speed
```

- 진행 거리 `s = speed * elapsed`
- `s <= maxDistance` → 나가는 중: `origin + axis * s`, `returning = false`
- 그 뒤 → 돌아오는 중: `origin + axis * (2*maxDistance − s)`, `returning = true`
- `s >= 2*maxDistance` → 왕복 완료

**이동 arm** (`ProjectileMoveSystem`)

1. `prevPos = 현재 위치` (PathHit 스윕의 입력 — 궤도 arm 과 같은 순서)
2. `elapsed += dt`
3. 위치 = 위 순수 함수
4. 왕복 완료면 `impactReached = true` → 히트 시스템이 마지막 스윕 후 파괴

### ⚠ `direction` 은 **발사 축이고 불변이다** — 되먹이지 말 것

초판 설계는 이 arm 이 매 프레임 `direction` 을 뒤집게 했다. **그러면 궤적이 깨진다**:
`direction` 은 위 함수의 `axis` **입력**이므로, 뒤집는 순간 다음 프레임이
`origin − axis*(…)` 를 내고 **부메랑이 발사점 뒤로 날아간다.**

궤도가 같은 함정을 피한 이유는 거기서 `direction` 이 **파생값**(접선)이라 아무것도 되먹이지
않았기 때문이다. 왕복에서는 그 필드가 **궤적의 정의 축**이라 구조가 다르다.

그래서 **「지금 어느 다리인가」를 나타내는 상태를 어디에도 두지 않는다**:

| 필요한 곳 | 어디서 얻나 |
|---|---|
| 넉백 방향 | 그 프레임 스윕 `pos − prevPos` (unit 2) |
| 화면상 향하는 쪽 | 뷰의 직전 위치 차이 (변경 0) |
| 궤적 계산 | `origin` · `direction`(발사 축) · `elapsed` |

`returning` 은 순수 함수의 **출력**일 뿐 상태로 저장하지 않는다. 계약 5 는 이 형태로 지켜진다.

### 드레인 (`SpawnProjectile`)

이 함수는 `req.movement` 별 분기 사슬이고 **궤적마다 전용 분기가 필수**다. 없으면
`origin`/`prevPos`/`direction`/`maxDistance` 가 전부 0 이라 — 컴파일은 되는데 —
**태어나자마자 죽거나, 첫 스윕 선분이 맵 원점에서 뻗는 방사선**이 된다
(궤도가 content-4 리뷰 M3 에서 겪은 바로 그 결함).

분기가 채울 것: `origin` = 발사 위치 · `prevPos` = **origin 과 같은 값**(0 이면 방사선) ·
`direction` = 발사 축 · `maxDistance` = 편도 거리 · `speed` · `pierceRemaining`.

**퇴화 저작은 여기서 loud 거절한다.** `speed <= 0` 또는 `maxDistance <= 0` 이면 왕복 완료
조건이 영원히 거짓이고, 재타격 탄은 관통 예산도 안 깎으므로 **불멸 투사체**가 된다.
직선탄이 **같은 이유로 이미 가드를 갖고 있다**(경고 + 엔티티 파괴 + 출력 스냅샷 해제) —
바로 위 분기이므로 그 형태를 그대로 복제한다.

⚠ **현(chord) 함정 없음.** 직선 왕복이라 스윕 선분이 실제 경로와 일치한다.
⚠ **중심(발사점)은 고정점이다**(계약 2). 「손으로 돌아온다」가 아니라 「던진 자리로 돌아온다」.

## 완료 기준

- [ ] EditMode(순수): 편도 끝에서 반환 · 왕복 후 발사점 복귀 · `returning` 전환 시각 =
      `maxDistance/speed` · 두 다리의 스윕 방향이 정확히 반대
- [ ] EditMode(**통합**): 탄 하나를 실제로 스폰해 **발사점에서 출발하고 발사점으로 돌아온다**.
      순수 함수 테스트만으로는 드레인 분기 누락이 전혀 안 잡힌다(그게 초판의 실제 결함이었다)
- [ ] 퇴화 저작(속도 0 / 거리 0)이 **스폰 단계에서 loud 거절**된다 — 불멸 투사체 0
- [ ] `MovementBinding` 전수 분류 초록
- [ ] 기존 궤적 6종 회귀 없음
