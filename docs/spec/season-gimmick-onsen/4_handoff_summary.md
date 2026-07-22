# 4. Handoff Summary

> 온천 "뜨끈하니 좋네요오오.. 뜨겁네?"(G4_Onsen) 인계 지도. 최신 계약은 [README.md](README.md) + 번호 문서 우선.

## Commit

- `890dc805` unit 0~2 — data/config/inject + `HeatMath` + `HeatAccrualSystem`
- `3a243a93` fix — pass1 구조변경 후 `_damageLookup` 갱신(stale BufferLookup)
- `b94be95b` test — critic 순수로직 리뷰: 엣지 테스트 4종 + SO `[Min(0f)]` 음수 percent 가드
- (이 커밋) unit 3 — `Gimmick_Onsen.asset` + `BattleConfig.gimmickPool` 등록(4번째)

## Implemented

- 5초마다 맵 위 **모든 유닛(아군+적)** 에 열기 +1. 스택 ≤5 = 최대체력 10% 회복, >5 = 10% 손실.
- **열기 손실은 아무도 못 죽인다** — HP 1 바닥(`HeatMath.Delta` 가 currentHp−1 로 floor). 마무리는 전투.
- 회복은 헤드룸 클램프(오버힐/만피 VFX 스팸 없음). 손실 미귀속(source=Null).
- `HeatAccrualSystem`(Effects, Burst, `RequireForUpdate<OnsenGimmickConfig>` self-gate): `WithAny<DefenderUnitTag,AttackUnitTag>` 대상, 적엔 `IncomingHeal` lazy-add, 부호별 `IncomingHeal`/`IncomingDamage` append, `UpdateBefore(DamageApplicationSystem)`, projectedHp 로 멀티틱 클램프 성립.
- 모든 수치 SO(`Gimmick_Onsen.asset`): heatInterval 5·flipThreshold 5·healPercent 0.1·lossPercent 0.1·heatMaxStack 6.

## Key Files

- `Scripts/Data/Gimmick/OnsenGimmickData.cs` · `Data/Gimmick/Gimmick_Onsen.asset`
- `Scripts/Battle/Effects/OnsenGimmickConfig.cs` · `HeatMath.cs` · `HeatAccrual.cs` · `HeatAccrualSystem.cs`
- `Tests/EditMode/HeatMathTests.cs` (13 케이스)
- `Scripts/Bridge/BattleBridge.cs`(CreateGimmickConfigIfActive Onsen 분기 + teardown) · `Data/Config/BattleConfig.asset`(pool)

## Verified

- 컴파일 CS 에러 0. `HeatMathTests` 13/13 green.
- 파이프라인: 새 ECS 맥락·새 NativeQueue·새 StackKind 없음(기존 IncomingHeal/IncomingDamage 채널 재사용).

## Notes (되돌리면 안 되는 의도)

- **"열기 단독은 안 죽임"** = 단일/멀티틱 모두 HP 1 바닥. 전투 합산 사망은 의도(README "마무리는 전투"). 전투분까지 floor 하려 하지 말 것(불사신化).
- 대상 = **모든 유닛**(적 포함) — 사용자 결정. 적은 초반 질기고 후반 녹음.
- `HeatMath.Delta` = 아키텍처 중립 순수 함수(제약 10). ECS 는 부호만 보고 소비.

## Follow-up

- ⚠ **열기 반전 육안 Play 검증**(초록 회복 → 6틱째 빨강 손실, HP 1 바닥) 사용자 명시 확인.
- 열기 게이지 UI / 전용 상태FX(김·아지랑이).
- 냉각/리셋 룰(gimmick-2) · 적 전용 밸런스 분리.
