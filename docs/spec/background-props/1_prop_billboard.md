# Prop Billboard Component

**작업 구분**: 1 / Runtime

## 목적

생성된 prop prefab 의 root 에 붙는 런타임 빌보드 컴포넌트. PropData 를 source of truth 로 유지하고, 카메라를 향해 Visual 을 회전시킨다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/PropBillboard.cs` (신규)

## 구현

`Wassup.Presentation.PropBillboard : MonoBehaviour`, `[DisallowMultipleComponent]`.

SerializeField:

- `PropData data` — prefab 저장 시 generator 가 연결
- `Transform visualRoot` — `root/Visual`
- `SpriteRenderer spriteRenderer` — sprite 경로일 때
- `SkeletonAnimation skeletonAnimation` — spine 경로일 때
- `PropBillboardMode billboardMode` — `data.billboardMode` 미러

### Configure(propData, visual, sprite, skeleton)

Editor generator 가 호출. 필드 4개를 주입하고 `billboardMode = propData.billboardMode` 세팅.

### Awake → ApplyData()

PropData 를 prefab instantiate 시점에 다시 반영한다. 의미: **PropData SO 가 source of truth**. prefab 은 snapshot 이 아니라 cache. data 값을 에디터에서 바꾼 뒤 재진입하면 반영된다.

`ApplyData()` 동작:

- `billboardMode = data.billboardMode`
- target = `visualRoot ?? transform`
- `target.localPosition = data.visualOffset`
- `target.localScale = Vector3.one * max(0.01f, data.visualScale)`
- Sprite 경로: `spriteRenderer.sprite / color / sortingOrder` 갱신
- Spine 경로: `skeletonDataAsset` 갱신 + `initialSkinName = data.spineSkinName ?? "default"` + `Initialize(false)` + `idleAnimation` 이 존재하면 loop 재생

### LateUpdate

- `billboardMode == None` 이면 early return
- `Camera.main` 캐시 (첫 성공 후 재사용, main 변경되면 다음 프레임 갱신 불필요 — v0 스코프 밖)
- `FullCamera`: `target.rotation = _camera.transform.rotation`
- `YAxis`: `target - cam` 벡터의 y=0 projection 으로 `LookRotation`

## 계약

- runtime 에 `data = null` 이면 ApplyData 는 no-op. LateUpdate 는 billboardMode 값 기준으로 회전만 수행.
- Visual 자식의 transform 은 `PropBillboard` 가 배타적으로 제어한다. prefab 편집 시 Visual 의 rotation 을 수동 세팅해도 LateUpdate 에 덮어써진다.
- Sprite 와 Spine 은 상호 배타. `Configure` 에 둘 다 non-null 로 전달되면 동작 정의 없음 (generator 가 한쪽만 붙인다).

## 완료 기준

- `Configure` 호출 없이 prefab 에 부착된 채로 생성된 컴포넌트도 Awake 에서 `ApplyData` 가 안전하게 실행
- FullCamera mode 씬에서 카메라 회전 시 Visual 이 항상 카메라를 향함
- YAxis mode 에서 Visual 이 수직축만 회전 (기울어짐 없음)
- Spine skin `default` fallback 동작
