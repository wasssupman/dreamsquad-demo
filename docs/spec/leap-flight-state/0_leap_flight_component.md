# 0 — `LeapFlight` 태그 + 시뮬 게이트

## 목적

"도약 비행 중" 이라는 사실을 sim 이 알게 하고, 공격·자기주도 이동 두 소비 지점을 잠근다.
피격은 건드리지 않는다(README 계약 2).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/LeapFlight.cs` — **신규** 태그 컴포넌트
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 공격자 쿼리 2곳
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — 자기주도 이동 게이트

## 구현

```csharp
namespace Wassup.Battle.Combat
{
    // leap-flight-state unit 0 — "도약 비행 중" 사실. 공격·자기주도 이동 불가, **피격 가능**.
    // ⚠ anti-계약: DamageApplicationSystem·타겟 후보 수집에 절대 넣지 않는다 — 넣으면 비행 중 무적.
    // 쓰기: 일반 도약 = BattleBridge(뷰 시계 소유, 창구 예외) / 궁극기 = Combat 시스템(ultimate-leap).
    public struct LeapFlight : IComponentData { }
}
```

- **AttackSystem**: 공격자 순회 쿼리의 기존 `.WithNone<PendingDeployment>()` 자리 2곳
  (`:43`·`:223` 부근)에 `LeapFlight` 를 나란히 추가.
- **MovementSystem**: `locked` 계산(`MovementSystem.cs:67`, `CcActionLock.IsLocked`)에 OR 로 합류 —
  `locked = cc잠금 || leapLookup.HasComponent(entity)`. Sleep/Stun 과 같은 규약이라 같은 변수에
  접는 것이 맞다: 자기주도 이동(flow/chase/hunting)만 멈추고 외력(impulse/tornado/portal)은 유지.
- 타겟 **후보** 수집(`NearestTargeting` 호출부들)은 **건드리지 않는다** — 비행 중 보스는 계속
  타겟팅·피격된다.

## 완료 기준

- compile 클린 · EditMode 무회귀 (신규 순수 함수 없음 — 쿼리 필터라 EditMode 신규 테스트 대상 아님)
- anti-계약 주석이 컴포넌트 정의에 존재
- 이 시점에는 태그를 붙이는 코드가 없어 **런타임 동작 무변경** (unit 1 이 붙인다)
