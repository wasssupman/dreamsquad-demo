# 1 — 방어유닛 규칙 5행

## 목적

이미 `DcMechanic` 규칙 경로를 타는 방어유닛 능력 5개를 concrete 로 옮긴다.
가장 싼 가족이다 — 어휘가 이미 같고 bake 도 이미 진영 중립이다.

## 변경 대상

- `Assets/_Project/Data/Abilities/` — SkyStrike · Taunt · AreaShield · OnPlaceBlast · BombMan
- `Assets/_Project/Scripts/Skills/Concrete/`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BakeUnitMechanics` 가 `skillId` 를 굽는다

## 구현

1. 5행 전부 `DcTriggerKind.OnPlace` 다(`DefenderTriggerArmed` 가 그것만 연다).
2. **`BakeUnitMechanics(hostIsEnemy:…)` 는 이미 진영 중립이다** — 적/방어유닛 공용이고
   `hostIsEnemy` 가 `BuildPatternTemplate` 의 `targetFaction` 을 파생시킨다.
   ⚠ 빠뜨리면 **방어유닛이 쏜 패턴이 방어유닛을 때린다.** concrete 이전 후에도 이 파생이
   유지되는지 확인한다 — 토대 unit 2b 의 `Opponents(caster)` 가 그 자리를 대신해야 한다.
3. **`EmitProjectilePattern` arm 은 공용 헬퍼로 뽑는다.** 오늘 `AttackSystem` 과
   `BossPeriodicTriggerSystem` **두 곳에 사본**이 있다(`skill-fire-dispatch` 계약 5 가
   「세 번째를 만들지 말라」고 경고). concrete 화가 그 통합의 자리다.
4. 이 가족이 끝나면 **보스 스킬과 방어유닛 스킬이 같은 `ISkill` 목록에 섞인다** —
   README 부수 질문이 여기서 처음 참이 된다.

## 진행 상태 (2026-08-25)

**5행 중 1행이 이미 이전됐다** — 의도한 게 아니라 구조 덕이다.

`Ability_AreaShield_ShieldShuttle` 은 `OnPlace × GrantShield` 인데, **`OnPlace` 슬롯도
`BossPeriodicTriggerSystem` 이 소비한다**(payload arm 을 주기와 공유하는 것이 그 시스템에
얹은 이유다 — 브리지에 실행부를 두면 `EmitProjectilePattern` 의 세 번째 사본이 된다).
그래서 unit 0 이 `GrantShield` 를 이전할 때 이 행도 같이 넘어갔고, `AbilityAreaShieldTest` 가
초록인 것이 그 증거다.

**함의**: 이 spec 의 이전 단위는 **payload** 이지 트리거나 가족이 아니다. 한 payload 를
옮기면 그것을 쓰는 모든 행이 같이 간다 — census 의 「행」은 검수 단위이고 작업 단위가 아니다.

남은 4행:

| 에셋 | payload | 상태 |
|---|---|---|
| `Ability_Taunt_Bastion` | `AreaTaunt` | 미이전 |
| `Ability_SkyStrike_Cannon` | `EmitProjectilePattern` | 미이전 (unit 0 이월분과 **같은 payload**) |
| `Ability_OnPlaceBlast_Shotgunner` | 〃 | 〃 |
| `Ability_UnitSkill_BombMan` | 〃 | 〃 |

→ `EmitProjectilePattern` 하나를 옮기면 **이 3행 + unit 0 이월 2행 = 5행**이 한 번에 간다.
그것이 unit 0 이 이 payload 를 여기로 넘긴 이유다.

## 완료 기준

- [ ] 5행이 concrete + 저작 SO 로 존재한다
- [ ] `EmitProjectilePattern` 실행 사본이 2개 → 1개로 줄었다
- [ ] 방어유닛이 쏜 패턴이 방어유닛을 때리지 않는다 (PlayMode 단언)
- [ ] `ISkill` 레지스트리에 적 스킬과 방어유닛 스킬이 함께 있다
- [ ] 그물 초록 + Play 스모크
