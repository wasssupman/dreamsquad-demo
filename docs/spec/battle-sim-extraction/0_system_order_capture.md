# 0 — 유효 시스템 총순서 캡처 + 미선언 순서 핀

## 목적

`BattleSimGroup` 44개 시스템의 어트리뷰트 순서 그래프는 불완전하다 — 미선언 지점의 실행 순서를 Unity의 토폴로지 정렬 tie-break이 결정하고 있다. 이 순서가 곧 시뮬 의미론(같은 틱 소비·IncomingDamage 정산 시점)이므로, ① 러닝 월드의 **실제 유효 총순서를 덤프해 사실로 기록**하고 ② 미선언 지점을 어트리뷰트로 박제해, 이후 골든(unit 4)과 신 sim 틱 파이프라인(M1)이 흔들리지 않는 기준을 만든다.

## 변경 대상

- 신규: 순서 덤프 에디터 유틸 (예: `Assets/_Project/Editor/Battle/SimOrderDumpMenu.cs` — `BattleSimGroup`의 실행 순서를 로그/파일로)
- 미선언 순서 핀 (감사에서 식별된 지점):
  - `Effects/LastRunSystem.cs` — IncomingDamage 즉시 기록인데 `DamageApplicationSystem` 대비 무순서. 동일 계약의 `HeatAccrualSystem`처럼 `[UpdateBefore(DamageApplicationSystem)]` 핀
  - `Effects/EffectTickSystem.cs` — IncomingDamage 기록 vs `DamageApplicationSystem` 소비 순서 미선언
  - `Combat/Projectile/ProjectileMoveSystem.cs` — `MovementSystem` 대비 무순서(호밍이 이동 후 최신 위치를 읽어야 함)
  - `Combat/BossPeriodicTriggerSystem.cs` — 무순서
  - 모디파이어 클러스터: `ModifierApplySystem`과 9개 생산자 시스템 간 관계 — 덤프된 현행 순서대로 핀
  - (참고: `MovementSystem` 자체는 10개 시스템이 상대 핀하고 있어 무어트리뷰트여도 결정됨 — 건드리지 않음)

## 구현

1. 덤프 유틸 작성 → Play 진입 1회, 유효 총순서를 이 spec 폴더에 `order-capture.md`로 기록 (틱 파이프라인 명세의 입력).
2. 덤프 순서를 기준으로 위 미선언 지점에 `[UpdateBefore/UpdateAfter]` 명시 — **현행 유효 순서를 그대로 고정**하는 것이 목적이며 순서를 "고치지" 않는다. 재배치 판단은 M1 설계의 몫.
3. 핀 후 재덤프 → 순서 무변 확인.

## 완료 기준

- compile 통과, 핀 전/후 덤프 순서 동일 (행동 변화 0).
- `order-capture.md`에 44개 시스템 총순서 + 어느 지점이 신규 핀인지 기록됨.
- Play smoke: 전투 1판 정상 진행, 콘솔 에러 0.
