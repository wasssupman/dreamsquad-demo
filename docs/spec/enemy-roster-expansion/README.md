# Spec — Enemy Roster Expansion (data-only)

> 상태: 완료 2026-06-18 (Vanguard/Sniper/Debuffer 3종 추가)
> enemy-behavior-components 거동 조합만으로 신규 적 3종 추가 (코드 0, SO 데이터만).

## 목표

기존 거동 컴포넌트(attackMethod/targetMode/aimMode/targetPriorityClass/attackTargetCount/outputs/projectile)를 조합해, 자석 디펜스 루프에 긴장을 더하는 신규 적 3종을 **SO 에셋만으로** 추가한다.

## 검증 질문

> "코드 변경 없이 SO 3개만으로 Vanguard(가디언 우선 처치) · Sniper(레인저 우선 저격) · Debuffer(디펜더 약화)가 의도대로 동작하는가?"

## 신규 적 3종 (placeholder 수치 — 밸런싱 위임)

| 적 | class | attackMethod | targetMode | aimMode | 우선타겟 | tc | HP | 속도 | dmg | range | cd | outputs |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **Vanguard** | Bruiser | Melee | FocusUntilDead | StopToAttack | **Guardian** | 1 | 120 | 2.2 | 35 | 2 | 0.8 | Damage 35 |
| **Sniper** | Shooter | Projectile | FocusUntilDead | StopToAttack | **Ranger** | 1 | 30 | 1.6 | 50 | 8 | 3.0 | Damage 50 (RitualBolt, pause 0.5) |
| **Debuffer** | Shooter | Projectile | Nearest | MoveAndShoot | None | 1 | 40 | 2.0 | 3 | 4 | 1.5 | Damage 3 + ApplyStat DamageMul ×0.6 / 3s (Needle) |

- **Vanguard**: 가디언(자석 닻)을 우선·집중 처치 → 어그로 전략 카운터.
- **Sniper**: 후방 Ranger 장거리 저격 (Rootcaster 강화판).
- **Debuffer**: 맞은 디펜더의 공격력을 일시 감소(ModifierStats DamageMul, faction 무관 적용 확인됨).

## feature-wide 계약

1. **코드 0** — 기존 시스템(AttackSystem 거동, modifier 파이프라인, projectile)만 재사용.
2. 어그로 시 sticky override 가 우선(가디언) — Vanguard 도 어그로되면 그 가디언 고정(목표 일치).
3. 에셋은 `Assets/_Project/Data/Enemies/` 에 `Enemy_{Name}.asset` + 머티리얼은 기존 적 머티리얼 재사용.
4. 수치는 placeholder — 밸런싱 spec 위임.

## 작업 단위

| 파일 | 작업 | 문서 |
|---|---|---|
| 0 | 3종 SO 생성 + 검증 | `0_create-enemies.md` |

## 비목표

- 새 거동 메커니즘(aggro-immune/비행/분열 등) — 별도 spec.
- WavePlan 에 신규 적 편성 — 작성자가 인스펙터에서 자유 편성(별도).
- 전용 비주얼/Spine — 기존 머티리얼 재사용(후속 아트).
