# 7 — armed 보드 드래그 실루엣 (rev 4, 2026-08-28 사용자 발의)

## 목적

탭 arm 후 보드를 **드래그**할 때, 지금은 range 격자 + footprint 고스트만 손가락을 따라간다 —
유닛 자신은 트레이에 남아 있어 «누가 어디에 서게 되는가» 의 절반(누가)이 화면에 없다.
드래그 승격 후에는 **유닛 실루엣(반투명 Spine)이 footprint 뷰 중심에 서서 스카우트를 따라다니게** 한다.

**무변 계약**: ① 트레이 D&D(키링 프리뷰) 무변 ② 탭-탭(즉시 배치) 무변 — 실루엣은 드래그
**승격 후**(`_boardDragging`)에만 등장하므로 탭 경로는 픽셀 하나 안 바뀐다. ③ sim 무변(뷰 전용).

> placement-armed-board-drag unit 1 의 «키링 유닛은 띄우지 않는다. 유닛은 트레이에 남는다» 계약의
> 의도적 개정이다 — 그 계약은 press-스카우트(탭 포함) 전 구간에 대한 것이었고, 이번 개정은
> 드래그 승격 구간만 연다. press~승격 전 구간은 여전히 유닛 없이 range+고스트만.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 실루엣 상태·빌더·추종·정리
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — ⑮ 노브 3종

## 구현

1. **등장 조건** = `_armedUnit != null && _boardDragging && 스카우트 앵커 유효`. 승격 후 첫
   스카우트 프레임에 lazy 생성, 등장 프레임은 위치 **스냅**(멀리서 미끄러져 오지 않게).
2. **비주얼** = Spine 실루엣만 — 키링 하드웨어(고리/줄/스윙) 없음. 보드에 «서 있는» 표현이므로
   root 에 `Billboard(Tilted, CharacterBillboardTilt)`(스폰 뷰와 동일 규약), Spine 자식은
   localBounds 로 **발이 root 원점**에 오게 정렬(키링의 머리-정렬과 반대). 스킨 합성은
   `SpineCombinedSkinCache.Apply` 재사용, 애니 = `idle → attack` 폴백(드래그 버둥 애니 아님 —
   서 있는 그림), 알파 = `SetPreviewAlpha`(노브), sortingOrder = `BoardSortOrder.DragPreviewOrder`.
   `skeletonDataAsset` 없는 유닛은 실루엣 생략(캡슐 폴백 안 만듦 — 위치는 고스트가 전담).
3. **위치** = `bridge.GridAnchorToViewCenter(anchor, unit)` — unit 2 의 뷰 피드 그대로(footprint
   기하 중심, 짝수 변 +0.5칸). 셀 스냅 점프는 지수 lerp 로 완충(노브, 0 = 즉시 스냅).
4. **수명**: 스카우트 앵커 무효(`ClearBoardScout`) = 숨김(재진입 시 재등장·위치 스냅) /
   제스처 종료(`ResetBoardGesture` — 커밋·해제·Disarm 공용) = 파괴 / `OnDestroy` 정리.
5. **유효/무효 무반응**: 실루엣은 알파 고정 — 유효성 전달은 Ghost 4색 전담(feature 계약 4 유지).
6. **노브** (`DragSwaySettings` ⑮, 에셋 미직렬화 → 이니셜라이저 지배):
   `armedSilhouetteEnabled`(기본 true) · `armedSilhouetteAlpha`(0.55) · `armedSilhouetteFollowSpeed`(14, 0=스냅).

## 알려진 연출 이음새 (수용)

드래그 릴리즈 커밋은 기존 `SimulateDragTo`(트레이→셀 비행) 재사용이라, 릴리즈 순간 실루엣이
사라지고 유닛이 **트레이에서** 날아온다 — 실루엣 위치에서 비행을 시작하는 개선은 비행 경로
소유권(BeginDrag 세션)을 건드려 스코프 밖. 체감이 어색하면 후속 후보로.

## 완료 기준

- [x] 컴파일 에러 0 · EditMode 코어 무회귀 — 2494 전건 실패 0
- [x] 탭-탭 경로 diff 0 (드래그 승격 분기 밖 무변). D&D 는 unit 8 에서 의도적으로 바뀜
- [x] 육안 Play: arm → 보드 드래그 시 실루엣이 footprint 위에 서서 따라다님 · 맵 밖 이탈 시 숨김 ·
      릴리즈/해제 시 소멸 · 탭-탭은 기존 그대로

확인 2026-08-30 — 사용자 육안 Play 확인. 커밋 a1795f0e(+리뷰 반영 d039611a).
