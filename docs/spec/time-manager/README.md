# Spec: time-manager

> 상태: 완료 2026-07-06 (Units 0–5 · 자동 검증 통과 · 투트랙 리뷰 반영 · 사용자 정지 육안 확인).
> 상세는 `6_handoff_summary.md`.

## 한 줄

도메인 스코프 시간 제어. 글로벌 `Time.timeScale` 은 **1 고정**, `TimeManager` 가 도메인별 시간 스케일을 소유·중재한다.

## 검증 질문 (완료 기준의 근원)

1. **일시정지**: 정지 시 전투(ECS 시뮬 + BattleBridge 웨이브/타이머 + 전투 표현)가 **완전히** 멈추고, 정지 UI 는 살아있는가?
2. **D&D 슬로우모**: 드래그 중 전투만 0.2x 로 느려지고, 드래그 유닛·배치·카메라·HUD 는 실시간인가?

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Mono 코어 | `0_time-manager-core.md` | `TimeManager` 싱글턴 + `TimeDomain` + `TimeLease`(멱등) + arbitration |
| 1 | ECS 구조 | `1_battle-sim-group.md` | `BattleSimGroup` 부모 그룹 신설 + 24개 시스템 `[UpdateInGroup]` 재타겟 |
| 2 | ECS 시간 | `2_scaled-rate-manager.md` | `BattleScaledRateManager : IRateManager` — pause=skip, slow=PushTime |
| 3 | 브리지 | `3_battlebridge-integration.md` | `BattleTimeScale` singleton 매 프레임 write + **웨이브/타이머 Battle-스케일 클럭**(BLOCKER) |
| 4 | 표현 | `4_presentation-scale.md` | 전투 Spine/VFX 스케일 반영 (스폰 pull + ScaleChanged) |
| 5 | 요청자 | `5_requesters-dreamcatcher.md` | 정지 UI·드래그 요청 배선 + `DreamcatcherController` 마이그레이션 |

## feature-wide 계약

1. **코드에서 `Time.timeScale` 을 쓰지 않는다** (1 고정). 시간 스케일 표현은 `TimeManager` 만.
2. **전투 도메인 = ECS `BattleSimGroup` + BattleBridge 웨이브/타이머 클럭 + 전투 표현(Spine/VFX)**. 이 셋이 같은 `ScaleOf(Battle)` 을 따른다.
3. **인터랙션 도메인은 항상 1** (드래그/배치/카메라/HUD/정지메뉴 UI). Battle 스케일을 소비하지 않는다.
4. `TimeManager` 는 **의도된 예외적 싱글턴** — 제약 #5(Manager 싱글턴 금지)의 명시적 예외, `docs/TRD.md` 섹션 5 에 기록.
5. **arbitration**: 도메인당 다중 요청, 승자 = (priority desc, 동률 시 scale asc). 요청 없으면 1. 요청은 `TimeLease`(멱등 dispose)로 해제.
6. **ECS 경계**: 스케일 값은 `BattleBridge` 가 `BattleTimeScale` singleton 에 write, RateManager 가 read. TimeManager 를 ECS 에서 직접 참조하지 않는다.
7. **RateManager**: `scale <= 0` → `ShouldGroupUpdate=false`(그룹 전체 skip, 완전 정지) · `scale > 0` → `World.PushTime(elapsed += realDt·scale, delta = realDt·scale)` 1회 후 pop+false. `Timestep` setter(≥0.0001 클램프)로 라우팅하지 않고 `TimeData` 직접 push.
8. **표현 스폰 레이스 방지**: 유닛/VFX 스폰 시 `ScaleOf(Battle)` 을 pull 해 초기화. `ScaleChanged` 는 변화 신호로만.

## 검증 근거 (critic 리뷰, 2026-07-05)

- 전투 시스템 24개 모두 `[UpdateInGroup(typeof(SimulationSystemGroup))]`, 시간 read 는 전부 `SystemAPI.Time.DeltaTime` (ElapsedTime/UnityEngine.Time 사용 0). 쿨다운은 countdown 방식 → RateManager 스케일이 전 타이밍 커버.
- IRateManager 계약: `com.unity.entities@…6.4` `RateUtils.cs` 확인. `FixedRateSimpleManager` 가 PushTime/PopTime canonical.
- 전투 ECB 는 전부 로컬 `Allocator.Temp` in-place playback → 그룹 skip 이 deferred 커맨드 스트랜딩 없음. NativeQueue 생산자 전원 그룹 내부, 소비는 BattleBridge Update → 빈 큐 drain no-op.
- `BattleBridge` 는 MonoBehaviour(Update/LateUpdate 보유) → singleton write 창구로 적합.

## 후속 후보 (현 스코프 밖)

- **투사체/hit VFX 파티클·spin·코루틴 수명 스케일** — 투사체 위치는 ECS(BattleSimGroup)라 이미 정지/슬로우 반영되지만, 파티클 `simulationSpeed`·`SpinAroundUp` dt·`WaitForSeconds` 수명은 실시간. 풀링 불변식(ResetVfx/TrailRenderer.autodestruct/emitterVelocityMode)과 얽혀 broader 변경이라 분리. 투사체는 순간적이라 cosmetic-tier.
- **독립 정지 버튼/메뉴 UI** — 정지 "능력"은 완성(Dreamcatcher 선택이 이미 lease 로 전투 정지). 플레이어용 정지 버튼+패널은 레이아웃/버튼 구성(재시작/종료/설정)이 product 결정이라 별도 spec. 트리거는 `TimeManager.Instance.Request(Battle, 0, 100)` 한 줄.
- **RateManager 그룹 allocator swap (M2)** — 정식 FixedRateSimpleManager 는 push 구간에 그룹 rewindable allocator 를 swap 한다. 해당 API(`CurrentGroupAllocators`/`SetGroupAllocator`)가 Unity.Entities internal 이라 유저 코드 접근 불가. 현재 배틀 시스템이 WorldUpdateAllocator 미사용·ECB Temp 라 안전. 배틀 시스템이 WorldUpdateAllocator 채택 시 재검토.
- 히트스톱(유닛 단위 로컬 시간) — 두 번째 독립 스케일 도메인이 실제로 필요해지면 `TimeDomain` 확장으로.
- Interaction 도메인 세분화(카메라/UI 분리).
- 슬로우모 진입/복귀 ease (현재는 즉시 전환).
