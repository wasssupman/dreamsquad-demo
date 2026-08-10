# 종료 인계 — battle-structures

rev 1 · 2026-08-10 · **구현 완료.** (rev 0 착수 인계의 논박 기록 ①~⑭ 는 git 이력 `c32523a1` 참조 — 계약의 정본은 README)

## Commit

`02eedea2` 이후 이 스펙의 커밋 사슬(주요만):

- `2d14d092`~`e43c1616` — unit 0 4커밋: 교차 비트·잠자는 골 기계 4자리 삭제·계약 4 폐기·`goalMaxStability` 축 제거
- `bac3521f` unit 1 저작 타겟 마스크 · `3b01416e` unit 2 도발 범위 게이트 · `b443ee29` unit 3 거점 저작 · `6a99eeff` 리뷰(0~3) 반영
- `7fbf71fd` unit 4 스폰·붕괴 ⓐ·뷰 · `5193f4f0` unit 5 본능 공격 · `108d2c2a` unit 6 공성 파생
- `e192abfa` Play 검증(저작물 + `StructureLivePlayTest`) · `94a29196` C-1(통행 차단 무효) 자체 적발 픽스
- `6d32ea0e` 리뷰(4~6) Track B 반영 · `2015ebd4` Track A 반영 · (다음 커밋) 종료 문서

## Implemented

- `Faction` = 진영×종류 교차 비트(구 `Goal` 비트를 `DefenderCore` 가 승계). 거점은 **거리순 일반 후보** — 타입 우선순위 없음(계약 4 폐기, 사용자 확정)
- 저작 의도(`EnemyTargetFilter.factionMask`) / 런타임 마스크 2분 — 도발 게이트는 저작을 `Resolve` 로 읽는다(0=미저작 폴백을 베이크·소비 양쪽이 공유)
- 거점 저작: `StructureData` SO + `MapDocument.structures[]`(셀×편×SO, 진영은 파생) + 페인터 브러시·파생 모드 배지 + `StructureAuthoringRules` 단일 규칙(페인터=`OnValidate`)
- 스폰: `SpawnStructureEntities` 두 소스(goals[]=덱 HP+`GoalTowerTag` / structures[]=SO HP) 한 아키타입(`StructureTag`)
- 붕괴 ⓐ(셀 단위): `_breachedCells` — 부서진 마음의 셀만 유출 전환, 나머지는 선다. 미러=«가장 위험한 골» 캐시(붕괴 프레임 0 → 생존 골 최저), 열기는 미러 갱신 뒤
- 본능: 3×3 통행 차단(계약 13 — 버퍼=점유 선언) + 9×9 배치 배제 + 공격 베이크(통합 루프 합류, 전용 시스템 0, TileAoe 거부)
- 공성 파생: 적 마음 셀 = `spawns[]`(투영 1곳, 소비처 8곳 무변경) + 연결성·Walk 검증

## Key Files

`Battle/Units/{Faction,StructureTag}.cs` · `Battle/Combat/EnemyTargetFilter.cs`(+`EnemyTargetDefaults`) · `Battle/Effects/{AggroStateSystem,ObstacleLifetimeSystem}.cs` · `Bridge/BattleBridge.cs`(`SpawnStructureEntities`/`SyncGoalStability`/`_structureRegistry`/`_breachedCells`) · `Data/{StructureData,StructurePlacement}.cs` · `Data/MapGrid/{MapDocument,MapDocumentBuilder}.cs` · `Data/MapConnectivity.cs` · `Editor/MapPainterWindow.cs`
테스트 축: `StructureFixtures`(공용 빌더) · `GoalTowerArchetypeTests`(브리지 산물 컴포넌트 집합 동일성 = drift 방지선) · `StructureSpawnAndBreachTests` · `StructureAuthoringTests` · `AuthoredTargetMaskTests` · PlayMode `StructureLivePlayTest`

## Verified

- EditMode **2049 / 실패 0 / 의도적 스킵 3**(기존) — 시작 기준선 2000 에서 이 스펙이 +49
- PlayMode: 골 3종(GoalStability·EndlessSmoke·StructureLive) 그린. 전량 91 중 잔여 실패 15는 타 서브시스템 귀속(auth/tween/기믹 오염/Gift 페이즈 stale/씬 환경 — base 대조는 미실행, `4_...md` 에 근거)
- Play(라이브): 저작 본능이 실제 판에 스폰·차단·프랍·연결성 생존(`e192abfa`)
- 투트랙 리뷰 2회 전 지적 반영 완료(기각 1건 포함 — B-M6 «AnyUnit 과대» 는 `TauntAttackGrantSystem` 의 OR 로 시나리오 불성립)

## Notes — 되돌리면 안 되는 것

- **계약 13**: `ObstacleLifetimeSystem` 다중셀 루프는 버퍼 기준이다. `WithAll<BlockingHazard>` 로 되돌리면 본능이 통행을 안 막고, 본능에 그 컴포넌트를 달면 hazard-dead 루프 오염 + 영구 미파괴
- **거점 타입 우선순위 금지**(계약 4 폐기): `AttackSystem` 에 «거점이니까» 분기를 다시 넣지 말 것 — 우선순위는 저작(`targetFactions`)이 표현한다
- **방어 마음의 정본 = `goals[]`**: `(Defender, Core)` 는 검증·스폰 이중 거부 — 풀면 골이 두 벌이 된다
- **붕괴 열기는 미러 갱신 뒤**(A-M1): 순서를 되돌리면 패배 제출값이 지난 프레임 양수를 싣는다
- **`_resolvedMapDoc` 은 fallback/hard-fail/teardown 에서 null**(H-3): 빼먹으면 폐기 문서 좌표에 거점이 선다

## Follow-up

README 후속 후보 참조(리뷰 이관 5건: 배치 페이즈 프랍 · NeutralInstinct 배제 일반화 · 본능 발사 라이브 검증 · 본능 광역 · `GoalCollapsed` 페이로드 재정의) + 공성 콘텐츠·중립 콘텐츠 등 기존 항목. 스펙 자산으로 커밋된 거점 저작 맵은 `MapDocument_Test`(dev 슬롯) 하나 — 정식 공성 맵 저작은 콘텐츠 작업.
