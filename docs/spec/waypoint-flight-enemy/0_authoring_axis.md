# unit 0 — 저작 축 (맵이 경로 N개를 소유한다)

## 목적

`MapDocument` 에 웨이포인트 경로를 저작하고 `GeneratedMap` 으로 투영한다. **읽는 코드 0 — 행동 변화 0.** 검증용 맵 1장을 수기로 저작해 unit 3 의 라이브 검증이 볼 것을 만들어 둔다.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` — 경로 저작 필드 + `OnValidate` 검증
- `Assets/_Project/Scripts/Data/GeneratedMap.cs` — 투영 배열 + `Dispose`
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` — `ToGeneratedMap` 전달
- `Assets/_Project/Data/Maps/MapDocument_Test.asset`(또는 MovementLab) — 경로 1~2개 수기 저작
- `Assets/_Project/Tests/EditMode/` — 투영·검증 테스트

## 구현

### 저작 형식 — 셀 목록의 배열 (README D1)

```csharp
[System.Serializable] public class WaypointPath { public Vector2Int[] cells; }
[SerializeField] private WaypointPath[] waypointPaths;   // null/빈 = 경로 없는 맵(현행 전부)
```

적 SO 는 **인덱스**로 참조한다(unit 3). id 문자열은 저작 비용 대비 이득이 없다 — 경로 순서를 바꾸는 저작은 페인터(unit 5)가 막으면 된다.

### `GeneratedMap` 투영 — flatten 2배열

`structures` 선례(NativeArray + 미생성/빈 = 없음)를 따르되, 경로가 가변 길이이므로 flatten 한다:

```csharp
public NativeArray<int2> waypointCells;   // 전 경로의 셀을 이어 붙임
public NativeArray<int2> waypointRanges;  // 경로별 (start, count)
```

- `Dispose` 에 두 배열 추가. **`IsCreated` 불변식에는 넣지 않는다**(`goals`·`placeMask` 와 같은 이유 — 픽스처 보호).
- 접근자 `WaypointPathCount` / `WaypointCellAt(path, i)` 를 두어 소비자가 flatten 을 직접 계산하지 않게 한다.

### `OnValidate` 검증 (경고/에러 규칙)

| 상태 | 판정 |
|---|---|
| 셀이 격자 밖 | **에러** |
| 셀의 `Derive(tile)` 에 지상 층(Ground\|Path)이 없음 | **경고** — «지상 층이 닫힌 칸: Air 경로 전용» (unit 4 이후 합법이 되므로 에러가 아니다) |
| 경로 셀이 골/스폰 셀과 겹침 | **경고** — 골 위를 지나는 경로는 유출을 일으킨다(unit 3 은 sim 의 골 판정을 건드리지 않는다) |
| 같은 경로에 같은 셀 연속 중복 | **경고** — 도달 판정(셀 일치)이 즉시 통과해 무의미 |

검증 로직은 `StructureAuthoringRules` 선례처럼 **순수 static 으로 분리**해 페인터(unit 5)와 `OnValidate` 가 같은 함수를 호출한다.

## 완료 기준

- [x] 컴파일 에러 0 · 기존 EditMode 전량 그린(기존 맵은 경로 미저작이라 무변경)
- [x] 투영 테스트: 경로 2개(길이 3·2) 저작 → `GeneratedMap` 에서 flatten 역참조가 저작과 일치
- [x] 미저작 폴백 테스트: `waypointPaths` null/빈 → `waypointCells` 미생성, `Dispose` 안전
- [x] 검증 테스트: 격자 밖 셀 = 에러 · 골 겹침 = 경고 (순수 함수 직접 호출)
- [x] 검증용 맵 1장에 경로 1~2개 수기 저작 완료(YAML 직접 편집 가능 — 씬 아님)

완료 확인: 2026-08-11 — Unity 컴파일 에러 0, EditMode 2,115건 중 실패 0
(2,112 통과·기존 Ignore 3), `MapDocument_MovementLab` 경로 2개 역직렬화·flatten 투영
실측 완료. 이 문서와 동일 커밋.
