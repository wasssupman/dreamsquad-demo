# 1 — Desert MapThemeData 구성

## 목적

forest.asset 을 복제해 desert `MapThemeData` 를 만들고, 사막용 틴트 + 중립 프랍 카탈로그로 구성한다.

## 변경 대상

- `Assets/_Project/Map/Theme/desert/desert.asset` (신규, forest.asset 복제 후 편집).

## 구현

- `CopyAsset(forest.asset → desert.asset)` 로 구조(타일 지오메트리/엣지/밀도) 보존.
- 틴트(사막): `placeBaseTint`(웜 크림), `walkBaseTint`(웜 탄), `envBaseTint`(웜), `propGlobalTint`(샌디), `tileSideColor`(샌디 브라운).
- `tileProps` = 바이옴-중립 13종(바위·boulder·dead_tree·skull·ruin·crates·log·stump). (unit 3 에서 desert 전용 PropData 복제본으로 재연결.)
- env/deco 텍스처는 sand 로 설정(legacy MapView 대비). **단, Tilemap 모드 바닥은 이 텍스처가 아니라 tileSet 이 결정**(unit 2 에서 발견) → desert.tileSet 이 실제 바닥.

## 계약

- 모든 수치는 데이터(틴트/밀도/프랍)에서. 하드코딩 금지.
- MapThemeData 의 tile 텍스처 필드는 Tilemap 모드에서 inert(레거시 전용) — 실제 바닥은 unit 2 의 `tileSet`.

## 완료 기준

- desert.asset 생성, 틴트/tileProps 세팅. (바닥 렌더는 unit 2 의 tileSet 으로 검증.)

확인: 2026-06-30 desert.asset 구성 OK (guid febf3efe…). 커밋 5ebe315.
