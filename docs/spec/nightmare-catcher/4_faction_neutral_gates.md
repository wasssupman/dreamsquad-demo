# 4 — 보스 식별 + 진영 게이트 (렌즈 B 대상)

## 목적

보스를 적 중에서 식별하고, MVP 두 메커닉의 발동/페이로드가 올바른 진영을 겨냥하게 한다. **정직한 스코프 정교화**: MVP 트리거(PeriodicTimer/HealthThreshold)는 신규 kind → **신규 arm** 이라, 기존 defender 게이트 5곳을 "여는" 게 아니라 신규 arm 을 태생적 중립으로 짓는다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Combat/BossTag.cs` (마커)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` (TileAoe victim 풀 진영 파라미터화)
- `ProjectileSpawnRequest`/`ProjectileState` (target-faction 플래그 — unit 1 의 `owner` 필드와 같은 구조체, 조율)

## 구현

### BossTag — 보스 식별
- 적(`AttackUnitTag`) 중 보스 마커. 스폰 시 authoring 으로 세팅(unit 5). ThreatTable/나이트매어 슬롯 부착 대상 판별.

### 진영 중립 원칙 (신규 arm)
- MVP 두 트리거의 **신규 arm**(BossPeriodicTriggerSystem, BossHealthThresholdSystem — unit 5)은 `DcTriggerSlot`/`ThreatTable` **버퍼 존재**로 게이트한다. `DefenderUnitTag` 참조 **금지**. → 진영 무관, 슬롯 가진 엔티티면 누구든(보스·나무·defender) 발동.

### 기존 arm 게이트 개방 = MVP 범위 밖 (지연)
아래 defender 게이트는 **그대로 둔다**(MVP 두 트리거가 안 씀):
- AttackN arm — `AttackSystem.cs:651`
- ProjectileBounce inject — `AttackSystem.cs:364`
- OnDamagedN — `DamageApplicationSystem.cs:~142` (rev 2: awakening-hand AwakeningReward lookup +4줄 드리프트)
- OnDeath — `UnitLifecycleSystem.cs:73`

보스가 이 트리거(AttackN 등)를 실제로 쓸 때 `isDefender→hasSlot` 완화. design.md D2 의 "5 게이트 완화"는 일반 메커니즘 서술이고, **이 MVP 는 신규 트리거라 실제로 건드리는 진영 게이트는 페이로드 1곳뿐**.

### 페이로드 진영 게이트 (실제 변경, HIGH-1)
- AreaBarrage 의 TileAoe victim 풀 `ProjectileHitSystem` `WithAll<AttackUnitTag>`(`ProjectileHitSystem.cs:59`) → **target-faction 파라미터화**. 투사체가 `targetFaction` 플래그를 싣고(스폰 시 세팅), 착탄 arm 이 플래그에 따라 `AttackUnitTag`(적) 또는 `DefenderUnitTag`(방어유닛) 풀 순회.
- **기본값 = enemy** → 플레이어 Meteor/기존 투사체 무회귀(N3). 보스 폭격만 defender 플래그.

## 완료 기준

- [ ] `BossTag` 컴파일. 신규 arm 이 slot/threat 버퍼 존재로만 게이트(`DefenderUnitTag` 미참조) — 코드 검증.
- [ ] AreaBarrage 착탄이 defender 풀, 플레이어 Meteor 는 enemy 풀(플래그 기본값) — 진영 분기 무회귀.
- [ ] 기존 arm 게이트(AttackN/OnDamagedN/OnDeath/bounce) **미개방** 확인 — 스코프 규율.
- [ ] (렌즈 B) 진영 파라미터화가 ProjectileHitSystem 의 맥락 경계/Burst 를 안 깨는지 ecs-reviewer.
