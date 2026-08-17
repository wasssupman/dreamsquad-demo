# 1. spawnRoutes 공성 부활

## 목적

레인별 기본 경로(`spawnRoutes`)를 공성 파생 스폰에서도 쓸 수 있게 한다. 기능 자체는
완성돼 있고(waypoint-routing unit 8), 공성 파생이 «스폰 1개 = 레인 개념 불성립» 전제로
배열을 무조건 버리고 있었다 — 그 전제가 unit 0 에서 사라졌다.

## 변경 대상

- `Assets/_Project/Scripts/Data/StructurePlacement.cs` — `SiegeSpawnOffsets` 상수(파생 순서
  단일 소스) + `CollectDerivedSiegeSpawns`(저작측 파생 목록)
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` — 공성 분기: 버리기 → 재구축
- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` — OnValidate 가 레인 검증에 파생
  스폰 목록을 넘긴다 (저작 spawns 0개를 넘기면 «어느 레인에도 안 붙는다» 거짓 경고 +
  인덱스 범위 검증 전체 스킵)
- 테스트: `StructureAuthoringTests`(채택/폐기), `MapDocumentPoolDevEntriesTests`(단언 반전)

## 구현

1. 파생 오프셋 `[(0,−1), (0,+1)]` = [하단, 상단] = lane 0, 1 을 `StructurePlacements` 상수로.
   빌더(파생)·OnValidate(레인 검증)·테스트가 같은 배열을 본다.
2. 빌더 공성 분기: 기존 배열(저작 spawns 길이 = 0)은 버리고, `doc.SpawnRoutes.Count == 파생
   스폰 수`일 때만 재구축. 어긋나면 폐기 + 경고(침묵 금지 — 조용히 다른 레인을 읽는 것도,
   조용히 지워지는 것도 막는다). 불변식 «미생성 이거나 정확히 spawns 길이» 유지.
3. Duel 은 계속 비움(최단거리가 이미 갈린다) — 라우트 저작은 unit 2 의 Ford·Isle 몫.

## 완료 기준

- EditMode: 채택(RouteForSpawn 이 저작값 반환)·길이 불일치 폐기(−1 + 미생성)·파생 목록
  순서 pin — 신규 3건 초록, 기존 전량 무회귀
- `SiegeDevSlot_IsWiredWithCurrentGenerationDeck` 의 «저작 금지» 단언이 «비움 또는 파생
  스폰 수와 일치» 로 반전
