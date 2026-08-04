# 0 — 유효 시스템 총순서 캡처 + 미선언 순서 핀

## 목적

`BattleSimGroup` 44개 시스템의 어트리뷰트 순서 그래프는 불완전하다 — 미선언 지점의 실행 순서를 Unity의 토폴로지 정렬 tie-break이 결정하고 있다. 이 순서가 곧 시뮬 의미론(같은 틱 소비·IncomingDamage 정산 시점)이므로, ① 러닝 월드의 **실제 유효 총순서를 덤프해 사실로 기록**하고 ② 미선언 지점을 어트리뷰트로 박제해, 이후 골든(unit 4)과 신 sim 틱 파이프라인(M1)이 흔들리지 않는 기준을 만든다.

## 변경 대상

- 신규: 순서 덤프 에디터 유틸 (예: `Assets/_Project/Editor/Battle/SimOrderDumpMenu.cs` — `BattleSimGroup`의 실행 순서를 로그/파일로)
- 미선언 순서 핀 (감사 식별 + 2026-08-03 교차검증 정정):
  - `Effects/LastRunSystem.cs` — IncomingDamage 즉시 기록인데 `DamageApplicationSystem` 대비 무순서. 동일 계약의 `HeatAccrualSystem`처럼 `[UpdateBefore(DamageApplicationSystem)]` 핀
  - `Effects/DotApplySystem.cs` — IncomingDamage 기록(라인 73·80·109·125)인데 현재 핀(`[UpdateAfter(CcApplySystem)]`·`[UpdateBefore(CcDecaySystem)]`)은 `DamageApplicationSystem` 대비 전이 순서를 만들지 못한다. ⚠ 초안이 지목했던 `EffectTickSystem` 은 **오지목** — IncomingDamage 를 쓰지 않고(캐리어 TTL 전용), 그 파일의 ModifierApplySystem 대비 무순서는 의도적이다(`EffectTickSystem.cs:47-49` "[UpdateAfter] 를 얹지 말 것" 코드 주석). **건드리지 않는다.** (dot-effect-extraction 으로 DoT 가 분리될 때 스펙 문구가 못 따라온 것)
  - `Combat/Projectile/ProjectileHitSystem.cs` — **캡처 후 실사 추가 발견**: 착탄 IncomingDamage 기록 5곳(라인 168·226·260·377·524)에 소비자(`DamageApplicationSystem`) 대비 선언 0 — LastRun/DotApply 와 같은 계약이므로 함께 핀
  - `Combat/Projectile/ProjectileMoveSystem.cs` — `MovementSystem` 대비 무순서(호밍이 이동 후 최신 위치를 읽어야 함)
  - `Combat/BossPeriodicTriggerSystem.cs` — 자기 순서 선언 없음. 단 `ProjectileEmitterSystem` 이 `[UpdateAfter(BossPeriodicTriggerSystem)]` 로 역방향 핀 중이라 완전 무순서는 아님
  - 모디파이어 클러스터: Stat/Stack 모디파이어 큐 생산자는 **11개**이고 그중 **9개**가 `ModifierApplySystem` 대비 미선언 — 덤프된 현행 순서대로 핀. 나머지 2개는 이미 순서가 있어 **핀 대상 아님**: `AllyBuffFieldSystem`(명시 `[UpdateBefore(ModifierApplySystem)]`) · `StackModifierTickSystem`(ModifierStatsAggregate 경유 전이 핀 = **의도된 1프레임 지연**, 헤더 주석 명시 — "고치지" 말 것)
  - (참고: `MovementSystem` 자체는 12개 시스템이 상대 핀하고 있어 무어트리뷰트여도 결정됨 — 건드리지 않음. 8 Before / 4 After)

## 구현

1. 덤프 유틸 작성 → Play 진입 1회, 유효 총순서를 이 spec 폴더에 `order-capture.md`로 기록 (틱 파이프라인 명세의 입력).
2. 덤프 순서를 기준으로 위 미선언 지점에 `[UpdateBefore/UpdateAfter]` 명시 — **현행 유효 순서를 그대로 고정**하는 것이 목적이며 순서를 "고치지" 않는다. 재배치 판단은 M1 설계의 몫.
3. 핀 후 재덤프 → 순서 무변 확인.

## 완료 기준

- compile 통과, 핀 전/후 덤프 순서 동일 (행동 변화 0).
- `order-capture.md`에 44개 시스템 총순서 + 어느 지점이 신규 핀인지 기록됨.
- Play smoke: 전투 1판 정상 진행, 콘솔 에러 0.

> 진행 기록 2026-08-03: 덤프 유틸(`SimOrderDumpMenu` + 1회성 `SimOrderCaptureBootstrap`) 구현,
> batch 자동 Play 로 캡처. 핀 13건/파일 12개 적용 후 재덤프 — **순서 diff 0 · 컴파일 에러 0**.
> 완료 확인 2026-08-04: 후속 unit 2·4의 자동 Play 하네스(7개 시나리오 × 2회)가 전투 구동 smoke를 상위 호환으로 충족했고 콘솔 오류 0을 확인했다. 완료 커밋 `8795ac3c`.
