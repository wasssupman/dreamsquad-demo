# 9 — 근경 프랍 blob 그림자 폴백 (모바일)

## 목적

캐릭터는 real shadow 가 꺼지는 모바일에서 `BlobShadow` 폴백으로 접지감을 유지하는데(SpineUnitView/
QuadUnitView), **근경 프랍은 모바일에서 그림자가 완전히 사라진다**(비대칭). 캐릭터와 대칭으로,
근경 프랍도 `castShadows=false`(모바일) 일 때 발밑 blob 타원을 붙인다.

범위: **근경(`InstantiateBackgroundProps`)만**. 원경 링은 제외(사용자 지정). 데스크톱은 real cast 유지.

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `using Wassup.Bridge; using Wassup.Presentation;` +
  `InstantiateBackgroundProps` 의 `castShadows` 분기에 blob 부착 + `AttachPropBlob` static 헬퍼

## 구현

- 캐릭터 패턴 재사용. 단일 asmdef(Wassup.Runtime) 이라 Core→Bridge/Presentation 참조 순환 없음.
- 현재 `if (castShadows) SetPropCastShadows(instance);` →
  `if (castShadows) SetPropCastShadows(instance); else AttachPropBlob(instance, prop);`
- `AttachPropBlob`: `BattleBridge.BlobShadowSprite==null` 이면 무시. 아니면
  `BlobShadow.Attach(instance.transform, sprite, BlobShadowSize * max(0.01, prop.visualScale), BlobShadowFootprint, BlobShadowColor, BlobShadowGroundY, BoardSortOrder.ShadowOrder)`.
- **크기**: blob 은 부모 lossyScale 을 보정(월드 크기 고정)하므로, 프랍 크기 반영은 `size *= prop.visualScale`
  (나무 큰 blob, 꽃 작은 blob). footprint/color/groundY 는 캐릭터와 공용 데이터(BattleBridge serialized).
- blob 은 instance 자식 → 프랍 destroy 시 함께 소멸. LateUpdate 가 발(셀 XZ)+groundY 에 평평하게 고정.

## 완료 기준

- compile 0 에러.
- 검증(모바일 경로): `BattleBridge.useRealShadows=false` 로 Play(에디터에서 모바일 분기 재현) →
  근경 프랍 발밑 타원 blob 확인. 데스크톱(real cast) 경로는 blob 없이 기존 cast 유지.
- 원경 링·캐릭터·Legacy3D 무영향.
