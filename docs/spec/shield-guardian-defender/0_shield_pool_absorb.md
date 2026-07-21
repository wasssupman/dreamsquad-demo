# 0 — ShieldPool + 흡수 (Units)

## 목적

실드 흡수 풀의 상태·산식·소비를 Units 맥락에 만든다. 생산자 없이도 독립 검증 가능한 토대.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Units/ShieldPool.cs` — `ShieldPool : IComponentData { float value; }` + 순수 static `ShieldMath.Absorb(float shield, float damage, out float remainingShield, out float pierced)`
- 신규 `Assets/_Project/Scripts/Battle/Units/IncomingShield.cs` — `IncomingShield : IBufferElementData { float amount; }` (IncomingHeal 동형: 매 프레임 drain + Clear)
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — 흡수 훅
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateDefenderEntity` 에서 전 defender 에 `ShieldPool(0)` + `IncomingShield` 버퍼 사전 부착 (IncomingHeal 선례 — ECB 구조변경 없이 append 하기 위함)
- 신규 EditMode 테스트 `Assets/_Project/Tests/EditMode/ShieldMathTests.cs`

## 구현

- `DamageApplicationSystem.OnUpdate` 루프 내 순서:
  1. `IncomingShield` drain(합산 후 Clear) → `ShieldPool.value = max(value, 프레임 최대 부여량)` — **max 갱신**(계약 4). 같은 프레임 다중 부여도 max.
  2. `totalDamage *= dmgTakenMul` (기존) **후** `ShieldMath.Absorb` 로 실드 먼저 소모, 관통분만 Health 차감.
  3. 이후 모든 기존 분기(wake-on-hit·데미지 넘버·가시갑옷·킬 귀속·DeadTag)는 **관통분(totalDamage 갱신값)** 기준 — 조건식 무변경(계약 3).
- 데미지 넘버의 per-hit 표시도 관통분 비례 배분: `hitShown = hitAmount × (관통 / 흡수전총량)` (0 이면 폰트 스킵 — 완전 흡수 히트 무표시). 단순 비례가 per-hit 정확 분배보다 계약(피격 아님)에 충분.
- 힐(pulseHeal/Regen)은 실드와 무관 — Health 만 회복(기존 그대로).
- `ShieldPool` 미보유 엔티티(적·기존 스폰)는 lookup 가드로 전 경로 무변경.

## 완료 기준

- [ ] compile 클린.
- [ ] `ShieldMathTests` 그린: 부분 흡수 / 완전 흡수(관통 0) / 실드 0(전량 관통) / 과잉 데미지 / max 갱신(잔량>B, 잔량<B).
- [ ] 기존 EditMode 전체 그린 (ShieldPool 미부여 경로 무회귀).
