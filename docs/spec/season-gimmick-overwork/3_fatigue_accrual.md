# 3. 피로도 누적 시스템 — 배치 유닛 주기 누적 → 번아웃 end-to-end

## 목적

야근 룰 1 완성. 기믹 활성 시 배치된 방어 유닛이 `fatigueInterval`(10s)마다 피로도 +1 을 받고, 5스택 도달 시 unit 0/1 의 임계 룰이 번아웃을 발동한다. 이 unit 으로 "시즌 기믹이 실제 플레이를 바꾸는" 첫 순간이 성립한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/FatigueAccrual.cs` — 신규 per-entity 타이머 컴포넌트
- `Assets/_Project/Scripts/Battle/Effects/FatigueAccrualSystem.cs` — 신규 누적 시스템
- `Assets/_Project/Scripts/Battle/Effects/FatigueDebugMenu.cs` — 검증용 에디터 메뉴 (기존 HazardDebugMenu 동형)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `DebugLogFatigueStacks()` (디버그 메뉴의 유일 창구)

## 구현

1. **FatigueAccrual** (Effects 소유): `{ float elapsed }`. FatigueAccrualSystem 이 lazy-attach — 스폰 경로 무수정 (MaxHealthScaleState 전례).
2. **FatigueAccrualSystem** (Effects, BattleSimGroup):
   - `RequireForUpdate<OverworkGimmickConfig>` + `RequireForUpdate<StackModifierApplyEventsSingleton>` — 기믹 비활성 시 시스템 자체가 안 돈다 (self-gate).
   - pass 1: `DefenderUnitTag`(Units, 읽기 전용) 보유 & `FatigueAccrual` 없음 → ECB 로 attach.
   - pass 2: `elapsed += dt`, `elapsed >= fatigueInterval` 마다 차감 후 `StackModifierApplyEvent{ kind=Fatigue, countDelta=fatigueAmount, maxStack/perAppDuration=config 사본 }` enqueue. `interval <= 0` 은 skip (무한 루프 방어).
   - 시간은 `SystemAPI.Time.DeltaTime` — BattleSimGroup 의 BattleScaledRateManager 를 그대로 탄다 (정지/슬로우모 일관).
3. **누적 기점 = 배치 시점** (사양 문언 "배치되고 10초마다" 그대로). 전투 시작 전 배치 페이즈도 sim 시간이 흐르면 누적된다 — 배치 페이즈 게이팅이 필요해지면 후속에서 튜닝.
4. **디버그 메뉴**: `Wassup/Battle/Debug/Log Fatigue Stacks` → `BattleBridge.DebugLogFatigueStacks()` — defender 별 Fatigue stackCount + ModifierStats(공속/공격력/최대체력 배율) 로그. ECS 접근은 BattleBridge 경유 (절대 제약 1).

## 완료 기준

- compile 통과 + 콘솔 클린.
- 활성 시즌에 Gimmick_Overwork **임시 연결** 후 Play: 유닛 배치 → 10초마다 피로도 누적 (디버그 메뉴로 스택 확인) → 50초에 번아웃 발동: ModifierStats 공속/공격력/최대체력 ×0.8 확인, 15초 후 복원 + 스택 0 부터 재누적.
- 임시 연결 해제 후 (gimmick=null) 무변화 재확인. 정식 시즌 연결은 unit 7.

확인 2026-07-15 · 커밋 `4ded63e1` — Play 실측(2s 인터벌): 누적→번아웃(3스탯 ×0.8 + HP 클램프)→Consume 리셋→재누적, 재입장 재주입. 주의: PrepareDraftMap 이 Awake 보다 먼저 불려 static SeasonRuntime 의존 주입이 누락되던 버그를 seam 에서 seasonRegistry 직독으로 수정.
