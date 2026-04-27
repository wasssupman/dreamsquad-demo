# 4. Region UV Continuity (Option A)

**전제**: `3_place_edge_mask_widen` 까지 적용. 사용자 보고 — region 내부 셀 격자선이 잔존해 "모자이크 퍼즐" 인상. 본 작업은 그 root cause 인 **mesh 셀 단위 seam** 을 해소.

## 핵심 발견

`MapView.CreateTiledSurfaceMesh` 가 `xSegments × ySegments` **개별 quad** 를 생성. 각 quad 는 UV (0,0)~(1,1) 로 텍스처 한 바퀴 보여줌. 이 구조에서:

- 셀 간 **mesh vertex seam** 이 grid 형태로 시각화됨 (같은 머티리얼이라도 mesh 가 셀 단위로 나뉘어 셀 경계 line 가시화)
- 텍스처 자체의 left↔right / top↔bottom 이음매가 셀 경계마다 visible
- 결과: region 내부가 1셀 단위 모자이크처럼 읽힘

해결: **region run 전체를 single quad** 로 만들고 UV 를 (0~xTiles, 0~yTiles) 로 scale → 텍스처는 wrap 으로 반복 (texture wrap mode = Repeat 전제), mesh seam 은 사라짐.

## 변경 대상

- `Assets/_Project/Scripts/Core/MapView.cs`:
  - `CreateTiledSurfaceMesh(float width, float height, int xSegments, int ySegments)` 시그니처 → `(float width, float height, float xTiles, float yTiles)` 로 변경
  - 구현 본체를 single quad 로 단순화
  - 호출부 3개 (line 268, 293, 386) 는 시그니처 자동 호환 (int → float 묵시 변환)

## 구현 가이드

```csharp
private static Mesh CreateTiledSurfaceMesh(float width, float height, float xTiles, float yTiles)
{
    var mesh = new Mesh { name = "BoardSurfaceQuad" };
    float halfW = width * 0.5f;
    float halfH = height * 0.5f;
    mesh.vertices = new Vector3[]
    {
        new Vector3(-halfW, 0f, -halfH),
        new Vector3(-halfW, 0f,  halfH),
        new Vector3( halfW, 0f,  halfH),
        new Vector3( halfW, 0f, -halfH),
    };
    mesh.uv = new Vector2[]
    {
        new Vector2(0f, 0f),
        new Vector2(0f, yTiles),
        new Vector2(xTiles, yTiles),
        new Vector2(xTiles, 0f),
    };
    mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
    mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
    mesh.RecalculateBounds();
    return mesh;
}
```

## 절차

1. `CreateTiledSurfaceMesh` 위 구현으로 교체.
2. 컴파일 통과 (Unity Console error 0).
3. Editor → Play (Battle 씬).
4. 시각 평가:
   - Place region 내부에 격자선이 사라졌는가?
   - Env region 도 동일하게 부드러워졌는가?
   - 텍스처 wrap 으로 인한 좌우 / 상하 이음매가 보이는가? (seamless 가 아니면 등간격 seam 잔존)
5. 결과 보고 + 후속 결정.

## 결과 분기

| 결과 | 다음 |
|---|---|
| **격자선 거의 사라짐. region 이 연속 면으로 읽힘** | 본 spec 종료. handoff + 커밋 |
| **격자선은 사라졌으나 텍스처의 좌우/상하 이음매가 등간격 seam 으로 보임** | 텍스처 자체가 seamless 가 아님. **Option A 한계 도달**. Option D (composite 2 layer + 새 base 텍스처) 로 진입. 본 spec 안에서 새 work unit `5_composite_continuity.md` 또는 별도 spec |
| **격자선이 mis-align 된 채 잔존** | UV scale 비정수 시도 (예: xTiles → xTiles * 0.7) 로 mis-align. 본 work unit 안에서 반복 |
| **다른 부작용 (Place edge overlay 와 충돌, hover overlay 깨짐 등)** | 회귀 — fix 후 재시도 |

## 완료 기준

- `CreateTiledSurfaceMesh` 가 single quad 생성으로 교체됨.
- 컴파일 / Play / region 내부 격자선 시각 평가 결과 사용자 OK.
- inner / outer corner overlay 회귀 없음.
- hover / flash 인터랙션 회귀 없음.
- Place ↔ Walk / Env edge fringe 유지 (작업 단위 2/3 효과 보존).

## 주의

- 텍스처 wrap mode 는 Repeat 전제. Clamp 면 single texture 가 stretch 됨. forest 의 tile_place / grass 텍스처는 default Repeat 일 가능성 높지만 결과 이상하면 import 설정 확인.
- mesh seam 이 사라져도 **텍스처 자체의 이음매 (left↔right / top↔bottom 컬러 차이)** 는 wrap 에서 그대로 노출. 이건 텍스처 작업 영역 (Option D 로 진입).
- single quad 로 바꾸면 vertex 수 감소 → GPU 부담 감소 (긍정적 부수효과).

확인 일자: 2026-04-27 — 통과. 1차 single-quad refactor 후 row 간 seam 잔존 → per-region single mesh 로 escalate (`BuildRegionSurfaceMesh`). 결과 Place / Env region 이 연속 면으로 보임. Place edge fringe / overlay 회귀 없음. 사용자 캡처 첨부 (스크린샷 2026-04-27 오후 4.55.30.png). Env per-cell texture variation 은 기각 (region anchor 텍스처만 사용).
