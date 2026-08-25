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

## 완료 기준

- [ ] 5행이 concrete + 저작 SO 로 존재한다
- [ ] `EmitProjectilePattern` 실행 사본이 2개 → 1개로 줄었다
- [ ] 방어유닛이 쏜 패턴이 방어유닛을 때리지 않는다 (PlayMode 단언)
- [ ] `ISkill` 레지스트리에 적 스킬과 방어유닛 스킬이 함께 있다
- [ ] 그물 초록 + Play 스모크
