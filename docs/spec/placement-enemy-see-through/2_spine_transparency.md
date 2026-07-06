# 2 — SpineUnitView 반투명 전환

**작업 구분**: view (Spine 적)

## 목적

Spine 적을 dim 동안 반투명으로. 머티리얼이 PMA transparent 라 블렌드 전환 없이 `skeleton.A` 로 페이드.
health tint(R/G/B)와 독립, `_dying` 존중, 그림자도 함께 페이드.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`

## 구현

- 필드 추가: `private BlobShadow _blob;`
- `ApplyTilemapShadow()` blob 분기에서 `BlobShadow.Attach(...)` **반환값을 `_blob` 에 보관**.
- 공개 API:
  ```csharp
  public void SetDimmed(bool transparent, float alpha)
  ```
  - `if (_dying || _skeleton == null || _skeleton.Skeleton == null) return;`
  - `_skeleton.Skeleton.A = Mathf.Clamp01(alpha);` (RGB 는 SetHealthTint 소관, 서로 안 지움)
  - 그림자:
    - blob: `_blob?.SetDimAlpha(transparent ? Mathf.Clamp01(alpha) : 1f);`
    - 실그림자 모드(`BattleBridge.UseRealShadows`): 자식 renderer `shadowCastingMode` 를
      `transparent ? Off : TwoSided` 로. (`GetComponentsInChildren<Renderer>` 재사용 — 기존
      `ApplyTilemapShadow`/`UpdateSortingOrder` 패턴과 동일.)
  - `transparent` 파라미터는 Spine 에선 블렌드 전환에 안 쓰이지만(PMA 이미 투명), 그림자 분기·시그니처
    통일용으로 유지.
- `skel.A` 는 매 프레임 sync 가 재적용(스폰 직후 A=1 기본값이라 첫 SetDimmed 로 즉시 dim).

## 완료 기준

- compile 통과, 콘솔 무에러.
- `SetDimmed(false, 1f)` = 원래 불투명 외형(회귀 없음).
- `SetDimmed(true, 0.3f)` 시 Spine 적이 반투명해져 뒤 타일 비침(unit 3·4 후 Play 실증).
- 사망 애니메이션 중(`_dying`) dim 이 색/알파를 덮지 않음.
- health tint(저체력 red)와 반투명이 동시 성립(R/G/B×tint + A=dim).
