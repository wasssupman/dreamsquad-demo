# Kickoff Handoff — Path Zone Hazards

**Status**: 브레인스토밍 완료, 구현 미착수.
**Spec 폴더**: `docs/spec/path-zone-hazards/` (README + 0~7 작업 단위).
**작성**: 2026-04-29.
**다음 작업자**: oh-my-claudecode:executor (Sonnet) 또는 사용자 manual.

## 본 spec 의 자리

이동 경로 위 *통과 가능 + 효과 발동* 형 hazard 시스템. MVP 3종 — 독지대 (DoT), 얼음지대 (Slow), 화염지대 (강한 DoT). Visual ⊥ Effects 분리 + spawn 진입점 단일 API 캡슐화. 차단형 hazard (rocks + 적 공격) 는 별도 후속 spec (`destructible-blocking-hazards`).

## 브레인스토밍 결정 요약

| 결정 | 채택 | 이유 |
|---|---|---|
| 지대 vs 차단형 | 본 spec = 지대만, 차단형 = 별도 spec | 적 공격 시스템이 차단형에만 필요해서 검증 질문 분리 |
| MVP zone 종류 | Poison + Ice + Fire (3종) | 효과 모델 3개 (순수 DoT, 순수 CC, 강한 DoT) 커버 |
| Producer 캡슐화 | `EffectSpawner.SpawnHazard(em, HazardSO, originCell)` 단일 진입점 | 미래 producer (스킬/배치/장비) plug-in 가능. 본 spec 은 디버그 1개 producer 만 |
| 데이터 구조 | a-3 (단일 HazardSO) + b-1 (entity 당 multi-cell) | SO 1개 = hazard 1정체성, entity 1개로 lifetime 관리 단순 |
| Visual ⊥ Effects | Visual = `HazardPresenter` MonoBehaviour, Effects = ECS CC pipeline | 화염중첩 같은 미래 stacking 도 effect 만 추가하면 됨 |
| DoT 처리 | `CcKind.DoT` 신규 + `DotApplySystem` (CC buffer → IncomingDamage) | 기존 HealthSystem 그대로 재사용 |
| 효과 적용 | 매 프레임 re-enqueue + CC merge refresh | enter/exit 추적 불필요, fast-moving 적도 안전 |
| Stacking | 본 spec 범위 밖 | `CcEffect` *데이터 구조* 는 backward-compat (stackCount 추가 가능). 단 *동작 정책* (merge / decay) 도 함께 변경해야 stacking 동작 — 본 spec 은 데이터 layout 만 보존하고 동작은 미래 변경에 위임 |
| Cell trim | Zone 은 차단 안 함 | `MovementCellTrim.IsWallCell` 미수정 |

## 구현 순서 (1 파일 = 1 commit)

```
0 ──▶ 1 ──▶ 2 ──▶ 3 ──▶ 4 ──▶ 7★
                                ▲
                       5 ───────┘
                       6 ───────┘
```

★ = PlayMode 게이트 (feature 검증).

## 절대 보존 (되돌리지 말 것)

- 외부 producer 진입점은 **`EffectSpawner.SpawnHazard` 1개만**. 미래 producer 도 모두 이 API. 별도 spawn 경로 만들지 말 것.
- Visual layer ⊥ Effects layer 의 *상호 무지* — Presenter 가 ECS 모르고, ECS 가 Presenter 모름. BattleBridge 만 둘을 연결 (`SpawnHazardWithVisual` wrapper).
- `CcKind.DoT` 추가는 기존 CC 채널 *확장*. 기존 Slow/Impulse 와 같은 buffer/큐 사용.
- DoT 의 데미지 출구는 기존 `IncomingDamage` 채널 (HealthSystem 처리). 별도 데미지 경로 만들지 말 것.
- `HazardSingleton.cellToEffects` 는 **Effects 맥락 소유**, 다른 시스템 read-only.
- Zone 은 차단 안 함 — `MovementCellTrim.IsWallCell` 분기 추가 금지.
- Burst 호환 유지. SpawnHazard 자체는 main-thread (EntityManager 직접). HazardShapeSampler 도 main-thread (List 사용).
- NativeMultiHashMap dispose 를 BattleBridge OnDestroy 에 추가 필수 (cc-pipeline-and-obstacle 의 C1 fix 패턴 미러).

## 작업 시 주의

- **Burst 호환 검증**: `HazardEffect`/`Hazard`/두 buffer 모두 blittable struct, enum byte. NativeMultiHashMap key=int2, value=HazardEffect 모두 blittable.
- **시스템 순서 critical**:
  - `HazardLifetimeSystem` UpdateBefore `CcApplySystem` (이 프레임 갱신된 cellToEffects 를 ZoneApply 가 read)
  - `ZoneApplySystem` UpdateAfter `HazardLifetimeSystem`, UpdateBefore `CcApplySystem`
  - `DotApplySystem` UpdateAfter `CcApplySystem`, UpdateBefore `CcDecaySystem`
- **placeholder 시각**으로 시작 — 정식 VFX 는 후속 (unity-vfx-authoring 스킬).
- **Hazard visual 패턴**: `TornadoFieldPresenter` / `MeteorWarningPresenter` 같은 frame-sync presenter 는 코드베이스에 *존재하지 않음*. 실제 패턴은 `VfxSpawner.SpawnTornado` / `BattleBridge.SpawnMeteorWarningVisual` 의 *fire-and-self-manage*. spec 6 는 이 결을 따라 `HazardVisualLifetime` MonoBehaviour 가 자가 destroy timer.
- **NativeMultiHashMap lifecycle**: BattleBridge 의 StartBattle / Cleanup / OnDestroy 세 곳 모두 처리 (C1 fix 패턴).

## 사용자 확인 protocol

- Unit 0~6: compile + 단위테스트 통과 → 사용자에게 "다음 unit 진행 OK?" 한 줄 확인.
- Unit 7 ★: PlayMode 시나리오 1~6 (Poison DoT / Ice Slow / Fire 강한 DoT / Composition / Visual ⊥ Effect / API encapsulation) 사용자 시각 확인 → spec 종료 → `8_handoff_summary.md` 작성.

## 작업 시작점

`docs/spec/path-zone-hazards/0_hazard_data_model.md` 부터. README 의 공통 원칙 8개 + 본 handoff 의 절대 보존 리스트를 상시 컨텍스트로 유지.
