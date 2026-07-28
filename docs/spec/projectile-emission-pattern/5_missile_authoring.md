# 5 — 미사일 authoring + 텔레포트 제거

## 목적

이 spec 의 콘텐츠 산출물을 만든다: 보스 나이트매어가 **0.5초 간격으로 맵 전체 방어유닛 중 랜덤 1기에게 곡선 호밍 미사일을 발사**하고, 텔레포트는 사라진다. **C# 코드 0줄** — asset 3개와 보스 SO 편집만으로 성립하는지가 이 unit 의 시험이다.

## 변경 대상

- 신규 `Assets/_Project/Data/Projectiles/Projectile_NightmareMissile.asset`
- 신규 `Assets/_Project/Data/Projectiles/Pattern_NightmareMissile.asset`
- `Assets/_Project/Data/Enemies/Enemy_Boss_Nightmare.asset` — 텔레포트 mechanic 삭제 + 미사일 mechanic 추가

## 구현

### 탄 asset (`Projectile_NightmareMissile`)

| 필드 | 값 | 근거 |
|---|---|---|
| `flightMode` | `BezierHoming` | unit 1 |
| `speed` | 6 (초안) | `flightTime = dist/speed`, `minFlightTime` 하한 |
| `minFlightTime` | 0.5 | 근거리에서도 곡선이 보이게 |
| `arcHeight` | 1.5 (초안) | **view 공간** Y 아치 (계약 9) |
| `bezierLateral` | 1.2 (초안) | 좌우 스윙 폭(타일) |
| `bezierForwardBias` | 0.35 (초안) | 제어점 전방 배치 비율 |
| `hitThreshold` | 0.4 | 근접 도달 판정 |
| `facing` | `AlongVelocity` | 노즈가 곡선 따라 회전(추가 코드 0) |
| `projectilePrefab` · `hitPrefab` | 벤더 VFX | 아래 주의 |
| `onHitEffect` | `None` | 단일 대상 — splash 없음 |

**벤더 프리팹 주의**(`docs/reference/lessons/` + 기존 통합 경험): 스트립 시 자체 무버/Rigidbody/Collider 제거 · `TrailRenderer.autodestruct = false`(풀링 필수, 런타임에만 드러나는 함정) · ParticleSystem `emitterVelocityMode = Transform`. `hitPrefab` 은 반드시 `vfx_Hit_*` 실물을 지정한다 — GA `ProjectileData` 들이 머즐 프리팹을 가리키는 기존 함정이 있다(사실상 무연출).

### 패턴 asset (`Pattern_NightmareMissile`)

| 필드 | 값 |
|---|---|
| `barrel` | `Projectile_NightmareMissile` |
| `damage` | **40 (초안 — 실측 튜닝)** |
| `selection` | `DeterministicShuffle` |
| `shotCount` | 1 |
| `shotIntervalSec` | 0 |
| `reselectPerShot` | false |
| `telegraphSec` | 0 |

`shotCount` 를 2~3 으로 올리면 곡선이 갈라지는 살포가 된다(unit 1 의 `swingIndex` 교대) — 밸런스 여지로 남긴다.

### 보스 SO

`nightmareMechanics` 배열:

| index | 이전 | 이후 |
|---|---|---|
| 0 | `PeriodicTimer(10s) × AreaBarrage` | `PeriodicTimer(10s) × EmitProjectilePattern(Barrage)` — unit 4 |
| 1 | `HealthThreshold(0.3) × SelfBlink` | **삭제** |
| 2 | `PeriodicTimer(0.5s) × AllyMoveSpeedAura` | 그대로(채찍질) |
| — | — | **신규**: `PeriodicTimer(0.5s) × EmitProjectilePattern(Missile)` |

텔레포트 코드(`SelfBlink` payload · `BlinkMath` · `BlinkApplySystem` · `BlinkRequestEventsSingleton` · `BlinkMathTests` 7건)는 **남긴다**(2026-07-28 사용자 결정). inert 이며 다른 적/보스가 순간이동을 요구하면 SO mechanic 1건으로 부활한다. `HealthThresholdSystem` 은 방어유닛 `last_stand`(HealthThreshold × SelfStatBuff)가 계속 쓰므로 어느 경우에도 유지 대상이다.

주기 0.5초가 채찍질과 같아 두 슬롯이 같은 프레임에 발화한다 — 독립 accumulator라 문제 없다(nightmare-catcher 계약 4).

## 완료 기준

- **코드 diff 0줄** (asset·SO 만). 이 조건이 깨지면 unit 0~3 의 seam 에 공백이 있다는 뜻이므로 그 unit 으로 되돌아간다.
- Play: 보스 등장 후 0.5초 간격 발사 · 대상이 매번 다른 방어유닛(맵 반대편 포함 = 사거리 무제한) · 미사일이 곡선을 그리며 날아 명중 · 40 데미지 · 텔레포트 미발생(HP 70%/40%/10% 통과 시에도 제자리).
- 융단폭격·채찍질 동시 동작 확인(3슬롯 동시 틱).
- 콘솔 경고/에러 0.
- 미사일 데미지·주기 체감은 사용자 확인 사항으로 남긴다.
