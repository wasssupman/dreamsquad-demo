# 1 — 메쉬 프랍 업라이트 지원

## 목적

`InstantiateProp` 이 3D 메쉬 프랍(`billboardMode == None`)을 **월드 기준 똑바로 세워** 배치하게 한다. 모든 프랍은 부모(XZ 바닥 90°X 회전, `TilemapMapView.cs:92`) 하위에 생성되는데, 재배향 없는 메쉬는 부모 회전을 상속해 바닥에 눕는다 — `PropBillboard.LateUpdate` 는 `None` 이면 early-return(`PropBillboard.cs:37`)이라 메쉬를 세워주지 않는다. billboard 경로(sprite/spine, PropBillboard 가 매 프레임 override)는 손대지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` (`InstantiateProp`)

## 구현

- `InstantiateProp` 에 `prop.billboardMode == PropBillboardMode.None` 분기:
  - `instance.transform.rotation = Quaternion.Euler(0f, placement.rotationYaw, 0f);` — `transform.rotation` 은 월드 기준이라 부모 90° 를 무효화 → 메쉬가 선다 + yaw 반영.
- billboard 모드(None 아님)는 기존 그대로: 부모회전 상속, PropBillboard 가 재배향(변경 없음).
- 접지: position 은 이미 `CellCenterToWorld`(셀 중앙 밑동). 메쉬 프리팹은 밑동=원점(pivot) 가정 — KayKit pivot 은 unit 2 authoring 에서 확인.
- 정렬/틴트: `MapView.ApplyPropSorting`/`ApplyPropGlobalTint` 는 SpriteRenderer 대상 → 순수 메쉬엔 no-op(정상, 메쉬는 depth-buffer 정렬). `AttachPropBlob` 은 월드크기 고정이라 메쉬에도 밑동 접지 유효.

## 완료 기준

- compile 통과 (`read_console` 클린).
- **회귀 가드**: 기존 `PropData` 에셋에 `billboardMode == None` 이 있는지 확인 — 있고 그게 눕는 걸 의도하면 별도 처리. (게임은 billboard 기반이라 대개 없음 → 구조물만 None.)
- 테스트: `billboardMode = None` KayKit 메쉬 1개를 배경/구조물 경로로 배치 → Play 게임뷰에서 **똑바로 서고** 셀 중앙 접지, yaw 반영. billboard 프랍 무변화(회귀 없음). 스크린샷 육안.
