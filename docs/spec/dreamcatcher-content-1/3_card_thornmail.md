# 3 — ① 가시 갑옷 (OnDamagedN × NextAttackDoubleFire)

## 목적

부착 유닛이 5회 피격하면 다음 공격 1회가 2연발. 크로스맥락: **Units 가 피격 카운트 → Combat 이 더블파이어 소비**.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 부착 시 OnDamagedN 을 **`DamagedCounter`(Units)** 로 베이크
- 수정: `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — 피격 카운트 + `NextAttackDoubleFire` 발화
- 수정: `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — `NextAttackDoubleFire` 소비(output 2회)
- 신규 에셋: `Assets/_Project/Data/Dreamcatcher/Card_Thornmail.asset`

## 구현

**부착 (critic H1)**: `trigger.kind==OnDamagedN && payload.kind==NextAttackDoubleFire` 이면 **`DamagedCounter{ period=5 }`** 를 부착(DcTriggerSlot 아님 — Units 가 쓸 상태는 Units 소유). NextAttackDoubleFire 는 파라미터 없음.

**카운트·발화 (Units)**: `DamageApplicationSystem` — defender(`DefenderUnitTag`)이고 이번 프레임 피격(`totalDamage>0`)이며 `DamagedCounter` 보유 시 `DcTrigger.Tick(ref counter, period)`(순수함수 재사용). 발동 시 `ecb.AddComponent(entity, new NextAttackDoubleFire{ charges=1 })`(이미 있으면 유지). 프레임당 피격=1카운트. `ComponentLookup<DamagedCounter>` RW.
- **핸드오프 (critic H2)**: `NextAttackDoubleFire` = Combat-소유 채널, Units 가 생산(Add)·Combat 이 소비(Remove). `IncomingDamage`(Units 소유, Combat append) 선례의 역방향 — 확립 패턴.

**소비 (Combat, critic H3/H4)**: `AttackSystem` RESOLVE — `nextAttackDoubleFireLookup.HasComponent(attackerEntity) && charges>0` 이면:
- **output 발행만 2회** 반복. 투사체 경로는 2번째 샷을 **캐리어 엔티티**(`ProjectileRequestCarrier`)로 발행 — `ProjectileSpawnRequest` 는 엔티티당 1개라 attacker 에 두 번 Add 불가. 근접 output 경로는 2회 적용.
- **DcTriggerSlot 틱·CC 넉백·쿨다운 리셋은 1회 유지**(2회 반복 밖). AttackOutputLog 는 발행 따라 2회(2히트 = 2로그, 의도).
- 소비 즉시 `ecb.RemoveComponent<NextAttackDoubleFire>`(다음 1회 한정).

## 완료 기준

- [ ] 컴파일 + 무회귀 (EditMode green — AttackN 카드 더블틱 없음)
- [ ] Play: 부착 유닛 5회 피격 후 다음 공격 2발(투사체 2개 또는 로그 Damage x2). 5회 미만 정상 1발.
- [ ] 맥락 경계: DamageApplicationSystem=DamagedCounter+NextAttackDoubleFire Add 만, AttackSystem=NextAttackDoubleFire read+Remove 만. DcTriggerSlot 은 Units 가 안 씀.
- [ ] 사용자 확인
