# 0. 모드별 유닛 스케일 + billboard tilt

## 목적

`CharacterVisualScale` const(0.7) 를 모드별 값으로 바꾸고, billboard tilt 를 mode-aware 로 만들어 Tilemap ortho 뷰에서 유닛이 적정 크기·정자세로 보이게 한다. Legacy3D 값은 현행 유지(회귀 무변경).

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — const 제거 + 모드별 SerializeField + 맵 빌드 시 모드 기준 설정.
- 참조처는 무변경: `BattleBridge.CharacterVisualScale` 읽기 경로(SpineUnitView/QuadUnitView/DragController/내부)는 const→static 프로퍼티로 바뀌어도 그대로 컴파일.

## 구현

- `public const float CharacterVisualScale = 0.7f;` → `public static float CharacterVisualScale { get; private set; } = 0.7f;` (기본값 = 기존 Legacy 값, 빌드 전까지 동일).
- SerializeField 추가:
  - `legacyCharacterScale = 0.7f` (현행 const 값)
  - `tilemapCharacterScale = 0.42f` (ortho 뷰 튜닝 시작값)
  - `tilemapBillboardTilt = 0f` (Tilemap 평면뷰는 틸트 없음)
- 맵 빌드(`BuildMapForBattle`, `UseTilemapView` 분기 직전) 1회 설정:
  - `CharacterVisualScale = UseTilemapView ? tilemapCharacterScale : legacyCharacterScale;`
  - `CharacterBillboardTilt = UseTilemapView ? tilemapBillboardTilt : characterBillboardTilt;`
- `Awake`/`OnValidate` 의 `CharacterBillboardTilt = characterBillboardTilt;` 는 유지(빌드 전 Legacy 기본). 빌드가 모드 기준으로 덮어쓴다.
- 하드코딩 금지 준수 — 세 값 전부 SerializeField.

## 완료 기준

- Unity compile 0 errors. 전체 EditMode green(회귀 0).
- Legacy3D Play: 유닛 크기/틸트 본 spec 이전과 동일(`CharacterVisualScale=0.7`, tilt=35).
- TilemapRect/Iso Play(메모리 배선): 유닛이 셀 대비 적정 크기(스케일 0.42 적용 확인), 틸트 0(정자세). 스크린샷 1장.
- 모드 전환(빌드) 시 스케일/틸트가 모드에 맞게 갱신됨을 reflection/로그로 확인.
