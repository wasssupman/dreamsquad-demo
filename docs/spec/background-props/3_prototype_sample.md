# Prototype Sample

**작업 구분**: 3 / Sample + 전체 완료 검증

## 목적

위 0~2 작업 단위가 end-to-end 로 동작함을 증명하는 최소 샘플을 커밋한다. 1x1 footprint + sprite 경로 + FullCamera billboard 의 가장 단순한 조합.

## 변경 대상

- `Assets/_Project/Data/Props/prop_prototype_1_1.asset` (신규 PropData SO)
- `Assets/_Project/Prefabs/Props/prop_prototype_1_1.prefab` (Generator 결과)
- `Assets/_Project/Data/Sprites/Sprite_Diamond.png.meta` (ResolveSprite 부수효과, textureType Default → Sprite)

## 구현

샘플 PropData 값:

```yaml
id: prop_prototype_1_1
displayName: Prototype Prop 1x1
footprintX: 1
footprintY: 1
visualOffset: {x: 0, y: 0.55, z: 0}   # 타일 중심 위 띄움 default
visualScale: 1
sprite: Sprite_Diamond
sourceTexture: Sprite_Diamond.png
spriteColor: white
sortingOrder: 0
billboardMode: FullCamera
```

생성 절차:

1. `Assets/_Project/Data/Props/` 에 `prop_prototype_1_1` asset 생성
2. `sourceTexture` 에 `Sprite_Diamond.png` 연결
3. Inspector 의 `Generate Billboard Prefab` 클릭
4. `ResolveSprite` 가 Diamond 텍스처를 Sprite 로 re-import 하고 `data.sprite` 에 write-back
5. `Assets/_Project/Prefabs/Props/prop_prototype_1_1.prefab` 자동 생성
6. `data.prefab` 필드에 prefab 참조 자동 연결

## 계약

- `visualOffset.y = 0.55` 는 이 샘플의 관례값. 타일 상면 위에 띄우기 위해 경험적으로 정한 수치이며, 공식 default 는 아니다. 실제 프랍별로 조정한다.
- Sprite_Diamond 의 textureType 변환은 generator 가 의도적으로 수행한 것이므로 `.meta` 변경은 커밋에 포함한다.
- 이 샘플은 MapView 에 배치되지 않는다. 씬에 manual drop 후 카메라 회전으로 billboard 동작만 확인.

## 완료 기준

- 빈 씬에 `prop_prototype_1_1.prefab` 을 drop 하면 Diamond sprite 가 직립 표시
- Scene 뷰에서 카메라를 회전해도 sprite 가 항상 카메라를 향함 (FullCamera)
- `billboardMode` 를 `YAxis` 로 바꾸면 Y축 기준으로만 회전 (기울어짐 없음)
- `billboardMode` 를 `None` 으로 바꾸면 회전 정지
- Unity console 에 error/warning 없음
- `background-props` README 의 "완료 확인" 체크박스 체결 가능
