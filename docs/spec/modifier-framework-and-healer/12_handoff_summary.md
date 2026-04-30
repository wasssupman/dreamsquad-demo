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

- **cc-effect-consolidation**: `EnemyAttackMovePause` → `CcEffect.Stun` 흡수, `EnemyCcEventsSingleton` rename, defender 도 CcEffect 받기
- **MoveSpeedMul + CcEffect.Slow 정리**: defender 이동 도입 또는 cc-effect-consolidation 과 함께
- **기존 defender outputs[] 마이그레이션**: Archer/Guardian 등 모든 defender 의 attack.damage 를 outputs[Damage] 로 일관화 → AttackOutputLog 가 모든 효과 트래킹
- **AttackUnitData.outputs[]**: 적 디버프 introduce 시 추가
- **Aura defender** (지속 영역 producer): framework 변경 0, AuraOutput[] + AuraApplySystem 신규
- **Projectile on-hit modifier**: 화염 화살 / 빙결 화살
- **Modifier UI 시각화**: HUD 아이콘 + 적 디버프 표시
- **Dispel/Cleanse 채널**: 슬롯 제거 + 면역 정책
- **Healer 전용 Spine asset**: 현재 Archer reuse → 자체 rig 도입
- **Spec 5~10 backfill**: hybrid 진행 시 누락된 단위 spec 파일 작성 (필요 시 — executor 가 dispatch prompt 로 가이드되어 진행, source-of-truth 는 commit message + handoff)
- **Testability 보강** — `BattleBridge._stackThresholds` 의 internal test 주입 API 또는 `IStackThresholdRegistry` 인터페이스 도입 (skipped Test 3 활성화)
- **AttackSystem outputs dispatch helper 추출** — `static ProcessAttackOutputs(...)` 등 단위 테스트 가능하게 (skipped Test 4 활성화)
- **추가 EditMode 회귀 테스트** — Stack threshold edge / Consume 모드 / IncomingHeal Clear 보장 / RegenPerSec 누적
