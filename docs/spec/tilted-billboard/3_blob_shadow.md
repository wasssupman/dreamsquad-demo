# 3 — 블롭 그림자 (접지)

## 목적

빌보드는 틸트가 제각각이라 실제 라이트 그림자는 일관성이 깨진다. 발밑에 평평한 타원 블롭을
깔아 **접지감 + 깊이감**을 만든다. 캐릭터·Quad 유닛 공용. (퍼스펙티브 + XZ 바닥 모델 기준)

## 변경 대상

- 신규: `Assets/_Project/Scripts/Presentation/BlobShadow.cs`
- 신규(에셋): `Assets/_Project/Art/blob_shadow.png` (radial-falloff 흰 스프라이트; SpriteRenderer color 로 틴트)
- 수정: `BoardSortOrder.cs` (`ShadowOrder = -5`)
- 수정: `BattleBridge.cs` (serialized 그림자 데이터 + static 미러)
- 수정: `SpineUnitView.cs` / `QuadUnitView.cs` (스폰 시 그림자 부착)

## 구현

`BlobShadow` (정적 팩토리 `Attach(target, sprite, size, footprint, color, groundY, sortingOrder)`):
- 유닛 **자식**으로 생성 → 유닛 파괴 시 함께 소멸(별도 생명주기 관리 불필요).
- `LateUpdate`: 월드 transform 직접 구동 — 위치 `(target.x, groundY, target.z)`(발밑 XZ),
  회전 `Euler(90,0,0)`(쿼드가 XZ 바닥에 눕는다), 스케일 `size·footprint` 를 **부모 lossyScale 로 보정**.
  타원: footprint.x=가로(X), footprint.y=깊이(Z, 로컬 Y→월드 Z).
- SpriteRenderer: 흰 blob 스프라이트 + `color`(검정 a=0.45) 틴트. URP 기본 스프라이트 머티리얼(자동).

데이터(하드코딩 금지): `BattleBridge` serialized → static 미러(빌드 시), view 가 스폰 때 읽음.
- `blobShadowSprite` / `blobShadowSize`(1) / `blobShadowFootprint`(1.35,0.95) / `blobShadowColor`(0,0,0,0.45) / `blobShadowGroundY`(0.02)

부착 조건: `BlobShadowSprite != null && Mode != Legacy3D` (Legacy3D 는 유닛이 떠 있어 미적용).

정렬: `ShadowOrder = -5` (ground −20 / overlay −10 위, 캐릭터 양수 아래).

## 완료 기준

- compile 통과(달성).
- Play(Tilemap) 스크린샷: 캐릭터 발밑 타원 그림자, 바닥 위·캐릭터 아래, z-fighting 없음.
- 캐릭터 이동 시 그림자 따라감, 유닛 사망 시 함께 사라짐.
- 퍼스펙티브에서 그림자가 지면과 함께 자연스럽게 깔려 접지로 읽힘.
