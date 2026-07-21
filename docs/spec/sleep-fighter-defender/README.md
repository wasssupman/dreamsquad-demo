# sleep-fighter-defender — 투머치토커 (수면 근접 파이터)

> 상태: 스펙 작성 2026-07-21 · 구현 대기

## 목표

히트한 적을 N초 재우는 단일 타겟 근접 방어유닛 **투머치토커**(id `too_much_talker`)를 추가한다.
저데미지·저속 공격의 가치를 데미지가 아니라 **수면 잠금**에 두는 첫 CC-정체성 방어유닛.

- 수면 지속(3.5s) ≥ 공격 쿨다운(3.0s) → 혼자 때리는 동안 대상 1체를 상시 잠금.
- 어그로 없음 — 지나가는 적을 재우는 소프트 초크. 무리에선 최근접 1체만 묶고 나머지는 통과.
- 기존 Sleep 계약(wake-on-hit) 유지 — 아군 화력 지역에선 수면이 즉시 깨지는 것이 밸런스 밸브.

검증 질문: **"수면 잠금이 데미지 없이도 배치 가치가 있는가, 그리고 wake-on-hit 아래에서 체감되는가?"**

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | `0_sleep_on_hit_cc.md` | SO `sleepOnHitSec` → `DefenderCcData` 베이크 → AttackSystem RESOLVE Sleep enqueue |
| 1 | asset | `1_unit_asset_and_catalog.md` | `Defender_TooMuchTalker.asset` 저작 + 카탈로그 등록 + Play 검증 |

## Feature-wide 계약

1. **히트 CC 경로 = `DefenderCcData`** (넉백 선례 그대로). `AttackOutputKind` 에 CC 를 신설하지 않는다 — 소비자 1개, 과잉 일반화(제약 8).
2. **Sleep 계약 불변**: wake-on-hit(피격 시 즉시 해제)·kind별 병합(remainingTime=max)·action-lock(공격+이동 정지)은 `docs/spec/combat-action-lock/` 계약을 그대로 소비. `CcKind`/`CcEffect` 스키마 변경 없음.
3. **수면 적용 대상 = RESOLVE 의 bestTarget 1체** (넉백과 동일 스코프). 다중 타겟 유닛에 붙여도 주 타겟만 잠든다 — 이 유닛은 `attackTargetCount=1` 이라 전 히트 = 수면.
4. **`sleepOnHitSec` 은 근접(무투사체) 유닛 전제.** 투사체 유닛에 설정하면 넉백과 동일하게 발사(RESOLVE) 시점 적용되는 기존 quirk 를 공유한다. 투사체 히트 시점 수면은 후속(payload 이관).
5. **자기 수면 자가 해제 없음은 시스템 순서가 보장**: 데미지 적용(프레임 N, DamageApplication) 시점에 Sleep 은 아직 미적용(N+1, CcApply) — 별도 가드 불요. 이 순서(CcApply→Movement→Attack→DamageApplication→CcClear)를 바꾸는 변경은 이 유닛을 깨뜨린다.
6. **신규 시스템/채널/컴포넌트 0** — 기존 `EnemyCcEventsSingleton` 재사용, 필드 추가만.
7. **보스 수면 면역 없음(MVP)** — 보스는 전 화력 집중 대상이라 wake-on-hit 이 실질 면역. 문제 시 BossTag 게이트 1줄 후속.
8. 수면 시간 포함 전 수치는 SO (하드코딩 금지).

## 파이프라인 커버리지 (Defender 아키타입 대조)

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Defender_TooMuchTalker.asset` 신규 + `DefenderUnitData.sleepOnHitSec` 필드 신설 + **DefenderCatalog 등록**(unit 1) |
| 스폰 진입점 | 변경 없음 — 기존 `PlaceDefenderAs`→`CreateDefenderEntity`. `DefenderCcData` 베이크에 1필드 추가(unit 0) |
| ECS 컴포넌트 (Units) | 표준 세트 그대로. HazardCastState/AggroProvider/DeployedFacing/VolleyFireState **N/A — 능력 비활성**(hazard 0·aggro 0·directional 0·shotCount 1) |
| 시뮬 시스템 | `AttackSystem` RESOLVE 에 sleep enqueue 분기 추가(unit 0). CcApply/CcClear/CcDecay 는 기존 그대로 |
| 이벤트 큐 | **신규 채널 0** — 기존 `EnemyCcEventsSingleton` 재사용 |
| View/Pool | 기존 `SpineUnitPool`(Casual Character 파츠 재조합). 잠 연출은 기존 `StatusFxKind.Sleep` 리컨사일이 자동 커버 |
| 체력 표시 | 변경 없음 — 기존 `UnitOverheadUiLayer` |
| 씬 wiring | **N/A — 신규 SerializeField 없음.** 카탈로그 등록만으로 로스터 노출 |

## 후속 후보

- **배치 시 광역 수면 펄스** [S/M] · `OnPlaceEffectType` 에 Sleep 변종 신설 — 등장하며 주변을 재우는 시그니처 연출.
- **no-wake 수면 변종** [M] · `CcEffect` 에 no-wake 플래그 — Sleep 계약 확장이라 별도 결정 필요.
- **전용 아트 패스** [S] · portrait/배치 컷씬/파츠 식별성 확정(현 placeholder 교체, guid 유지).
- **보스 수면 면역** [S] · CcApply 또는 enqueue 측 BossTag 게이트 1줄. 어그로 면역 백로그(nightmare-catcher)와 같은 결.
- **투사체 히트 시점 수면** [M] · sleep 을 projectile payload 로 이관 — 원거리 수면 유닛 성립 조건.
