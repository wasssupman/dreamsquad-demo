# 1 — ② 작별 선물 (OnDeath × SelfTileAoe)

## 목적

부착 유닛이 죽을 때 죽은 셀 주변 2타일에 폭발 데미지 100. 신규 트리거 `OnDeath` + 신규 페이로드 `SelfTileAoe`(기존 TileAoe 투사체 재사용).

## 변경 대상

- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyDreamcatcherCardToUnit`(SelfTileAoe 슬롯 베이크) + `DrainDefenderDeathEvents`(OnDeath 검사·발동)
- 신규 에셋: `Assets/_Project/Data/Dreamcatcher/Card_Farewell.asset`

## 구현

**부착**: mechanics 루프에서 `trigger.kind==OnDeath` && `payload.kind==SelfTileAoe` 슬롯 베이크 — 기존 `DcTriggerSlot` 에 담되, OnDeath 는 period 무의미(카운트 없음). SelfTileAoe 파라미터(magnitude=100, tileRange=2, projectileDataIndex)를 슬롯에 베이크. AttackN 카운트 arm 은 OnDeath 를 무시(kind 체크).

**발동**: `DrainDefenderDeathEvents` 에서 `binding.entity`(엔티티 파괴 전, 슬롯 보유)의 `DcTriggerSlot` 순회 → `trigger==OnDeath && payload==SelfTileAoe` 슬롯마다:
- 죽은 셀 월드 좌표(`GridMath.CellToWorldCenter`)를 impact 로 락한 `ProjectileSpawnRequest{ movement=SkyFall(또는 즉발), payload=TileAoe, impact=셀월드, damage=slot.magnitude, impactTileRange=slot.tileRange, flightTime=0, dataIndex }` 를 캐리어 엔티티에 부착(unit-trigger 의 `ProjectileRequestCarrier` 재사용) → 기존 drain 이 TileAoe 투사체 스폰 → ImpactSystem 이 2타일 AOE 100 해결.
- **타이밍 주의**: 슬롯은 엔티티 파괴 전에 읽어야 한다. `DrainDefenderDeathEvents` 는 `binding.entity` 를 아직 접근 가능(spineUnitPool.NotifyDeath 도 여기서 함) → 슬롯 읽기 안전. 단 `_defenderByTile.Remove(cell)` 전에 읽을 것.

**SkyFall vs 즉발**: flightTime=0 이면 스폰 즉시 착탄(경고 텔레그래프 없음). 사망 폭발은 즉발이 자연스러움. impact 는 죽은 셀 락.

## 완료 기준

- [ ] 컴파일 + 무회귀 (EditMode green)
- [ ] Play: OnDeath 카드 부착 유닛을 죽여 주변 2타일 적에게 100 데미지 폭발 (로그/육안). 미부착 유닛 사망은 무폭발.
- [ ] 콘솔 에러 0
- [ ] 사용자 확인
