# Modifier Legacy Migration Spec

**작성일**: 2026-05-01
**상태**: completed
**출처**: `docs/spec/modifier-framework-and-healer/` follow-up
**목표**: modifier framework 도입 이후 남은 legacy 호환 경로를 channel/outputs[]/context-boundary 모델로 단계적으로 이전한다. 본 spec 은 신규 콘텐츠 추가가 아니라 데이터/경계 정합성 회복이 목적이다.

## 구현 문서 목록

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_defender_outputs_asset_migration.md` | defender SO 의 legacy `attack.damage` fallback 제거를 위한 outputs[] 일괄 변환 |
| 1 | `1_attackunit_outputs_unification.md` | 적 `AttackUnitData` 도 outputs[] 모델로 통일 |
| 2 | `2_movespeedmul_slow_migration.md` | `CcEffect.Slow` 를 `StatKind.MoveSpeedMul` / `ModifierStats.moveSpeedMul` 로 이전 |
| 3 | `3_movement_pause_boundary.md` | `EnemyAttackMovePause` write ownership 을 Movement 맥락으로 이동 |
| 4 | `4_tests_and_handoff.md` | 통합 회귀 검증 + handoff |
| 5 | `5_handoff_summary.md` | 완료 결과와 남은 리스크 인계 |

## 공통 원칙

- **버전 기준**: Unity `6000.4.3f1`, Entities `6.4.0`.
- **신규 framework 금지**: 이미 있는 `AttackOutput[]`, `ModifierStats`, `StatModifierApplyEvents`, `StackModifierApplyEvents`, `NativeQueue` 패턴을 재사용한다.
- **호환 경로 제거는 단계별**: 각 단계는 compile-safe 해야 하며, 다음 단계가 없으면 컴파일되지 않는 중간 커밋을 만들지 않는다.
- **Presentation 무관**: Spine/Quad/VFX 표현 변경은 본 spec 범위 밖. 로그/Play smoke 는 검증에만 사용한다.
- **맥락 경계 우선**: Combat 은 Movement 소유 컴포넌트를 직접 write 하지 않는다. 필요한 경우 queue/buffer 요청으로 전달한다.
- **asset migration 은 코드 변경과 같은 작업 단위에 묶는다**: serialized SO 를 바꾸는 단계는 Play smoke 로 회귀를 확인한다.

## 범위

포함:
- defender `attack.damage` fallback 제거
- enemy attack schema 를 outputs[] 로 통일
- Slow 를 stat modifier 로 이전
- enemy attack 이동 정지 컴포넌트의 Movement ownership 회복

제외:
- Aura defender, Projectile on-hit modifier, Dispel/Cleanse, Modifier UI
- Healer 전용 Spine asset
- 새 enemy/defender 콘텐츠 밸런싱

## 검증 질문

1. outputs[] 로 이전한 뒤 기존 defender/enemy 데미지 동작이 유지되는가?
2. Slow 가 `ModifierStats.moveSpeedMul` 로 이전되어도 Movement 결과가 기존과 동등한가?
3. Combat→Movement pause 요청이 queue 경유로 바뀌어도 장거리 적의 attack pause 동작이 유지되는가?
4. legacy fallback 제거 후 신규 콘텐츠는 outputs[] / modifier channel 만으로 확장 가능한가?

## 후속 후보

- `AttackSystem` outputs 분기 helper 추출 및 skipped test 활성화.
- Stack threshold registry test hook 추가 및 skipped test 활성화.
- Aura defender producer.
