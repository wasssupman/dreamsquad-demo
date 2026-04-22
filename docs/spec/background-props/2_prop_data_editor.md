# Prop Data Editor

**작업 구분**: 2 / Editor

## 목적

`PropData` Inspector 에 `Generate Billboard Prefab` 버튼을 추가하고, Sprite/Spine 중 하나를 자동 해석해 root + Visual 구조의 prefab 을 생성/갱신한다.

## 변경 대상

- `Assets/_Project/Editor/PropDataEditor.cs` (신규)

## 구현

`Wassup.Editor.PropDataEditor : UnityEditor.Editor`, `[CustomEditor(typeof(PropData))]`.

`Editor/` 폴더는 asmdef 없이 default Assembly-CSharp-Editor 에 컴파일된다. `Wassup.Runtime.asmdef` 의 `autoReferenced: true` 로 `Wassup.Data.PropData` / `Wassup.Presentation.PropBillboard` 참조 가능.

### OnInspectorGUI

- `DrawDefaultInspector()` 호출
- `GUILayout.Button("Generate Billboard Prefab", Height 32)` → `GeneratePrefab(target)`

### GeneratePrefab(PropData)

1. `ResolveSprite(data)` 호출
2. sprite == null && !data.HasSpineVisual 이면 dialog 띄우고 return
3. `Directory.CreateDirectory("Assets/_Project/Prefabs/Props")`
4. 임시 root GameObject 생성: 이름 = `data.id ?? data.name`
5. 자식 `Visual` Transform 생성, `visualOffset / visualScale` 적용
6. `HasSpineVisual` 이면 Visual 에 `SkeletonAnimation` 추가, skin `default` fallback, `Initialize(false)`
7. 아니면 Visual 에 `SpriteRenderer` 추가, `sprite / spriteColor / sortingOrder` 세팅
8. root 에 `PropBillboard` 추가, `Configure(data, visual, spriteRenderer, skeletonAnimation)`
9. `PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Project/Prefabs/Props/{data.name}.prefab")`
10. 임시 root `DestroyImmediate`
11. `data.prefab = prefab`, `EditorUtility.SetDirty(data)`, `AssetDatabase.SaveAssets()`
12. Selection + PingObject 로 UX 마무리

### ResolveSprite(PropData) → Sprite

- `data.sprite` non-null 이면 그대로
- `data.sourceTexture ?? LoadSiblingTexture(data)` 로 Texture2D 확보
- Texture path 가 없으면 return null
- `TextureImporter.textureType != Sprite` 이면 `Sprite / Single / alphaIsTransparency=true` 로 `SaveAndReimport`
- 결과 `Sprite` 를 `data.sprite` 에 **write-back** 하고 `SetDirty`

### LoadSiblingTexture(PropData) → Texture2D

- `AssetDatabase.LoadAssetAtPath<Texture2D>("{folder}/{data.name}.png")`. `folder` 는 data asset path 의 부모.

## 계약

- Generator 는 **idempotent**: 같은 이름 prefab 은 overwrite. prefab 내부 수동 수정은 v0 에서 보존되지 않는다 (디자이너 수작업 단계는 v1 에서 overwrite 정책과 함께 도입).
- `ResolveSprite` 의 텍스쳐 재임포트는 부수효과. `.png.meta` 가 Sprite 모드로 변경되며 git diff 가 발생한다.
- Spine prop 의 material/shader override 는 generator 범위 밖 (후속 후보).

## 완료 기준

- Sprite sibling PNG 만 있는 PropData 에서 버튼 1회 클릭으로 prefab + sprite import 동시 처리
- Spine `SkeletonDataAsset` 만 있는 PropData 에서 버튼 1회 클릭으로 SkeletonAnimation prefab 생성
- 같은 PropData 로 재클릭 시 같은 prefab 을 overwrite (중복 생성 없음)
- `data.prefab` 필드가 자동 연결되고 Ping 으로 포커스
