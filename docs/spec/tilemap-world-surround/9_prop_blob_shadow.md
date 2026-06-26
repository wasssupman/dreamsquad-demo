# 9 — 근경 프랍 blob 그림자 (real cast 미사용)

## 목적

근경 프랍의 실시간 그림자(real cast)가 데스크톱에서도 안 보였다(원인: 틸트 빌보드 + URP cast 불안정).
실시간 그림자는 프랍에 불필요하다는 판단(사용자 결정) → **근경 프랍 그림자를 `BlobShadow` 발밑 타원으로
통일**한다. real cast 분기 제거, 데스크톱/모바일 모두 blob.

> rev 이력: 초기(9)는 "모바일에서만 blob 폴백, 데스크톱은 real cast 유지"였으나, real cast 가 프랍에서
> 동작하지 않아 **blob 통일**로 변경(2026-06-26).

범위: **근경(`InstantiateBackgroundProps`)만**. 원경 링은 그림자 없음(사용자 지정, 대충 OK).
캐릭터는 별개로 데스크톱 real cast 유지(잘 동작).

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `using Wassup.Bridge; using Wassup.Presentation;` +
  `InstantiateBackgroundProps` 에서 `castShadows` 파라미터 제거, 항상 `AttachPropBlob` 부착 + static 헬퍼
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 호출에서 `castShadows`(UseRealShadows) 인자 제거

## 구현

- 캐릭터 패턴 재사용. 단일 asmdef(Wassup.Runtime) 이라 Core→Bridge/Presentation 참조 순환 없음.
- `InstantiateBackgroundProps` 루프에서 분기 없이 `AttachPropBlob(instance, prop)` 항상 호출.
- `AttachPropBlob`: `BattleBridge.BlobShadowSprite==null` 이면 무시. 아니면
  `BlobShadow.Attach(instance.transform, sprite, BlobShadowSize * max(0.01, prop.visualScale), BlobShadowFootprint, BlobShadowColor, BlobShadowGroundY, BoardSortOrder.ShadowOrder)`.
- **크기**: blob 은 부모 lossyScale 을 보정(월드 크기 고정)하므로, 프랍 크기 반영은 `size *= prop.visualScale`
  (나무 큰 blob, 꽃 작은 blob). footprint/color/groundY 는 캐릭터와 공용 데이터(BattleBridge serialized).
- blob 은 instance 자식 → 프랍 destroy 시 함께 소멸. LateUpdate 가 발(셀 XZ)+groundY 에 평평하게 고정.
- `SetPropCastShadows` 는 RingProps 참조로 남되 미호출(원경 그림자 없음). 추후 원경 그림자 필요 시 재활용.

## 완료 기준

- compile 0 에러.
- 검증: 데스크톱 Play → 근경 프랍(44개) 발밑 타원 blob 확인(이전엔 그림자 0). 캐릭터 real cast 무영향.
- 원경 링·Legacy3D 무영향.
