# 6. 퇴근 코스트 환급

## 목적

퇴근(clockOut) 1회당 플레이어에게 코스트 `clockOutCostRefund`(1) 환급 — 기존 코스트 지급 패스(`CostRuntime.AddCost`) 재사용. 퇴근으로 유닛이 빠져나가도 재배치 자원이 돌아온다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Gimmick/ClockOutGimmickData.cs` — `clockOutCostRefund`(int, 1)
- `Assets/_Project/Scripts/Battle/Effects/ClockOutGimmickConfig.cs` — `costRefund`
- `Assets/_Project/Scripts/Battle/Effects/ClockOutRefundEvent.cs` + `ClockOutRefundEventsSingleton.cs` (신규 채널)
- `Assets/_Project/Scripts/Battle/Effects/ClockOutSystem.cs` — 퇴근 시 enqueue
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — config 복사 + 채널 lifecycle + `DrainClockOutRefundEvents`(→`CostRuntime.AddCost`)
- `CLAUDE.md` — NativeQueue 채널 19→20
- `Assets/_Project/Data/Gimmick/Gimmick_ClockOut.asset` — `clockOutCostRefund=1`

## 구현

1. **`clockOutCostRefund`** SO 노브(하드코딩 금지) → `config.costRefund` blittable 복사.
2. **신규 Effects→Bridge 채널** `ClockOutRefundEventsSingleton`(메테오 barrage 채널 동형). 퇴근=ECS(Effects), `CostRuntime`=Mono 라 경계상 채널 필요.
3. **`ClockOutSystem`**: 퇴근(사직서 스폰+치명damage) 시 `ClockOutRefundEvent{amount=config.costRefund}` enqueue. 뷰 reconcile 이 아니라 **퇴근 순간**에 쏴 off-by-one(5번째 사직서가 같은 프레임에 임계 소모되어 뷰가 못 보는 케이스) 회피.
4. **`BattleBridge.DrainClockOutRefundEvents`**: `GameManager.CostRuntime.AddCost(amount)` — 기존 지급 패스(max clamp·코스트 UI 일관). CostRuntime 부재 시 큐 비우고 드롭.

## 완료 기준

- compile 0 에러(Unity 재컴파일).
- Play(ClockOut): 유닛 퇴근 시 코스트 +1(코스트 게이지 증가 육안). off/다른 기믹에선 무변화.

확인 2026-07-20 — Unity 재컴파일 후 read_console CS 에러 0(Burst JIT 캐시 경고만). 코스트 +1 육안은 통합검증에서.
