# combat-action-lock

> 상태: 구현 완료 2026-07-10 (critic-clean · compile 클린 · EditMode 17/17 · PlayMode 13/13; 게이트-동작 PlayMode 는 combat 하네스 follow-up)

## 목표

"행동 불가(action-lock)" 상태를 CC 프레임워크의 1급 소비로 만든다. warmup(쿨다운 직접쓰기 해킹)을
**잠(Sleep) 상태**로 승격하고, 이미 정의만 돼 있고 무효(inert)이던 **Stun** 도 같은 게이트로 활성화한다.

- **Sleep / Stun 은 공격+이동을 모두 정지**시킨다. **적·아군 공통** 구조.
- **Sleep**: 최대 N초 유지(무한 = `+∞`), **피격 시 남은시간 무시하고 해제(wake-on-hit)**.
- **Stun**: 시간만(기존 생성 경로가 이제 실제로 행동을 막음). wake 없음.
- dreamcatcher placement-aura 의 warmup 은 이제 **Sleep 적용**으로 대체 → 앞선 "warmup 직접쓰기 층위 비대칭"도 근본 해소.

## 검증 질문
- 잠/스턴 걸린 유닛(적·아군)이 공격도 이동도 안 하는가?
- 잠든 유닛이 피격되면 즉시 깨어 행동 재개하는가? 스턴은 피격에도 시간까지 유지되는가?
- placement-aura 신규 배치 유닛이 Sleep 으로 N초 멈췄다 깨어 공속 버프로 싸우는가?

## 배경 (조사 결과 — 기존 자산)
- **CC 프레임워크 존재**: `CcEffect`(IBufferElementData: kind/vector/scalar/**remainingTime**), `CcKind{Slow,Impulse,DoT,Stun}`.
  적용 `EnemyCcEventsSingleton` 큐 → `CcApplySystem`(kind별 merge, remainingTime=max). 만료 `CcDecaySystem`(시간 감소·제거).
  `EffectSpawner.ApplyCc(em,target,effect)` 로 브릿지 직접 적용.
- **Stun 은 inert**: `StackModifierTickSystem`(ApplyStun)이 생성만, **소비 시스템 없음**. MovementSystem=Impulse만, AttackSystem=CC 미참조.
- **CcEffect 버퍼는 적 스폰에만**(BattleBridge:4226). defender 엔 없음.
- **공격 게이트**: AttackSystem 은 `cooldownRemaining`/`hitDelay` 로만 START 판정.
- **피격 훅**: `DamageApplicationSystem`(Units)이 `IncomingDamage` 적용, `DamagedCounter` 가 여기서 피격을 셈.
- **현 warmup**: `BattleBridge.ApplyPlacementWarmup` 이 `AttackState.cooldownRemaining` 직접 write(층위 비대칭).

## 결정 (2026-07-10 사용자 확정)
1. Sleep/Stun = **공격+이동 정지**, **적·아군 공통**.
2. **Sleep+Stun 동시 활성화**(공용 action-lock 게이트). Sleep=wake-on-hit, Stun=시간만.
3. (엔지니어링) 무한=`+∞`(CcDecay 자연 통과). wake-on-hit=신규 Units→Effects 이벤트 채널(직접 CcEffect 쓰기 금지). Sleep=Effects 소유 CcKind.

## 작업 단위
| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | `0_sleep_kind_and_lock.md` | `CcKind.Sleep`(append) + 순수 `CcActionLock.IsLocked`(Sleep‖Stun) + EditMode |
| 1 | code | `1_attack_move_gate.md` | AttackSystem·MovementSystem action-lock 게이트(Stun 동시 활성) |
| 2 | code | `2_defender_cc_buffer.md` | defender 스폰에 `CcEffect` 버퍼 부여 |
| 3 | code | `3_wake_on_hit_channel.md` | 신규 `CcClearRequestsSingleton`(Units→Effects) + DamageApplicationSystem enqueue + `CcClearSystem` 소비 |
| 4 | code | `4_bridge_warmup_to_sleep.md` | `ApplyPlacementWarmup`→Sleep 적용 교체 + PlacementAuraTest 갱신 |
| 5 | test | `5_playmode_verify.md` | 공격+이동 정지 / wake-on-hit / Stun no-wake / infinite / 양진영 PlayMode |

## Feature-wide 계약
1. **행동불가 소비는 읽기전용**: AttackSystem(Combat)·MovementSystem(Movement)이 `CcEffect`(Effects 소유)를 **읽기만** 해서 게이트. 쓰기는 Effects(Apply/Decay/Clear)만.
2. **action-lock = Sleep‖Stun**: 공용 판정 `CcActionLock.IsLocked(buffer)` 순수 함수. 새 lock 종류는 여기 추가.
3. **wake-on-hit = 이벤트 경유**: Units 는 CcEffect 를 직접 못 지운다. `DamageApplicationSystem` 이 Sleep 보유 피격 감지 → `CcClearRequestsSingleton` enqueue → `CcClearSystem`(Effects)이 해당 kind 제거. Stun 은 wake 대상 아님.
4. **무한 = `+∞`**: `remainingTime=float.PositiveInfinity`. CcDecay 가 자연히 유지. placement-aura 는 유한(2s) 사용.
5. **warmup 은퇴**: **`ApplyPlacementWarmup` 의** `cooldownRemaining` 직접쓰기만 제거(critic LOW1 — `CreateDefenderEntity` 의 `cooldownRemaining=deployDelaySec`(BattleBridge:3514)은 배치딜레이라 **스코프 밖·유지**). placement-aura 는 Sleep CcEffect 적용. `_activeWarmups` 의미 = "sleep 초".
5-1. **lock = 자기주도 이동만 정지, 외력 유지**(critic MED1): Impulse 넉백·Tornado pull·Portal 텔레포트는 잠/스턴 중에도 적용. `locked` 는 MovementSystem 에서 **AiState 읽은 직후 조기 계산**해 Chasing/Engaging/flow 전 분기 게이트.
6. **양 진영**: defender 도 CcEffect 버퍼 보유(적은 기존). 게이트·wake 는 진영 무관.
7. **append-only**: `CcKind.Sleep` enum 끝(byte). 신규 채널은 15→16, CLAUDE.md 채널 목록 갱신.

## 파이프라인 커버리지
**N/A** — 신규 플레이 오브젝트 없음. 기존 defender/enemy 스폰 + CcEffect/AttackSystem/MovementSystem 재사용, 신규 이벤트 채널 1개.

## 후속 후보
- Sleep/Stun VFX·프레젠테이션(잠 zzz, 스턴 별). presentation 계층.
- 다른 action-lock 종류(속박/공포 등) 추가 시 `IsLocked` 확장.
- squad-warmup 인프라(`placementWarmupSec`) 정리 여부(현재 미사용 필드).
