# 0 — knockupOnHitSec: 전 히트 대상 Stun enqueue

## 목적

히트한 **모든** 대상에게 짧은 Stun 을 거는 히트 CC 필드를 신설한다. 이 spec 의 유일한 시뮬 코드.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `knockupOnHitSec` 필드
- `Assets/_Project/Scripts/Battle/Combat/DefenderCcData.cs` — 대응 필드 (Combat 소유 컴포넌트)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateDefenderEntity` 베이크 1필드
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — RESOLVE 히트 루프에 enqueue 분기
- `Assets/_Project/Tests/EditMode/` — 히트 CC 케이스 (sleepOnHit 테스트 파일 위치를 따른다)

## 구현

1. 필드 신설 + `DefenderCcData` 베이크 (sleepOnHitSec 선례 그대로).
2. RESOLVE 에서 `knockupOnHitSec > 0` 이면 **hitTargets 전원**에게
   `EnemyCcEvent { kind = Stun, remainingTime = knockupOnHitSec }` enqueue.
   - 위치: 기존 outputs 루프와 같은 히트 확정 지점 — 가디언 어그로 enqueue(`hitTargets` 순회,
     AttackSystem:1200 부근)와 같은 스코프를 따른다.
   - **sleepOnHitSec(주 타겟 1체) 경로는 불변** — 두 필드 주석에 스코프 차이 명시(계약 2).
3. Stun 병합은 기존 계약(remainingTime=max) 그대로 — 다중 말파이트 중첩 시 자연 상한.
4. EditMode 케이스: attackTargetCount 3 유닛이 3체 히트 시 3체 모두 Stun 수신 /
   knockupOnHitSec 0 이면 enqueue 없음 / sleepOnHitSec 유닛의 기존 테스트 무회귀.

## 완료 기준

- [ ] compile clean + 신규 EditMode 케이스 green + sleep-fighter 기존 테스트 무회귀
- [ ] (unit 3 이후 Play 에서) 히트된 다수 적이 동시에 잠깐 멈추는지 — 연출 없이 심 선행 확인 가능
