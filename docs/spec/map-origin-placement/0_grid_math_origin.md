# 0 — GridMath / MovementCellTrim 에 origin 파라미터 추가

## 목적

모든 grid↔world 변환의 토대인 순수 함수에 board origin 을 도입한다. **기본값 `default`(=zero) 파라미터**로 추가해 기존 호출부가 그대로 컴파일되도록 하고(origin=0 → 기존 동작 동일), 이후 작업 단위에서 호출부를 점진적으로 마이그레이션한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/GridMath.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs`
- 신규: `Assets/_Project/Tests/EditMode/GridMathOriginTests.cs` (또는 기존 GridMath 테스트 파일에 추가)

## 구현

`GridMath` (Burst 호환 유지, `float3 origin = default` 추가):

```csharp
public static int2 WorldToCell(float3 worldPos, float tileSize, int2 gridSize, float3 origin = default)
{
    float3 local = worldPos - origin;
    int cx = (int)math.floor(local.x / tileSize + 0.5f);
    int cy = (int)math.floor(local.z / tileSize + 0.5f);
    return new int2(math.clamp(cx, 0, gridSize.x - 1),
                    math.clamp(cy, 0, gridSize.y - 1));
}

public static float3 CellToWorldCenter(int2 cell, float tileSize, float y = 0f, float3 origin = default)
    => origin + new float3(cell.x * tileSize, y, cell.y * tileSize);
```

주의: `y` 와 `origin` 둘 다 기본값을 가지므로, `CellToWorldCenter(cell, ts, casterPos.y)` 같은 기존 3-인자 호출은 그대로 동작한다. origin 만 넘기려면 named arg(`origin: o`) 사용.

`MovementCellTrim.ClampToBoundary` 도 cell 경계를 월드로 환산하므로 origin 필요:

```csharp
public static float3 ClampToBoundary(float3 desired, int2 currentCell, float tileSize, float3 origin = default)
{
    float half = tileSize * 0.5f;
    return new float3(
        math.clamp(desired.x, origin.x + currentCell.x * tileSize - half, origin.x + currentCell.x * tileSize + half),
        desired.y,
        math.clamp(desired.z, origin.z + currentCell.y * tileSize - half, origin.z + currentCell.y * tileSize + half));
}
```

## 완료 기준

- [ ] compile green. 기존 GridMath/MovementCellTrim 호출부 수정 없이 빌드 통과.
- [ ] EditMode 테스트: `WorldToCell` / `CellToWorldCenter` 가 origin=0 일 때 기존 값과 동일, origin=(10,0,5) 일 때 round-trip(`CellToWorldCenter(c) → WorldToCell` == c) 성립.
- [ ] `ClampToBoundary` origin 적용 시 셀 경계가 origin 만큼 평행이동하는지 1 케이스 검증.

> ✅ 확인 2026-06-10 — Unity MCP EditMode 21/21 passed (GridMathTests 10 + MovementCellTrimTests 11), 컴파일 green, 콘솔 에러 0. 커밋: 8362150
