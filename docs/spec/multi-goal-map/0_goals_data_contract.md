# 0. goals 목록 데이터 계약

## 목적

골을 단일 셀에서 **목록**으로 확장하는 토대. 저장만 바꾸고 소비는 이후 유닛에서. 기존 단일 `goal` 은 primary(goals[0])로 병존시켜 compile-safe + 회귀 안전.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` (+ `SetFrom` 시그니처 — 리뷰 m5)
- `Assets/_Project/Scripts/Data/GeneratedMap.cs`
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` (ToGeneratedMap, WriteToDocument, `SetFrom` 호출처 `:66`)
- `Assets/_Project/Scripts/Data/BattleMapBuilder.cs` (`BuildFallbackLinear:86` — keep-set 안전망, goals 명시 세팅)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (GeneratedMap.goals dispose)
- 테스트: `Tests/EditMode/MapGrid/MapDocumentRoundTripTests.cs` (`:32` SetFrom, `:53` goals 왕복 추가)

## 구현

1. **MapDocument**: `[SerializeField] Vector2Int[] goals;` 추가. `Vector2Int Goal => (goals!=null&&goals.Length>0)?goals[0]:goal;`(primary 접근자). `IReadOnlyList<Vector2Int> Goals`. 기존 `goal` 필드는 마이그레이션 폴백으로 유지(비우지 않음). Validate: `goals.Length` 1~4, 각 셀 in-bounds. **`SetFrom(... Vector2Int g ...)` 에 `goals[]` 파라미터/오버로드 추가**(리뷰 m5) — 호출처 갱신.
2. **GeneratedMap**: `NativeArray<int2> goals;` 추가. `int2 goal`(primary=goals[0])은 유지. `Dispose()` 에 `goals` 정리(created 가드). **`IsCreated` 는 `tiles.IsCreated && spawns.IsCreated` 그대로 — `goals.IsCreated` 를 넣지 않는다**(리뷰 B1-연관: 런타임 5곳·테스트 픽스처 ~10곳이 IsCreated=false 로 뒤집힘).
3. **ToGeneratedMap**: `var g = doc.Goals; if empty → [doc.Goal]`(폴백). `goals = new NativeArray<int2>(g.Count, allocator)` 복사. `goal = goals[0]`.
4. **BuildFallbackLinear(keep-set 안전망)**: object-initializer 에 `goals` 세팅(`[goal]` 최소 보장). — 이 생산자는 라이브 폴백이라 반드시 채운다. **나머지 legacy 생산자**(BuildFromFixture/Manual/ProceduralMapGenerator/CellClassifier)는 cleanup 스펙 삭제 대상이며, 그때까지는 **소비 지점 폴백**(유닛 1·3)이 커버 → 유닛 0 에서 손대지 않음.
5. **WriteToDocument**: `goals` 기록(painter Bake 용, 유닛 4 가 채움). 지금은 `[goal]` 1원소라도 왕복 무결.
6. **BattleBridge TeardownGeneratedMap**: `_generatedMap.goals` dispose(Dispose() 가 처리하면 자동).

## 계약

- `goal`(단일) = `goals[0]` 는 항상 유효(비-빈 보장). 기존 `goal`/`goalCell` 소비자는 무변경으로 계속 작동.
- **IsCreated 불변식 불변** — goals 는 소비 지점 폴백으로 안전 처리(B1). 이 결정이 회귀 안전의 핵심.
- 기존 5맵(goals 미설정)은 폴백 `[goal]` 로 1원소 goals 를 얻는다 → 마이그레이션 불요.

## 완료 기준

- [x] MapDocument.goals + SetFrom 오버로드 + GeneratedMap.goals 추가, primary goal=goals[0] 보장
- [x] GeneratedMap.IsCreated 불변식 그대로(goals 미포함), BuildFallbackLinear goals 세팅
- [x] ToGeneratedMap 폴백([goal]) + 복사, Dispose goals 처리(created 가드)
- [x] compile 0 error, 기존 EditMode green(RoundTrip 에 goals 왕복 케이스 + SetFrom 오버로드 반영)
- [x] 기존 단일골 맵 로드 시 goals=[goal] (폴백 실증)

확인 2026-07-23 — compile 0 error/warning, EditMode 1276 중 1274 green(2 skip=기존 Ignored), 신규 `MultiGoal_ToGeneratedMap_And_RoundTrip_Preserved` 통과. 폴백 실증: 실 asset `MapDocument_Serpent`(docGoals=0) → ToGeneratedMap goals.Length=1, goals[0]=goal=primary=(9,1). OnValidate 는 빈 goals 를 폴백으로 허용(>4 만 에러).
