# 0. GridMath 헬퍼 추가

## 목적

Chebyshev 거리 함수와 float range → int 타일 변환 헬퍼를 추가한다.  
이후 모든 단위가 이 두 함수만 의존하며, 별도 싱글턴 추가 없음.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/GridMath.cs`

## 구현

기존 `WorldToCell` / `CellToWorldCenter` / `CellIndex` 아래에 추가:

```csharp
[BurstCompile]
public static int ChebyshevDistance(int2 a, int2 b)
    => math.cmax(math.abs(a - b));

// half-away-from-zero 반올림. math.round(banker's) 미사용.
public static int RangeToTiles(float r)
    => (int)(r + 0.5f);
```

두 함수 모두 Burst 호환 (unmanaged 연산만 사용).

## 완료 기준

- [ ] compile error 0
- [ ] EditMode: `ChebyshevDistance(int2(0,0), int2(1,1)) == 1` (대각 = 1)
- [ ] EditMode: `ChebyshevDistance(int2(0,0), int2(2,1)) == 2`
- [ ] EditMode: `RangeToTiles(0.5f) == 1`, `RangeToTiles(1.5f) == 2`, `RangeToTiles(4.5f) == 5`, `RangeToTiles(5.5f) == 6`
- [ ] EditMode: `RangeToTiles(3f) == 3`
