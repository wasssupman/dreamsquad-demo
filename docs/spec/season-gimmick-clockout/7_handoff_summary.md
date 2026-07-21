# 7. Handoff Summary

> ⚠️ **unit 8(2026-07-21) 이후 부분 폐기**: 아래는 unit 0~6 시점 지도다. 룰1 "10초 강제 퇴근"과 "퇴근 코스트 환급"은 unit 8 재설계로 제거됐다 — 이제 defender **사망 시** 사직서를 드랍한다(`ResignationDropSystem`). 최신 계약은 [README.md](README.md) + [8_death_drop_rework.md](8_death_drop_rework.md) 우선.

## Commit

- `c99c432f` unit 0 — 기믹 데이터+config+주입 seam (`ClockOutGimmickData`/`ClockOutGimmickConfig`)
- `c50b793b` unit 1 — 사직서 아키타입 + 뷰 (`Resignation`/`ResignationPresenter`)
- `80c7c4d6` unit 2 — 퇴근 타이머 + 사직서 스폰 (`ClockOutTimer`/`ClockOutSystem` + `BattleRunning`)
- `fb03a00f` unit 3 — 사직서 임계 → 메테오 barrage 요청 (`ResignationThresholdSystem` + `MeteorBarrageRequestsSingleton`)
- `7007df79` unit 4 — 메테오 barrage cast (`BattleBridge.DrainMeteorBarrageRequests` + `MatchSeed.DeriveMeteorSeed`)
- `eed855f8` unit 5 — 기믹 asset + gimmickPool 등록 (`Gimmick_ClockOut.asset`)
- `150f4847` unit 6 — 퇴근 코스트 환급 +1 (`ClockOutRefundEventsSingleton` → `CostRuntime.AddCost`)
- `6e5cb0eb` balance — 메테오 데미지 40→150
- (부수) `792a4b5f` pull 리워크 병합 후 재리뷰 기록 · `da722d01` WaveA 유출 허용치 10→30(별개 밸런스)

## Implemented

- 세 번째 시즌 기믹 "집에 가도 되나요?"(`G3_ClockOut`), `BattleConfig.gimmickPool` 3번째 등록(랜덤 배정).
- **퇴근**: running 후 배치 defender 가 `clockOutSeconds`(10s) 만료 시 배치 타일에 사직서 스폰 + 치명 IncomingDamage 로 사망(기존 death 경로 재사용, running-only).
- **사직서**: Effects 아키타입, poll-reconcile 뷰(플레이스홀더 흰 종이). 소비형 아님 — 전역 임계로만 소멸(사용자 결정: 재배치 습득 없음).
- **메테오**: 사직서 `resignationThreshold`(5) 도달 시 5장 소모 → Walk 타일 결정론 3곳에 SkyFall×TileAoe 순차 낙하(적만, dmg 150). 기존 `SpawnProjectile` bridge-cast 재사용.
- **코스트 환급**: 퇴근 1회당 +1 → `CostRuntime.AddCost`(기존 지급 패스).
- 신규 인프라: 싱글턴 `BattleRunning`(Mono→ECS running 신호), 큐 2개 `MeteorBarrageRequestsSingleton`·`ClockOutRefundEventsSingleton`(Effects→Bridge, 채널 20개).

## Key Files

- `Scripts/Data/Gimmick/ClockOutGimmickData.cs` · `Data/Gimmick/Gimmick_ClockOut.asset`
- `Scripts/Battle/Effects/ClockOut{GimmickConfig,Timer,System}.cs` · `Resignation.cs`·`ResignationPresenter.cs`·`ResignationThresholdSystem.cs` · `MeteorBarrageRequest(s Singleton).cs` · `ClockOutRefundEvent(s Singleton).cs`
- `Scripts/Battle/BattleRunning.cs` · `Scripts/Core/MatchSeed.cs`(DeriveMeteorSeed)
- `Scripts/Bridge/BattleBridge.cs` — config 주입·running push·reconcile·barrage cast·refund drain·채널 lifecycle
- `Data/Config/BattleConfig.asset`(pool) · `Scripts/Data/Decks/WaveA.asset`(유출 30)

## Verified

- compile CS 에러 0 (Unity 재컴파일, MCP refresh — Burst JIT 캐시 DLL 경고는 환경성/기존, 코드 무관).
- Play 통합검증 **사용자 통과**: 퇴근→사직서→5장 소모→메테오 순차 낙하 + 퇴근 코스트 +1.
- pull 로 들어온 투사체/공격 리워크(directional-volley·sweep·CC/DoT·DeployedFacing)와 병합 후 무회귀 — 리워크는 가법적(SkyFall×TileAoe·death·IncomingDamage 불변).

## Notes (되돌리면 안 되는 의도)

- 퇴근 사망 = 치명 IncomingDamage sentinel(1e9) → 기존 death 경로 재사용(사용자 결정). `BattleRunning` running-only 게이트(배치 페이즈 미가동).
- 사직서 = 비소비형. 재배치 습득 없음(사용자 확인). 전역 임계로만 소멸.
- 코스트 환급은 뷰 reconcile 이 아니라 **퇴근 순간 채널**로 — 5번째 사직서 동프레임 소모 시 뷰가 못 보는 off-by-one 회피.
- 메테오 `targetFaction=Enemy`(보스 메테오만 Defender). cast 는 content-1 OnDeath 폭발과 동형(`Entity.Null`).
- 모든 수치 SO(`Gimmick_ClockOut.asset`): clockOut 10 / threshold 5 / meteorCount 3 / dmg 150 / tileRange 1 / warn 1.2 / stagger 0.4 / costRefund 1.

## Follow-up

- 사직서/퇴근/메테오 정식 아트 + VFX(현 플레이스홀더).
- 사직서 시각 겹침(같은 타일 반복 퇴근) 오프셋/스택 표시 — 비소비형이라 쌓일 수 있음.
- 사직서 누적 카운터 UI(5까지 진행 표시).
- 밸런스 재튜닝(메테오 데미지/빈도, 코스트 환급량, clockOut 시간).
- effect-trigger-unification 파킹 문서 — 시즌 기믹 3종 됨(착수 압력↑). 단 퇴근-타이머/월드-스폰/barrage 는 그 프레임 밖.
