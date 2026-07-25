# 3 — 시체폭발 (corpse_burst): OnKill × 킬 위치 TileAoe

## 목적

부착 유닛이 적을 처치할 때마다 **그 적이 죽은 위치**에서 폭발(flat 데미지 AoE). 밀집 웨이브 연쇄 킬 카드. OnKill 발동 지점(`DamageApplicationSystem` 킬 분기)은 이미 killer 의 `DcTriggerSlot` 을 RO 로 읽고(킬속/킬딜 선례), AoE 실행은 OnDeath 폭발과 동형 경로를 재사용한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/EnemyKilledEvent.cs`(정의 위치 확인) — 버스트 필드 위드닝
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — OnKill 슬롯 루프에 SelfTileAoe 분기
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — EnemyKilled 드레인에 TileAoe 실행
- `Assets/_Project/Data/Dreamcatcher/Card_CorpseBurst.asset` (신규) + 카탈로그 등록

## 구현

**운반 = `EnemyKilledEvent` 위드닝** (README 계약: 신규 채널 금지, `DefenderDeathEvent.hasOnDeathAoe` 선례):

- 필드 추가: `hasKillBurst`, `burstDamage`, `burstTileRange`, `burstDataIndex`
- `DamageApplicationSystem` 킬 분기: killer 슬롯 루프에서 `OnKill × SelfTileAoe` 첫 매칭 슬롯을 찾아 위 필드를 채운다 (**첫 슬롯만** — OnDeath v1 선례). 기존 enqueue 가 필드 세팅보다 먼저면 evt 조립 순서 재배열. OnKill×SelfStatBuff 루프와 별개 분기로 두지 말고 한 루프에서 payload 로 갈라도 됨 — 구현 재량.
- bake 는 무수정: SelfTileAoe payload 분기가 trigger 무관하게 `projectileDataIndex/tileRange/magnitude` 를 이미 굽는다 (AOE-view ProjectileData + 양수 magnitude 요구 가드 포함).

**실행 = 브리지 드레인**: EnemyKilled 드레인(점수/각성 처리하는 기존 위치)에서 `hasKillBurst && burstDataIndex >= 0` 이면 `SpawnProjectile(SkyFall × TileAoe, impact=킬 위치, flightTime 0)` — `DrainDefenderDeathEvents` 의 OnDeath 폭발 블록과 동형.

`Card_CorpseBurst.asset`:

- id `corpse_burst` · displayName `시체폭발` · axis All · type Unit
- mechanics[0]: trigger `{ OnKill(6) }` / payload `{ SelfTileAoe(2), magnitude: 25, tileRange: 1, projectile: 작별 선물(Card_Farewell)의 AOE-view ProjectileData 재사용 }` — 수치·뷰 모두 초안, 실아트/전용 뷰는 후속
- description: `처치할 때마다 → 그 자리에서 폭발 (반경 1)`
- 주의: 폭발 데미지가 새 킬을 만들면 그 킬도 OnKill 을 발동(연쇄) — **투사체 데미지의 owner 귀속이 부착 유닛으로 이어지는지 확인** 후, 연쇄 허용을 사양으로 명시한다 (Meteor/작별 선물 owner 처리 선례 참조). 연쇄 폭주는 flat 데미지 + 반경 1 로 자연 제동.

## 완료 기준

- [x] compile 클린 + EditMode 전체 green
- [ ] 스크립트 배틀 e2e 또는 Play smoke: 킬 위치 폭발 발생·주변 적 데미지 적용·폭발발 킬의 연쇄 발동 여부 1회 확인 (TestModeContext 하네스 가능)
- [ ] 콘솔: unhandled payload 경고 없음 (OnKill 지점의 비-SelfStatBuff 슬롯이 이제 소비됨)

구현 커밋 9a465578 (2026-07-25). 이벤트에 killer 도 동봉(owner 귀속) — 폭발발 킬의 OnKill 연쇄 재발동을 사양으로 확정. Play smoke 대기.
