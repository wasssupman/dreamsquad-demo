# beam-ranger-defender — 버스터즈 (지속 레이저 빔 레인저)

> 상태: **unit 0 완료(`45b1d645`) · units 1~2 미착수** (2026-07-29)
>
> unit 0 으로 심(0.2초마다 7 피해)은 이미 돈다 — 지금 배치하면 빔 없이 데미지 넘버만 초당 5회 뜬다.
> 남은 것은 **빔 비주얼(unit 1)** 과 그것을 재사용하는 **배치 스킬(unit 2)** 이며, 이 spec 에서
> 가장 반복 튜닝이 필요한 부분이다(벤더 프리팹 스트립 + 코얼레스 규칙 + 씬 배선 + 육안 확인).

## 목표

타겟에게 **지속 레이저 빔**을 쏘는 레인저 **버스터즈**(id `busters`)를 추가한다.
0.2초 간격으로 7 피해 — 투사체 없는 첫 원거리 유닛(히트스캔)이자 첫 빔 비주얼 유닛.

- **심 모델 = 고속 틱 직접 데미지**: `attackCooldown 0.2` + projectile 없음. 투사체 스폰은
  `ProjectileRef` 보유 게이트(`AttackSystem:763`)라 무투사체 원거리 유닛은 자동으로 직접
  데미지 경로를 탄다 — 신규 심 코드 0. 킬 크레딧/데미지 넘버도 기존 경로 그대로.
- **빔은 프레젠테이션**: 기존 `UnitAttackVisualEventsSingleton`(초당 5회 발생)을 코얼레스해
  빔을 유지하는 BeamPresenter 신설. VFX = `Assets/PixPlays/ElementalBeams/FireBeam/Version_URP/FireBeam.prefab`.
- 배치 스킬 = **개점 일제 조사(照射)**: 배치 순간 2타일 내 모든 적에게 2초간 빔 —
  심은 이산 tick DoT(0.2s 간격, 틱당 7 = 총 70), 연출은 대상별 빔 2초.

검증 질문: **"고속 틱 히트스캔이 기존 공격 계약(애님/사운드/데미지 넘버 포함)을 깨지 않고 돌며, 지속 빔이 공격으로 읽히는가?"**

## 작업 단위

| # | 구분 | 문서 | 상태 | 목적 |
|---|---|---|---|---|
| 0 | asset+test | `0_hitscan_unit_and_test.md` | **완료** `45b1d645` | 유닛 SO(히트스캔 0.2s/7) + 카탈로그 + 전제 회귀 테스트 |
| 1 | code | `1_beam_presenter.md` | 미착수 | FireBeam 통합 + 공격 이벤트 코얼레스 빔 프레젠터 |
| 2 | code | `2_onplace_beam_barrage.md` | 미착수 | 배치 스킬: 2타일 2초 tick DoT + 대상별 빔 연출 |
| 3 | docs | `3_handoff_summary.md` | — | 인계 요약 (종료 시) |

### unit 0 에서 실증된 것 (unit 1 진입 전 읽을 것)

- **"투사체 SO 없는 원거리 = 직접 데미지"** 전제가 실제로 성립한다. `AttackSystem` 의 발사 분기가
  `projectileRefLookup.HasComponent(attacker)` 게이트이고 else 가 Outputs path(직접 IncomingDamage)다.
  `HitscanDefenderTest` 가 이 전제를 고정한다(적을 1.6 거리에 세워 "무투사체=근접 퇴화"도 검출).
- `hitDelaySec 0` 이 필수다. 프로젝트 기본값 0.3 은 틱 간격 0.2보다 길어 타격이 밀린다.
- 아직 관측 안 된 것: **공격 애님/SFX 가 0.2s 마다 재트리거되는지**. unit 1 코얼레스 규칙의
  입력이므로 Play 진입 시 먼저 볼 것.

## Feature-wide 계약

1. **심에 "빔" 개념 없음.** 시뮬은 고속 틱 직접 데미지일 뿐이고 빔은 뷰가 공격 이벤트를 코얼레스한
   결과다(메커닉 연출은 메커닉이 소유 — StatusFx/bridge kind 분기 금지 원칙과 동근).
2. `hitDelaySec 0` — 0.3 기본값이면 틱 간격(0.2)보다 히트 지연이 길어 타격이 밀린다.
3. **타겟 전환 허용(MVP)**: 기존 타겟팅 그대로 — 대상이 죽거나 이탈하면 빔이 다음 타겟으로 옮겨
   붙는다. 락온/스티키 타겟은 후속 후보.
4. 배치 스킬의 심 효과 = 기존 이산 tick DoT(`CcEffect kind=DoT` + tickInterval — dot-tick-cadence
   계약 재사용). 신규 시스템/채널 0, `OnPlaceEffectType.DotNearby` enum 멤버 + SO 필드만 신설.
5. **공격 애님/SFX 는 0.2s 마다 재트리거하지 않는다** — 뷰 계층에서 코얼레스(빔 유지 중 1루프).
   상세 규칙은 unit 1 소유.
6. FireBeam 은 URP 버전 사용, 벤더 VFX 통합 규칙(`docs/reference/lessons/` 벤더 투사체 편) 준수 —
   무버/RB/Collider 스트립·풀링·scalingMode 확인.
7. 전 수치는 SO — 하드코딩 금지.

## 초기값 (전부 튜닝 대상, SO 소유)

Ranger · Epic · 코스트 4 · HP 160 · 사거리 3 · 쿨다운 0.2s · hitDelay 0 · outputs `[Damage 7]`
(지속 35dps 단일 — 로스터 최고 지속딜, 저 HP 로 상쇄) · 개점 조사: 반경 2 · 지속 2s · tick 0.2s · 틱당 7.

## 파이프라인 커버리지 (Defender 아키타입 대조)

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Defender_Busters.asset` 신규 + **DefenderCatalog 등록**(unit 0). 투사체 SO **N/A — 무투사체(히트스캔)가 사양** |
| 스폰 진입점 | 변경 없음 — `PlaceDefenderAs`→`CreateDefenderEntity` |
| ECS 컴포넌트 (Units) | 표준 세트 그대로. HazardCastState/AggroProvider/DeployedFacing/VolleyFireState N/A(능력 비활성) |
| 시뮬 시스템 | 변경 없음 — AttackSystem 직접 데미지 경로·DotApplySystem(tick) 기존 그대로 |
| 이벤트 큐 | 신규 채널 0 — UnitAttackVisualEvents(빔 소스)·EnemyCcEvents(DoT) 재사용 |
| View/Pool | 기존 SpineUnitPool + **BeamPresenter 신설**(unit 1, Presentation 계층·풀링) |
| 체력 표시 | 변경 없음 — UnitOverheadUiLayer |
| 씬 wiring | BeamPresenter 배선 필요 여부는 unit 1 에서 확정 (bridge drain 에서 구동하면 신규 SerializeField 1개 예상) — `unity-feature-wiring` 스킬 대상 |

## 후속 후보

- **스티키 타겟(락온)** [M] · 대상 사망/이탈 전까지 타겟 고정 — 타겟팅 규칙 변경이라 별도 결정.
- **빔 두께/색 = 위력 표현** [S] · 버프 받으면 빔이 굵어지는 등 — 뷰 전용.
- **다른 빔 유닛(Ice/Lightning)** [S] · ElementalBeams 팩 내 변형 — BeamPresenter 재사용 전제.
- **전용 아트 패스** [S] · portrait/파츠 (placeholder 교체, guid 유지).
