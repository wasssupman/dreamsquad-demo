# 1 — Tilemap 범위 레이어 + 동기 펄스

## 목적

전용 `_rangeTilemap` 에 공격 범위 셀을 노란 타일로 칠하고, 전체를 동기 alpha 펄스시킨다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Core/TilemapMapView.cs`

## 구현

**필드**:

```csharp
private Tilemap _rangeTilemap;
private readonly HashSet<Vector2Int> _rangeCells = new();
private int2 _gridSize;
```

**Initialize**: `Clear()` 이후에서 `_gridSize = map.IsCreated ? map.gridSize : default;` 보관(경계 clip 용).

**Clear()**: `if (_rangeTilemap != null) _rangeTilemap.ClearAllTiles();` + `_rangeCells.Clear();` 추가.
`_rangeTilemap` GameObject 는 **파괴하지 않는다** — effect 타일맵과 동일하게 reuse(누수 아님).

**EnsureRangeTilemap()** — `EnsureEffectTilemap` 미러:

```csharp
if (_rangeTilemap != null) return;
if (grid == null) return;
var go = new GameObject("PlacementRangeTiles");
go.transform.SetParent(grid.transform, false); // grid 90°X 상속 — ground/overlay 와 동일 평면
_rangeTilemap = go.AddComponent<Tilemap>();
var r = go.AddComponent<TilemapRenderer>();
_rangeTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
r.sortingOrder = -12;
r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
// F4 — 검증된 반투명 tint 머티리얼 재사용(overlay 는 반투명 hover/reject 를 이미 정상 렌더).
if (overlayTilemap != null)
{
    var or = overlayTilemap.GetComponent<TilemapRenderer>();
    if (or != null) r.sharedMaterial = or.sharedMaterial;
}
```

**SetPlacementRange(Vector2Int center, int tileRange)**:

- `if (grid == null || _tileSet == null || _tileSet.rangeTile == null || tileRange <= 0) return;`
- `ClearPlacementRange();` (이전 범위 제거 — 매 갱신 안전)
- `EnsureRangeTilemap();`
- 이중 루프 `dx, dz ∈ [-tileRange, tileRange]`, `(0,0)` 제외(중심). `cell = center + (dx,dz)`.
  경계 clip `0 <= cell.x < _gridSize.x && 0 <= cell.y < _gridSize.y`.
  통과 시 `_rangeTilemap.SetTile(ToCell(cell), _tileSet.rangeTile); _rangeCells.Add(cell);`
- **RGB 만 세팅**(alpha 는 Update 단독 소유 — F6):
  `var c = _tileSet.rangeColor; c.a = _rangeTilemap.color.a; _rangeTilemap.color = c;`

**ClearPlacementRange()** — idempotent(ClearHover / CleanupSession 둘 다 호출):

```csharp
if (_rangeCells.Count == 0) return;
if (_rangeTilemap != null)
    foreach (var cell in _rangeCells) _rangeTilemap.SetTile(ToCell(cell), null);
_rangeCells.Clear();
```

**Update()** — 신규(현재 `TilemapMapView` 에 `Update()` 없음, 충돌 없음. F2a: struct 프로퍼티 직접 대입 금지):

```csharp
if (_rangeTilemap == null || _rangeCells.Count == 0 || _tileSet == null) return;
float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _tileSet.rangePulseSpeed);
float a = Mathf.Lerp(_tileSet.rangePulseMinAlpha, _tileSet.rangePulseMaxAlpha, t);
var c = _tileSet.rangeColor; c.a = a; // RGB=rangeColor, alpha=펄스
_rangeTilemap.color = c;
```

## 완료 기준

- compile.
- **tint 검증 우선(F2b)**: execute_code 또는 Play 로 `SetPlacementRange(center, 3)` 호출 →
  중심 제외, 경계 내 노란 타일이 칠해지고 alpha 가 오르내림을 **에디터 렌더로 육안/스크린샷 확인**.
  노란 tint / alpha 펄스가 안 보이면 README 의 per-cell 폴백(`SetTileFlags` + `SetColor`)으로 전환.
- `ClearPlacementRange()` 후 범위 타일 전부 사라짐.
- 경계 밖 셀은 안 칠해짐. 맵 재빌드(Initialize) 후에도 `_rangeTilemap` 누수/중복 없음.
