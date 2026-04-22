# PropData SO

**작업 구분**: 0 / SO 계약

## 목적

프랍 1종을 정의하는 `PropData` ScriptableObject 를 추가한다. v0 는 1x1 footprint + sprite/spine 선택 + billboard mode 까지만. v1 확장 후보 필드는 README 후속 후보.

## 변경 대상

- `Assets/_Project/Scripts/Data/PropData.cs` (신규)

## 구현

`Wassup.Data.PropData : ScriptableObject`, `[CreateAssetMenu("Wassup/PropData", order = 20)]`.

필드 (v0):

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

## 계약

- `skeletonDataAsset` 이 있으면 Spine 경로 (Sprite 필드는 무시).
- `sprite` 가 있으면 그대로. 없고 `sourceTexture` 가 있으면 generator 가 Sprite import 로 전환한 뒤 `data.sprite` 에 write-back.
- Naming 은 권장 `prop_{name}_{x}_{y}` 지만 v0 에서는 강제하지 않는다. v1 에서 file name validator 가 `footprintX/Y` 와 일치 검사.
- `id` 가 있으면 prefab root GameObject 이름에 쓰고, 비면 `name` 사용. prefab 저장 파일명은 항상 `PropData.name`.

## 완료 기준

- `Assets/_Project/Data/Props/` 에 `Create > Wassup > PropData` 로 SO 생성 가능
- Inspector 에서 모든 필드 편집 가능
- `Footprint / HasSpriteVisual / HasSpineVisual` 프로퍼티 기대대로 계산
- Wassup.Runtime asmdef 안에서 compile 통과
