# 3. 드림캐쳐 카드 2종 + 카탈로그 [data]

## 목적

OnShieldBreak 트리거를 실제로 쓰는 Unit 카드 2장을 만들고 카탈로그에 등록. 이제 카드를 유닛에 부착하면 실드 파열 효과가 발현.

## 변경 대상

- **신규** `Assets/_Project/Data/Dreamcatcher/Card_ShieldBurst.asset` — 카드 A(산산조각).
- **신규** `Assets/_Project/Data/Dreamcatcher/Card_ShieldLull.asset` — 카드 B(고요한 파문).
- `Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset` — `cards[]` 에 두 카드 등록.

## 구현

`DreamcatcherCard`(union SO) **Unit 타입**(`type=1`). `CardBinding` 은 제거됨(taxonomy-cleanup) — `type` 이 유일 판별자. `mechanics[0]` 에 OnShieldBreak(trigger.kind=7) 트리거.

- **A 산산조각**(`id=shield_burst`): payload=SelfTileAoe(kind 2), magnitude=80(데미지)·tileRange=1(반경 3×3)·projectile=`Projectile_Meteor`(기존 AoE view 재사용). 실드 파열 → host 중심 폭발.
- **B 고요한 파문**(`id=shield_lull`): payload=AreaSleep(kind 16), magnitude=2(M)·tileRange=1(N)·duration=2.5(L)·projectile 없음. 실드 파열 → 가까운 2명 2.5초 수면. (튜닝 2026-07-22: 범위 2→1·대상 3→2.)

MCP `manage_scriptable_object` 로 생성(스칼라 + `mechanics.Array.data[0].*` struct 패스). 카탈로그는 `cards[]` 에 guid 2개 append.

**시트 동기화 안전**: DcCards/DcMechanics 시트 import 는 id-keyed 머지(빈 셀=유지, 미지 id 스킵), exporter 는 SO 생성/삭제 안 함 → 새 id 카드는 sync 로 안 지워짐(다음 export 시 시트에 반영됨).

## 완료 기준

- Unity 임포트 CS/콘솔 에러 0, 카탈로그 sync 테스트 그린.
- (유닛 4 Play) 카드를 실드 받는 유닛에 부착 → 피격으로 실드 파열 시 A 폭발 / B 수면 발현.
- 수치: A 80/반경1. B **반경1·2명·2.5초**(튜닝 2026-07-22: 범위 2→1·대상 3→2) — Play 후 SO 튜닝.
