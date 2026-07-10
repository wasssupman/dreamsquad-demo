# Spec — Attack Anim Speed Match (공격 애니 ↔ 공속 동기)

> 상태: **완료 2026-07-10** (units 0~1). 별도 SO 제거→공격속도 필드 직접 파생 재설계 + 산식 critic 반영. handoff `2_handoff_summary.md`. 사용자 체감 확인(하한 1.0 "딱 좋음") 통과.
> 출처: 공속 논의(enemy-walk-anim-speed 후속). 공속 스탯이 애니 재생속도와 분리돼, 빠른 공격이 "빈도만 늘고 모션은 그대로"인 문제. 접근 **A(공속→애니 재생배율)** 채택.

## 문제

공격 rate 는 시뮬 쿨다운(`interval = cooldownDuration / attackSpeedMul`)이 결정하지만, 공격 애니는 **저작된 고정 길이**로 재생된다(공속 무관). 그래서 (1) 공속이 "빠른 스윙"으로 안 느껴지고, (2) 극단 공속에선 애니가 완주 전 재시작 → 버벅임. 모션과 스탯이 decoupled.

## 검증 질문

> "공속이 오른 유닛의 공격 모션이 눈에 보이게 빨라지는가(간격에 맞춰 압축 완주)? 느린 공격은 자연속도로 재생 후 대기하는가? 캡을 넘는 초고속에선 파괴적 버그 없이 캔슬/재시작로 안전하게 처리되는가? 시뮬 rate/데미지(source of truth)는 불변인가?"

## 해법 (compress-to-fit, 별도 데이터 없음)

공격 간격(sim 값)을 visual 이벤트에 실어 뷰로 전달 → 뷰가 공격 트랙만 스케일. **별도 튜닝 SO 없이 공격속도 필드에서 직접 도출**(사용자 결정 2026-07-10):

```
TrackEntry.TimeScale = max( 1, animDuration / attackAnimPeriod )        // 그 공격 애니만 스케일
attackAnimPeriod     = max( cooldownDuration / attackSpeedMul, hitDelaySec )   // sim 이 계산(공격속도 필드)
```

- period ≥ animDuration(느린 공격) → TimeScale=1, 자연재생 후 idle 대기.
- period < animDuration(빠른 공격) → TimeScale>1, 주기에 딱 맞게 압축 완주 → 빠른 스윙.
- **발사 주기 = max(간격, hitDelay).** `hitDelayRemaining>0` 동안 다음 START 가 막히므로(AttackSystem) 실제 발사 주기는 둘의 max. 애니를 이 주기에 맞춰야 실발사보다 빨리 끝나지 않는다(critic MEDIUM #1 반영). 둘 다 유닛 SO 필드라 SoT 불변 유지.
- **상한 없음** — `attackSpeedMul` 이 modifier 정책상 [0.2, 5.0] 로 클램프(3중첩 +50%=2.5). 단 배율의 실질 유한성은 `animDuration/period` 이므로 authoring 규율(과도히 작은 cooldownDuration 지양)이 근거 — 클램프는 그 위 배수만 제한(critic LOW #4). degenerate 설정만 큰 값 → 사실상 즉시 재생(honest).

## feature-wide 계약

1. **시뮬 불변.** rate/데미지/hitDelay 는 그대로. 이 spec 은 **공격 애니 재생속도(시각)** 만 변조. ECS 경계: 뷰는 이벤트에서 `attackInterval` **숫자만** 읽는다.
2. **간격 = sim 이 계산.** `attackInterval = cooldownDuration × (1/attackSpeedMul)` 를 AttackSystem 이 계산해 이벤트에 싣는다. double-fire(2연발) 로 cooldown 을 0 으로 만드는 경우에도 애니엔 **정상 간격**을 싣는다(0 나눗셈/무한배율 방지).
3. **TrackEntry.TimeScale 사용.** 공격 애니만 스케일 — skeleton.timeScale(걷기/battleScale)과 독립. 걷기 로직(enemy-walk-anim-speed)과 충돌 없음.
4. **별도 데이터 금지.** 애니 배율은 **공격속도 필드(cooldownDuration + attackSpeedMul)에서만** 파생. `AttackAnimSpeedStyle` 같은 별도 튜닝 SO/미러 두지 않는다. 하한 1.0 은 튜닝값이 아니라 **구조 상수**(저작속도보다 느리게 늘리지 않음).
5. **상한 없음 = 공속 클램프에 위임.** 별도 캡 대신 sim 의 `attackSpeedMul` 클램프(0.2~5.0)가 실사용 배율을 유한하게 유지. 캡을 재도입해야 할 만큼 초고속이 문제되면 그때 재논의.
6. **폴백 = 현행.** `attackInterval<=0`(이벤트 폴백)이면 TimeScale=1 → 현행 동작. 회귀 없음.
7. **hitDelay 는 발사 주기에 반영(hit 프레임 정렬은 밖).** 발사 주기 계산에 hitDelaySec 를 포함(계약 2). 단 데미지가 떨어지는 hit 프레임을 압축된 스윙의 접촉점에 맞추는 **정렬**은 후속 후보(critic MEDIUM #2, 순수 시각 desync). 이번엔 시각 압축만, sim hit 타이밍 유지.

## Critic 반영 (2026-07-10)

산식 critic 1회 실행 → **불변 법칙 준수 판정**(compress 구간에서 animDuration 소거, SoT 가 유일 저작자). 반영:
- MEDIUM #1: 발사 주기에 hitDelaySec 포함(`max(interval, hitDelay)`) — 반영 완료.
- LOW #4: 유한성 근거 주석 정정(animDuration/period 가 실질, 클램프는 배수만) — 반영 완료.
- MEDIUM #2(hit 프레임 정렬), LOW #3(느린 공격 floor 는 compress 구간 밖에서 animDuration 종속 = 의도된 구조 상수, 유지) — 후속/수용.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 계약 | `0_contract.md` | `UnitAttackVisualEvent.attackInterval` 필드 추가 (SO/미러 없음) |
| 1 | 배선 | `1_interval_to_view.md` | AttackSystem 간격 계산·enqueue → Bridge/Pool → PlayAttack `TrackEntry.TimeScale = max(1, dur/interval)` |

> 초기 초안엔 `AttackAnimSpeedStyle` SO + 에셋 배선(구 unit 2)이 있었으나, 사용자 결정으로 **별도 데이터 없이 공격속도 필드 직접 파생**으로 재설계하며 제거됨.

## 파이프라인 커버리지

신규 아키타입/정거장 없음. 기존 **공격 visual 이벤트 채널**(`UnitAttackVisualEventsSingleton`, Combat→Presentation)에 필드 1개 추가 + 뷰 공격 애니 재생 파라미터 변조. 채널 개수 불변(15개). 표 복사 N/A.

## 후속 후보

- **hitDelay(윈드업) 를 애니에 정렬** — 공속따라 hit 프레임 당김. 이번 시각 압축과 별개 sim 변경.
- **초고속 상한/다단히트 전환** — 필요 시 캡 재도입 또는 스택 상한 도달 후 rate 대신 다른 축.
- **적(Spine) vs 디펜더 거동 분리** — 밸런스상 필요 시.
