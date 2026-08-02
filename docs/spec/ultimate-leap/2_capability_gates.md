# 2 — 피격·타겟팅 완전 차단

## 목적

`UltimateLeapState` 존재 = 판 밖 존재. 새 타겟팅에서 제외하고, 이미 들어온 피해도 버린다.
공격·이동 잠금은 unit 1 이 붙인 `LeapFlight` 가 이미 담당 — 이 유닛은 **들어오는 축**만.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — 피해 드랍
- 타겟 후보 수집 지점 전수 — `AttackSystem`(방어유닛 타겟팅)·투사체 재조준(bounce/pierce 후보 풀)
  등. `NearestTargeting` 의 "호출부 책임" 주석 규약대로 **후보를 만드는 쪽**에서 제외한다

## 구현

### 피해 드랍 (DamageApplicationSystem)

`UltimateLeapState` 보유 유닛의 `IncomingDamage` 버퍼를 **처리하지 않고 비운다(Clear)**.

- **스킵이 아니라 드랍이어야 한다** — 스킵만 하면 버퍼에 2초치 피해가 적립됐다가 착지 프레임에
  몰아서 터진다(README 계약 3). DoT 틱·잔여 투사체 히트도 같은 버퍼로 들어오므로 한 지점 드랍이
  전부를 커버한다.
- `PendingDeployment` 처럼 쿼리 `WithNone` 으로 빼면 버퍼가 적립된다 — **쿼리 제외가 아니라
  루프 안 드랍**이다. 이 차이를 코드 주석에 남긴다.

### 타겟팅 제외

- 후보 수집 지점을 grep 으로 전수 확인하는 것이 완료 기준이다: `NearestTargeting` 호출부 ·
  `AttackSystem` 방어유닛 타겟 후보 · 투사체 재조준 후보 풀(attack-decoupling) · 드림캐쳐 타일
  조준은 타일 기준이라 빈 타일 히트로 자연 처리(제외 불필요 — 확인만).
- 각 지점에서 `ultimateLookup.HasComponent(candidate)` 면 후보에서 제외. fail-open(컴포넌트
  부재 = 후보 유지).

### anti-계약 재확인

`LeapFlight` 는 여기 어디에도 등장하지 않는다 — 일반 도약은 계속 피격·타겟팅된다.

## 완료 기준

- compile 클린 · EditMode 무회귀
- 타겟 후보 수집 지점 전수 목록이 이 문서 하단에 기재됨(구현 중 확정) — grep 계약
- (Play 검증은 unit 5 에서: 이탈 중 방어유닛이 타겟을 다른 적으로 옮기고, 보스 체력이 2초간
  변하지 않는다)
