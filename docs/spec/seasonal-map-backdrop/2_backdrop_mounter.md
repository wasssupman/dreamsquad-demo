# 2. BackdropMounter + AnchorTable + Shader

## 목적

런타임 컴포지션 단일 게이트웨이를 구현한다. SO + 카메라 + map 정보를 받아 `_Backdrop` root GameObject 1개를 인스턴스화하고, Unmount 시 정리한다.

## 변경 대상

신규 스크립트 (`Assets/_Project/Scripts/Presentation/Backdrop/`)

- `BackdropMounter.cs`
- `BackdropAnchorTable.cs`

신규 셰이더 (`Assets/_Project/Shaders/`)

- `Backdrop_Unlit.shader`

신규 테스트 (`Assets/_Project/Tests/EditMode/`)

- `BackdropAnchorTableTests.cs`

## 구현

### BackdropAnchorTable.cs

순수 함수. `(EdgeAnchor anchor, Vector3 boardCenter, Vector2 boardHalfWorld, float paddingTiles, float tileSize)` → `Vector3 worldPos`.

```csharp
public static class BackdropAnchorTable
{
    public static Vector3 Resolve(EdgeAnchor anchor, Vector3 boardCenter,
                                  Vector2 boardHalfWorld, float paddingTiles, float tileSize)
    {
        float pad = paddingTiles * tileSize;
        float xL = boardCenter.x - boardHalfWorld.x;
        float xC = boardCenter.x;
        float xR = boardCenter.x + boardHalfWorld.x;
        float zS = boardCenter.z - boardHalfWorld.y;  // South
        float zN = boardCenter.z + boardHalfWorld.y;  // North
        float zM = boardCenter.z;                     // Middle

        return anchor switch
        {
            EdgeAnchor.NorthLeft     => new Vector3(xL,         boardCenter.y, zN + pad),
            EdgeAnchor.NorthCenter   => new Vector3(xC,         boardCenter.y, zN + pad),
            EdgeAnchor.NorthRight    => new Vector3(xR,         boardCenter.y, zN + pad),
            EdgeAnchor.EastTop       => new Vector3(xR + pad,   boardCenter.y, zN),
            EdgeAnchor.EastMiddle    => new Vector3(xR + pad,   boardCenter.y, zM),
            EdgeAnchor.EastBottom    => new Vector3(xR + pad,   boardCenter.y, zS),
            EdgeAnchor.SouthRight    => new Vector3(xR,         boardCenter.y, zS - pad),
            EdgeAnchor.SouthCenter   => new Vector3(xC,         boardCenter.y, zS - pad),
            EdgeAnchor.SouthLeft     => new Vector3(xL,         boardCenter.y, zS - pad),
            EdgeAnchor.WestBottom    => new Vector3(xL - pad,   boardCenter.y, zS),
            EdgeAnchor.WestMiddle    => new Vector3(xL - pad,   boardCenter.y, zM),
            EdgeAnchor.WestTop       => new Vector3(xL - pad,   boardCenter.y, zN),
            _ => boardCenter,
        };
    }
}
```

테스트는 12 anchor × 2 boardHalf × 2 padding 조합으로 결정적 좌표를 비교한다.

### Backdrop_Unlit.shader

URP-호환 Unlit. 특징:
- `_MainTex` (백드롭 텍스처), `_TintColor`
- `ZWrite Off`, `ZTest Always`
- Render Queue `"Background+10"`
- Fog 무시 (`#pragma multi_compile_fog` 미사용)
- Cull Off

타일/캐릭터 셰이더와 무관하게 항상 가장 뒤에 그려지도록 `Background+10`. 본 스펙은 Backdrop 전용 layer 를 만들지 않고 RenderQueue 만으로 정렬한다.

### BackdropMounter.cs

```csharp
public static class BackdropMounter
{
    public static GameObject Mount(GeneratedMap map, Camera camera,
                                   SeasonBackdropData data, float tileSize);
    public static void Unmount(ref GameObject root);
}
```

Mount 절차:

1. `root = new GameObject("_Backdrop")`. `root.transform.position = Vector3.zero`.
2. **Backdrop quad child**:
   - `position = camera.transform.position + camera.transform.forward * data.backdropDistance`
   - `heightWorld = data.backdropHeightWorld`
   - `widthWorld = heightWorld * camera.aspect`
   - `localScale = (widthWorld, heightWorld, 1)`
   - `transform.LookAt(camera.transform.position, Vector3.up)` → 그 후 180° y 회전 (quad 의 정면이 카메라를 향하도록)
   - material 은 `Backdrop_Unlit` shader + `data.farBackdropTexture` + `data.backdropTint`
   - mesh 는 `PrimitiveType.Quad` (`GameObject.CreatePrimitive` 후 BoxCollider 제거)
3. **EdgeProps child** (`_EdgeProps` 빈 GameObject 묶음):
   - `boardHalfWorld = (gridSize.x * tileSize / 2, gridSize.y * tileSize / 2)`
   - `boardCenter = (gridSize.x * tileSize / 2, 0, gridSize.y * tileSize / 2)` — `BattleBridge.FrameMainCameraForMap` 의 center 식과 같지만, `(gridSize - 1)` 이 아닌 `gridSize` 기준임에 주의 (실제 보드 외곽까지의 거리). 차이가 발생하면 `FrameMainCameraForMap` 과 동일한 식으로 통일한다.
   - 각 entry e:
     - `basePos = BackdropAnchorTable.Resolve(e.anchor, boardCenter, boardHalfWorld, data.edgePadding, tileSize)`
     - `worldPos = basePos + new Vector3(e.worldOffset.x, 0, e.worldOffset.y)`
     - `lookDir = boardCenter - worldPos; lookDir.y = 0;`
     - `rot = (lookDir.sqrMagnitude > 1e-4 ? Quaternion.LookRotation(lookDir, Vector3.up) : Quaternion.identity) * Quaternion.Euler(0, e.yawDegrees, 0)`
     - `var go = Object.Instantiate(e.propData.prefab, worldPos, rot, edgePropsParent.transform)`
     - `go.transform.localScale *= e.scaleMultiplier`
     - **PropBillboard 비활성화**: `if (go.TryGetComponent<PropBillboard>(out var pb)) pb.enabled = false;` — EdgeProp 은 정적 풍경으로 카메라 추종 회전을 끈다. (PropData.billboardMode 가 None 이어도 이중 안전망.)

Unmount 절차: `if (root != null) Object.Destroy(root); root = null;`

`Camera.main` 이 null 인 비정상 경로는 spec 사전조건 위반 (3번 단위에서 차단). Mount 는 valid camera 를 받는다고 가정한다.

## 완료 기준

- 컴파일 clean.
- `BackdropAnchorTableTests.cs` 12 anchor × 2 조합 = 최소 24 케이스 단위 테스트 통과.
- Mount 호출 시 `_Backdrop` root + Quad child + EdgeProps 부모 + N 개 prop child 가 콘솔 에러 없이 생성. (시각 검증은 6번 단위.)
- 인스턴스화된 EdgeProp 의 PropBillboard 가 disabled 상태로 확인.

## 의존

- 선행: 1번 (데이터 모델)
- 후행: 3번 (Bridge 가 호출), 6번 (Play 검증)

확인 일자: 2026-05-10 / 커밋: 729f1e9

## Revision 2026-05-22 — Skybox/Panoramic 전환

unit 7~9 (Lava/Lunar/Cosmic) 스코프 확장 시 backdrop 표현이 **카메라 정면 quad → URP Skybox/Panoramic** 으로 전환됐다.

- `BackdropMounter.Mount` 가 `RenderSettings.skybox` 에 panoramic 머티리얼 주입 + `Camera.clearFlags = Skybox`. `Unmount` 가 이전 skybox/clearFlags 복원.
- `SeasonBackdropData` 에 `skyboxExposure (0~8)` / `skyboxRotationDegrees (0~360)` 필드 추가.
- backdrop 텍스처 사양: 4096×2048 equirectangular PNG, 좌우 seam 일치 (`_Mapping = 1`, `_ImageType = 0`).
- 기존 `Backdrop_Unlit` 셰이더는 사용처가 없어지지만 자산 파일은 남겨둔다 (후속 활용 가능성).
