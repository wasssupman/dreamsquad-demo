# 0. placeMask 데이터 레이어 — 직렬화·왕복·파생 폴백

## 목적

`placeMask` 를 MapDocument(저작 자산) ↔ GeneratedMap(런타임) 양방향에 배선한다. 이 유닛은 **판정을 바꾸지 않는다** — 데이터가 흐르고 폴백이 서는 것까지만. 기존 자산·테스트 전부 무회귀.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs`
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs`
- `Assets/_Project/Scripts/Data/GeneratedMap.cs`
- `Assets/_Project/Scripts/Data/BattleMapBuilder.cs`
- `Assets/_Project/Tests/EditMode/` — MapDocument 왕복·빌더 테스트에 케이스 추가

## 구현

1. **MapDocument**: `[SerializeField] private byte[] placeMask;` + `IReadOnlyList<byte> PlaceMask` + `SetFrom` 파라미터 추가(기존 optional 파라미터 `goalStabilityArr` 뒤에 `byte[] placeMaskArr = null`) + `OnValidate` 길이 검증. **검증식은 `goalMaxStability` 패턴과 동형**(`placeMask != null && placeMask.Length > 0 && placeMask.Length != n` 이면 에러) — Unity 는 신규 배열 필드를 기존 asset 에 **length-0 으로 로드**하므로(`MapDocument.cs:86` 주석, goals 마이그레이션 선례) length-0 을 에러로 잡으면 기존 asset 6종이 임포트마다 에러 스팸을 낸다. length-0 = 부재 = 폴백으로 유효.
2. **GeneratedMap**: `public NativeArray<byte> placeMask;` 필드 + `Dispose` 에 해제 추가. `IsCreated` 불변식에는 **넣지 않는다**(goals 와 같은 이유 — 직접 구성 픽스처 보호). 판정 헬퍼:
   ```csharp
   // 마스크 미생성(직접 구성 픽스처/legacy 생산자) = tiles==Place 파생 폴백.
   public bool PlaceableAt(int2 cell)
       => placeMask.IsCreated ? placeMask[CellIndex(cell)] != 0
                              : tiles[CellIndex(cell)] == MapTileType.Place;
   ```
3. **MapDocumentBuilder.ToGeneratedMap**: doc.PlaceMask 가 존재하고(길이 > 0) 길이가 n 과 일치하면 복사, 아니면 `tiles[i]==Place` 로 파생해 **항상 생성**한다(빌더 산출물 불변식: IsCreated ⇒ placeMask 생성됨). 복사 시 **0/1 정규화**(`(byte)(doc.PlaceMask[i] != 0 ? 1 : 0)`) — 비정규값(2 등)이 unit 1 의 intent 비교를 오염시키지 않게 한다.
4. **MapDocumentBuilder.WriteToDocument**: `map.placeMask` 를 doc 으로 내보낸다(빌더 산출물은 항상 생성돼 있음. 미생성 map 이 들어오면 `tiles==Place` 파생으로 채워 내보냄).
5. **BattleMapBuilder.BuildFallbackLinear**: 파생 마스크(`tiles==Place`)를 생성해 반환(빌더 불변식 유지).

## 완료 기준

- compile 클린.
- EditMode 신규: ① doc 에 마스크 저장 → ToGeneratedMap → WriteToDocument 왕복 보존 ② 마스크 부재 doc → `tiles==Place` 파생 확인 ③ 길이 불일치 doc → 파생 폴백 ④ `PlaceableAt` — 마스크 미생성 struct 에서 tiles 폴백 / 마스크 생성 시 마스크 우선(Walk 셀 mask=1 → true).
- 기존 EditMode 전부 그린(특히 `MapDocumentRoundTripTests` · `MapGridBattleAdapterTests` · `SpatialPlacementCheckTests` — 판정은 아직 tiles 기반이므로 무변경 통과).
