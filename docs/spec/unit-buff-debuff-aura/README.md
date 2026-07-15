# Spec — Unit Buff/Debuff Aura (스탯 상태 온-바디 오라)

> 상태: **초안 (승인 대기)** — 2026-07-15
> 출처: unit-status-fx 후속("실제 상태 registry 등록 + ECS 소스 훅", "on-body 오프셋0 follow 모드").
> 축 구분: 본 spec 은 **"느끼게 할 상태 연출"**(온-바디 오라). 머리 위 정보 배지(`unit-modifier-indicators`)와 다른 축.

## 목표

타일·시너지·드림캐쳐·Slow 등으로 유닛의 **순 스탯이 기본값에서 버프/디버프**된 동안, 그 상태를
직관적·임팩트 있게(초사이언식) 보여주는 온-바디 오라 VFX 를 **자동 부착/해제**한다. 버프 오라(금빛
상승+발광)와 디버프 오라(검보라 하강+탁틴트) 2종을 분리하고, 한 유닛에 둘 다면 겹쳐 표시한다.
방어유닛(버프)·적(디버프 슬로우 등) 양 진영 공통.

## 검증 질문

> "스탯이 순증한 방어유닛에 버프 오라가, 순감한 적에 디버프 오라가 붙고, 효과가 사라지면 오라가
> 해제되는가? 한 유닛에 버프·디버프가 동시면 두 오라가 겹쳐 보이는가? 오라가 임팩트 있게 느껴지는가?"

## feature-wide 계약

1. **상태원 = `ModifierStats`.** 순 버프/디버프는 Effects 맥락의 `ModifierStats`(전 유닛 상주:
   방어유닛 `BattleBridge:3395`·적 `:4205`)를 base identity 와 비교해 판정. 시너지(src 1)·드림캐쳐(src 100+)·
   on-place/스킬(src 0)·Slow(`ZoneApplySystem:44` 에서 `CcKind.Slow`→`MoveSpeedMul` 변환)가 전부
   `StatModifierSlot`→`ModifierStats` 로 수렴하므로 **소스별 분기 0**. writer 는 `ModifierStatsAggregateSystem`
   단독. BattleBridge 는 **읽기만** 한다(맥락 계약 준수 — CcEffect/Sleep 과 동일 경로).
2. **판정 = 순수 함수 + 스탯별 방향표.** `ModifierAuraClassifier.Classify(in ModifierStats) → (bool buffed, bool debuffed)`.
   양방향 독립(한 유닛이 둘 다 true 가능), epsilon `1e-4` 비교. **스탯마다 buff 방향이 다르다**(H1 — 나이브 구현 시 오라 반전):
   - `damageMul`·`attackSpeedMul`·`moveSpeedMul` : `>1+ε` = buff, `<1−ε` = debuff
   - `dmgTakenMul` : **역방향** — `<1−ε` = buff(피해 감소), `>1+ε` = debuff(피해 증가)
   - `regenPerSec` : base 0, 집계 시 **비음수 클램프**(`ModifierStatsAggregateSystem:105`) → `>ε` = buff **전용**, 디버프 판정 제외(M2)
   - `damageVsCcMul` : **판정 제외**(M1 결정) — "CC 걸린 적 대상"에만 작동하는 조건부 배율이라 상시 오라는 상태를 오도. 향후 필요 시 비교 1줄 추가로 편입 가능
   아키텍처-blind plain 입력→plain 출력(CLAUDE.md 제약 10). EditMode 테스트: `dmgTakenMul=0.87`(buff)·`1.4`(debuff)·버프+디버프 동시·전 스탯 base(둘 다 false) 케이스 필수.
3. **kind 2종 append.** `StatusFxKind { …, Buffed, Debuffed }` — append-only(직렬화 안전).
   **범위 경계(M3)**: Buffed/Debuffed 는 **스탯 모디파이어 상태 한정**. CcEffect 기반 상태(Stun/DoT/Impulse
   등 행동잠금·직접데미지)는 여기 포함 안 되고, Sleep 처럼 각각 별도 `StatusFxKind` + reconcile 훅으로 추가한다.
4. **상태 구동 reconcile 재사용.** `BattleBridge.ReconcileStatusFx` 에 `ModifierStats` 쿼리(양 진영 공통)
   추가 → classify → `Ensure(e, Buffed)` / `Ensure(e, Debuffed)`. 효과 소멸·사망 시 기존 `EndFrame` 이
   자동 회수. **신규 ECS 컴포넌트/큐 0.**
5. **멀티 동시.** 한 유닛에 buffed+debuffed 동시 가능 → 두 오라 겹침(`(entity,kind)` 키).
6. **on-body 오라.** registry `Entry` 의 offset≈0·scale·billboard 로 유닛을 감싸는 배치. `StatusFxView`
   추종 관용구 그대로(파티클 애니는 프리팹 자체). 오프셋/스케일/틴트는 전부 에셋에서(하드코딩 금지).
7. **신규 저작 VFX 2종.** `BuffAura`(금빛 상승+발광 림)·`DebuffAura`(검보라 하강+탁틴트) `_SKELETON`,
   모바일-세이프(unity-vfx-authoring). registry 프리팹 슬롯에 배선.
8. **성능 특성 수용.** reconcile 이 `ModifierStats` 보유(=전 유닛) 스캔 — StatusFx 소스 중 **가장 넓은
   population**(Aggroed 는 어그로 적만, ModifierStats 는 전 유닛). per-entity 는 6-float 비교로 경량,
   O(units)·TD 규모(수십~수백) 수용. 프로파일 등재 시 enableable dirty-tag 후속(unit-status-fx 5 M2 선례).

## 작업 단위

| 파일 | 작업 | 문서 |
|---|---|---|
| 0 | `StatusFxKind` 2종 + 순수 `ModifierAuraClassifier` + EditMode | `0_kind-and-classifier.md` |
| 1 | `BattleBridge.ReconcileStatusFx` ModifierStats 소스 훅(양 진영) | `1_reconcile-source.md` |
| 2 | Buff/Debuff 오라 `_SKELETON` 저작(unity-vfx-authoring) | `2_aura-vfx-authoring.md` |
| 3 | registry 에셋 배선 + Play 시각 검증 + handoff | `3_registry-wiring-and-play.md` |

> 빌드 순서: 0~1 은 registry 에 임시 폴백 글리프를 물려 **부착/해제 메커닉**을 먼저 Play 검증하고,
> 2~3 에서 실제 저작 오라로 교체한다(절차 폴백을 최종 산출물로 삼지 않음 — 사용자 결정: 신규 Shuriken 저작).

## 파이프라인 커버리지 (상태 연출 = 온-바디 View, unit-status-fx 아키타입 재사용)

기존 Status FX 파이프라인에 **새 상태원(ModifierStats) + 2 kind + 저작 프리팹**만 추가. 신규 아키타입 아님.

| 정거장 | Buff/Debuff Aura |
|---|---|
| 데이터(SO) | `StatusFxRegistry`(Buffed/Debuffed → 저작 프리팹/offset≈0/scale/billboard/tint) |
| ECS 상태 | `ModifierStats`(Effects, 전 유닛 상주) — **FX용 신규 컴포넌트 없음**, 읽기 전용 |
| 판정 | 순수 `ModifierAuraClassifier.Classify` (EditMode 테스트) |
| 생성 트리거 | `BattleBridge.ReconcileStatusFx` 매 프레임(SyncMonoUnitViews 뒤) |
| 뷰/풀 | `StatusFxSpawner`(kind별 풀) / `StatusFxView`(저작 프리팹 추종) |
| teardown | `StatusFxSpawner.Clear()`(기존) |

## 저작/검증 유의 (unit 2~3, 리뷰 지적)

- **on-body 렌더 정렬(W3)**: 폴백 sprite 는 `sortingOrder=15000`(전부 위)이지만 offset≈0 온-바디 오라는
  저작 프리팹이 **자체 정렬**을 정의해 유닛을 완전히 가리지 않게 해야 한다(감싸되 실루엣 보존).
- **앵커 pivot(Q2)**: `ResolveUnitViewTransform` 앵커가 Spine 방어유닛/quad 적에서 발치인지 중심인지 확인 후
  registry `localOffset` 로 kind별 보정. 프리팹 pivot 규약을 저작 시 확정.
- **unit 1 Play repro(W4)**: 버프 = 방어유닛 인접 배치(시너지 이웃 2+), 디버프 = 적을 슬로우 타일/존에 진입.

## 후속 후보

- **적 버프 오라 진영 구분(Q1)** — 보스 `AllyMoveSpeedAura` 로 버프된 적에 금빛 버프 오라가 뜨면 플레이어
  혼란 가능. 현 계약은 "양 진영 공통"(버프된 적은 실제로 버프됨 → 표기 일관). 필요 시 진영별 틴트 변종.
- **강도별 단계 오라** — `|net-1|` 크기에 따라 scale/emission 세기. 현재는 on/off 이진.
- **스탯별 색 세분** — 공격/공속/이동/방어 버프에 다른 서브 tint. 현재는 buff/debuff 방향 2색.
- **디버프 종류별 전용 오라** — Slow=빙결·Poison=독 등 CcKind/스탯 조합별 프리팹. 콘텐츠 디자인 선행.
- **정보 배지 축 공존** — `unit-modifier-indicators` 스트립과 y-오프셋/레이아웃 충돌 회피.
- **enableable dirty-tag 최적화** — 프로파일 등재 시 전 유닛 스캔을 narrow 쿼리로.
