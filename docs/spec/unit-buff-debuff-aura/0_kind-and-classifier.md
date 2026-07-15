# 0 — StatusFxKind 2종 + 순수 ModifierAuraClassifier

## 목적

버프/디버프 오라의 **판정 토대**를 만든다: 상태 kind 2종(append-only) 추가 + `ModifierStats` 를 순
버프/디버프 두 bool 로 분류하는 순수 함수 + EditMode 회귀 테스트. 이 단계엔 reconcile/VFX 없음(선언·계산만).

## 변경 대상

- `Assets/_Project/Scripts/Data/StatusFxKind.cs` — `Buffed`, `Debuffed` append
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierAuraClassifier.cs` — **신규** 순수 static
- `Assets/_Project/Tests/EditMode/ModifierAuraClassifierTests.cs` — **신규** EditMode

## 구현

1. **enum**: `StatusFxKind { Aggro=0, Sleep=1, Buffed=2, Debuffed=3 }` (append-only 계약).
2. **순수 classifier** — `Battle/Effects/Modifiers` 에 배치(`ModifierStats` 의미의 도메인 지식이 여기 상주,
   `ModifierMath` 순수 함수 선례와 동거). ECS 타입 의존 없음(`in ModifierStats` = POD unmanaged struct,
   EntityManager 불요). Burst 불요(프레젠테이션 호출).
   ```csharp
   public static class ModifierAuraClassifier
   {
       public const float Eps = 1e-4f;
       // buffed = 어느 스탯이든 base 보다 유리, debuffed = 어느 스탯이든 base 보다 불리 (독립).
       public static void Classify(in ModifierStats s, out bool buffed, out bool debuffed)
       {
           buffed =
               s.damageMul      > 1f + Eps ||
               s.attackSpeedMul > 1f + Eps ||
               s.moveSpeedMul   > 1f + Eps ||
               s.dmgTakenMul    < 1f - Eps ||   // 역방향: 피해 감소 = 버프
               s.regenPerSec    > Eps;          // base 0, 비음수 클램프 → 버프 전용
           debuffed =
               s.damageMul      < 1f - Eps ||
               s.attackSpeedMul < 1f - Eps ||
               s.moveSpeedMul   < 1f - Eps ||
               s.dmgTakenMul    > 1f + Eps;      // 역방향: 피해 증가 = 디버프
           // regenPerSec: 디버프 방향 없음(clamp≥0). damageVsCcMul: 조건부 → 판정 제외.
       }
   }
   ```
3. **테스트** (필수 케이스, 리뷰 H1/M2 반영):
   - base(전부 identity) → buffed=false, debuffed=false
   - `damageMul=1.3` → buffed=true, debuffed=false
   - `dmgTakenMul=0.87`(eHP 버프) → **buffed=true**, debuffed=false (역방향 회귀 가드)
   - `dmgTakenMul=1.4`(타일 디버프) → buffed=false, **debuffed=true**
   - `moveSpeedMul=0.4`(슬로우) → debuffed=true
   - `damageMul=1.3` + `moveSpeedMul=0.4` → buffed=true **AND** debuffed=true (동시)
   - `regenPerSec=5` → buffed=true (regen 버프 전용)
   - `damageVsCcMul=2`, 나머지 base → buffed=false, debuffed=false (제외 가드)
   - epsilon: `damageMul=1.00001`(< 1+ε) → buffed=false (부동소수 노이즈 무시)

## 완료 기준

- [x] 컴파일 클린 (`Data`·`Battle.Effects` 어셈블리). 기존 Aggro/Sleep 직렬화 무손실(append-only)
- [x] `ModifierAuraClassifierTests` EditMode 전 케이스 green — **9/9 통과** (2026-07-15)
- [x] 이 단계엔 시각 변화 없음(reconcile 은 unit 1) — Play 검증 대상 아님

사용자 확인 2026-07-15. 커밋: (본 커밋)
