# 4. Image Generation — Backdrop + 신규 EdgeProp 2종

## 목적

Codex 이미지 생성 스킬로 ① Forest 시즌 백드롭 일러스트 1장, ② 신규 EdgeProp 2종 PNG 를 생성한다. import 설정은 두 종류로 갈린다 — 백드롭은 일반 텍스처, EdgeProp 은 PropDataEditor 의 기존 importer 경로.

## 변경 대상

신규 PNG (Codex 이미지 생성 결과)

- `Assets/_Project/Art/Season/forest/backdrop_forest_dawn.png` (4096×2048)
- `Assets/_Project/Generated/Props/Textures/prop_edge_forest_pine_cluster_2_2.png` (1024×1024 transparent)
- `Assets/_Project/Generated/Props/Textures/prop_edge_forest_mossy_boulder_2_1.png` (1024×512 transparent)

## 이미지 생성 가이드

### Backdrop — backdrop_forest_dawn

- 가로 16:9 (4096×2048).
- 컨셉: 이른 새벽 깊은 숲. 안개 낀 침엽수림 파노라마, 멀리 산맥 실루엣, 차분한 청록-황금 빛, 부드러운 햇살. 캐릭터/이펙트 가독성을 깎지 않게 **저채도 + 저대비 + 중앙 하단부 어둡게**.
- 단일 객체/인물 없음. 명확한 포컬 포인트 X. 양 끝까지 균일한 환경 일러스트.
- 음영 방향: 중앙에서 밝아지지 않게. 하늘 위쪽이 가장 밝고 아래쪽으로 어두워짐.

### EdgeProp — pine_cluster_2_2

- 1024×1024, 투명 배경.
- 컨셉: 큰 침엽수 3~4그루 무리. 살짝 뒤로 기운 3/4 view, 이끼 낀 밑동. Codex 가 만든 기존 `prop_concept_*` 톤(어두운 보라/회색 + 황금 강조)과 매칭.

### EdgeProp — mossy_boulder_2_1

- 1024×512, 투명 배경.
- 컨셉: 가로 2 셀 폭 이끼 보울더 무리. 풍화감, 작은 양치/버섯 디테일.

## Import 정책

### EdgeProp PNG 2종

`PropDataEditor.ConfigureTextureImporter` 의 기존 정책을 그대로 따른다 (`Assets/_Project/Editor/PropDataEditor.cs:115`):

- TextureType: Sprite
- spriteImportMode: Single
- alphaIsTransparency: true
- mipmapEnabled: false
- filterMode: Bilinear
- wrapMode: Clamp
- textureCompression: Uncompressed
- crunchedCompression: false
- spritePixelsPerUnit = `PropDataEditor.PropSpritePixelsPerUnit`
- 플랫폼 오버라이드: maxTextureSize 2048, RGBA32 Uncompressed (Standalone/Android/iPhone/WebGL)

→ **수동 importer 변경 금지**. 5번 단위에서 PropDataEditor 의 `Generate Billboard Prefab` 버튼을 누르면 자동으로 위 설정이 적용된다.

### Backdrop PNG

PropData 가 아니므로 별도 import. Inspector 에서 직접 설정 또는 .meta 작성:

- TextureType: Default
- sRGBTexture: true
- alphaSource: None (또는 FromInput)
- mipmapEnabled: true
- wrapMode: Clamp
- filterMode: Bilinear
- maxTextureSize: 4096
- textureCompression: Compressed (BC7 on Standalone, ASTC 6x6 on Android/iPhone)

## 구현 순서

1. Codex 이미지 스킬 prompt:
   - Backdrop: 위 가이드 + "no characters, no central focal point, low-saturation panoramic forest backdrop for a top-down tournament defense game".
   - EdgeProp pine: "transparent PNG, single asset of pine tree cluster, 3/4 view, painterly fantasy game prop, dark purple-gray with gold highlights, matching style of arcane lantern / runic portal concept set".
   - EdgeProp boulder: 동일 스타일 + "wide mossy boulder cluster".
2. 결과 PNG 를 위 경로에 저장.
3. EdgeProp 2종은 5번 단위에서 PropData 생성 + `Generate Billboard Prefab` 호출 시 자동 import 정규화.
4. Backdrop PNG 는 Inspector 에서 위 설정으로 import (또는 .meta 직접 작성).

## 완료 기준

- 3개 PNG 가 위 경로에 생성.
- Unity 콘솔 import 경고/에러 없음.
- Backdrop 은 4096×2048, EdgeProp 2종은 transparent 영역 정상.
- 본 단위에선 SO/prefab 까지는 만들지 않는다. 5번 단위에서 묶음.

## 의존

- 선행: 0번 (working tree 정리)
- 후행: 5번 (이 PNG 들이 5번 SO 와 묶임)

확인 일자: 2026-05-10 / 커밋: 75e7d01
