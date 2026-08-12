# 2. 소비자 없는 맵 메타데이터 3배열 제거

## 목적

`mergeDegree` / `chokepoint` / `propLayerId` 는 **읽는 소비자가 하나도 없다.** 그런데도 전체 파이프라인을 완주한다:

- 페인터가 Bake 때 계산해 넣고 (`MapPainterWindow.cs:586-596` — `merge`=walk 이웃 수, `choke`=`d≥3`, `prop`=**항상 0**)
- `MapDocument` 가 직렬화하고 (`:12-14`, 맵 9장에 각각 3개의 긴 hex 문자열)
- `MapDocumentBuilder` 가 `NativeArray` 로 왕복시키고 (`:16-31`, `:132-134`, `:156-166`)
- `OnValidate` 가 길이를 검증하고 (`:109-114`)
- `MapDocumentRoundTripTests` 가 지킨다

즉 **판마다 `셀 수 × 3` 개의 `NativeArray` 를 할당하고 Dispose 하는 것 외에 아무 일도 하지 않는다.** `propLayerId` 는 페인터에 저작 UI 조차 없어 항상 0 이다.

맵 개편이 `MapDocument` 스키마를 건드릴 예정이므로, 죽은 축 3개를 지고 들어가지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/GeneratedMap.cs` — 3개 `NativeArray<byte>` 필드(`:12-14`) + `Dispose` 3줄(`:70-72`)
- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` — SerializeField 3(`:12-14`), 프로퍼티 3(`:40-42`), `SetFrom` 파라미터 3(`:52-64`), `OnValidate` 길이 검증 3(`:109-114`)
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` — `ToGeneratedMap` 할당·복사(`:16-18`, `:29-31`, `:132-134`), `WriteToDocument` 역방향(`:156-158`, `:164-166`, `:192`)
- `Assets/_Project/Editor/MapPainterWindow.cs` — `merge`/`choke`/`prop` 배열 할당·계산(`:575~596`), `GeneratedMap` 초기화 3줄(`:608-610`), `IsWalk` 헬퍼가 이 계산 전용이면 함께 제거
- `Assets/_Project/Tests/EditMode/MapGrid/MapDocumentRoundTripTests.cs` — 해당 픽스처·단언(`:23-34`, `:68-70`, `:103-112`, `:249-261`)

## 구현

- 3개 축을 위 경로에서 통째로 걷어낸다. `placeMask` 는 **살아있는 축이다** — 같은 파일에서 나란히 다뤄지므로 실수로 함께 지우지 않도록 주의한다.
- `GeneratedMap` 은 unmanaged struct 라 필드 제거가 곧 할당 감소다. `Dispose` 에서 짝을 빠뜨리지 않는다(반대로, 지운 필드의 Dispose 를 남기면 컴파일 에러라 자동 검출된다).
- **맵 9장의 `.asset` 은 편집하지 않는다.** 필드가 사라지면 `mergeDegree:` / `chokepoint:` / `propLayerId:` 키는 orphan 으로 남는다. Unity 6.4 의 `ForceReserializeAssets` 는 orphan 키를 떨구지 않으므로(기존 판례) 억지로 정리하지 않는다. 길지만 무해하다.
- 라운드트립 테스트는 3축 단언만 지운다. **테스트 자체를 지우지 않는다** — `tiles`/`placeMask`/`goals`/`spawns` 왕복 계약은 그대로 유효하다.

## 완료 기준

- Unity compile 0 errors.
- EditMode 전체 green. `MapDocumentRoundTripTests` 가 남은 축(`tiles`/`placeMask`/`goals`/`spawns`/`seed`/`generatorVersion`)을 여전히 검증한다.
- **맵 페인터 왕복 검증**: Map Painter 로 기존 맵 1장(예: `MapDocument_Coil`)을 Load → Bake → 다시 Load 했을 때 타일·마스크·spawns·goals·structures 가 동일하다.
- Play 1판(Coil): 경로·배치·전투가 정리 전과 동일. 콘솔 신규 error/warning 0.
- `git status` 에 `Assets/_Project/Data/Maps/*.asset` 이 스테이징되지 않았는지 확인 — 포함됐다면 Unity 가 재직렬화한 것이므로 내용을 보고 판단한다.
