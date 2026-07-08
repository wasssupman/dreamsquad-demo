# 0 — portrait 필드 추가 + Sprite 재import

## 목적

`DefenderUnitData` 에 포트레이트를 담을 데이터 필드를 만들고, 포트레이트 원본
텍스처를 UI Image 가 참조할 수 있도록 Sprite 타입으로 재import 한다. (데이터 토대만.
실제 배정은 unit 1, UI 표시는 unit 2/3.)

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — 필드 추가.
- `Assets/_Project/Art/DefenderPortraits/{bishoujo,modern}/*.png` — import 설정
  (`.meta`) 변경. 배정표의 16장은 필수, 폴더 전체 32장 일괄 전환 허용(후속 스타일
  스왑 대비, 동일 타입 준비 작업이므로 스코프 확장 아님).

## 구현

1. `DefenderUnitData` 에 프레젠테이션 헤더로 필드 추가:
   ```csharp
   [Header("Presentation")]
   // defender-portraits 0 — 스쿼드/배치 UI 표시용 클래스 포트레이트. null 이면
   // 텍스트/단색 폴백. ECS 런타임은 참조하지 않는 순수 프레젠테이션 데이터.
   public Sprite portrait;
   ```
   기존 필드/직렬화 순서를 깨지 않도록 파일 하단 계열(예: Deployment Presentation
   근처)에 추가한다. `using UnityEngine;` 이미 존재.

2. 포트레이트 텍스처를 Sprite 로 재import. `.meta` 기준 목표 값:
   - `textureType: 8` (Sprite)
   - `spriteMode: 1` (Single)
   - `alphaIsTransparency: 1`
   - `mipmap enableMipMap: 0` (UI 용이라 불필요)
   - 나머지(maxTextureSize 2048, 압축)는 현행 유지.

   방법: unityMCP `manage_asset`(import 설정 수정) 또는 일회용 MenuItem 에디터
   스크립트로 `TextureImporter.textureType = TextureImporterType.Sprite;
   importer.spriteImportMode = SpriteImportMode.Single;` 설정 후 `SaveAndReimport()`.
   (프로젝트 메모리: unityMCP `execute_code` 불가 → 필요 시 일회용 MenuItem 패턴.)

## 완료 기준

- Unity 컴파일 에러 없음(`read_console` 클린).
- `DefenderUnitData` 인스펙터에 `Portrait` 슬롯이 보이고 Sprite 를 드롭할 수 있다.
- 배정표 16개 포트레이트가 Project 뷰에서 Sprite(하위 스프라이트 펼침 가능)로 보인다.
- 기존 방어 SO 들이 필드 추가로 인해 깨지지 않는다(기존 값 보존).

---
완료 확인: 2026-07-08 · 커밋 c423be4c
