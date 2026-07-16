# 0 — 셀 스냅 리졸버 (순수 함수 + 테스트)

**작업 구분**: foundation

## 목적

포커스 셀 선택에 히스테리시스를 주는 순수 함수를 만든다. 좌표 변환/브리지 배선과 무관하게
"이전 셀 + 소수 셀 좌표 + 여유(margin) → 새 정수 셀" 만 결정한다. 제약 10(아키텍처 중립 로직은
순수 함수 + EditMode 테스트). 이 단위는 배선하지 않는다(다음 단위에서 소비).

## 변경 대상

- New: `Assets/_Project/Scripts/UI/PlacementCellSnap.cs` (static class, `Wassup.UI` 네임스페이스)
- New: `Assets/_Project/Tests/EditMode/PlacementCellSnapTests.cs`

## 구현

```
public static class PlacementCellSnap
{
    // frac = (sim - boardOrigin) / tileSize 의 (x, z). 셀 중심 = 정수, 경계 = ±0.5.
    // current == null → 그냥 round(frac). 아니면 축별로 [current-0.5-margin, current+0.5+margin]
    // 밴드 안이면 current 유지, 벗어나면 round(frac). x/y 독립. gridSize 로 clamp.
    public static Vector2Int Resolve(Vector2Int? current, Vector2 frac, float stickMargin, Vector2Int gridSize);
}
```

- 축별 round = `Mathf.FloorToInt(f + 0.5f)` (GridMath.WorldToCell 과 동일 half-up).
- 유지 조건(축): `current` 있을 때 `f ∈ [current - (0.5f + margin), current + (0.5f + margin)]` 이면 `current` 축 유지, 아니면 `round(f)`.
- `stickMargin` 은 `[0, 0.49]` 로 clamp(0.5 이상이면 이웃 진입 불가). 음수는 0.
- 결과는 `Mathf.Clamp(_, 0, gridSize.-1)`.
- 순수·결정론. `Time`/`EntityManager`/`SkeletonAnimation` 등 아키텍처 타입 미참조.

## 완료 기준

- `Assets/_Project/Tests/EditMode/` 위치(제약: Scripts 아래 X)에서 EditMode 통과.
- 테스트 케이스(회귀 방지 수준):
  - current=null → round 동작(0.4→0, 0.6→1, 2.5→3 half-up).
  - 경계 지터 흡수: current=(3,y), frac.x 가 3.5±margin 안에서 진동 → (3) 유지.
  - 확실한 전환: frac.x 가 3.5+margin 초과 → 4 로 전환.
  - 반대 방향 대칭(2.5-margin 미만 → 2).
  - x/y 독립(한 축만 전환).
  - margin=0 → 순수 round 와 동일. margin 0.5+ clamp.
  - grid 경계 clamp.
- 전체 EditMode 스위트가 이 파일로 새 실패를 만들지 않음(무관 사전 실패는 제외).
