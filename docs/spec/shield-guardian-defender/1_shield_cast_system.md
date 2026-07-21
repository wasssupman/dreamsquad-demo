# 1 — ShieldCast (SO → 베이크 → Effects 주기 캐스트)

## 목적

A초마다 공격범위 내 아군 C명을 필터로 골라 실드 B를 부여하는 생산자를 Effects 에 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `[Header("Shield Cast")] float shieldCastCooldown`(A, 0=비활성) · `float shieldAmount`(B) · `int shieldTargetCount`(C) · `ShieldTargetFilter shieldTargetFilter`
- 신규 `Assets/_Project/Scripts/Data/ShieldTargetFilter.cs` — `enum ShieldTargetFilter : byte { Self, All, MinHealth }`
- 신규 `Assets/_Project/Scripts/Battle/Effects/ShieldCastState.cs` — range(=attackRange 복사)/cooldownDuration/cooldownRemaining/amount/targetCount/filter
- 신규 `Assets/_Project/Scripts/Battle/Effects/ShieldCastSystem.cs` — ISystem, BattleSimGroup
- 신규 `Assets/_Project/Scripts/Battle/Effects/ShieldTargeting.cs` — 순수 선별 함수
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `shieldCastCooldown > 0` 시 `ShieldCastState` 베이크 (HazardCastState 베이크 옆)
- 신규 EditMode 테스트 `Assets/_Project/Tests/EditMode/ShieldTargetingTests.cs`

## 구현

- `ShieldCastSystem`: `ShieldCastState` 보유 defender 마다 cooldown tick → 도달 시
  1. 후보 수집: 생존(`DeadTag` 없음)·배치완료(`PendingDeployment` 없음) 아군 defender 중 거리 ≤ range. **자신 포함**(계약 6).
  2. `ShieldTargeting.Select`(순수): `Self`=자신만 / `All`=거리 오름차순 C / `MinHealth`=**유효HP 비율 `(HP+실드합)/maxHP` 오름차순** C(계약 6 — 만충 대상 no-op 재부여 방지; 후보의 실드합은 `ShieldSlot` 버퍼 RO 합산으로 입력). 동률은 인덱스 순(결정론).
  3. 선택 대상의 `IncomingShield` 버퍼에 `{ source = 캐스터 entity, amount }` append (BufferLookup RW — 맥락 간 Buffer 통신, 계약 1). 출처별 병합(같은 출처 max·교차 출처 합산)은 Units drain 이 담당.
- action-lock 게이트 없음(계약 7 — HazardCast 와 동급). 캐스터 사망 시 상태 소멸(엔티티 파괴에 동승).
- 순수 함수 시그니처는 plain 배열/스팬 입력(엔티티/lookup 비의존)으로 — EditMode 에서 아키텍처 무관 검증(제약 10).

## 완료 기준

- [ ] compile 클린.
- [ ] `ShieldTargetingTests` 그린: Self / All 거리정렬+C컷 / MinHealth 유효HP정렬+C컷 / **실드 만충 대상이 무실드 저HP 대상에 밀림** / 후보<C / 자신 포함 / 결정론 동률.
- [ ] 기존 EditMode 전체 그린.
