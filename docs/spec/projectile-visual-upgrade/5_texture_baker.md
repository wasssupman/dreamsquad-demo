# Editor: Projectile Texture Variant Baker

**작업 구분**: 5

## 목적

PixPlays 키트의 main texture 를 입력으로 받아 HSV/밝기/대비 변동을 적용한 N장의 변종 텍스처를 베이크하는 에디터 도구를 도입한다. 결과는 자산으로 커밋.

## 변경 대상

- New: `Assets/_Project/Editor/ProjectileTextureBaker.cs`
- New: `Assets/_Project/Generated/Projectiles/Textures/*.png` (베이크 결과 자산, 12장 안팎)

## 도구 동작

메뉴: `Wassup/Tools/Generate Projectile Texture Variants`

기본 입력: 코드 안에 명시된 키트 매핑 (Wind / Stone / Fire / Water 의 main tex GUID 또는 path).

```csharp
private struct BakeJob {
    public string sourcePath;     // 원본 텍스처 경로
    public string outputPrefix;   // "wind", "stone", "fire", "water"
    public int variantCount;      // 3
}
```

각 variant 마다:
- HSV hue shift: variant index × (1f / variantCount) 를 추가 hue offset 으로.
- Brightness shift: 0.85 / 1.0 / 1.2 등 단계적 multiplier.
- (선택) Saturation shift: 0.7 / 1.0 / 1.3.

처리:
1. 원본 텍스처 Read/Write 임시 활성화 (`TextureImporter.isReadable = true` 후 `AssetDatabase.ImportAsset`).
2. `texture.GetPixels()` → 픽셀별 HSV 변환 → 시프트 적용 → 새 `Texture2D` 에 SetPixels.
3. PNG 인코딩 → `File.WriteAllBytes(outputPath)`.
4. `AssetDatabase.ImportAsset` → 컬러 텍스처 import 설정 (`alphaIsTransparency = true`, `mipmapEnabled = false`, `wrapMode = Clamp`).
5. 원본 텍스처 isReadable 원복.

## 안전 규칙

- 출력 경로는 `Assets/_Project/Generated/Projectiles/Textures/` 만. 다른 경로 쓰기 금지.
- 도구 실행은 명시적 메뉴 클릭만. 자동 invoke 금지.
- 베이크 결과는 git 에 커밋 (재현성). `.gitignore` 에 `Generated/` 넣지 않음.
- 기존 .png 가 있으면 덮어쓰기 (변종 셋이 코드로 정의되어 있으므로 결정적).

## 결과 자산

이번 task 에서 베이크 대상 (4 키트 × 3 variant = 12장):
- `wind_var0.png`, `wind_var1.png`, `wind_var2.png`
- `stone_var0.png`, `stone_var1.png`, `stone_var2.png`
- `fire_var0.png`, `fire_var1.png`, `fire_var2.png`
- `water_var0.png`, `water_var1.png`, `water_var2.png`

## 완료 기준

- 메뉴 실행 시 4 키트 × 3 = 12장이 새로 생성/갱신되고 콘솔에 진행 로그 1줄/job.
- 결과 텍스처가 Project view 에서 미리보기 가능, 알파 보존 확인.
- 두 번째 실행 시 결과가 결정적 (동일 픽셀).
- 도구 실행 후 read_console Error/Warning 0.
- 런타임 코드 변경 없음 (이 task 는 에디터 자산만).
