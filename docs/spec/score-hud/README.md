# Spec — 라이브 점수 HUD (Score HUD)

> 상태: **완료 2026-06-05**
> 검증 질문: *"전투 중 상단 중앙(타이머 아래)에 점수가 뜨고, 적을 처치할 때마다 강하고 스타일리쉬하게 증가하는가?"*

## 상위 목표

전투 화면 **상단 중앙 타이머 바로 아래**에 라이브 점수를 표시한다. **적을 처치할 때마다 +가점**되며, 데미지 폰트와 같은 톤(Bangers SDF + 아웃라인)으로 **카운트업 롤 + 펀치 스케일 + 색 플래시**로 강하게 증가하는 느낌을 준다.

킬 신호가 현재 없으므로 새 ECS 채널(#16, 적 사망)을 추가하고, `damage-number-popup` spec 의 unit 1 에서 만든 `_attackTagLookup` 을 재사용해 `DamageApplicationSystem` 의 DeadTag 부여 지점에서 enqueue 한다. 점수는 **표시 전용** — `ResultScreen`/리더보드의 기존 점수 공식(시간×10−골인×50)은 건드리지 않는다.

## 결정 사항 (사용자 확정 2026-06-05)

- **점수 소스**: 적 처치당 +가점. 모든 적 동일 가치(적 SO 에 bounty 필드 없음 → 후속 후보).
- **배치**: 상단 중앙 타이머(0,-20) 바로 아래(~0,-90). 점수가 더 크게.
- **최종 점수**: 표시 전용. ResultScreen 공식 불변.

## 작업 단위 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | ECS 토대 | `0_kill-event-channel.md` | `EnemyKilledEvent` + `EnemyKilledEventsSingleton`(채널 #16) + BattleBridge 생성/해제 + 스텁 드레인 |
| 1 | ECS enqueue | `1_enqueue-on-enemy-death.md` | `DamageApplicationSystem` 에서 적(`AttackUnitTag`) 사망 시 enqueue |
| 2 | HUD 뷰 | `2_score-hud-view.md` | `ScoreHudView` (UGUI, 타이머 아래, Bangers SDF, 카운트업 롤+펀치+플래시, phase 표시/리셋) |
| 3 | 브리지·씬 | `3_bridge-and-wiring.md` | BattleBridge 실 드레인 + ScoreHudView 참조 + 씬 wiring + Play 검증 |
| 4 | 인계 | `4_handoff_summary.md` | 구현 종료 요약 (커밋 후) |

## Feature-wide 계약

- **이벤트 구조체**: `EnemyKilledEvent { float3 position; }`. position 은 사망 적 `LocalTransform.Position`(현재 미사용, 향후 "+점수" 플로팅/킬 위치 연출용 reserved).
- **채널 소유**: `BattleBridge` 가 생성·소유·해제(다른 채널과 동일 패턴, `HealAppliedEventsSingleton` 미러). 채널 수 **15 → 16**. `CLAUDE.md` 채널 목록도 unit 0 에서 갱신.
- **enqueue 위치**: `DamageApplicationSystem`(Units 맥락) 한 곳. `newHp <= 0` 전이 + `AttackUnitTag` 보유일 때만(= 디펜더가 적 처치). 골 도달은 별도 경로(UnitLifecycleSystem)라 포함 안 됨.
- **드레인**: `BattleBridge.Update()` 드레인 시퀀스에 추가. HUD null 이면 큐 Clear 후 return.
- **점수 로직은 뷰 소유**: `pointsPerKill`(직렬화) + 누적 점수는 `ScoreHudView` 가 보유. BattleBridge 는 킬당 `OnEnemyKilled()` 만 호출. 표시 전용이라 외부 노출 불필요.
- **표시/리셋**: `GameManager.Instance.PhaseChanged` 구독. `Battle` 진입 시 점수 0 리셋 + 표시, 그 외 phase 숨김.
- **연출 수치 전부 직렬화**: pointsPerKill, 롤 시간, 펀치 스케일/시간, 플래시 색은 `ScoreHudView` `[SerializeField]`. 하드코딩 금지(TRD §5).
- **폰트 구분**: 점수는 데미지 숫자와 **다른 폰트**를 쓴다. 점수=`Anton SDF.asset` + `Score Outline Mat.mat`(굵은 콘덴스드), 데미지=`Bangers SDF`. 미할당 시 기본 폰트 폴백. (사용자 요청 2026-06-05: 데미지/점수 폰트 분리) — **2026-07-07 갱신: 점수 폰트를 Anton → `Kanit Bold Italic`(다이내믹 이탤릭)로 교체. spec `score-hud-impact-upgrade` unit 0. "다른 폰트" 원칙은 유지.**
- **배치(개정)**: 점수를 화면 최상단 여백(게임영역 바깥)으로 올린다 — `topOffset=-8`. 보드 위로 뜨는 데미지 숫자와 겹치지 않게. (초기 "타이머 아래(-92)" 에서 상향)
- **크기**: 데미지/점수 모두 1.3배 — 데미지 폰트 5.2~11.7, 점수 값 83 / 캡션 29. (사용자 요청 2026-06-05)

## 비범위 (후속 후보)

- 적별 점수 차등(bounty 필드를 적 SO 에 추가) — 현재 전 적 동일.
- 라이브 점수를 ResultScreen 최종 점수로 승격 — 점수 모델 변경, 별도 spec.
- 킬 위치에서 "+10" 이 점수로 날아가는 플로팅 연출 — `EnemyKilledEvent.position` 으로 가능, 별도 작업.
- 콤보/연속 처치 배수 — 범위 밖.
- 골 도달(생명 감소) 시 감점 라이브 반영 — 표시 전용 범위 밖.
