# 4 — 블롭 정적 스폰 (매 프레임 강제 제거)

## 목적

블롭이 매 프레임 `LateUpdate` 로 transform 을 강제하던 걸, 정적 타깃(프랍)은 **스폰 시 1회 세팅**으로 끝낸다. 프랍은 안 움직이고, 부모(프랍 루트)는 틸트되지 않으므로(틸트는 `PropBillboard` 가 `visualRoot` 자식에만 적용) 매 프레임 재고정이 불필요.

## 결정 (사용자 합의)

- **방향 offset 없음.** "해 전방" 그림자 살리기는 화면 균일 + 카메라 보정이 필요한데, 그건 매 프레임 카메라 읽기(=강제)를 요구한다. 스폰-앤-포겟과 양립 불가 → **발밑 정중앙(offset 0)** 으로 확정. 좌우/깊이 편차 0, 페이즈 pitch 무관.
- 직전 실험분(`blobShadowScreenLift`, 그 전 `blobShadowOffsetZ`) 전량 제거.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/BlobShadow.cs` — `live` 플래그. `Attach` 에서 `ApplyTransform()` 1회 호출. `LateUpdate` 는 `if (_live) ApplyTransform();` (정적이면 no-op). screen-lift 로직 삭제.
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `AttachPropBlob` 가 `live: false`.
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`, `QuadUnitView.cs` — 유닛은 이동하므로 `live: true`.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `blobShadowScreenLift` serialized/static/미러 제거.

## 계약

- **프랍 블롭 = 정적**: 스폰 1회 세팅. 프랍 안 움직임 + 부모 틸트 없음이 전제. 부모 회전/스케일은 스폰 시점에 이미 확정.
- **유닛 블롭 = 라이브**: 이동 따라가기 유지(모바일 폴백). `live: true`.
- **offset 0**: 방향성 그림자는 본 spec 범위 밖(후속). 발밑 정중앙 고정.

## 완료 기준

- compile 0. Play → 프랍 그림자가 발밑 정중앙 원형, 좌우/깊이 무관하게 일관. 매 프레임 강제 없음(정적 프랍).
- 사용자 육안 통과. 통과 시 확인 일자 + 커밋 해시 추가.
