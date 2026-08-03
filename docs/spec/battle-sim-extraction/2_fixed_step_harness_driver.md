# 2 — 고정 스텝 하네스 드라이버 (StepOneTick)

## 목적

현행 sim은 가변 프레임 dt 구동이라 같은 seed 2회 실행이 같은 결과를 내지 않는다 — 골든의 전제가 없다. **주의: dt 상수 주입만으로는 안 된다** — `BattleScaledRateManager`는 렌더 프레임당 1회 갱신이라 고정 dt를 꽂으면 게임 속도가 프레임레이트에 비례한다. 하네스 모드 한정으로 **명시적 `StepOneTick()` 드라이버**를 만들어, ECS 시계·Mono `_battleClock`(웨이브/스폰)·`SkillRuntime` 쿨다운(현재 별도 `Time.deltaTime`)을 **한 스텝 안에서** 전진시키고, 입력을 벽시계가 아닌 sim tick 스케줄로 반입한다. 라이브 게임 경로는 무변(fixed tick 상시화는 M1 신 sim의 몫).

## 변경 대상

- `Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs` — 하네스 모드: 외부 주입 고정 dt를 push (프레임 결합 해제)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `StepOneTick(fixedDt)` 진입점: `_battleClock` 가산·웨이브/스폰 체크·큐 drain을 스텝 구동으로 호출 가능하게 (Update 경로와 상호 배타)
- `Assets/_Project/Scripts/Core/SkillRuntime.cs` — 하네스 시계 주입(스텝 dt 소비)
- 스크립트 배틀 확장 — 기존 `TestModeContext.Set`+StartBattle 경로에 **입력 스케줄**(tick N에 배치/카드/스킬 커맨드) 주입기 추가. 기존 e2e는 웨이브 캐리만 하므로 신규 작성분
- 하네스 실행 중 `Time.captureDeltaTime` 고정(뷰 코루틴 잔여 결합 방어)

## 구현

하네스 모드 플래그는 `TestModeContext`에 둔다. 스텝 루프: `입력 반입(스케줄된 tick) → BattleSimGroup 1회 갱신(고정 dt) → Bridge 스텝(시계·웨이브·drain)`. 에디터 포커스 함정(비포커스 시 frame 정지 — lessons 참조)은 스텝 구동이라 회피됨을 확인. pause/slow-mo는 하네스에서 미사용(라이브 전용 — gameplay 시계 정책화는 M1 후속).

## 완료 기준

- 하네스에서 같은 seed + 같은 입력 스케줄 2회 → `_battleClock` 궤적·웨이브 스폰 tick·이벤트 카운트 완전 동일.
- 라이브(비하네스) 경로 행동 무변 — Play smoke 1판 정상, 콘솔 에러 0.
- 에디터 비포커스 상태에서도 하네스 실행이 정지 없이 완주.
