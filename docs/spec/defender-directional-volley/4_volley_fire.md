# 4. ECS 다연발 — 스프레드 동프레임 N발 + 버스트 시간차 연발

## 목적

1트리거=1발 고정을 깬다. RESOLVE 가 SO 파라미터(shotCount/shotIntervalSec/spreadAngleDeg)에 따라 캐리어 엔티티 N개를 스폰한다 — 동프레임(스프레드) 또는 시간차(버스트, sim 시간 틱).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/VolleyFireState.cs` (신규)
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (유닛 스폰 시 SO→VolleyFireState 사전 부착)

## 구현

**`VolleyFireState` 전용 컴포넌트** (rev1 — 초안의 "AttackState 확장" 대체):
- config: `shotCount / shotIntervalSec / spreadAngleDeg`, runtime: `burstRemaining / burstTimer / template`.
- **AttackState 를 늘리지 않는 이유**: 그 컴포넌트는 적까지 공유한다 — 볼리와 무관한 전 유닛이 6필드를 짊어질 이유가 없다. 다연발 유닛(shotCount > 1)만 이 상태를 진다.
- **template = 트리거 첫 발의 request 통째 스냅샷**: 후속 발은 이를 verbatim 복사하고 발 인덱스로 재조준만 한다 → 버스트 2~10발이 1발과 **바이트 동일**(damage/owner/prio/heavy 전부). 버스트 도중 카드가 만료돼도 7번 발이 1번 발과 달라질 수 없다.
- **스폰 시 사전 부착**(BattleBridge, IncomingHeal 선례) → 발사마다 구조 변경 0. shotCount ≤ 1 이면 미부착 = 현행 단발 경로 무변화.

**RESOLVE 변경** (unit 3 의 방향 발사 지점):
- **0 번 발은 언제나 지금, 공격자 본인 request 로** — 단발 유닛 경로가 다연발 도입 전과 바이트 동일하게 남는다. **규약**: 첫 발이 t=0 이어야 버스트 span 이 (shotCount−1)×interval 이 되어 `CooldownAfterVolley` 모델과 정합(리뷰 LOW-1 고정).
- `shotIntervalSec == 0` (확산형): 1..N−1 발도 같은 프레임에 캐리어로. request 는 엔티티당 1개뿐이라 발마다 캐리어가 필요(카드 캐리어 선례).
- `shotIntervalSec > 0` (버스트형): `burstRemaining = shotCount − 1`, `burstTimer = interval`, `template` 스냅샷. 쿨다운은 `VolleyMath.CooldownAfterVolley(현재 cooldownRemaining, shots, interval)` 로 연장 — 버스트 완주 후 기산(계약 8).

**버스트 틱** (AttackSystem 기존 루프 내, 새 시스템 없음):
- **START/RESOLVE 앞에 배치**: 트리거 프레임엔 `burstRemaining` 이 아직 0 이라 no-op → 0 번 발과 1 번 발 간격이 정확히 interval(뒤에 두면 트리거 프레임에 dt 를 한 번 먹어 한 프레임 일찍 발사).
- `VolleyMath.TickBurst` 반환 발수만큼 캐리어 스폰. 발 인덱스 = `shotCount − 남은수` 로 확산각 분배가 0 번 발과 이어진다. 느린 프레임은 한 프레임에 여러 발 소화.
- 쿨다운/타겟 게이트 밖 — 레인에서 적이 사라져도 완주(계약 8). 공격자 사망 시 컴포넌트째 사라져 자연 중단.
- 버스트×스프레드 조합: 시간차 발도 발 인덱스 기준 동일 각 분배(머신건은 spread 0 이라 전탄 facing 직진).

**오디오 주의**: 캐리어 발은 `DefenderUnitTag` 가 없어 drain 의 발사 SFX 게이트에 걸리지 않는다 → 볼리당 SFX 1회(0 번 발). 머신건 연사음은 battle-audio 스코프 — 후속 후보.

**스코프 노트**: 다연발 필드는 SO 상 모든 유닛에 열리지만, 이번 spec 의 e2e 검증은 Directional 투사체 조합만. Homing×버스트 등 조합 검증은 후속 후보(README).

## 완료 기준

- [ ] compile + 기존 테스트 회귀 없음. TickBurst/CooldownAfterVolley/SpreadDirection 은 unit 0 테스트가 커버
- [ ] execute_code 스모크: shotIntervalSec 0.1·shotCount 10 유닛이 트리거당 10발을 0.1s 간격으로 발사(슬로우모션 중 간격도 함께 늘어짐 확인), spreadAngleDeg 30·shotCount 3 유닛이 동프레임 부채꼴 3발
- [ ] 쿨다운이 버스트 종료 후 기산됨을 로그로 확인
