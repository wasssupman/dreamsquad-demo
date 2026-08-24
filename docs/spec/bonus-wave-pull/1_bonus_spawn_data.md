# 1 — 보너스 포탈 칸 저작 축 (런타임)

## 목적

보너스 포탈이 열릴 칸을 맵이 소유하게 한다. **소비처는 unit 4 에서 붙는다** — 이 unit 이 끝난
시점에 `bonusSpawns` 는 「저작해도 아무 일이 없는 필드」다. 의도된 공백이며, 그렇게 두는
이유는 런타임 데이터 축과 에디터 도구(unit 2)의 asmdef·테스트 lane 이 갈리기 때문이다.

(선례 경고: `MapDocument` 는 `goalMaxStability` 를 **소비처가 영영 안 붙어서** 지웠다.
이 필드는 unit 4 에서 반드시 소비된다.)

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs`
- `Assets/_Project/Scripts/Data/GeneratedMap.cs`
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs`
- `Assets/_Project/Scripts/Data/MapGrid/BonusSpawnAuthoringRules.cs` — 신규(순수 검증)
- `Assets/_Project/Tests/EditMode/MapGrid/MapDocumentRoundTripTests.cs`

## 구현

1. `MapDocument` 에 `[SerializeField] private Vector2Int[] bonusSpawns;` + 읽기 프로퍼티.
   null/빈 = 보너스 당기기 없는 맵(계약 8).

2. **`SetFrom` 에 끼우지 않는다.** 별도 `SetBonusSpawns(Vector2Int[])` 를 둔다 —
   `SetStructures`/`SetWaypointPaths`/`SetSpawnRoutes` 와 같은 이유다. `SetFrom` 에 끼우면
   「전달 안 하면 지워짐 / 유지됨」이 암묵 규칙이 된다.

3. `GeneratedMap.bonusSpawns : NativeArray<int2>` 추가. **`Dispose()` 에 등재**한다.
   `IsCreated` 불변식에는 **넣지 않는다** — `goals` 와 같은 이유로, 안 채우는 생산자(폴백·
   테스트 픽스처)가 조용히 뒤집힌다.

4. `MapDocumentBuilder.ToGeneratedMap` 에서 투영. 미저작이면 길이 0 배열.

5. **순수 검증 함수** `BonusSpawnAuthoringRules.Validate(cells, width, height, tiles, goals, errors)`
   — 양성 조건 3개(계약 8): ⓐ 걸을 수 있는 칸 ⓑ 골까지 도달 가능(BFS) ⓒ 두 칸이 서로 다름.
   추가로 개수는 0 또는 `BonusWaveData.portalCount`(현재 2)여야 한다 — 다만 이 unit 에는
   그 SO 가 없으므로 **개수 규칙은 unit 3 이후에 붙인다**(여기서는 「중복 없음」까지).
   `MapDocument.OnValidate` 와 페인터(unit 2)가 **이 같은 함수**를 부른다.

## 완료 기준

- [x] 컴파일 에러 0
- [x] `MapDocumentRoundTripTests` — `bonusSpawns` 저장→로드 왕복 동치
- [x] `Dispose` 에 등재됐고 이중 dispose 없음
- [x] 미저작 문서(기존 16장)가 길이 0 으로 투영되고 `OnValidate` 가 조용하다
- [x] `BonusSpawnAuthoringRules` 단위 테스트 — 벽 칸·격리 칸·중복 칸 각각 에러
- [x] EditMode 전체 green

**확인 2026-08-24** — `MapDocumentRoundTripTests` 왕복 2건 + `BonusSpawnAuthoringRulesTests`(7) green.
⚠ 통행 판정은 `== Walk` 다(`!= Place` 로 쓰면 Duel 중앙의 Env 기둥이 통과한다 — 실측으로 잡음).
