# Path Zone Hazards Spec

**작성일**: 2026-04-29
**연결 문서**: 본 spec 의 CC 채널은 `docs/spec/cc-pipeline-and-obstacle/` 의 인프라를 *확장*. 차단형 hazard 는 별도 spec `destructible-blocking-hazards` (후속).
**목표**: 이동 경로 위 *통과 가능 + 효과 발동* 형 hazard 시스템 도입. MVP 3종 — 독지대 (DoT), 얼음지대 (Slow), 화염지대 (강한 DoT). Visual ⊥ Effects 분리 + spawn 진입점 단일 API 캡슐화로 미래 producer (스킬/배치/장비) 가 동일 채널로 plug-in.

## 상태

완료 (2026-04-29).

## 구현 문서 목록

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_hazard_data_model.md` | `HazardSO` + `HazardEffect` struct + `HazardShape` enum + `Hazard`/`HazardEffectsBuffer`/`HazardCellsBuffer` ECS 컴포넌트 |
| 1 | `1_dot_cckind_and_dotapplysystem.md` | `CcKind.DoT` 추가 + `DotApplySystem` (CC buffer → IncomingDamage 매 프레임 누적) |
| 2 | `2_hazard_lifetime_and_singleton.md` | `HazardSingleton.cellToEffects` (NativeMultiHashMap) + `HazardLifetimeSystem` (수명 tick + map 재구축) |
| 3 | `3_zone_apply_system.md` | `ZoneApplySystem` 매 프레임 적 cell → effects 조회 → CC enqueue |
| 4 | `4_spawn_hazard_api.md` | `EffectSpawner.SpawnHazard` 단일 진입점 + `HazardShapeSampler` 셀 계산 |
| 5 | `5_sample_hazard_sos.md` | 3 sample SO asset (Poison/Ice/Fire 3×3) + placeholder visual prefab |
| 6 | `6_hazard_presenter.md` | `HazardPresenter` MonoBehaviour + BattleBridge spawn/destroy sync |
| 7 | `7_debug_spawn_entry.md` | `BattleBridge.DebugSpawnHazardAt` + Editor 메뉴 (**feature 게이트**) |
| 8 | `8_handoff_summary.md` | 구현 결과 + 검증 로그 + 후속 주의점 |

## 공통 원칙 (feature-wide 계약)

- Hazard = `Shape + Lifetime + Visual + Effects[]` 4-layer composition. 새 hazard 타입 = SO asset 추가만 (enum 확장 없이).
- **Visual ⊥ Effects**: Visual = `HazardPresenter` MonoBehaviour (Presentation 계층). Effects = ECS CC pipeline 재사용. 서로 의존 없음. BattleBridge 만 둘을 연결.
- `HazardSO` = visual prefab ref + `HazardEffect[]` inline. Hazard 의 모든 정체성을 한 asset 에 응집.
- `EffectSpawner.SpawnHazard(em, HazardSO so, int2 originCell)` 가 **모든** producer 의 단일 진입점. 본 spec 은 디버그 1개 producer 만 만들고, 미래 producer (스킬/배치/장비) 는 같은 API 호출하는 별도 spec.
- 효과 적용 = 매 프레임 re-enqueue + CC merge refresh. enter/exit 상태 추적 없음. 적이 zone 빠져나가면 짧은 잔존시간 후 자연 감쇠.
- DoT 는 `CcKind.DoT` 로 CC 채널 합류 → `DotApplySystem` 이 매 프레임 `IncomingDamage` 누적 → 기존 HealthSystem 재사용.
- Zone 은 **차단 안 함** — `MovementCellTrim.IsWallCell` 미수정. 차단형은 후속 spec.
- Stacking (화염중첩 등) 은 본 spec 범위 밖. `CcEffect` *데이터 구조* 는 backward-compatible 하게 `stackCount` 추가 가능 형태로 남김. **단 *동작 정책* (현재 merge = 같은 kind 1 entry 강제) 는 stacking 도입 시 동시 변경 필요** — merge 정책 갱신 + buffer 다중 entry 허용 + CcDecaySystem tick 정책. 즉 stacking 은 데이터 변경만으로 끝나지 않음.

## 검증 질문 (= 종료 조건)

1. 3 zone 효과 (독 DoT / 얼음 Slow / 화염 강한 DoT) 가 의도대로? (game feel) → Unit 7 PlayMode 사용자 확인.
2. `SpawnHazard` API 가 producer 종류와 무관하게 일관된 진입점인가? (encapsulation) → **weak proof**: 본 spec 의 producer 는 디버그 메뉴 1개뿐. 코드 리뷰 (시그니처가 producer 컨텍스트 의존 0) + spec 4 의 미래 producer 의사코드 예시로 *간접* 검증. 진짜 검증은 후속 spec (디펜더 on-place hazard / 스킬 카드 / 장비 효과) 통합 시 재확인 필요.

## 후속 후보 (현 spec 범위 밖)

- 차단형 hazard 별도 spec (`destructible-blocking-hazards`) — Rock + HP + 적 공격 시스템
- Hazard stacking (화염중첩/독중첩) — `CcEffect.stackCount` 또는 buffer 다중 entry
- 실 producer 통합: 디펜더 on-place hazard, 스킬 카드, 장비 효과 — 각각 별도 spec
- 추가 zone 종류: 가시 (instant damage on entry), 늪 (강한 Slow), 자석 (Pull impulse), 번개 (chain damage)
- Zone 진입/이탈 SFX/VFX 트리거 (Presenter 확장)
- `cellToEffects` incremental 갱신 (hazard 수 ↑ 시 부하 측정 후)
- HazardShape 의 4-neighbor circle / 임의 cell list 지원
- 정식 VFX prefab (unity-vfx-authoring 스킬)
- Burn 디버프 같은 화염-특화 stack CC 도입 (composition 검증)
