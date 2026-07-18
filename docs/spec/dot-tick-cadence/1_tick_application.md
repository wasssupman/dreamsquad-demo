# 1 — DotApplySystem 이산 tick 지급

## 목적

`tickInterval > 0`인 DoT를 주기마다 `scalar` 청크 1회로 지급한다(연속 fallback 유지). 이 단위가 실제 행동 변화 + 폰트 스팸 해소의 핵심. EditMode 테스트로 회귀 고정.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/DotApplySystem.cs`
- `Assets/_Project/Tests/EditMode/DotApplySystemTests.cs`

## 구현

`DotApplyJob`/`DotApplyWithEventsJob`의 `Execute` 시그니처를 `in DynamicBuffer<CcEffect>` → **`ref DynamicBuffer<CcEffect>`** 로 바꿔 `tickTimer` 를 갱신한다. DoT 엔트리별:

```csharp
var cc = ccBuffer[i];
if (cc.kind != CcKind.DoT) continue;

if (cc.tickInterval <= 0f)
{
    // 레거시 연속: scalar = DPS
    damageBuffer.Add(new IncomingDamage { amount = cc.scalar * DeltaTime });
    // (WithEvents 잡이면 기존대로 프레임당 1 이벤트)
}
else
{
    // 이산 tick: scalar = tick당 데미지
    cc.tickTimer += DeltaTime;
    // while: 저프레임/큰 dt 에서 다중 tick 보정(결정론적)
    while (cc.tickTimer >= cc.tickInterval)
    {
        cc.tickTimer -= cc.tickInterval;
        damageBuffer.Add(new IncomingDamage { amount = cc.scalar }); // 청크 1개 = 폰트 1개
        // WithEvents 잡: tick당 HazardRuntimeEvent(DotDamage) 1개 (프레임당→tick당, 스팸↓)
    }
    ccBuffer[i] = cc; // tickTimer 되쓰기
}
```

주의:
- 청크당 `IncomingDamage` 엔트리 1개 → `DamageApplicationSystem`이 엔트리당 폰트 1개(`:131-142`) → tick당 폰트 1개. 청크가 정수(10/20)라 `Max(1,RoundToInt)` 바닥 문제 없음.
- `remainingTime`(restDuration linger)은 `CcDecaySystem`이 계속 관리 — tick 로직과 독립. 존 이탈 후 linger 동안 tick은 계속되다 슬롯 만료 시 종료.
- 순수 tick 산식은 자명(비자명 분기 아님)하지만 sim-critical(데미지)이라 테스트 가치 있음 → 인라인 유지 + 테스트로 고정(제약 10 판정).

## 테스트 (DotApplySystemTests 확장)

- **연속 fallback**: tickInterval=0, scalar=20, dt=0.1 → damage 2.0 누적 (기존 회귀).
- **이산 즉발**: tickInterval=0.5, scalar=10, tickTimer 초기=0.5 → 첫 Run(dt=0.016)에 10 지급.
- **이산 주기**: tickInterval=0.5, dt=0.016 반복 → 0.5초 경과마다 정확히 10씩(누적 tickTimer 검증).
- **다중 tick 보정**: 큰 dt(예: 1.2, interval=0.5) → 한 Run에 청크 2개(1.0 소진, timer 0.2 잔여).
- **병합 후 tick 진행**: CcApply refresh를 여러 번 태워도 tickTimer 보존되어 주기 도달(계약 4).

## 완료 기준

- [x] `run_tests` EditMode 전체 그린 (신규 케이스 포함 — 최종 964개)
- [x] 컴파일 그린, ecs-reviewer 통과(버그 0; 병합 정책 통합·interval 환산 리뷰 반영)
- [x] 연속 DoT 소스(StackModifier·3x3) 행동 불변 확인

> 확인 2026-07-18 · 커밋 aedcb66f · 순수 산식 DotTick + DotTickTests 6, 시스템 테스트 4
