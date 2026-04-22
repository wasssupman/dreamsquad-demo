# Theme Asset Layout

**작업 구분**: 4 / Asset Pipeline

## 목적

Theme 별 `PropData` SO 와 원본 이미지를 파일명으로 매칭해, 디자이너가 정해진 경로에 자산을 넣으면 generator/placement 가 일관되게 사용할 수 있게 한다.

## 경로 규칙

ScriptableObject:

```text
Assets/_Project/Data/Theme/{themeName}/prop_{name}_{x}_{y}.asset
```

원본 이미지:

```text
Assets/_Project/Art/Theme/{themeName}/prop_{name}_{x}_{y}.png
```

생성 prefab:

```text
Assets/_Project/Prefabs/Props/{themeName}/prop_{name}_{x}_{y}.prefab
```

예:

```text
Assets/_Project/Data/Theme/forest/prop_tree_1_1.asset
Assets/_Project/Art/Theme/forest/prop_tree_1_1.png
Assets/_Project/Prefabs/Props/forest/prop_tree_1_1.prefab
```

Unity 에서 SO 파일 확장자는 `.asset` 이다. 사용자 문맥의 `.SO` 는 `ScriptableObject asset` 의미로 본다.

## 이름 파싱

basename 은 다음 정규식을 따른다.

```text
^prop_(?<name>.+)_(?<x>[1-9][0-9]*)_(?<y>[1-9][0-9]*)$
```

규칙:

- `x`, `y` 는 `PropData.footprintX/Y` 와 일치해야 한다.
- 불일치하면 Editor validation warning 을 띄운다.
- `PropData.id` 가 비어 있으면 basename 을 runtime id 로 사용한다.
- 이미지 매칭은 같은 `{themeName}` 과 같은 basename 만 허용한다.

## Generator 매칭 순서

`PropDataEditor.Generate` 또는 batch generator 는 visual source 를 다음 순서로 해석한다.

1. `skeletonDataAsset` 이 있으면 Spine 경로 사용.
2. `sprite` 가 있으면 명시 Sprite 사용.
3. `sourceTexture` 가 있으면 해당 Texture2D 를 Sprite import 로 보정.
4. 없으면 `Assets/_Project/Art/Theme/{themeName}/{PropData.name}.png` 를 찾는다.
5. theme 경로가 아니면 기존 prototype 호환으로 SO 와 같은 폴더의 `{PropData.name}.png` 를 찾는다.

## Theme 로딩

초기 구현은 `MapThemeData` 에 명시 배열을 둔다.

```csharp
public PropData[] tileProps;
public PropData[] decorProps;
```

자동 folder scan 은 editor tool 범위로 둔다. runtime 은 `MapThemeData` 에 들어 있는 참조만 사용한다.

## 완료 기준

- `Data/Theme/{themeName}` 의 `prop_*_x_y.asset` 와 `Art/Theme/{themeName}` 의 동명 `.png` 매칭 가능.
- 파일명 footprint 와 `PropData.footprintX/Y` 불일치 warning.
- generator 결과 prefab 이 `Prefabs/Props/{themeName}/` 아래 생성.
- 기존 prototype 경로 `Assets/_Project/Data/Props/` 도 깨지지 않음.
