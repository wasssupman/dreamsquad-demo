# 3 — ① 가시 갑옷 (OnDamagedN × NextAttackDoubleFire)

## 목적

부착 유닛이 5회 피격하면 다음 공격 1회가 2연발. 크로스맥락: **Units 가 피격 카운트 → Combat 이 더블파이어 소비**.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — defender 피격 시 OnDamagedN 슬롯 카운트
- 수정: `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — `NextAttackDoubleFire` 소비(RESOLVE 2회 발행)
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 부착 시 OnDamagedN×NextAttackDoubleFire 슬롯 베이크
- 신규 에셋: `Assets/_Project/Data/Dreamcatcher/Card_Thornmail.asset`

## 구현

**부착**: mechanics 루프에서 `trigger.kind==OnDamagedN`(period=5) && `payload.kind==NextAttackDoubleFire` 슬롯 베이크(기존 `DcTriggerSlot`). NextAttackDoubleFire 는 파라미터 없음.

**카운트(Units)**: `DamageApplicationSystem` — defender(`DefenderUnitTag`)이고 이번 프레임 피격(`totalDamage>0`)이며 `DcTriggerSlot` 보유 시, 슬롯 순회하며 `trigger==OnDamagedN` 만 `DcTrigger.Tick(ref counter, period)`(unit-trigger 의 순수함수 재사용). 발동 시 `ecb.AddComponent(entity, new NextAttackDoubleFire{ charges=1 })`(이미 있으면 charges 증가 대신 1 유지 — v1 단순). **BufferLookup<DcTriggerSlot> RW** 추가. 프레임당 피격=1카운트.
- 맥락 경계: OnDamagedN 슬롯 카운터는 Units 만 쓴다(AttackN 은 Combat). 같은 버퍼라도 kind 별로 쓰는 맥락이 하나 → 경계 유지(계약 2). NextAttackDoubleFire 는 Units write / Combat read+clear(계약 3).

**소비(Combat)**: `AttackSystem` RESOLVE — `nextAttackDoubleFireLookup.HasComponent(attackerEntity) && charges>0` 이면 이 공격의 output/투사체 발행을 **2회** 수행(기존 발행 블록을 2회 루프) 후 `ecb.RemoveComponent<NextAttackDoubleFire>`(또는 charges--). 투사체 경로면 캐리어 2발, 근접 output 이면 2회 적용.
- 주의: 더블파이어는 "다음 1회 공격"에만 — 소비 즉시 제거. 쿨다운/타겟은 기존대로.

## 완료 기준

- [ ] 컴파일 + 무회귀 (EditMode green)
- [ ] Play: 부착 유닛을 적에게 5회 맞게 한 뒤 다음 공격이 2발 나가는지(로그 `Damage x2` 또는 투사체 2개). 5회 미만은 정상 1발.
- [ ] 맥락 경계: DamageApplicationSystem 이 슬롯 카운터만, AttackSystem 이 charge 소비만.
- [ ] 사용자 확인
