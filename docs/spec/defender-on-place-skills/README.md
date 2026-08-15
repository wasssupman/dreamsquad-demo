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
| 4 | `4_forward_burst_direction.md` | 전방 관통 일격 조준 방향 + 통로 폭 (버그 수정) |

## 공통 원칙

- On-place skill 은 defender 배치 성공당 정확히 1회만 발동한다.
- `PendingDeployment` 중에는 일반 공격/피격/타겟팅이 발생하지 않는다.
- On-place skill 은 배치 sequence 안에서 1회 실행한다.
- `PendingDeployment` 제거와 일반 전투 활성화는 deploy presentation 이후에 실행한다.
- 스킬 구현은 우선 기존 `DefenderUnitData`의 `onPlaceRange`, `onPlaceMagnitude`, `onPlaceDuration` 필드를 재사용한다.
- 각 defender의 최종 스킬 정체성은 추후 밸런싱 대상이며, 이번 구현에서는 10종에 임시 배정한다.

## 후속 후보

- **배치 스킬을 브리지 밖으로** [L] · `ApplyOnPlaceEffect` 는 ECS 시스템이 아니라 `BattleBridge`
  메서드다. 적 수집·피해·CC·스택·도트·연출 호출이 전부 여기 산다. Effects 맥락 시스템으로 내리고
  연출은 이벤트 큐로 빼면 다른 연출들과 같은 모양이 된다. `battle-sim-extraction` 과 같은 축.
- **전방 관통 4종의 배치 스킬 재설계** [M] · 지금은 각자의 평소 공격과 **같은 방향·같은 직선**이고
  크기만 다르다: 머신거너 평소 5딜×10발=50/1.9초 vs 배치 70 1회, 마크스맨 52→70, 스나이퍼 84→120,
  피어서 32→90. 즉 "평소 한 방을 조금 세게, 한 번". 그래서 명중을 고쳐도(unit 4) 배치 스킬이
  있다는 체감이 없고, 연출을 붙여도 화장에 그친다(2026-08-15 시도했다가 되돌림).
  배치 스킬은 그 유닛이 **평소 못 하는 일**이어야 한다. 머신거너·마크스맨이 6칸 70딜로 완전히
  같은 것도 여기서 함께 푼다.
- **짧은 빔이 한 프레임도 못 그리고 사라진다** [S] · `BeamPresenter.Tick` 은 TTL 을 먼저 깎고
  만료면 `TryPlace` 전에 닫는다. 프레임이 한 번 길어지면(씬 로드 직후·히치) 첫 Tick 에서 이미
  음수라 **한 번도 그려지지 않고** 세션이 사라진다 — 실측으로 확인(TryPlace 미호출). 고치려면
  "한 번도 그리지 못한 세션은 만료로 닫지 않는다"(`placedOnce` 조건 추가) 한 줄. 현재 소비자
  (버스터즈 공격/조사 빔)는 TTL 이 길어 잘 안 드러난다.
- **배치 페이즈 발동 정책** [M] · 전투 시작 전 배치는 적이 없어 배치 스킬이 통째로 낭비된다.
  전투 시작 시점으로 미루거나, 그 시점엔 안 쓰이는 스킬임을 UI 로 알리는 선택지.
