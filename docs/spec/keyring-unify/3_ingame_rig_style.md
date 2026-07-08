# 3 · 인게임 rig 스타일 적용 (홀로그램 이식 본체)

## 목적

인게임 드래그 프리뷰 rig(절차적 원 루프 고리 + 단색 LineRenderer 줄)에 `KeyringStyle` 을 적용해 아웃게임과 같은 홀로그램 키링으로 만든다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — `KeyringStyle style` 필드 추가
- 수정: `Assets/_Project/Data/Config/DragSwaySettings.asset` — `KeyringStyleHologram` 할당
- 수정: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `TryBuildKeyringPreview` 의 고리/줄 생성부

## 구현

- **주입 경로 (계약 8)**: `DragSwaySettings.style`. 컨트롤러는 런타임 AddComponent + `Configure(DragSwaySettings)` 주입이므로 시그니처 무변경. 미주입 `CreateInstance` 폴백이면 style null → 절차적 자동 성립.
- **고리**: style 유효 시 절차적 원 LineRenderer 루프 대신 `SpriteRenderer`(`ringSprite` + `worldRingMaterial` 또는 공용 월드 머티리얼) + 기존 `Billboard.Setup(Tilted, BattleBridge.CharacterBillboardTilt)`. 크기는 지름 등가: `localScale = ringRadius * 2f * scale / sprite.bounds.size.x`.
- **줄**: 기존 LineRenderer 유지(카메라 페이싱 공짜) + `worldCordMaterial`. `widthMultiplier = cordWidth * scale` 유지하되, 홀로 빔 텍스처는 글로우 여백 포함이라 코어가 가늘어 보임 — `cordWidth` 를 SO 에서 재튜닝(에셋 값 변경, 코드 무변경).
- **틴트 중성화 (계약 7)**: 스타일 머티리얼 적용 시 `startColor/endColor`(LR)·`color`(SR) = white 강제. `cordColor`(현 에셋 갈색 0.45/0.38/0.28)는 절차적 폴백에서만 사용 — 홀로 셰이더의 vertex color 곱으로 시안→마젠타가 갈색 오염되는 것 방지.
- **소팅/렌더 순서 승계**: ring = `BoardSortOrder.DragPreviewOrder`(20000), cord = DragPreviewOrder−1 — 현행 값 유지 (SpriteRenderer 는 `sortingOrder` 직접 지정).
- 머티리얼 공유: style 의 머티리얼은 sharedMaterial 로 사용(세션마다 인스턴스 생성 금지). 절차적 폴백용 `_cordMaterial` 생성/파괴 경로는 현행 유지.

## 완료 기준

- compile 클린, 콘솔 에러 0.
- Play 드래그 스크린샷: 시안→마젠타 발광 빔 줄 + 홀로 링 + 스캔라인/펄스 확인 (사용자 육안 — 최종 판정). 전투 배경에서 시인성/하이라이트 충돌 확인, 필요 시 팔레트·_Intensity 튜닝(머티리얼 파라미터).
- `style` 해제 시 절차적 비주얼(원 루프 + 갈색 줄) 재현 — 폴백 회귀 없음.
- 스와이프 스윙·하이라이트 마우스 고정·배치 동작이 기존과 동일 (unit 0 이후 재확인).
