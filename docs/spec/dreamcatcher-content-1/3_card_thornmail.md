# 3 — ① 가시 갑옷 (OnDamagedN × NextAttackDoubleFire)

## 목적

부착 유닛이 5회 피격하면 다음 공격 1회가 2연발. 크로스맥락: **Units 가 피격 카운트 → Combat 이 더블파이어 소비**.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 부착 시 OnDamagedN 을 **`DamagedCounter`(Units)** 로 베이크
- 수정: `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — 피격 카운트 + `NextAttackDoubleFire` 발화
- 수정: `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — `NextAttackDoubleFire` 소비(output 2회)
- 신규 에셋: `Assets/_Project/Data/Dreamcatcher/Card_Thornmail.asset`

## 구현

**부착 (critic H1)**: `trigger.kind==OnDamagedN && payload.kind==NextAttackDoubleFire` 이면 **`DamagedCounter{ instanceId=_dcInstanceCounter++, period=5, counter=0 }`** 를 defender 의 `DynamicBuffer<DamagedCounter>` 에 append(DcTriggerSlot 아님 — Units 가 쓸 상태는 Units 소유. buffer 라 같은 카드 2장=독립 카운터 2개, DcTriggerSlot 부착과 동형). NextAttackDoubleFire 는 파라미터 없음.

**카운트·발화 (Units)**: `DamageApplicationSystem` — defender(`DefenderUnitTag`)이고 이번 프레임 피격(`totalDamage>0`)이며 `DamagedCounter` 버퍼 보유 시 **슬롯 순회**하며 각 slot 에 `DcTrigger.Tick(ref slot.counter, slot.period)`(순수함수 재사용, 값 write-back). 어느 슬롯이든 발동하면 `ecb.AddComponent(entity, new NextAttackDoubleFire{ charges=1 })`(이미 있으면 유지). 프레임당 피격=1카운트. `BufferLookup<DamagedCounter>` RW.
- **핸드오프 (critic H2)**: `NextAttackDoubleFire` = Combat-소유 채널, Units 가 생산(Add)·Combat 이 소비(Remove). `IncomingDamage`(Units 소유, Combat append) 선례의 역방향 — 확립 패턴.

**소비 (Combat) — 쿨다운0 자연 2연발 (H3/H4 원천 회피)**: RESOLVE 블록을 2회 복제하면 투사체 request 충돌(H3)·CC/틱 중복(H4)이 생긴다. 대신 **AttackSystem START 에서 더블파이어 charge 를 만나면 그 공격의 쿨다운을 0 으로** 만들고 charge 를 즉시 `ecb.RemoveComponent`. 결과: 유닛이 다음 프레임(hitDelay 후) 곧바로 한 번 더 공격 → **2연발**. 각 샷이 온전한 정상 공격이라 DC틱/CC/넉백/로그가 **실제 샷당 1회씩** 자연 발생(복제 없음). 2번째 샷은 charge 없어 정상 쿨다운(정확히 2발). `ComponentLookup<NextAttackDoubleFire>` + ecb Remove.

## 완료 기준

- [x] 컴파일 + 무회귀 (EditMode green — 쿨다운0 방식이라 AttackN 더블틱 원천 없음)
- [x] 구조 검증: 부착 시 **DamagedCounter 버퍼(period=5), DcTriggerSlot 아님**(맥락 경계 H1 확인). counter 4→(피격)→5도달 발화·리셋·charge 부여, 아처 공격이 charge 소비(RemoveComponent) — 랩어라운드+발화+소비 실증.
- [x] 맥락 경계: DamageApplicationSystem=DamagedCounter tick+NextAttackDoubleFire Add 만, AttackSystem=charge read+Remove 만. DcTriggerSlot 은 Units 가 안 씀.
- [ ] 2연발 육안(투사체 2개) — 3장 완성 후 사용자 포커스 e2e

완료 확인: 2026-07-09 — 구조 검증(DamagedCounter 베이크·카운트 랩어라운드·발화·charge 소비). 2발 시각은 사용자 e2e. 이 문서와 동일 커밋.
