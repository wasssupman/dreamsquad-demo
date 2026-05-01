# 12. Handoff Summary

## Commit

- 본 spec 의 코드/문서 commit 16개 (e0238f5 ~ 6889d29). 핵심 단위:
  - `45608f2` Unit 0 data model
  - `3c10cb7` Unit 1 apply channels
  - `02bdc56` Unit 2 ModifierApplySystem
  - `9bdab13` Unit 3 stat tick + aggregate
  - `658dac6` Unit 4 stack tick + thresholds
  - `b2c99f8` Unit 5 AttackOutput + AttackSystem
  - `9d6484d` Unit 6 IncomingHeal + DamageApplicationSystem
  - `8976549` Unit 7 aggregate legacy adapter
  - `1a3695a` hotfix singleton entity destroy (Unit 1 결함)
  - `c62c934` Unit 8 producer 마이그레이션
  - `19bdedb` Unit 9 legacy 컴포넌트 제거
  - `3cb929e` Unit 10 healer authoring
  - `7261607`/`6130fdc`/`cb9c533` healer scene 등록 + Spine reuse
  - `6889d29` AttackOutput 로깅

## Implemented

- Producer-agnostic modifier framework: 두 분리 buffer (`StatModifierSlot` / `StackModifierSlot`) + 공통 `ModifierHeader` 임베딩 + `BuffStats` 캐시 (`IEnableableComponent` dirty mark)
- 4 system: `ModifierApplySystem` / `StatModifierTickSystem` / `StackModifierTickSystem` / `BuffStatsAggregateSystem`
- 3 채널: `StatModifierApplyEventsSingleton` / `StackModifierApplyEventsSingleton` (신규) + `EnemyCcEventsSingleton` (재사용 — Stack 임계 DOT/Stun 파생)
- StatKind 4 (`DamageMul`/`AttackSpeedMul`/`DmgTakenMul`/`RegenPerSec`), StackKind 5 (`None`/`Fire`/`Ice`/`Bleed`/`Poison`)
- `StackModifierSO` + `ThresholdRule[]` (Edge/Consume + multi-threshold 모두 발화 + 1프레임 지연)
- `AttackOutput[]` producer 어댑터 (Damage/Heal/ApplyStat/ApplyStack) — `DefenderUnitData.outputs[]` 만 도입
- `IncomingHeal` Buffer (즉시 펄스) + `RegenPerSec` StatModifier (지속) — 별도 경로
- Legacy 마이그레이션 완료: `DamageBoost`/`CooldownReduction`/`SynergyBuff` 정의 제거, producer 측 `BattleBridge.RecomputeSynergyFor` / OnPlace 효과 / 스킬 스킬 모두 channel enqueue 로 이전
- Healer defender SO + Material + DraftController/defenderPool 등록 + Archer Spine reuse
- AttackOutput 발화 로깅: `attack_outputs` JSON 필드 (source_unit/kind/magnitude/detail/source_tile/target_tile/time)

## Key Files

- `Assets/_Project/Scripts/Battle/Effects/Modifiers/`: framework 핵심 6 파일 (data model + 4 system + 2 channel)
- `Assets/_Project/Scripts/Data/AttackOutput.cs` + `StackModifierSO.cs`: producer 어댑터 + Stack 메타데이터
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`: outputs 분기 + BuffStats 곱
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs`: DmgTakenMul + Heal pulse + RegenPerSec 통합 처리
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`: 채널 lifecycle + RecomputeSynergyFor / OnPlace 마이그레이션 + Enqueue* 헬퍼 + DrainAttackOutputLogEvents
- `Assets/_Project/Data/Defenders/Defender_Healer.asset`

## Verified

- Compile: 모든 단위 commit 후 Unity Console error 0, warning 0
- BuffStats 합성식 (execute_code, Adapter 기간): DamageBoost x1.5 → damageMul=1.5, CR x0.5 → attackSpeedMul=2.0 (역수 변환), SY x1.2 → damageMul=1.2 — 기대값 일치
- Healer Heal 펄스 (PlayMode 로그): magnitude=15, target_tile 정확
- Synergy 마이그레이션 (PlayMode 로그): activations=15, peakCount=15 — 다수 Archer 인접 시나리오에서 정상 발화
- Persistent allocator leak 0 (모든 채널 dispose 확인)

## Notes

- **Adapter 기간 종료**: Aggregate dirty-only 모드 복귀. legacy 3 컴포넌트 모두 제거. 이후 buff/debuff 는 StatModifier channel 단일 경로.
- **호환 경로 보존**: outputs[] 미설정 defender (기존 Archer 등) 는 legacy `attack.damage` 경로 유지 — 회귀 0. AttackOutputLog 도 미발화. 후속 spec 에서 마이그레이션 시 자동 트래킹.
- **NativeQueue singleton 11 채널**: 기존 8 + 신규 3 (StatModifierApply / StackModifierApply / AttackOutputLog). 모두 BattleBridge lifecycle 패턴 답습.
- **Healer targetMask**: `Faction.Defender` 재사용 (`AllyDefender` enum 신설 안 함). 자가 힐 방지는 AttackSystem self-skip 분기로 자동 처리.
- **Stack 임계 파생 1 프레임 지연**: 게임 체감 무영향 (60fps 기준 ~16ms). 무한 재진입 회피 안전판.
- **Synergy duration=무한 정책**: producer 가 magnitude refresh 로 갱신, neighbors=0 시 magnitude=1.0 (곱셈 항등원) 으로 사실상 무효화.

## Post-close hotfix + tests

종료 후 3-reviewer 리뷰에서 발견된 결함 수정 + EditMode 테스트 보강:
- `31239f0` hotfix: ① StatModifierTickSystem 의 dirty-only query 결함 (유한 duration modifier 만료 안 됨), ② enemy entity 에 BuffStats / BuffStatsDirty 부착 추가, ③ ModifierApplySystem.MarkDirty 의 ECB AddComponent race
- `715c92a` Unit 11 보강: ModifierFrameworkTests.cs (3 PASSED + 2 [Ignore]) — merge key refresh, BuffStats 합성식, multi-frame 만료 회귀 방지

스킵된 2 테스트는 testability 결함 (private static dictionary, 인라인 dispatch) 때문이며 Follow-up 의 후보 항목에 포함.

## Follow-up

각 항목: **What** (무엇) / **Why** (왜 필요) / **Scope** (예상 규모: S=단일 unit, M=2~5 unit spec, L=5+ unit spec).

- **cc-effect-consolidation** [M]
  - What: `EnemyAttackMovePause` (Combat 측 ad-hoc 컴포넌트) 를 `CcEffect.Stun` 으로 흡수. `EnemyCcEventsSingleton` → `EntityCcEvents` rename 으로 defender 도 CcEffect 받을 수 있게.
  - Why: AttackSystem 의 `enemyPauseLookup` + `!isDefender` 분기가 맥락 경계 위반(`Combat→Movement` cross-context write). 또한 향후 defender self-stun / cast lock 같은 효과 도입 시 일관 채널 필요.
  - 의존: 없음. 본 spec 종료 후 즉시 진입 가능.

- **MoveSpeedMul + CcEffect.Slow 정리** [M]
  - What: `BuffStats` 에 `moveSpeedMul` 필드 추가, MovementSystem 이 read-only 소비. 기존 `CcEffect.Slow` 와 의미 중복 정리.
  - Why: 적 둔화 / defender 이동 둘 다 동일 메커니즘으로 표현 가능. 현재는 이동 둔화는 CcEffect, attack 둔화는 BuffStats 분리.
  - 의존: cc-effect-consolidation 과 같이 진행하는 게 자연스러움 (둘 다 CcEffect 정리).

- **기존 defender outputs[] 마이그레이션** [S]
  - What: Archer / Guardian / Cannon 등 기존 10 종 defender SO 의 `attack.damage` 를 `outputs = [{Damage, magnitude=damage}]` 로 author 변환. legacy 호환 경로 코드 제거.
  - Why: 현재 outputs 미설정 defender 는 AttackSystem 의 호환 분기를 타서 AttackOutputLog 에 안 잡힘. 모든 효과를 통합 트래킹하려면 일관화 필요. AttackSystem 의 호환 분기 코드도 제거 가능 → 코드 단순화.
  - 의존: 없음. SO 일괄 변환 + AttackSystem 분기 제거.

- **AttackUnitData.outputs[]** [S]
  - What: 적(Enemy) SO 도 `outputs[]` 모델 도입. defender 와 동일 구조.
  - Why: 적이 defender 에 디버프 거는 케이스 (예: 마법사 적의 공격력 감소 디버프) 도입 시 필요. 현재는 enemy 가 IncomingDamage append 만 가능.
  - 의존: 적 디버프 콘텐츠 디자인 결정이 선행되어야 의미 있음.

- **Aura defender** [M]
  - What: 지속 영역 효과 producer. `AuraOutput[]` + `AuraApplySystem` 신규 — 일정 반경 ally 에 매 프레임 (또는 N초마다) StatModifier 발화.
  - Why: 현재는 OnPlace 1회성 또는 attack hit 만 가능. "주변 ally 에 항상 +DamageMul" 같은 토템형 defender 표현 불가.
  - 의존: framework 변경 0 — producer layer 추가만. 본 spec 의 producer-agnostic 설계 검증 시점.

- **Projectile on-hit modifier** [S]
  - What: `ProjectileResolveSystem` 이 hit 시 ModifierApply 채널 enqueue. 화염 화살 (Fire stack), 빙결 화살 (Ice stack), 둔화 투사체 (MoveSpeedMul 디버프).
  - Why: 현재 projectile 은 데미지만. modifier 적용은 oneOff melee defender 만 가능. projectile 류 defender 가 다양한 효과 적용하려면 필요.
  - 의존: `MoveSpeedMul` 도입 시 적용 대상 확대. 기본 Fire/Ice 만이면 즉시 가능.

- **Modifier UI 시각화** [M]
  - What: defender HUD 에 활성 modifier 아이콘 표시. 적 머리 위에도 디버프 표시. `BuffStats` / Slot buffer 를 read-only view layer 가 구독.
  - Why: 디버그 + UX. 현재 player 는 어떤 buff 가 적용 중인지 시각 확인 불가.
  - 의존: UI/디자인 리소스 (아이콘 set). 시간 부담 큼.

- **Dispel/Cleanse 채널** [S]
  - What: ModifierBuffer 의 슬롯 제거 채널 (kind 또는 source 기반). CombineOp 별 면역 정책.
  - Why: "디버프 해제" 스킬 / "버프 무효" 적 도입 시 필요. 현재는 자연 만료 외 제거 경로 없음.
  - 의존: 콘텐츠 측 dispel 효과 디자인 선행.

- **Healer 전용 Spine asset** [S]
  - What: 현재 Archer Spine reuse 중 (placeholder). Healer 전용 rig + 애니메이션 (idle / heal-cast / death).
  - Why: 시각 식별성 + 게임 완성도. 기능 영향 없음.
  - 의존: Spine 아티스트 작업.

- **Spec 5~10 backfill** [S]
  - What: hybrid 진행 시 누락된 단위 spec 파일 (5\_attack\_output\_..., 6\_..., 7\_..., 8\_..., 9\_..., 10\_...) 작성. executor 가 dispatch prompt 로 가이드되어 진행됨, 단위 spec 파일 자체 부재.
  - Why: source-of-truth 정합성. 다음 에이전트가 단위 완료 여부 / 의도 / 완료 기준 추적 시 commit message + handoff 만으로 한계.
  - 의존: 없음. 사후 작성. 필수는 아님 (commit history 가 대체).

- **Testability 보강** [S]
  - What: `BattleBridge._stackThresholds` private static dictionary 에 internal test 주입 API (`SetStackThresholdsForTest`) 또는 `IStackThresholdRegistry` 인터페이스 도입. skipped Test 3 (Stack multi-threshold) 활성화.
  - Why: framework 코어 동작 (multi-threshold edge 발화 + Consume 모드) 의 회귀 방지 테스트 부재. dispatch 결함 발생 시 PlayMode 시각 검증 외 잡을 방법 없음.
  - 의존: 없음.

- **AttackSystem outputs dispatch helper 추출** [S]
  - What: `AttackSystem.OnUpdate` 의 outputs 분기 (Damage/Heal/ApplyStat/ApplyStack 4-way) 를 `static ProcessAttackOutputs(...)` 헬퍼로 추출. skipped Test 4 (AttackOutput 분기) 활성화.
  - Why: 인라인 dispatch 라 단위 테스트 불가. PlayMode 통합 테스트로만 검증 가능.
  - 의존: 없음.

- **추가 EditMode 회귀 테스트** [S]
  - What: Stack threshold edge 검출 (5→6→5 빠른 변화 시 재발화) / Consume 모드 (stack 차감 + lastTriggeredStack 갱신) / IncomingHeal drain Clear 보장 (2 프레임 연속 1 회만 적용) / RegenPerSec 누적 (BuffStats.regenPerSec * dt 매 프레임 가산).
  - Why: 현재 EditMode 커버리지 3 PASSED + 2 [Ignore] — Stack 사이드와 IncomingHeal/RegenPerSec 회귀 방지 미흡.
  - 의존: Testability 보강 (Test 3 활성화) 후 합쳐서 진행이 효율적.
