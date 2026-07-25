# 4 — 진동갑주 (tremor_plate): HealthThreshold × 자기 위치 TileAoe

## 목적

부착 유닛이 최대체력의 10%를 잃을 때마다 자기 주위 폭발. "맞는 것이 곧 광역딜"인 탱커 정체성 카드 — 빈사폭주(HealthThreshold×SelfStatBuff)와 트리거는 같고 효과가 액션. 발동 순수함수(`DcTrigger.HealthThresholdEval`)·래치 상태는 기존 그대로 재사용.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — HealthThreshold 상태 bake 를 SelfStatBuff 분기 밖으로 호이스팅
- `Assets/_Project/Scripts/Battle/Combat/HealthThresholdSystem.cs` — SelfTileAoe arm 분기 (ECB carrier)
- `Assets/_Project/Data/Dreamcatcher/Card_TremorPlate.asset` (신규) + 카탈로그 등록

## 구현

**bake 호이스팅**: 현재 `fraction/maxHpRef/nextBoundaryIndex` bake 는 SelfStatBuff 분기 안에만 있다. 이를 trigger-키 공통 블록으로 빼서 `HealthThreshold × SelfTileAoe` 도 임계 상태를 갖게 한다 (fraction ≤ 0 skip·Health 부재 skip 가드 포함, 기존 SelfStatBuff 경로 회귀 없음이 리뷰 포인트). SelfTileAoe payload 필드(AOE view/tileRange/magnitude)는 기존 분기가 이미 굽는다.

**arm**: `HealthThresholdSystem` 발동 분기에 `payload == SelfTileAoe` case 추가 — ECB 로 carrier entity 를 만들어 `ProjectileSpawnRequest { movement: SkyFall, payload: TileAoe, impact: 자기 위치, flightTime: 0, damage: magnitude, impactTileRange: tileRange, dataIndex, owner: 자기 entity }` + `ProjectileRequestCarrier` 부착. 선례 = `AttackSystem` 의 ProjectileToTarget dcCarrier(ECB 생성→브리지 드레인이 스폰 후 파괴). HealthThresholdSystem 에 ECB 가 없으면 도입한다.

`Card_TremorPlate.asset`:

- id `tremor_plate` · displayName `진동갑주` · axis All · type Unit — All 로 두되 가디언 시너지가 자연스럽다 (axis 제한은 밸런스 후 결정)
- mechanics[0]: trigger `{ HealthThreshold(5), fraction: 0.10 }` / payload `{ SelfTileAoe(2), magnitude: 15, tileRange: 1, projectile: AOE-view ProjectileData(작별 선물 재사용) }` — 초안값
- description: `체력 10%를 잃을 때마다 → 자기 주위 폭발 (반경 1)`
- 특성상 최대 9회 발동(90% 소실까지) + maxHp 스냅샷 기준 — 기존 래치 사양 그대로.

## 완료 기준

- [x] compile 클린 + EditMode 전체 green (HealthThresholdEval 기존 테스트 무회귀)
- [x] 기존 빈사폭주(last_stand) Play/e2e 무회귀 — bake 호이스팅의 핵심 검증 (PlayMode `LastStand_BelowHpThreshold_BuffsAttackDamage` green)
- [ ] Play smoke: 부착 탱커가 두들겨 맞을 때 10% 경계마다 자기 위치 폭발·주변 적 데미지 확인

구현 커밋 9186ab85 (2026-07-25). PlayMode 킬임계/온히트/전투데미지 전부 green. `DreamcatcherEffectTest.CardBuffs` 1건 실패는 clean HEAD 리그 재현으로 **이 spec 과 무관한 사전 실패** 판정(가디언 dmgTaken 에 여분 ×1.25 — 별도 조사 후보). Play smoke 대기.
