# 1 — ② 작별 선물 (OnDeath × SelfTileAoe)

## 목적

부착 유닛이 죽을 때 죽은 셀 주변 2타일에 폭발 데미지 100. 신규 트리거 `OnDeath` + 신규 페이로드 `SelfTileAoe`(기존 TileAoe 투사체 재사용).

## 변경 대상

- 수정: `Assets/_Project/Scripts/Battle/Units/DefenderDeathEvent.cs` — OnDeath 페이로드 필드 확장
- 수정: `Assets/_Project/Scripts/Battle/Units/UnitLifecycleSystem.cs` — death enqueue 시 DcTriggerSlot(RO) 읽어 페이로드 베이크
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyDreamcatcherCardToUnit`(SelfTileAoe 슬롯 베이크) + `DrainDefenderDeathEvents`(이벤트 데이터로 TileAoe 스폰)
- 신규 에셋: `Assets/_Project/Data/Dreamcatcher/Card_Farewell.asset`

## 구현

**부착**: mechanics 루프에서 `trigger.kind==OnDeath && payload.kind==SelfTileAoe` 슬롯 베이크 — `DcTriggerSlot` 에 담되 period/counter 무의미. magnitude=100, tileRange=2, projectileDataIndex(AOE 뷰) 베이크.

**사망 감지·베이크 (핵심 — critic C1)**: defender 는 death 프레임에 `UnitLifecycleSystem`(Units) 이 `DefenderDeathEvent` enqueue 후 `ecb.DestroyEntity` → **bridge 드레인 시점엔 엔티티 파괴됨**. 따라서:
- `DefenderDeathEvent` 확장: `{ int2 cell; bool hasOnDeathAoe; float aoeDamage; int aoeTileRange; int aoeDataIndex; }`.
- `UnitLifecycleSystem` 이 죽는 엔티티 파괴 **직전** `DcTriggerSlot`(RO — cross-context 읽기 허용)을 순회해 `trigger==OnDeath && payload==SelfTileAoe` 슬롯이 있으면 이벤트에 페이로드를 실어 enqueue. (첫 OnDeath 슬롯만; 다중은 후속.)

**발동**: `DrainDefenderDeathEvents` 에서 `evt.hasOnDeathAoe` 이면 죽은 셀 월드(`GridMath.CellToWorldCenter`)를 impact 로 락한 `ProjectileSpawnRequest{ movement=SkyFall, payload=TileAoe, impact=셀월드, damage=evt.aoeDamage, impactTileRange=evt.aoeTileRange, flightTime=0(즉발), dataIndex=evt.aoeDataIndex }` 를 `ProjectileRequestCarrier` 캐리어에 부착 → 기존 drain 이 TileAoe 투사체 스폰 → ImpactSystem 이 2타일 AOE 해결. **파괴된 엔티티 접근 없음.**

## 완료 기준

- [x] 컴파일 + 무회귀 (DefenderDeathEvent 확장이 기존 enqueue 무해)
- [x] 구조 검증: 부착 시 OnDeath×SelfTileAoe 슬롯 베이크 정확(mag=100/tileRange=2/dataIdx). 카드 유닛 즉사 → "Defender died" 로그 + **예외 0**(SpawnProjectile(Entity.Null) 안전). TileAoe 즉발 스폰·해결.
- [x] 콘솔 에러 0 (파괴 엔티티 접근 예외 없음)
- [ ] 적 피격 시각 확정 — 3장 완성 후 사용자 포커스 e2e 로 이관(TileAoe 100뎀 적용은 기존 검증된 프리미티브)

완료 확인: 2026-07-09 — 구조 검증(부착 베이크·사망 감지·이벤트 베이크·무예외 스폰). 폭발 데미지는 기존 TileAoe(Meteor 선례). 이 문서와 동일 커밋.
