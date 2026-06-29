# 2 — Handoff Summary

## Commit

- `50a5c5e` feat(presentation): 배경 프랍 외곽선 셰이더 — 내부 가장자리 스트로크 (prop-outline-shader 0~1)
- (+ 본 docs 확정 커밋)

## Implemented

- 단일 스프라이트 셰이더 `Wassup/Prop Outline (Sprite)` — 텍스처는 SpriteRenderer 가 `_MainTex` 로 공급(전 프랍 드롭인).
- 외곽선 = **내부 가장자리 스트로크(alpha erosion)**: 보이는 아트(a≥cutoff) 가장자리를 `_OutlineColor` 로 덮음. 배경 무관 또렷, 발밑 링 없음.
- 해상도 독립 두께 `_OutlineWidth`(짧은 변 비율, 확정 0.03). `_LIT_ON`(Lit/Unlit), `_ZWrite`(기본 On) 토글.
- 전 프랍 적용: 7 `_cast.mat` 인플레이스(tree/rock_l/rock_m → Lit On, flower×3/rock_s → Off) + 공유 `PropOutline_Sprite_Unlit.mat` 신규 → 패키지-기본 프랍 27개 프리팹 재할당.

## Key Files

- `Assets/_Project/Shaders/Prop_Outline_Sprite.shader` — 셰이더 본체(프래그먼트 erosion + Lit 분기).
- `Assets/_Project/Prefabs/Props/forest/mat/*_cast.mat` (7), `PropOutline_Sprite_Unlit.mat` (공유 unlit).
- `Assets/_Project/Prefabs/Props/forest/*.prefab` (27, SpriteRenderer 머티리얼 재할당).

## Verified

- 컴파일 0(ShaderHasError false), console 0. Play 168 프랍 전부 신 셰이더.
- 스크린샷 `Assets/Screenshots/prop_outline_v6..v9_*`. 사용자 육안 통과 @ width 0.03.

## Notes

- **발밑 옅은 그림자는 외곽선 아님** — 기존 블롭 발그림자(`BlobShadow`, 검정 α0.3). 외곽선 OFF 해도 남음. 건드리지 않음.
- 텍스처에 페인트 발밑 그림자가 있는 프랍(예: Tree)도 inner-stroke 는 sub-cutoff 라 안 침 → 링 없음.
- 모든 외곽선 수치는 머티리얼 라이브 값(색/두께 reimport 없이 조정). 하드코딩 없음.
- ⚠️ `refresh_unity mode=force scope=all` 은 전 에셋 reimport + MCP 브리지 끊김 유발(이번 세션에서 발생). 머티리얼 값 변경은 `SetFloat`+`SaveAssets`(타깃 reimport)만으로 충분.
- 사용자 `BattleScene.unity` WIP 미커밋 보존(불간섭).

## Follow-up

- 어두운 프랍에서 더 강한 외곽선 원하면 색 순수검정/두께↑ 또는 프랍별 미세조정.
- 거리 기반 외곽선 페이드(원경 과강조 방지), outside/centered 모드 옵션.
- 발밑 텍스처 페인트 그림자 vs 블롭 그림자 중복 정리는 별도 spec.
