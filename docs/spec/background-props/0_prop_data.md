# PropData SO

**작업 구분**: 0 / SO 계약

## 목적

프랍 1종을 정의하는 `PropData` ScriptableObject 를 추가한다. 현재 코드는 기본 prefab prototype 수준이지만, 최종 계약은 theme 별 `Data/Theme` 자산과 footprint placement 에서 사용할 수 있어야 한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/PropData.cs` (신규)

## 구현

`Wassup.Data.PropData : ScriptableObject`, `[CreateAssetMenu("Wassup/PropData", order = 20)]`.

현재 prototype 필드:

```csharp
// Identity
string id;           // runtime lookup 키. 비면 name 으로 fallback.
string displayName;

// Placement
int footprintX = 1;  // [Min(1)], 좌하단 셀 기준 +X 확장
int footprintY = 1;  // [Min(1)], 좌하단 셀 기준 +Y 확장
Vector3 visualOffset;       // Visual 자식의 local position
float visualScale = 1f;     // [Min(0.01f)]

// Generated
GameObject prefab;   // generator 가 채움

// Sprite path
Sprite sprite;
Texture2D sourceTexture;    // sprite 없을 때 sibling PNG 대체
Color spriteColor = Color.white;
int sortingOrder;

// Spine path (sprite 보다 우선)
SkeletonDataAsset skeletonDataAsset;
string spineSkinName;       // 비면 "default"
string idleAnimation = "idle";

// Billboard
PropBillboardMode billboardMode = PropBillboardMode.FullCamera;
```

헬퍼:

```csharp
public Vector2Int Footprint => new(Mathf.Max(1, footprintX), Mathf.Max(1, footprintY));
public bool HasSpriteVisual => sprite != null || sourceTexture != null;
public bool HasSpineVisual => skeletonDataAsset != null;
```

`PropBillboardMode` enum: `FullCamera`, `YAxis`, `None`.

## 최종 배치 계약

- 권장 저장 경로는 `Assets/_Project/Data/Theme/{themeName}/prop_{name}_{x}_{y}.asset`.
- `{x}` 와 `{y}` 는 `footprintX/Y` 와 일치해야 한다.
- 동일 basename 이미지는 `Assets/_Project/Art/Theme/{themeName}/prop_{name}_{x}_{y}.png` 에 둔다.
- `footprintX/Y` 는 맵 배경 타일 영역에서 실제 점유 크기다.
- footprint 기준점은 좌하단 셀이다.
- 맵 생성 후 placement 알고리즘은 이 footprint 로 bounds/allowed tile/occupancy 를 검사한다.

## Visual 계약

- `skeletonDataAsset` 이 있으면 Spine 경로 (Sprite 필드는 무시).
- `sprite` 가 있으면 그대로. 없고 `sourceTexture` 가 있으면 generator 가 Sprite import 로 전환한 뒤 `data.sprite` 에 write-back.
- `id` 가 있으면 prefab root GameObject 이름에 쓰고, 비면 `name` 사용. prefab 저장 파일명은 항상 `PropData.name`.

## 후속 필드 후보

초기 placement 는 최소 필드로 시작한다. 다음 필드는 배치 룰이 필요해지는 시점에 추가한다.

```csharp
int weight;                       // 랜덤 선택 가중치
PropPlacementSurface surface;      // TileOnly / DecorOnly / Both
MapTileType[] allowedTiles;        // 기본 Deco / Env
bool blocksPlacement;              // Place 타일 소모 여부
bool allowRotation;                // 2x1 <-> 1x2 회전 허용
Vector2 randomOffsetRange;
Vector2 randomScaleRange;
```

## 완료 기준

- `Assets/_Project/Data/Theme/{themeName}/` 에 `Create > Wassup > PropData` 로 SO 생성 가능
- Inspector 에서 모든 필드 편집 가능
- `Footprint / HasSpriteVisual / HasSpineVisual` 프로퍼티 기대대로 계산
- Wassup.Runtime asmdef 안에서 compile 통과
