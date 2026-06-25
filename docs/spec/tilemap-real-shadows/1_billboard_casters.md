# 1 — 빌보드 캐스터 (실루엣 그림자)

## 목적

Spine/Quad 빌보드가 바닥에 **실루엣** 그림자를 드리우게 한다(사각형 X).

## 변경 대상

- 수정: `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`
- 수정: `Assets/_Project/Scripts/Presentation/QuadUnitView.cs`
- (검증) `Assets/_Project/Shaders/Billboard_Unlit.shader` / Spine 셰이더 ShadowCaster alpha-clip

## 구현

공통: 진짜 그림자 모드일 때만(unit 2 토글) 캐스터 활성. 평면이라 `ShadowCastingMode.TwoSided`.

SpineUnitView (스폰 시):
- `foreach renderer in GetComponentsInChildren<Renderer>` → `shadowCastingMode = TwoSided`.
- Spine `Spine/Skeleton` 셰이더는 ShadowCaster 보유. **alpha-clip 확인** — 안 되면 Spine 머티리얼의
  ShadowCaster cutoff 활성(`_ShadowAlphaCutoff` 류) 또는 alpha-clip 변형 사용. 실루엣이 떨어지는지 확인.

QuadUnitView (Tilemap 경로, 스폰 시):
- 현재 URP/Unlit + `_AlphaClip=1` + `_ALPHATEST_ON` → ShadowCaster 패스가 alpha-clip 실루엣 cast.
- MeshRenderer `shadowCastingMode = TwoSided`.

> 그림자 방향: Directional Light Euler(50,-30,0) → 바닥에 비스듬한 길쭉한 실루엣(룩 OK).
> 캐스터가 너무 진하면 light shadowStrength/RP soft 로 조절(데이터, unit 2/후속).

## 완료 기준

- Play(데스크톱, 진짜 그림자 ON): 캐릭터 발에서 **캐릭터 모양** 그림자가 바닥에 드리움(사각형 아님).
- 이동 시 그림자 따라감. 좌우반전(ScaleX)해도 실루엣 정상.
- Legacy3D 불변.
