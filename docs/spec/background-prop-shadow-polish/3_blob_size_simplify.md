# 3 — 블롭 크기 모델 단순화

## 목적

블롭 크기 노브 3개(`BlobShadowSize` 전역 × `prop.visualScale` × `BlobShadowFootprint` 타원)를 줄인다. 1타일=1월드유닛이므로 "1타일 기준 + 프랍별 스케일" 모델로 단순화한다.

## 결정 (사용자 합의)

- **footprint 타원 제거 → 원형.** 블롭은 XZ 바닥에 평평히 눕고 카메라 pitch 40~58° 라, 원형이라도 화면엔 이미 타원으로 보인다. y<x 추가 압축은 잉여.
- **`BlobShadowSize` 는 "1타일 기준" 전역 다이얼로 유지(1.0).** `visualScale` 이 `PropBillboard.cs:82` 에서 프랍 렌더 스케일로도 쓰이므로(=공유), 이걸 제거하면 블롭 전용 튜닝 다이얼이 0개가 됨 → 유지.
- **프랍별 크기 = `prop.visualScale` 재사용.** 새 필드 없음. 블롭이 프랍 비주얼 크기를 따라감(접지 그림자의 자연스러운 결합). 특정 프랍만 안 맞으면 후속에 독립 `blobScale` 필드 도입.
- **부모 lossyScale 나눗셈 유지.** 1줄, load-bearing(틸트·jitter·placement 스케일 상쇄). 자식 부모관계 유지.

## 모델

```
블롭 월드 지름 = BlobShadowSize × max(0.01, prop.visualScale)   // 타일 단위, 원형
```

## 변경 대상

- `Assets/_Project/Scripts/Presentation/BlobShadow.cs` — `_footprint` 필드/파라미터 제거, `LateUpdate` 의 localScale 을 균일(`_size/sx`, `_size/sy`)로.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `blobShadowFootprint` serialized 필드 + `BlobShadowFootprint` static + 미러 대입 제거.
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `AttachPropBlob` 의 footprint 인자 제거.
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`, `QuadUnitView.cs` — `BlobShadow.Attach` footprint 인자 제거.
- 씬(`BattleScene.unity`)의 `blobShadowFootprint` 직렬화 줄은 **orphan 으로 남겨둔다**(코드에서 필드 제거 시 Unity 가 무시 → 무해, 다음 정식 씬 저장 때 정리). 씬 저장이 미저장 WIP 를 베이크하는 문제 회피.

## 완료 기준

- compile 0. Play → 프랍 블롭이 원형 접지로 보이고 프랍 발 크기와 어울린다. 크기 어색하면 `BlobShadowSize`(전역) 또는 프랍별 `visualScale` 로 튜닝.
- 사용자 육안 통과. 통과 시 확인 일자 + 커밋 해시 추가.

확인: 2026-06-29 사용자 육안 통과 · 커밋 4704d4f (원형, BlobShadowSize×visualScale, 측정 worldScale 1.50/0.68). 스크린샷 `Assets/Screenshots/prop_shadow_v2_circular.png`.
