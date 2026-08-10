# handoff — target-persistence (완료 2026-08-10)

## Commit

| | |
|---|---|
| unit 0 공격 1회 커밋 | (units 0~2 는 2026-08-09 커밋, 세션 요약 참조) |
| units 1·2 술어 단일화 + 범위 이탈 해제 | 〃 |
| unit 3 적 `Nearest` 락 + CC 해제 | `538233e0` |
| unit 4 방어유닛 락 | `10d1dc96` |
| 코드 리뷰 수정(계약 6 + 테스트 3건) | `6bc3f96c` |

## Implemented

- **B1 제거** — START 에서 겨눈 대상을 RESOLVE 에서 때린다(`AttackState.committedTarget`). 겨눈≠맞은 **0/125**
- **B2 제거** — `FocusUntilDead` 적이 사거리를 벗어난 락을 붙든 채 골로 걸어가던 결함. 라이브 91,540 관측 반례 0
- **락이 전 진영으로** — 적 `Nearest` 4종(보스 2종 포함, D4) + 방어유닛(제외 4종 빼고)
- **해제 사유 4종** — 사망 · 사거리 이탈 · 어그로 끌림(적) · **자기 CC 해제**(D5, 전 진영 균일)
- **계약 6** — 락 유지 판정이 `targetMask` 를 다시 본다. 후보 루프 재사용(신규 lookup 0)
- 신규 컴포넌트 0 · 신규 시스템 0 · 신규 이벤트 채널 0. `FocusTarget` 재사용

## Key Files

- `Battle/Combat/AttackSystem.cs` — 락 블록 **2벌**(적 `:640~690` · 방어유닛 `:769~815`) + unit 0 커밋 `:820~`
- `Battle/Combat/TargetPersistence.cs` — `KeepsLock` 순수 술어. `AttackSystem` 과 `EnemyAiStateSystem` 이 **같은 함수**를 부른다(계약 4)
- `Battle/Combat/EnemyAiStateSystem.cs` — 미러. **게이트가 `AttackSystem` 과 항상 같아야 한다**
- `Bridge/BattleBridge.cs` — `FocusTarget` 부착 3곳(적 `:7793` · 배치 방어유닛 `:6158` · 순찰병 `:6387`)
- 테스트 4파일 33건: `AttackCommitTests`(8) · `TargetPersistenceTests`(7) · `NearestLockTests`(7) · `DefenderLockTests`(11)

## Verified

- **EditMode 2097 중 2094 통과 · 실패 0** (스킵 3은 기존 `[Ignore]`)
- **기존 타겟팅 테스트 6종 기대값 갱신 0건** — 「선정 규칙을 안 건드렸다」의 증거
- 라이브 카운터 — 방어유닛: 사유 없는 전환 **0** / 예전이라면 갈아탔을 **1,569** / 평균 유지 **454프레임(7.5초)**
- 사용자 Play 확인 — 빈 스윙 체감 · 뒤 놓침 없음 · 보스 집중

## Notes — 되돌리면 안 되는 것

1. **블록 순서**: `frontmost → [unit 4 락] → unit 0 커밋 → facing`. unit 4 를 facing 뒤로 옮기면 wind-up 커밋을 덮어써 **B1 이 부분 부활**한다. `DuringWindup_TheCommittedTargetWins_OverTheLock` 이 가드.
2. **CC 비움은 `else` 로 감싼다.** 비우기만 하고 흘리면 해제 분기가 그 프레임 최근접으로 **즉시 재잠금**한다(초판 결함, 테스트가 잡음).
3. **제외 4종은 계약이다** — facing·frontmost·힐러·가디언. "빠뜨렸네" 하고 채우면 넷 다 조용히 망가진다. 각각 코드 주석에 이유 있음.
4. **미러 게이트 동기** — `AttackSystem` 과 `EnemyAiStateSystem` 의 락 게이트가 갈리면 «락은 있는데 FSM 은 Marching» 데드락(B2 의 절반)이 재발한다.
5. **`FactionTag` ComponentLookup 을 추가하지 말 것** — 시도했더니 `ObjectDisposedException(EntityTypeHandle invalidated by a structural change)` 으로 AttackSystem 전체가 무너졌다(EditMode 25건, 2회 재현). 마스크 판정은 후보 루프에서 표시한다.
6. **`!EnemyBehavior` 게이트** — 순찰병은 적 AI 스택을 물려받아 unit 3 블록이 처리한다. 지우면 한 프레임에 두 번 잠근다.

## 검증 방법에서 남길 것

**두 축을 항상 같이 본다**: 「위반 = 0」(규칙이 지켜지나) + **「예전이라면 달랐을 > 0」**(시나리오가 결함을 자극했나). 후자가 0이면 전자는 아무 말도 안 한다.

이 spec 에서 그 함정을 세 번 밟았다 — ① `Nearest` 적이 0기인 웨이브에서 「위반 0」(분모가 0) ② 「갈아탔을 523」이 전부 **기존 Focus 적** 것(모드별로 안 갈랐다) ③ 「때려서 죽인 것」과 lapse 를 한 카운터로 셈(분모 정의 누락).

## Follow-up

- **락 블록 2벌**(리뷰 F3) — 지금은 호출처 2라 수용. **세 번째 락 지점이 생기면 추출**한다. 계약 6 수정이 두 곳을 똑같이 고쳐야 했던 것이 이미 비용이었다
- **`attackTargetCount > 1` 의 보조 타겟** — 락은 primary 만 고정하고 보조는 매 프레임 재선정한다. 의도인지 미확인
- **`Nearest` 보스 2종의 타겟 정책** — `Halt` 라 락이 사실상 무효다. 「집요한 보스」를 원하면 `engageMovement` 저작이 실제 knob 이다
- **동거리 히스테리시스** — unit 4 로 자동 소멸. 별도 장치 불필요(후속 후보에서 삭제 가능)
