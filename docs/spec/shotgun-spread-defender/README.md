# shotgun-spread-defender — 샷건너 (초근거리 부채꼴 스프레드 레인저)

> 상태: 초안 (사용자 승인 대기, 2026-07-29)

## 목표

방향 지정 2페이즈 배치 + **동프레임 부채꼴 5발**을 쏘는 초근거리 레인저 **샷건너**(id `shotgunner`)를 추가한다.
directional-volley 가 완성해 두고 실증 유닛이 없던 **스프레드 엔진**(`spreadAngleDeg`)의 첫 소비자다
(`defender-directional-volley` 후속 후보 "스프레드 실증 유닛(샷건형)" 승격).

- 사거리 2 × 확산각 90° — 발동 창은 좁고(정면 2칸 레인) 볼리당 화력은 최대. 하이리스크 하이리턴.
- 배치 스킬 = **방사 밀치기**(`onPlacePush*` 재사용): 밀려난 적이 다시 레인으로 걸어 들어와 짧은 발동 창을 보완.
- 신규 엔진 코드 0 — 유닛/능력/투사체 SO + 통합 테스트 + Play 검증이 전부.

검증 질문: **"동프레임 부채꼴 N발이 기존 볼리 계약 위에서 데이터만으로 성립하고, 초근거리 광각 샷건이 배치 가치가 있는가?"**

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | test | `0_spread_integration_test.md` | 동프레임 부채꼴 발사 통합 테스트 — 미실증 엔진 경로 선검증 |
| 1 | asset | `1_unit_asset_and_catalog.md` | 유닛 SO + `Ability_Volley_Shotgunner` + 펠릿 투사체 + 카탈로그 + Play 검증 |
| 2 | docs | `2_handoff_summary.md` | 인계 요약 (종료 시) |

## Feature-wide 계약

1. **신규 시뮬 코드 0.** 스프레드는 `VolleyMath.SpreadDirection`(EditMode pinned) + `DirectionalVolleyAbility.spreadAngleDeg` 로 이미 구현됨. 이 spec 은 데이터와 테스트만 추가한다.
2. **`shotIntervalSec 0` = 동프레임 스프레드 계약** (`VolleyMath.TickBurst` — interval ≤0 이면 잔여 전탄 즉시). 쿨다운 연장(`CooldownAfterVolley`)도 0.
3. **레인 게이트 유지**: 확산각이 넓어도 발동 게이트는 정면 폭 1타일 × 사거리 레인(계약 6, directional-volley). 콘 커버리지는 발사 후 탄의 진행일 뿐이다.
4. **밀치기는 `onPlacePush*` 필드 재사용** — 필드·bake(`DefenderCcData`)·실행이 완비돼 있어 값만 넣는다. `OnPlaceEffectType` 은 None 유지(밀치기는 별도 축).
5. **bouncy_bead(통통구슬) 무효 quirk 를 머신거너와 공유** — 방향 유닛엔 bounce 가 조용히 무시된다. 개통은 backlog "방향탄 bounce 개통" [M] 스코프, 이 spec 에서 건드리지 않는다.
6. 탄당 데미지 = 유닛 `outputs[0].magnitude`(머신거너 선례). 전 수치는 SO — 하드코딩 금지.

## 초기값 (전부 튜닝 대상, SO 소유)

Ranger · Rare · 코스트 3 · HP 200 · 사거리 2 · 쿨다운 2.2s · 탄당 데미지 12 (풀히트 60)
· shotCount 5 / interval 0 / spread 90° · 펠릿 pierce 0 · 밀치기 distance 1.5 / duration 0.35 / radius 2

## 파이프라인 커버리지 (Defender 아키타입 대조)

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Defender_Shotgunner.asset` + `Ability_Volley_Shotgunner.asset` + `Projectile_ShotgunPellet.asset`(MachineGunBullet 사본) + **DefenderCatalog 등록**(unit 0) |
| 스폰 진입점 | 변경 없음 — `PlaceDefenderAs`→`CreateDefenderEntity`. 기존 `GetAbility<DirectionalVolleyAbility>()` bake 그대로 |
| ECS 컴포넌트 (Units) | 표준 세트 + DeployedFacing + VolleyFireState(shotCount>1) — 머신거너와 동일 조합. HazardCastState/AggroProvider N/A(능력 비활성) |
| 시뮬 시스템 | 변경 없음 — AttackSystem 볼리 arm·ProjectileMove/Hit 기존 그대로 |
| 이벤트 큐 | 신규 채널 0 — ProjectileHitEvents 등 기존 재사용 |
| View/Pool | 기존 SpineUnitPool(파츠 placeholder 허용) + ProjectileViewPool(펠릿 = MachineGunBullet 비주얼 재사용) |
| 체력 표시 | 변경 없음 — UnitOverheadUiLayer |
| 씬 wiring | **N/A — 신규 SerializeField 없음.** 카탈로그 등록만으로 로스터 노출 |

## 후속 후보

- **탄별 데미지 거리감쇠** [M] · 원거리 스침은 약하게 — PathHit 페이로드에 거리 계수 필요.
- **탄퍼짐 지터** [S] · 현 균등 부채꼴(결정론) → index 기반 미세 지터(구조적 결정론 원칙 유지).
- **방향탄 bounce 개통 시 샷건 포함** [–] · backlog 항목이 흡수(계약 5).
- **전용 아트 패스** [S] · portrait/파츠/펠릿·머즐 VFX (placeholder 교체, guid 유지).
