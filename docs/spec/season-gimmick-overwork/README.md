# Season Gimmick — 야근 (Overwork) Spec

**상태**: 완료 2026-07-15 — 야근 기믹 두 룰(피로도→번아웃, 레드불→라스트런) end-to-end 구현·Play 검증. 야근 시즌(`season_overwork`) 정식 default. 상세 인계는 [8_handoff_summary.md](8_handoff_summary.md).
**후속 (2026-07-15)**: [9_gimmick_split_and_burnout_vfx.md](9_gimmick_split_and_burnout_vfx.md) — 야근 기믹을 Burnout/RedBull 2개로 분할(매치당 랜덤 배정, gimmick-match-integration 계약 9 초과) + 레드불 빈도 5→3s + 번아웃 전용 VFX(먹구름+번개). 남은 육안: 실 유닛 번아웃 end-to-end 플레이테스트.

## 목표

시즌마다 한 판의 특수 룰을 제공하는 **시즌 기믹** 시스템의 첫 구현. 시즌 SO 에 기믹을 묶어 활성화하는 프레임을 만들고, 첫 기믹 **"야근"** 을 end-to-end 로 플레이 가능하게 한다.

야근 기믹의 두 룰:

1. **피로도 → 번아웃**: 배치된 유닛은 10초마다 피로도 +1. 5스택 도달 시 **번아웃** (최대체력·공격력·공격속도 -20%, N초 지속 후 해제 + 스택 소모, 이후 다시 누적 시작).
2. **레드불 → 라스트런**: 매 5초마다 이동/배치 가능 타일에 레드불 아이템 스폰. 유닛(배치 시) 또는 적(이동 통과 시)이 같은 타일에 있으면 소비되고 **라스트런** 발동 — 공격속도 +50% (5초), 종료 시 최대체력 -90% (판 끝까지). **적도 동일하게 라스트런을 받는다** (2026-07-15 사용자 결정).

## 검증 질문 (이 spec 이 답해야 할 것)

- 야근 시즌 SO 를 활성화하고 매치를 시작하면, 다른 코드 수정 없이 두 룰이 모두 동작하는가?
- 기믹이 없는 시즌(gimmick = null)에서는 기존 플레이가 완전히 무변화인가?
- 배치 50초 후 유닛이 번아웃에 빠지고, 지속시간 뒤 회복해 다시 누적을 시작하는가?
- 레드불이 유닛/적 양쪽에 소비되며, 5초 뒤 최대체력 컷이 실제로 들어가는가?

## 효과 분류 배경 (2026-07-15 사용자 정의)

효과는 큰 카테고리에서 **이상효과**와 **감정효과**로 나뉜다.

- **이상효과**: 시즌 기믹 등에 의해 그때그때 부여. **누적 매개체(스택)가 임계값에 도달하면 효과가 트리거**되는 구조 (예: 피로도 5 → 번아웃). 기존 `StackModifier` 임계값 파이프라인(`StackKind` + `ThresholdRule[]`)이 정확히 이 모양 — 이상효과는 이 프레임 위에서 풀어나간다.
- **감정효과**: 희·노·애·락 4상태. **각 상태의 설계 미정 — 본 spec 범위 밖** (후속 후보 참조). 여기서는 분류만 인지한다.

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_fatigue_stack_data.md` | `StackKind.Fatigue` append + 피로/번아웃 StackModifierSO (임계 룰 3연: Edge×2 + Consume) |
| 1 | `1_max_health_modifier.md` | `StatKind.MaxHealthMul` + `ModifierStats.maxHealthMul` + Units 쪽 유효 최대체력 소비/클램프 + EditMode 테스트 |
| 2 | `2_gimmick_frame.md` | `GimmickData`/`OverworkGimmickData` SO + `SeasonData.gimmick` 필드 + BattleBridge 활성화 seam (기믹 config ECS 주입) |
| 3 | `3_fatigue_accrual.md` | 배치 유닛 10초 주기 피로도 누적 시스템 (Effects) → 번아웃 end-to-end 에디터 검증 |
| 4 | `4_pickup_archetype.md` | 레드불 픽업 아키타입 — 엔티티 정의 + 주기 스폰 시스템 (이동/배치 셀 후보에서 결정론적 랜덤) |
| 5 | `5_pickup_consume_lastrun.md` | 픽업 소비 판정 (defender 배치 셀 / enemy 통과 셀) → 라스트런: AS 버프 + 지연 MaxHealth 컷 캐리어 |
| 6 | `6_presentation.md` | 레드불 뷰 presenter + 번아웃 StatusFx 등록 + 소비 순간 연출 (플레이스홀더 아트 허용) |
| 7 | `7_scene_wiring_play_verify.md` | 야근 시즌 SO 생성 + 씬 wiring + Play 통합 검증 (검증 질문 4개 전부) |
| 8 | `8_handoff_summary.md` | 인계 지도 |
| 9 | `9_gimmick_split_and_burnout_vfx.md` | (후속) 기믹 2분할 + 레드불 빈도↑ + 번아웃 전용 VFX(먹구름+번개) |

## Feature-Wide 계약

- **기믹은 시즌에 묶인다**: `SeasonData.gimmick` (nullable). null 이면 기믹 시스템 전체가 비활성 — 기존 플레이 무변화가 계약이다.
- **활성화 seam 은 BattleBridge 하나**: `BuildMapForBattle` 시점에 `SeasonRuntime.Active.gimmick` 을 읽어 ECS 쪽 config(싱글턴 component + 기존 static SO registry 방식)로 주입. 기믹 시스템들은 config 유무로 self-gate.
- **이상효과 = StackModifier 프레임 재사용**: 피로도는 `StackKind.Fatigue` (append-only enum). 번아웃 발동은 기존 `StackModifierTickSystem` 의 ThresholdRule 로 표현 — 같은 `atStack` 에 Edge 룰을 먼저, Consume 룰을 마지막에 배치하면 다중 스탯 발동 + 스택 소모가 데이터만으로 성립 (Consume 이 stackCount 를 줄이므로 반드시 마지막; unit 0 에서 계약으로 명문화).
- **번아웃 수명**: 지속시간 후 해제 + 스택 소모(Consume) 후 재누적 (2026-07-15 사용자 결정). 파생 StatModifier 의 duration 이 번아웃 지속시간.
- **MaxHealth 모디파이어의 맥락 경계**: `ModifierStats.maxHealthMul` 은 Effects 소유(쓰기). Units 가 읽기 전용으로 소비해 유효 최대체력 산출 + 현재 체력 클램프 — Health 쓰기는 Units 안에서만 일어난다.
- **픽업은 신규 아키타입**: 해저드(지속 영역+CC)와 의미가 달라 재사용하지 않고, 해저드의 스폰/뷰 패턴을 동형으로 따르는 one-shot 소비형 아키타입을 Effects 에 신설. 신규 NativeQueue 채널은 최소화 — 뷰는 `BlockingHazardPresenter` 동형의 엔티티 추적을 우선 검토하고, 필요 시에만 소비 이벤트 큐 1개 추가 (unit 4 에서 확정).
- **모든 수치는 SO**: 누적 주기(10s)/발동 스택(5)/번아웃 -20%/지속시간, 스폰 주기(5s)/AS +50%/라스트런 5s/MaxHP -90% — 전부 `OverworkGimmickData` + StackModifierSO 필드. 하드코딩 금지.
- **시뮬 랜덤은 결정론적**: 레드불 스폰 셀 선택은 seed 주입된 `Unity.Mathematics.Random` 사용 (`Date/UnityEngine.Random` 금지).
- **감정효과(희노애락)는 범위 밖**: 분류 인지만. 설계는 후속 spec.

## 파이프라인 커버리지 — 레드불 픽업 (신규 아키타입, 해저드 표 기준 대조)

| 정거장 | 계획 |
|---|---|
| 데이터 SO | `OverworkGimmickData` (스폰 주기·효과 수치·뷰 프리팹/스프라이트) |
| 스폰 진입점 | `PickupSpawnSystem` (Effects) — 기믹 config self-gate, 주기 스폰. 해저드의 staged-request 와 달리 ECS 내부 ECB 생성 (Mono 개입 불필요 시) |
| ECS 컴포넌트 (Effects) | `Pickup { cell, kind, remainingLife }` + `PickupSpawnState`(후보 셀·rng·cadence 싱글턴, BattleBridge 소유). **수명=만료 확정** |
| 시뮬 시스템 | `PickupSpawnSystem`(스폰+만료) · `PickupConsumeSystem`(소비) · `LastRunSystem`(지연 crash) (Effects) |
| 이벤트 큐 | **N/A** — 엔티티 추적 뷰(poll-reconcile)로 성립, 신규 NativeQueue 채널 0. 라스트런은 기존 `StatModifierApplyEvents` 재사용 |
| View | `PickupPresenter`(절차적 플레이스홀더) + BattleBridge `ReconcilePickupViews` poll-reconcile. 소비 원샷 VFX·정식 아트는 후속 |
| 상태 연출 | **위임** — 번아웃/라스트런은 임시 버프/디버프 모디파이어 → unit-buff-debuff-aura 의 Buffed/Debuffed 오라가 자동 분류 (별도 `Burnout` StatusFx 미제작 = 중복 회피) |

## 비목표

- 감정효과(희·노·애·락) 설계/구현.
- 야근 외 추가 기믹, 기믹 랜덤 로테이션/매칭 메타 (서버 훅 없음 — `SeasonRegistry.activeSeason` 수동 교체로 활성화).
- 기믹 룰의 범용 조합 프레임워크 (룰 모듈 선언 시스템). 두 번째 기믹이 생길 때 반복을 보고 추출한다 (제약 8).
- 레드불 정식 아트 (플레이스홀더 스프라이트로 진행, 아트는 후속).
- 매치 시작 시 기믹 안내 UI (배지/툴팁).

## 후속 후보

- 감정효과 (희노애락) 상태별 구현체 설계 — 별도 spec (분류만 인지, 범위 밖)
- 두 번째 시즌 기믹 + 기믹 룰 모듈 일반화 (반복 추출 시점)
- **피로도/픽업 placement-phase 게이팅** (running-only) 튜닝 — 현재 배치 페이즈에도 누적/스폰

(완료 이관: PickupConsume/LastRun Burst화 + 검증 로그 제거 `19c690ee`, `ModifierOrigin.Gimmick` 추가 `19c690ee`.)
- 레드불 정식 아트 + 소비/스폰 VFX + 뷰 지면 grounding (원근 부유 완화)
- 피격 시 피로도 +1 누적 소스 (야근 변형 룰 — 누적 소스 2종화)
- 매치 시작 UI 기믹 배지/설명 노출 (seasonal-map-backdrop 후속 "시즌 배지" 와 합류 가능)
