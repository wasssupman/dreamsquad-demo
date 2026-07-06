# 1 — QuadUnitView 반투명 전환

**작업 구분**: view (Quad 적)

## 목적

cutout quad 적을 dim 동안 transparent 블렌드로 런타임 전환해 뒤 타일이 비치게 한다. health tint 와
알파를 합성하고, 재스폰(머티리얼 리빌드)에도 정확히 재적용되게 상태를 리셋한다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/QuadUnitView.cs`

## 구현

- 필드 추가: `private bool _transparentApplied;` `private float _dimAlpha = 1f;`
  `private BlobShadow _blob;`
- `Configure()`:
  - blob 모드 분기에서 `BlobShadow.Attach(...)` **반환값을 `_blob` 에 보관**(현재는 버림).
  - 함수 말미에 `_transparentApplied = false; _dimAlpha = 1f;` (재스폰 시 cutout 기준으로 리셋).
- 공개 API:
  ```csharp
  public void SetDimmed(bool transparent, float alpha)
  ```
  - `_dimAlpha = Mathf.Clamp01(alpha);`
  - `transparent != _transparentApplied` 일 때만 블렌드 상태 플립(매 프레임 setter 낭비 방지):
    - transparent: `_SrcBlend=SrcAlpha(5)`, `_DstBlend=OneMinusSrcAlpha(10)`, `_ZWrite=0`,
      `DisableKeyword("_ALPHATEST_ON")`, `_AlphaClip=0`, `renderQueue=Transparent(3000)`;
      실그림자 모드면 `shadowCastingMode=Off`.
    - 복원: `_SrcBlend=One(1)`, `_DstBlend=Zero(0)`, `_ZWrite=1`,
      `EnableKeyword("_ALPHATEST_ON")`, `_AlphaClip=1`, `renderQueue=AlphaTest(2450)`;
      `shadowCastingMode = BattleBridge.UseRealShadows ? TwoSided : Off`.
    - `_transparentApplied = transparent;`
  - 그림자: `_blob?.SetDimAlpha(transparent ? _dimAlpha : 1f);`
  - 알파 반영은 `SetHealthTint` 경로에 위임(아래) — sync 가 매 프레임 SetDimmed→SetHealthTint 순.
- `SetHealthTint(Color tint)`: 마지막 줄을 알파 합성으로.
  ```csharp
  Color c = _baseColor * tint;
  c.a = _baseColor.a * _dimAlpha;
  _ownedMaterial.SetColor("_BaseColor", c);
  ```

## 완료 기준

- compile 통과, 콘솔 무에러.
- `SetDimmed(false, 1f)` + 기존 `SetHealthTint` = 원래 cutout 외형(회귀 없음).
- `SetDimmed(true, 0.3f)` 시 quad 적 뒤 타일이 비침(unit 3·4 배선 후 Play 실증).
- 재스폰(풀 재사용/머티리얼 리빌드) 후 첫 `SetDimmed(true,..)` 에서 정상 transparent(리셋 검증).
- 저체력 red-shift tint 가 반투명 상태에서도 색조 유지(RGB×tint, alpha=dim).
