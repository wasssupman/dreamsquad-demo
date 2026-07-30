# shotgun-spread-defender — 샷건너 (초근거리 부채꼴 스프레드 레인저)

> 상태: 완료 (2026-07-30) — 초기 5발 실증 완료. 현재 발사 계약은
> `docs/spec/projectile-shot-sequence/`에서 10발·4타일 시퀀스로 대체됨.

## 목표

방향 지정 2페이즈 배치와 부채꼴 발사를 쓰는 레인저 **샷건너**(id `shotgunner`)를 추가했다.
이 spec은 directional-volley의 첫 스프레드 소비자를 만든 **초기 구현 이력**이다.

- 최초값은 5발·사거리 2·90°였고, 현재값은 10발·사거리 4·−30°..+30°다.
- 배치 스킬 = **방사 밀치기**(`onPlacePush*` 재사용): 밀려난 적이 다시 레인으로 걸어 들어와 짧은 발동 창을 보완.
- 유닛·카탈로그·펠릿 에셋은 계속 사용하며 스케줄러만 공용 emitter로 이관됐다.

최신 검증 질문과 수치는 `projectile-shot-sequence/README.md`가 source of truth다.

## 현재 계약

- 10발, `-30°..+30°`, 결정론적 불규칙 방향, `5-3-2` 마이크로 클러스터(총 0.05초).
- 사거리 4, 탄당 피해 6, 펠릿 속도 14, 개별 `maxDistance=4*tileSize`.
- `DirectionalVolleyAbility`는 `ProjectilePatternData`를 참조하고 공용 emitter가 발사한다.
- START가 성사된 방향탄은 witness가 죽거나 이탈해도 고정 facing으로 완주한다.

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | test | `0_spread_integration_test.md` | 동프레임 부채꼴 발사 통합 테스트 — 미실증 엔진 경로 선검증 |
| 1 | asset | `1_unit_asset_and_catalog.md` | 유닛 SO + `Ability_Volley_Shotgunner` + 펠릿 투사체 + 카탈로그 + Play 검증 |
| 2 | docs | `2_handoff_summary.md` | 인계 요약 (종료 시) |

## 최초 구현 계약 (역사 기록)

1. **신규 시뮬 코드 0.** 스프레드는 `VolleyMath.SpreadDirection`(EditMode pinned) + `DirectionalVolleyAbility.spreadAngleDeg` 로 이미 구현됨. 이 spec 은 데이터와 테스트만 추가한다.
2. **`shotIntervalSec 0` = 동프레임 스프레드 계약** (`VolleyMath.TickBurst` — interval ≤0 이면 잔여 전탄 즉시). 쿨다운 연장(`CooldownAfterVolley`)도 0.
3. **레인 게이트 유지**: 확산각이 넓어도 발동 게이트는 정면 폭 1타일 × 사거리 레인(계약 6, directional-volley). 콘 커버리지는 발사 후 탄의 진행일 뿐이다.
4. **밀치기는 `onPlacePush*` 필드 재사용** — 필드·bake(`DefenderCcData`)·실행이 완비돼 있어 값만 넣는다. `OnPlaceEffectType` 은 None 유지(밀치기는 별도 축).
5. **bouncy_bead(통통구슬) 무효 quirk 를 머신거너와 공유** — 방향 유닛엔 bounce 가 조용히 무시된다. 개통은 backlog "방향탄 bounce 개통" [M] 스코프, 이 spec 에서 건드리지 않는다.
6. 탄당 데미지 = 유닛 `outputs[0].magnitude`(머신거너 선례). 전 수치는 SO — 하드코딩 금지.

## 최초 저작값 (역사 기록 — 현재값 아님)

Ranger · Rare · 코스트 3 · HP 200 · 사거리 2 · 쿨다운 2.2s · 탄당 데미지 12 (풀히트 60)
· shotCount 5 / interval 0 / spread 90° · 펠릿 pierce 0 · 밀치기 distance 1.5 / duration 0.35 / radius 2

## 파이프라인 커버리지 (Defender 아키타입 대조)

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Defender_Shotgunner.asset` + `Ability_Volley_Shotgunner.asset` + `Projectile_ShotgunPellet.asset`(MachineGunBullet 사본) + **DefenderCatalog 등록**(unit 0) |
| 스폰 진입점 | 변경 없음 — `PlaceDefenderAs`→`CreateDefenderEntity`. 기존 `GetAbility<DirectionalVolleyAbility>()` bake 그대로 |
| ECS 컴포넌트 (Units) | 표준 세트 + DeployedFacing. 현재 스케줄은 Combat 소유 `PatternSlot`·`EmitterInstance` |
| 시뮬 시스템 | 현재 `AttackSystem` trigger → `ProjectileEmitterSystem` → ProjectileMove/Hit |
| 이벤트 큐 | 신규 채널 0 — ProjectileHitEvents 등 기존 재사용 |
| View/Pool | 기존 SpineUnitPool(파츠 placeholder 허용) + ProjectileViewPool(펠릿 = MachineGunBullet 비주얼 재사용) |
| 체력 표시 | 변경 없음 — UnitOverheadUiLayer |
| 씬 wiring | **N/A — 신규 SerializeField 없음.** 카탈로그 등록만으로 로스터 노출 |

## 후속 후보

- **`UnitKitSummary` 스프레드 미인지** [S] · 폴백이 `shotCount>1` 을 무조건 "N연발 사격"으로 쓴다(`UnitKitSummary.cs:39`) — 동프레임 부채꼴을 시간차 연발로 오독. 샷건너는 authored desc 로 우회했지만 다음 스프레드 유닛이 desc 를 비우면 같은 오독이 재발한다. 문구는 테스트 고정 계약이라 변경 시 `UnitKitSummaryTests` 동반 갱신 필요 — 그래서 별도 결정.
- **탄별 데미지 거리감쇠** [M] · 원거리 스침은 약하게 — PathHit 페이로드에 거리 계수 필요.
- **전용 아트 패스** [S] · portrait/파츠/펠릿·머즐 VFX (placeholder 교체, guid 유지).
