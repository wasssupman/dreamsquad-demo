# 8 — D&D 키링 은퇴 → 실루엣 통일 (2026-08-30 사용자 발의)

## 목적

unit 7 의 «footprint 위 실루엣» 방식을 트레이 D&D 에도 적용한다. D&D 의 손끝 키링 프리뷰
(고리+줄+매달린 유닛)를 **비활성화**하고(구현 유지 — 스위치 왕복 가능), 드래그 중 유닛
그림은 armed 탭-드래그와 동일하게 **footprint 뷰 중심에 서는 반투명 실루엣**이 담당한다.
두 드래그 제스처의 시각 문법이 하나로 통일된다.

**무변 계약**: ① 탭-탭/탭-드래그 커밋의 **시뮬 비행**(트레이→셀 키링 하마)은 유지 —
그건 드래그 프리뷰가 아니라 배치 확정 연출이다. ② sim 무변(뷰 전용). ③ 판정·커밋 산식
무변 — 손가락 셀 히스테리시스·앵커 산식·표시=확정 전부 그대로(실루엣은 `SetHover` 가
확정한 앵커를 소비만 한다).

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — ⑮ 노브 개편

## 구현

1. **세션이 시뮬 여부를 들고 있는다** (`DragSession.simulated`). 라이브 D&D 와 시뮬 비행을
   가르는 축이 `BeginDrag` 파라미터로만 있어 세션 수명 동안 되물을 수 없었다.
2. **BuildSession 분기**: 라이브 세션 + `dndSilhouetteEnabled` 면 손끝 프리뷰를 **아예 만들지
   않는다**(`preview = null`). 추종 스프링·취소 알파·하마 잔류물이 각자의 기존 null 가드로
   조용히 비활성되므로 경로 분기를 새로 만들지 않는다.
3. **Update 루프의 가드 재구성 (load-bearing 발견)**: 셀 판정 블록이
   `preview != null && endNode != null` 가드 **안**에 있었다 — 프리뷰를 없애면 칸을 하나도
   못 정해 배치 자체가 죽는다. 가드에서 프리뷰/키링 노드를 빼고(앞머리 = 셀 판정·취소·카메라
   포커스는 손가락만 있으면 성립), 키링 트랜스폼을 쓰는 **꼬리만** `endNode != null` 로 감쌌다.
   - **따름정리(선행 결함 해소)**: 폴백 capsule 프리뷰(`endNode` 없음)도 같은 이유로 D&D 배치가
     불가능했다 — Spine 에셋 없는 유닛이 로스터에 없어 드러나지 않았을 뿐. 이 재구성이 함께 고친다.
4. **실루엣 공용화**: `_dragSilhouette` / `UpdateDragSilhouette(anchor, unit)` /
   `TryBuild·Hide·DestroyDragSilhouette`. **등장 조건은 호출측이 판단한다** — 두 주인의 조건이
   서로 다른데(armed = 드래그 승격 / 세션 = 라이브 + 스위치) 한 함수가 둘을 다 알면 안 된다.
   - **유닛 신원(`_dragSilhouetteUnit`) 재도입**: unit 7 리뷰에서 «도달 불능»으로 뺐던 검사를
     되살렸다. 주인이 하나였을 땐 «`_armedUnit` 이 바뀌는 모든 경로가 먼저 파괴한다»가 성립했지만,
     이제 주인이 둘이고 멀티터치(보드 드래그 중 다른 트레이 슬롯 탭 = `Disarm` 없는 `_armedUnit`
     교체)로 요청 유닛이 갈릴 수 있다. 미래 방어가 아니라 **현재 두 호출부의 불변식**이다.
5. **수명 = hover 수명**: `SetHover` 갱신 / `ClearHover` 숨김 / `CleanupSession` 파괴.
   취소 예고·칸 없음은 상위 분기가 이미 `ClearHover` 로 가므로 그 사유들을 실루엣 쪽에 다시
   적지 않는다 — «보드에서 아무 일도 일어나지 않는다» 신호가 한 덩어리로 유지된다
   (원안의 `UpdateCancelVisual` 분기는 그래서 **불필요**로 판명, 미구현).
   - 부수 효과: 취소 예고의 신호 (a)가 «프리뷰 알파 저하»에서 «유닛이 보드에서 내려감»으로
     바뀐다. drag-cancel-affordance 계약(신호 2개)의 렌더링만 달라지고 개수·의미는 같다.
6. **커밋 착지**: 실루엣 모드 라이브 세션은 `StartDropDismount`(손끝→타일 하마)를 **태우지
   않는다** — 유닛이 이미 착지 타일 위에 서 있어 아치 이동 거리가 0 이고(제자리 튀기), 분리할
   고리·줄도 없다. 기존 폴백인 배치 연출(`RunDeployment`)이 실루엣→실유닛 교대를 받는다.
   시뮬 비행은 하마 유지(무변 계약 ①).
7. **노브** (⑮ 개편, 에셋 미직렬화 → 이니셜라이저 지배):
   `armedSilhouetteEnabled`(유지) · 신규 `dndSilhouetteEnabled = true`(false = 키링 복원) ·
   공용 튜닝 리네임 `silhouetteAlpha` / `silhouetteFollowSpeed`.

## unit 5 rev 동반 (같은 세션 사용자 지적)

되돌리기 버튼이 배치 **비행 중**에 떠서 날아가는 유닛을 따라다녔다 — 유예 창이 커밋
시점부터 열려 있어(`PendingDeployment` 전 구간) 설계상 열려 있던 구간이다. 유예 창 자체와
그 동안의 취소 유효성은 **불변**, **노출만** 착지 이후로 민다(`_activeDismounts` 재중 = 숨김).
노출 소유권도 `UpdateUndoWindow` 단독으로 옮겼다 — `BeginUndoWindow` 가 켜면 위치를 잡기 전
프레임에 직전 배치의 화면 좌표에서 한 번 번쩍인다.

## 완료 기준

- [x] 컴파일 에러 0 · EditMode 코어 2494 전건 실패 0(스킵 3 = 선행 Ignore)
- [x] 시뮬 비행(탭-탭·탭-드래그 커밋)은 키링 하마 경로 무변 · `dndSilhouetteEnabled=false` 면 키링 복원
- [ ] 육안 Play: D&D 중 키링 없음 + 실루엣이 footprint 에 서서 추종 · 취소 존/칸 없음에서 내려감 ·
      릴리즈 시 그 자리 실체화 · 되돌리기 버튼이 착지 후에만 노출 (**사용자 확인 대기 축**)
