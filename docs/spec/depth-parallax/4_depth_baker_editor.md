# 4 — Editor 뎁스 베이커 + 오프라인 파이프라인

## 목적

오프라인에서 만든 뎁스 PNG 를 올바른 임포트 설정(R8·linear·no-mip·무압축)의 `Texture2D` 에셋으로
가져오는 Editor 유틸. 오프라인 bake(파이썬) 절차를 문서화해 다른 프로젝트에서도 재현 가능하게 한다.

## 변경 대상

- New: `Assets/_Project/Modules/DepthParallax/Editor/Wassup.DepthParallax.Editor.asmdef`
- New: `Assets/_Project/Modules/DepthParallax/Editor/DepthMapBaker.cs`
- New: `Assets/_Project/Modules/DepthParallax/Tools~/depth_bake.py` (`~` = Unity 임포트 제외)
- New(문서): 본 파일에 bake 절차 + 라이선스 주의

## 구현

- **Editor asmdef**: `includePlatforms:["Editor"]`, `references:["Wassup.DepthParallax"]`
  (`Wassup.Editor.UnitStatImport` 템플릿). 런타임 asmdef 가 참조 안 하므로 빌드 자동 제외.
- **`DepthMapBaker`**: 메뉴/EditorWindow. 입력 = bake 된 PNG 폴더, 출력 = 임포트된 뎁스 `Texture2D`.
  `TextureImporter` 설정 강제: `textureType=Default`, `sRGBTexture=false`(linear), `mipmapEnabled=false`,
  `textureCompression=Uncompressed`, `filterMode=Bilinear`, single-channel R8 지향, **non-atlased full-rect**
  (아틀라스 UV remap 회피 — 셰이더가 plain [0,1] UV 샘플). **파이썬을 실행하지 않는다** — bake 는 오프라인 산물.
- **`Tools~/depth_bake.py`** (오프라인, 레포 밖 실행 가능): Depth Anything V2 **Small**(Apache-2.0),
  `device="mps"` CPU 폴백. 절차:
  1. **기본: 단일 정적 뎁스.** 대표 프레임(가장 선명/줌인) 1장만 추론 → 그대로 전 프레임 공유
     (`deployCutsceneDepth` 길이 1). 컷신 줌이 미세하고 진폭 ≤4% 라 프레임 미세 어긋남은 sub-perceptual.
  2. **에스컬레이션(실루엣이 실제로 움직일 때만)**: 프레임별 뎁스. 단 프레임별 독립 추출은 flicker →
     대표 1장을 프레임간 **측정 정렬(ECC/feature match)로 워프**하거나 프레임별 추론 + 글로벌 정규화 +
     stats EMA. 이 art 는 프로그램적 줌이 아니라 리프로젝션 변환이 ground-truth 로 주어지지 않음
     ("알려진 줌 배율" 가정 금지).
  3. **글로벌 퍼센타일(2/98) 정규화**(프레임별 min-max 금지 — breathing). 흰색=near.
  4. 8bit 양자화 시 dither + 약한 Gaussian blur(halo/edge-smear 완화 겸).
  5. half-res R8 PNG 출력.
- **라이선스(하드)**: DA-V2 Base/Large/Giant(CC-BY-NC)·Depth Pro(ASCL) **금지**. 더 큰 품질 필요 시
  MiDaS DPT-Large(MIT) 또는 DA-V1 Large(Apache). 셀아트는 신호가 적어 **수동 페인트오버 패스**(뜬 소품/무기/
  머리카락/평면 내부) 예산 확보.

## 완료 기준

- Editor asmdef 컴파일 클린, 빌드에서 제외됨(런타임 미참조 확인).
- 베이커로 임의 PNG 를 임포트 → R8/linear/no-mip/무압축/non-atlased 설정으로 들어옴(.meta 확인).
- `Tools~/depth_bake.py` 가 절차/라이선스 주석 포함해 존재(재현 가능).
