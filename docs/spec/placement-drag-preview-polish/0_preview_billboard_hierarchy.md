# 0 — 프리뷰 빌보드 계층 정합 (버그 수정)

## 목적

드래그 프리뷰가 배치된 유닛과 다른 각도(꼿꼿이 섬)로 보이는 버그를 수정한다.
배치 유닛과 동일 Billboard 틸트를 적용하되, unit 1 sway 가 얹힐 **최종 2노드 계층**을 이 unit 에서 구성한다.

## 배경 (원인)

- 배치 유닛: `SpineUnitView.Setup` 이 `Billboard`(Tilted, `CharacterBillboardTilt` ≈ 45°) 부착 →
  월드 X 45° 틸트로 카메라를 향해 섬.
- 프리뷰: `TryCreateSpinePreview` 는 raw `SkeletonAnimation` 만 생성, **Billboard 미부착** → 틸트 0(꼿꼿).

## 변경 대상

- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`

## 구현

`TryCreateSpinePreview` 를 2노드 계층으로 재구성:

- **root** = 빈 GameObject(`DragPreview_{name}`). `Billboard` 부착 →
  `billboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt)`.
  드래그 position / scale / SetActive / Destroy 의 대상.
- **child** = `SkeletonAnimation` 보유 GameObject. `root` 자식,
  `localPosition = 0, localRotation = identity`. 기존 skin / animation / alpha(0.62) 세팅은 child 의 skeleton 에 그대로.
  스케일: 기존 `spineVisualScale * CharacterVisualScale` 로직을 child(또는 root) 에 적용 —
  uniform scale 이라 어디 두어도 동일(정리상 root 에 두고 child 는 identity 권장).
- **참조 보관**: `_session.preview = root`. child 는 unit 1 이 회전하므로 참조를 남긴다 —
  `DragSession` 에 `Transform swayPivot`(= child) 필드 추가(또는 `root.GetChild(0)` 로 조회).
  계약: **sway 는 이 child(swayPivot) 를 회전**한다.
- `using Wassup.Presentation;` 추가(또는 풀네임 `Wassup.Presentation.Billboard` / `BillboardMode`).
- `UpdateDrag` 의 `preview.transform.position` / `SetActive`, `CleanupSession` 의 `Destroy` 는
  전부 root 대상이라 로직 변경 없이 동작.

## 완료 기준

- compile.
- Play: 카드 드래그 시 프리뷰가 배치된 유닛과 **같은 기울기**로 카메라를 향해 섬(정면 꼿꼿 X).
  스크린샷으로 드래그 프리뷰 vs 배치된 동일 유닛 각도 일치 확인.
- 드래그 이동 / 드롭 / 취소 정상(position / SetActive / Destroy 가 root 대상이라 회귀 없음).
- child(swayPivot) 참조가 확보되어 unit 1 이 회전 대상으로 쓸 수 있음.
