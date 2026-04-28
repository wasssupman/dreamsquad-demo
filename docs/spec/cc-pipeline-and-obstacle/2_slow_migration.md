# Slow Migration to CcEffect Buffer

**작업 구분**: 2 (회귀 게이트)

## 목적

기존 `SlowEffect : IComponentData` 를 제거하고 `CcEffect` buffer 의 `kind = Slow` entry 로 흡수한다. 외부 호출자 (BattleBridge) 의 `EffectSpawner.ApplySlow` 시그니처는 *그대로* 유지한다. 마이그레이션 회귀가 본 spec 의 첫 번째 검증 질문 ("Slow 회귀 없이 통일이 안정적인가") 에 답하는 게이트.

## 변경 대상

- Delete: `Assets/_Project/Scripts/Battle/Effects/SlowEffect.cs`
- Modify: `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs`
- Modify: `Assets/_Project/Scripts/Battle/Effects/EffectTickSystem.cs`
- Modify: `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

## EffectSpawner 변경

- `ApplySlow(em, entity, duration, multiplier)` 시그니처 유지.
- 새 helper:
  ```csharp
  public static void ApplyCc(EntityManager em, Entity target, CcEffect effect);
  ```
  내부에서 `DynamicBuffer<CcEffect>` 가 없으면 추가, 같은 kind 가 있으면 merge (Unit 1 의 CcApplySystem 과 동일 정책), 아니면 add.
- `ApplySlow` 는 `ApplyCc` 위의 thin wrapper:
  ```csharp
  public static void ApplySlow(EntityManager em, Entity target, float duration, float multiplier)
      => ApplyCc(em, target, new CcEffect {
              kind = CcKind.Slow,
              scalar = multiplier,
              remainingTime = duration,
          });
  ```
- BattleBridge 측 호출자는 EntityManager 직접 접근이 가능하므로 즉시 buffer add/merge (큐 우회 1프레임 지연 방지).

## EffectTickSystem 변경

- Slow 관련 foreach 루프 (현재 36-43 행) 제거.
- DamageBoost / CooldownReduction / TornadoField / PortalLink 루프는 그대로 유지 (CC 패밀리가 아닌 defender buff / carrier).
- `RequireAnyForUpdate` 의 `SlowEffect` query 제거 (22 행).

## MovementSystem 변경

- `slowLookup` (현재 28 행) 제거.
- 적 entity 마다 `DynamicBuffer<CcEffect>` lookup. 없으면 multiplier = 1.
- buffer 순회하면서 `kind == CcKind.Slow` 인 entry 의 `scalar` 를 누적 곱.
  - 누적 정책: 여러 Slow entry 가 동시 존재하면 *곱*. (현재 merge 정책상 같은 kind 는 1개만 존재 → 행동 동일)
- 기존 91 행의 `slowLookup.HasComponent(entity) ? slowLookup[entity].multiplier : 1f` 를 buffer 누적 결과로 교체.
- Tornado pull (73-83 행) 과 Portal (45-56 행) 분기는 변경 없음.

## 회귀 검증 (PlayMode 필수)

- Slow 효과를 발동하는 디펜더 (또는 BattleBridge.CastSlow* 류) 를 사용하여 적 1마리에 효과 적용 → 적 속도가 multiplier 만큼 줄어드는지 확인.
- 효과 만료 후 속도 복귀 확인.
- 같은 wave seed 로 마이그레이션 전후 적 도달 시간 차이 < 5%.

## 완료 기준

- `SlowEffect.cs` 파일 부재. 코드베이스에 `SlowEffect` 식별자 0건 (`grep -r "SlowEffect" Assets/Scripts/`).
- 컴파일 + 단위테스트 통과.
- PlayMode 회귀 검증 사용자 확인 통과.
- 콘솔 에러/경고 0.
- 본 commit 전에 사용자 manual 확인 *반드시* 받고 다음 unit 진입.

완료: 2026-04-28 — 1638c77 (fix: a47e858) PlayMode 회귀 확인 통과
