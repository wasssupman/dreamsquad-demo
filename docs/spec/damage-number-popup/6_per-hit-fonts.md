# 6 — 히트별 개별 데미지 폰트 (rev)

## 목적

같은 적이 한 프레임에 여러 히트를 받아도(기본 공격 + 드림캐쳐 트리거 투사체 등) 데미지가 **합산되어 한 숫자로 뜨던 것**을, **히트마다 별도 폰트**로 바꾼다. 사용자 확정 스펙(2026-07-09): 각 히트는 자기 데미지 폰트를 가진다.

## 배경

기존 `DamageApplicationSystem` 은 엔티티의 `IncomingDamage` 버퍼를 프레임당 합산(`totalDamage`)해 `DamageNumberEvent` **1개**만 enqueue 했다. 드림캐쳐 트리거(`Archer` 기본 화살 + 5회째 별도 투사체)가 같은 프레임에 같은 적을 맞히면 두 데미지가 하나로 합쳐 보였다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs`
- `Assets/_Project/Scripts/Battle/Units/DamageNumberEvent.cs` (주석만 — amount 의미가 "프레임 합계" → "히트 1개")

## 구현

- 합산 루프는 **Health 차감용으로만** 유지(총 데미지로 HP 감소 — 게임플레이 무변경).
- `damageBuffer.Clear()` 를 합산 직후가 아니라 **폰트 enqueue 이후로 이동**(버퍼를 한 번 더 읽어야 하므로).
- 폰트 enqueue 를 버퍼 엔트리 루프로 교체: 엔트리마다 `amount = entry.amount * dmgTakenMul` 로 `DamageNumberEvent` 1개. `amount <= 0` 은 skip.
- `hpRatio` 는 프레임 정산 후 최종 비율(`Health.ComputeRatio(newHp, maxHp)`)을 모든 엔트리에 동일 적용 — 히트 마이크로바는 어차피 최종 비율만 필요.

## 계약 갱신

- README "enqueue 위치" 계약: enqueue 단위 = 히트(버퍼 엔트리) 1개, 프레임 합산 아님.
- `dmgTakenMul`(받는 피해 감소, 드림캐쳐 HP 프록시 포함)은 **엔트리마다** 곱해 표시값이 실제 적용값과 일치.

## 완료 기준

- [x] 컴파일 통과
- [x] 같은 프레임 다중 히트가 별도 폰트로 표시(드림캐쳐 5회째: 기본 화살 + 샤드 20 이 두 숫자) — Play 육안
- [x] Health 차감 무회귀(합계 그대로), 겹침은 `damage-number-visual-upgrade` 배치 격자가 흡수
- 완료 확인: 2026-07-09 — 개별 폰트 Play 확인, 사용자 승인. 이 문서와 동일 커밋.
