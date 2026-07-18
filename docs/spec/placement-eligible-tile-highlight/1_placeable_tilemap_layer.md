# 1 — 배치 하이라이트 타일맵 레이어

## 목적

`TilemapMapView` 에 전용 `_placeableTilemap` 을 추가하고, 배치 가능 셀 집합을 받아 은은한 fill+림으로
밝힌다. 정적(페이드인 후 고정), 펄스 없음. 드래그 중엔 range 처럼 유닛 위로 상승. `_rangeTilemap` 과 분리.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Core/TilemapMapView.cs`

## 구현

### Ensure (EnsureRangeTilemap 미러 — 상승 반영)

`EnsureRangeTilemap()`(order `_highlightAbove ? 10000 : -12`) 을 미러해 `EnsurePlaceableTilemap()` 신설:

```csharp
private Tilemap _placeableTilemap;
private bool _placeableActive;
private float _placeableShowTime; // unscaledTime 캡처(페이드 기준)

private void EnsurePlaceableTilemap()
{
    if (_placeableTilemap != null) return;
    // ... grid.transform 자식 생성 ...
    r.sortingOrder = _highlightAbove ? 9998 : -13; // 정적 −13 / 드래그 상승 시 9998(range 10000 아래·유닛 위)
}
```

`SetPlacementHighlightAboveUnits(bool above)` 에 `_placeableTilemap` 도 함께 상승/하강 추가
(range 와 나란히): `if (_placeableTilemap != null) SetOrder(_placeableTilemap, above ? 9998 : -13);`
그리고 lazy 생성 시 `_highlightAbove` 반영은 Ensure 안에서 위처럼(range 함정 동일 처리).

### Set / Clear

```csharp
// placeable = 배치 가능 셀(bridge 가 SpatialPlacementCheck==None 으로 수집). 균일 tint(per-cell 색 없음).
public void SetPlacementHighlight(IReadOnlyList<Vector2Int> placeable)
{
    EnsurePlaceableTilemap();
    var tile = _tileSet != null ? _tileSet.placeableTile : null;
    if (tile == null) return;                    // placeableTile 미할당 방어(no-op)
    if (!_placeableActive)
    {
        _placeableActive = true;
        _placeableShowTime = Time.unscaledTime;
        var c0 = _tileSet.placeableColor;
        _placeableTilemap.color = new Color(c0.r, c0.g, c0.b, 0f); // 첫 프레임 흰 번쩍 방지
    }
    _placeableTilemap.ClearAllTiles();
    foreach (var c in placeable)
        _placeableTilemap.SetTile(new Vector3Int(c.x, c.y, 0), tile);
    // 색/알파는 Update() 소유. 리프레시(_placeableActive 유지)면 showTime 안 리셋 → 재페이드 없음.
}

public void ClearPlacementHighlight()
{
    if (_placeableTilemap != null) _placeableTilemap.ClearAllTiles();
    _placeableActive = false;
}
```

### Update — 페이드 알파 소유 (펄스 없음)

`Update()`(range 펄스 소유) 에 블록 추가:

```csharp
if (_placeableActive && _placeableTilemap != null && _tileSet != null)
{
    float t = _tileSet.placeableFadeInDuration > 0f
        ? Mathf.Clamp01((Time.unscaledTime - _placeableShowTime) / _tileSet.placeableFadeInDuration) : 1f;
    var c = _tileSet.placeableColor;
    _placeableTilemap.color = new Color(c.r, c.g, c.b, c.a * t); // 0→목표, 이후 고정(펄스 없음)
}
```

전역 `.color` tint(range 펄스와 동형). 림/플랫폼 형태는 `placeableTile` 스프라이트가 소유.

### Clear() teardown

`Clear()`(RebuildDraftMap 재진입) 에 `_placeableTilemap` 정리 — `ClearAllTiles` + `_placeableActive=false`.

## 완료 기준

- 컴파일 0 errors.
- 에디터/Play: `SetPlacementHighlight` 시 지정 셀이 은은한 시안+림으로 밝아지고 0.2s 페이드인, 이후 정적.
  `ClearPlacementHighlight` 로 즉시 소거.
- `_rangeTilemap`(노란 사거리)과 동시 표시 시 둘 다 읽힘(하이라이트 정적·저알파, 사거리만 펄스).
- 드래그 시작 시 `SetPlacementHighlightAboveUnits(true)` → 하이라이트가 9998 로 상승해 밀집 전투 중 적
  빌보드 위로 보임(range 10000 아래 유지).
- `placeableTile` 미할당 시 no-op. `Clear()` 재진입 후 잔류/누수 없음.

- [x] 2026-07-18 · 컴파일 0 err/0 warn (MCP 검증) · (이 커밋). 시각 검증은 unit 2 배선 + placeableTile 스프라이트 후 일괄.
