# 4. ECS 다연발 — 스프레드 동프레임 N발 + 버스트 시간차 연발

## 목적

1트리거=1발 고정을 깬다. RESOLVE 가 SO 파라미터(shotCount/shotIntervalSec/spreadAngleDeg)에 따라 캐리어 엔티티 N개를 스폰한다 — 동프레임(스프레드) 또는 시간차(버스트, sim 시간 틱).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackState.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (유닛 스폰 시 SO→AttackState 복사)

## 구현

**AttackState 확장** (Combat 소유 runtime 필드 — attack-hit-delay 의 hitDelayRemaining 선례):
- config: `int shotCount; float shotIntervalSec; float spreadAngleDeg;` — 유닛 스폰 시 `DefenderUnitData` 에서 복사(BattleBridge).
- runtime: `int burstRemaining; float burstShotTimer;` — 진행 중 버스트 상태.

**RESOLVE 변경** (facing 발사 경로 — unit 3 의 방향 캐리어 스폰 지점):
- `shotIntervalSec == 0`: 같은 프레임에 shotCount 개 캐리어 스폰. 발마다 `VolleyMath.SpreadDirection(facing, i, shotCount, spreadAngleDeg)` 방향.
- `shotIntervalSec > 0`(버스트): 첫 발 즉시 스폰 + `burstRemaining = shotCount − 1`, `burstShotTimer = shotIntervalSec` 세팅.
- 쿨다운은 `VolleyMath.CooldownAfterVolley` 로 세팅 — 버스트 완주 후 기산(계약 8).

**버스트 틱** (AttackSystem 기존 프레임 업데이트 내, 새 시스템 없음):
- `burstRemaining > 0` 인 공격자는 `VolleyMath.TickBurst(dt, …)` 가 반환한 발수만큼 캐리어 스폰. 레인에서 적이 사라져도 완주(계약 8). 느린 프레임은 한 프레임에 여러 발 소화.
- 버스트×스프레드 조합(interval>0 && spread>0): 버스트의 i번째 발도 `SpreadDirection(facing, i, shotCount, spreadAngleDeg)` 를 적용한다 — 동프레임/시간차 모두 발 인덱스 기준 동일 각 분배(머신건은 spread 0 이라 전탄 facing 직진).
- 공격자 사망 시 잔여 버스트는 엔티티 소멸과 함께 자연 중단(별도 처리 없음).

**캐리어 스폰 헬퍼**: RESOLVE 와 버스트 틱이 같은 "방향 캐리어 1개 스폰" 코드를 쓰므로 AttackSystem 내부 static 헬퍼로 공유(2+ 호출처 — 추출 기준 충족).

**스코프 노트**: 다연발 필드는 SO 상 모든 유닛에 열리지만, 이번 spec 의 e2e 검증은 Directional 투사체 조합만. Homing×버스트 등 조합 검증은 후속 후보(README).

## 완료 기준

- [ ] compile + 기존 테스트 회귀 없음. TickBurst/CooldownAfterVolley/SpreadDirection 은 unit 0 테스트가 커버
- [ ] execute_code 스모크: shotIntervalSec 0.1·shotCount 10 유닛이 트리거당 10발을 0.1s 간격으로 발사(슬로우모션 중 간격도 함께 늘어짐 확인), spreadAngleDeg 30·shotCount 3 유닛이 동프레임 부채꼴 3발
- [ ] 쿨다운이 버스트 종료 후 기산됨을 로그로 확인
