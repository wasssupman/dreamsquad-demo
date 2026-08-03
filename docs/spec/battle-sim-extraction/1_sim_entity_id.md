# 1 — SimEntityId: 매치 내 stable ID 도입

## 목적

`Entity.Index/Version`이 ① 타겟팅 동률 tiebreak(`NearestTargeting`·`FrontmostTargeting`·`LowestHealthTargeting`)과 ② **발사 패턴 RNG seed**(`AttackSystem`: `math.hash(int2(attackerEntity.Index, fireCountBase))`)에 직접 쓰인다. Entity 번호는 할당 순서 산물이라 신 sim(M1)에서 재현 불가 — 골든(unit 4) 생성 **전에** 매치 내 비재사용 `SimEntityId`(spawnOrdinal)로 축을 통일해야 A/B parity가 성립한다. 커맨드·이벤트·스냅샷·뷰 키의 유일 축이 될 ID이기도 하다.

**의도된 행동 변경**: 동률 해소 결과와 랜덤 탄막 시퀀스가 현재와 달라질 수 있다(규칙은 동일, 동률 승자·난수열만). 골든은 이 unit 이후를 기준선으로 삼는다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Units/SimEntityId.cs` — `IComponentData { int value }` (매치 시작 0부터 스폰 순 발급, 재사용 없음)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 유닛/투사체/해저드/캐리어 스폰 경로에서 ordinal 발급·부착, 매치 시작 시 카운터 리셋 (ECS 내부 스폰 시스템 — `ProjectileEmitterSystem`, `AttackSystem`의 캐리어 생성, `PickupSpawnSystem` 등 — 은 ECB 생성 시 부착)
- `Combat/NearestTargeting.cs` · `Combat/FrontmostTargeting.cs` · `Combat/LowestHealthTargeting.cs` — tiebreak 축을 `entityIndex/Version` → `SimEntityId` 교체
- `Combat/AttackSystem.cs` — `PatternShotRandomizer.Apply` seed를 `SimEntityId` 기반으로
- `Effects/HazardCastSystem.cs` — 최근접 타겟 선택(라인 83-88)에 tiebreak **신설**(현재 부재 — 동률 6지점 중 유일한 무-tiebreak 타겟팅)

## 구현

발급은 스폰 단일 지점(Bridge + ECB 생성부)에서만. `Entity.Index` 사용처는 위 목록이 전부인지 `grep -rn "\.Index" Battle/`로 전수 확인 후 교체(뷰/디버그 로그 용도는 제외 가능하되 목록화). EditMode 타겟팅 테스트의 기대값을 새 tiebreak 축으로 갱신.

## 완료 기준

- compile + EditMode 타겟팅/랭킹 테스트 통과(기대값 갱신 포함).
- 같은 배치 시나리오 2회 Play에서 타겟 선택·탄막 로그 동일(스폰 순서가 같으므로 ordinal 동일).
- sim 로직의 `Entity.Index/Version` 직접 사용 잔존 0건(허용 예외 목록 명시).
