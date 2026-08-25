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

## unit 0 에서 박은 핀 (2026-08-22)

캡처 시점 시스템 수는 **48**(스펙 작성 08-03 의 44 에서 증가), 무순서 **8 → 3**.
전부 **현행 위치를 그대로 고정**하는 핀이며 순서를 고친 것은 하나도 없다.

| 시스템 | 박은 핀 | 근거 |
|---|---|---|
| `LastRunSystem` | `UpdateBefore(DamageApplicationSystem)` | IncomingDamage 인박스 append — 같은 계약의 `HeatAccrualSystem` 과 동형 |
| `EffectTickSystem` | `UpdateAfter(MovementSystem)` + `UpdateBefore(AttackSystem)` | 실측 위치 고정 (아래 ⚠) |
| `ProjectileMoveSystem` | `UpdateAfter(MovementSystem)` | 호밍이 이동 후 최신 위치를 읽어야 한다 |
| `FatigueAccrualSystem` | `UpdateAfter(ModifierApplySystem)` | 스택 생산자이며 소비자보다 뒤 = 다음 프레임 반영(현행) |
| `ResignationThresholdSystem` | `UpdateBefore(StackModifierTickSystem)` | 사직서 스택을 읽어 임계 소모 |
| `ZoneApplySystem` | `UpdateBefore(ModifierApplySystem)` | 소비자보다 앞선 생산자 셋 중 하나(같은 프레임 반영) |
| `BossPeriodicTriggerSystem` | `UpdateBefore(ModifierApplySystem)` | 〃 |
| `ModifierApplySystem` | `UpdateBefore(MovementSystem)` | 아래 ⚠⚠ |

**핀하지 않은 무순서 3개 (의도)**
- `MovementSystem` — 15개 시스템이 이것을 기준으로 핀하고 있어 무어트리뷰트여도 결정된다(스펙 지시).
- `PickupSpawnSystem` — `PickupConsumeSystem` 이 `UpdateAfter(this)` 라 **의미 있는 상대 순서는 이미 고정**. 절대 위치는 관측에 영향 없음.
- `HitFlashSystem` — 순수 프레젠테이션(자기 `LocalTransform.Scale` 만 씀). 위치가 sim 의미를 바꾸지 않는다.

### 캡처가 드러낸 사실 2건 (고치지 않고 기록만)

⚠ **`EffectTickSystem` 의 주석이 실제와 반대다.** 파일 주석은 「`MovementSystem` + `AttackSystem` **뒤**에 돌아 이번 프레임 소비자가 tick 전 값을 본다」고 적혀 있으나, 실측은 `AttackSystem` **앞**이다(27 < 35). 주석이 아니라 실측을 박제했다 — 어느 쪽이 옳은지는 M1 의 판단이고, 지금 고치면 골든의 기준선이 무너진다.

⚠⚠ **모디파이어는 대부분 1프레임 지연된다.** 소비자 `ModifierApplySystem` 은 9번이고, 생산자 11개 중 **8개가 그 뒤**에 있다(`AttackSystem`·`DamageApplicationSystem`·`ProjectileHitSystem`·`HealthThresholdSystem`·`StackModifierTickSystem`·`DreamCocoonSystem`·`PickupConsumeSystem`·`FatigueAccrualSystem`). 즉 그들의 모디파이어는 **다음 프레임**에 반영된다. 같은 프레임에 반영되는 생산자는 셋뿐이다(`AllyBuffFieldSystem`·`ZoneApplySystem`·`BossPeriodicTriggerSystem`). 소비자를 `MovementSystem` 앞에 묶어 이 비대칭을 통째로 고정했다 — 소비자가 뒤로 밀리면 그 8개가 **조용히** 같은 프레임 반영으로 바뀐다.

## 완료 기준

- [x] compile 통과, 핀 전/후 덤프 순서 동일 (행동 변화 0) — 48개 전부 동일 위치 실측.
- [x] `order-capture.md`에 총순서 기록 + 신규 핀은 위 표에 기록(생성물은 손대지 않는다).
- [x] Play smoke: 전투 1판 정상 진행(적 15→4, 그룹 가동), 콘솔 에러 0.

확인 2026-08-22 · 덤프 유틸 `Assets/_Project/Editor/Battle/SimOrderDumpMenu.cs`
(`Wassup/Battle/Sim Order/Dump BattleSimGroup Order`, Play 중 실행).
