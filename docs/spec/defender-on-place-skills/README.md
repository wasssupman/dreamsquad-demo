# Defender On-place Skills Spec

**작성일**: 2026-04-21  
**연결 문서**: `docs/spec/defender-drag-drop-deployment/7_on_place_sequence.md`  
**목표**: Defender 배치 순간에 유닛별 고유 on-place 스킬을 1회 발동한다. 스킬은 Drop 성공 frame 이후 배치 sequence 안에서 실행되고, 일반 전투 활성화는 deploy VFX/animation 이후로 지연한다.

## 구현 문서 목록

| 작업 구분 | 문서 | 목적 |
|---|---|---|
| Phase 0 | `0_skill_contract.md` | 공통 계약과 effect enum 확장 |
| Phase 1 | `1_effect_implementation.md` | 신규 on-place 효과 구현 방식 |
| Phase 2 | `2_unit_assignment.md` | Defender 10종 임시 스킬 배정 |
| Phase 3 | `3_vfx_logging_validation.md` | VFX, log, 검증 기준 |

## 공통 원칙

- On-place skill 은 defender 배치 성공당 정확히 1회만 발동한다.
- `PendingDeployment` 중에는 일반 공격/피격/타겟팅이 발생하지 않는다.
- On-place skill 은 배치 sequence 안에서 1회 실행한다.
- `PendingDeployment` 제거와 일반 전투 활성화는 deploy presentation 이후에 실행한다.
- 스킬 구현은 우선 기존 `DefenderUnitData`의 `onPlaceRange`, `onPlaceMagnitude`, `onPlaceDuration` 필드를 재사용한다.
- 각 defender의 최종 스킬 정체성은 추후 밸런싱 대상이며, 이번 구현에서는 10종에 임시 배정한다.
