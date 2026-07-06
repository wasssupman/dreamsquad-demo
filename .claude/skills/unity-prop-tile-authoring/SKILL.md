---
name: unity-prop-tile-authoring
description: Use when adding a new map decoration prop or ground tile from a source image (PNG) — importing the sprite, creating PropData/prefab or Tile/TileSetData assets, or registering them into MapThemeData pools (tileProps / tileSet).
---

# Unity Prop/Tile Authoring

새 소스 이미지 1장을 맵에 등장시키기까지의 정식 파이프라인. **레거시 애셋을 미러링하지 말 것** — 이 코드베이스에는 구세대/신세대 패턴이 공존하며, 정식 경로는 아래에 명시된 것뿐이다.

## Step 0 — 라우팅 (반드시 명시적으로 질문)

사용자에게 **1회 질문으로 묶어서** 확인: ① 데코 프랍인가 바닥 타일인가 ② 대상 테마 ③ (프랍이면) 디자인 값 diff 승인.

- 폴더/파일명 휴리스틱으로 판별 금지. `Art/Theme/forest/tile_*.png` 는 죽은 레거시(Deprecated quad 시스템, `MapThemeData.walkTileTexture` 등 Texture2D 필드)를 가리킨다 — `tile_` 프리픽스 ≠ Tilemap 타일.
- 소스 이미지의 **픽셀 수정(크롭/리컬러/테두리 제거)은 임의 진행 금지** — 필요 근거와 함께 보고하고 확인 후 진행.
- **AutoTile(룰타일 변형 세트) 요청은 스코프 아웃** — PNG 1개→SO 1개 공식이 성립하지 않는 배치 생성물(`Generated/Tiles/`). 정중히 거절하고 별도 작업으로 안내.

## A. 데코 프랍 파이프라인

정식 도구 = `Assets/_Project/Editor/PropDataEditor.cs`. 임포트 설정과 프리팹 구조를 도구가 강제하므로 **손으로 PPU/압축/프리팹 YAML을 정하지 않는다.**

1. **PNG 배치**: `Assets/_Project/Art/Theme/{theme}/{name}.png`. 네이밍 `prop_{desc}_{A}_{B}` (예: `prop_tree_b_1_2`). **sprite/PropData/prefab 세 애셋은 동일 basename** — PropDataEditor 의 텍스처 자동해석이 `Art/Theme/{theme}/{data.name}.png` 경로 일치에 의존한다.
   - 배치 전 **배경 알파 검사**: 모서리 픽셀 alpha 가 255 면 배경이 불투명(검은 박스로 렌더됨) — 정지하고 픽셀 수정 게이트로 (flood-fill 투명화 제안).
2. **PropData 생성**: `Assets/_Project/Data/Theme/{theme}/{name}.asset`. baseline 은 **해당 테마 `tileProps` 에 실제 등록된(활성) PropData 중에서만** 골라 값 복사 후 diff 만 사용자에게 제시. 미등록 PropData(prop_style_* 등)의 값은 화면 검증이 안 된 값 — 복사 금지.
   - 검증된 활성 로스터 표준: `billboardMode: Tilted`(tiltAngle 38~50) + `visualOffset: 0` + 스프라이트 피벗 **BottomCenter**. FullCamera + offset 조합은 카메라 pitch 에서 쿼드가 지면을 뚫는다.
   - footprint 규칙: 빌보드 프랍은 `footprintX/Y = 1,1`(sim 발자국), **파일명 AxB 는 `visualFootprint`** (틸트 시각 가림). 멀티셀 차단 프랍만 예외 — 그때만 질문.
   - 디자인 값(사람 판단): `placementWeight`, `category`(+`sameCategoryMinDistanceCells`), `billboardMode`/`tiltAngle`, `visualScale`. baseline 복사값을 기본 제안으로.
3. **프리팹 생성**: 에디터에서 PropData 인스펙터의 **"Generate Billboard Prefab" 버튼** (또는 UnityMCP `execute_code` 로 `Wassup.Editor.PropDataEditor` 의 private static `GeneratePrefab` reflection 호출). 도구가 자동 처리: 임포트 설정(Sprite/Single, **PPU 256 고정**, mipmap off, Bilinear, Clamp, **Uncompressed RGBA32 전 플랫폼**), Root+Visual 계층, `PropBillboard.Configure`(data 자기참조), `data.prefab` 역참조, `Prefabs/Props/{theme}/` 저장.
   - 피벗은 도구가 **BottomCenter(7) 로 강제**한다 (`ConfigureTextureImporter`, 2026-07-02 fix — Center 피벗이면 Tilted 모드에서 절반이 지면에 묻힘). 도구를 거치지 않고 임포트한 텍스처만 `spriteAlignment` 수동 확인.
   - 도구가 **BlobShadow 자식을 자동 내장** (footprint 종횡비 + 틸트 투영 기본값). 그림자 위치/크기의 source of truth 는 프리팹 — 아트 여백 따라 프리팹에서 미세조정하고, 재생성해도 기존 블롭 튜닝은 보존됨. 색/알파는 전역(BattleBridge) 소유.
   - **PPU 256 고정 → 캔버스 px 가 월드 크기 결정**: `visualScale = 목표 월드폭 ÷ (캔버스px/256)`. 소스를 다른 해상도로 교체하면 visualScale·블롭 재점검.
4. **머티리얼**: 생성 직후 Visual 의 SpriteRenderer 에 공용 `Prefabs/Props/forest/mat/PropOutline_Sprite_Unlit.mat` 할당. **프랍별 `_cast` mat 복제 금지** (구세대 레거시 패턴).
5. **테마 등록**: `Map/Theme/{theme}/{theme}.asset` 의 `tileProps` 배열에 PropData guid append. 순서 무의미.
6. 에디터 없이 파일 레벨로 작업해야 하면: 3번 도구가 강제하는 값과 **동일하게** .meta/.prefab 을 작성한다 (위 괄호 값이 스펙).

## B. 바닥 타일 파이프라인

활성 체인: `MapThemeData.tileSet` → `TileSetData.{walk|place|env|deco}Tile` → `Tile.m_Sprite`. 배치는 `Tilemap.SetTilesBlock` — GameObject/프리팹 없음.

1. **STOP 게이트**: 대상 테마의 `tileSet` 이 null 이면 (예: forest) **정지하고 질문**. `TileSet_{Theme}.asset` 신설은 아키텍처 결정이다. 폴백(`BattleBridge.cs:702`)은 per-slot 이 아닌 **whole-object 스왑**이라, 일부 존만 채운 TileSet 을 연결하면 나머지 존이 전부 null 이 되어 맵이 깨진다 — 신설하려면 전 슬롯을 채워야 하고, scene fallback 에서 **스냅샷 복사하면 drift 위험** — 복사 vs 참조 유지 방침을 사용자가 정한다.
2. **임포트 설정** (레거시 meta 값 유지 금지, 아래로 정렬): Sprite/Single, PPU = 텍스처 한 변 픽셀(1셀 = 텍스처 전체), mipmap **on**, filter **Bilinear**(Trilinear 금지), wrap **Repeat**, 무압축(격자선 방지 규칙).
3. **Tile 애셋**: `Assets/_Project/Data/TileSets/Tile_{Name}.asset` (표준 `UnityEngine.Tilemaps.Tile`).
4. **등록**: `TileSetData` 의 해당 존 필드에 연결. 어느 존(walk/place/env/deco)인지는 Step 0 질문에 포함.

## 완료 기준 (공통)

- 에디터 refresh 후 콘솔 클린 (**force reimport 금지** — MCP 브리지 끊김).
- **Play 진입 → 게임뷰 스크린샷 육안 확인** (배경/프랍 변경 시 필수 검증 표준).
- 프랍이 가중치 랜덤이라 안 보일 수 있음 → `placementWeight` 임시 상향 또는 seed 고정으로 결정론적으로 확인 후 원복. **`placementWeight: 0` 이면 등록해도 절대 안 나온다.**

## Common Mistakes

| 합리화 | 현실 |
|---|---|
| "기존 Tree.png(545 PPU) meta 를 미러링했다" | Tree/Flower/Rock 은 구세대. 정식 스펙은 PropDataEditor 가 강제하는 PPU 256 + Uncompressed RGBA32 |
| "로스터 프랍은 각자 전용 _cast mat 을 쓴다" | 구세대만. 신규 프랍은 공용 PropOutline_Sprite_Unlit.mat |
| "기존 텍스처의 filter/wrap 오버라이드를 존중했다" | 레거시 잔재. 활성 타일 스펙(Bilinear/Repeat)으로 정렬 |
| "TileSet 신설은 데이터 추가일 뿐이니 그냥 만들었다" | 테마 전체 지면을 fallback 에서 분리하는 아키텍처 결정. 정지하고 질문 |
| "prefab 에 스프라이트/스케일 값을 박았다" | 런타임 `PropBillboard.Awake()` 가 PropData 값으로 덮어씀. **PropData 가 source of truth** |
| "동형 프랍(crates_barrel 등)이 있어 그 값을 baseline 으로 복사했다" | tileProps 미등록 프랍은 화면 검증이 안 된 값. **활성 로스터에서만 baseline** (2026-07-02 지면 관통 사고) |
