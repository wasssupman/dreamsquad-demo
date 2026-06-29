# Prop Outline Shader

> 상태: 완료 2026-06-29 (내부 스트로크, 전 프랍 적용, 사용자 육안 통과 @ width 0.03).
> 전제: 배경 프랍은 전부 `SpriteRenderer` 빌보드. forest 34종 중 **7종은 프로젝트 소유 `_cast.mat`**(3 Simple Lit + 4 Unlit), **27종은 패키지 기본 `Sprite-Unlit-Default`**. 런타임 보드는 ~131 Simple Lit(링의 나무/바위) + ~36 Unlit. 텍스처는 머티리얼 `_BaseMap` 이 아니라 **SpriteRenderer 가 `_MainTex` 로 공급** → 텍스처 소스를 `_MainTex` 로 통일해야 전 프랍 드롭인.
> 대상: `Assets/_Project/Shaders/`, 프랍 머티리얼/프리팹. Legacy3D 불변.

## 목표 / 검증 질문

> **모든 배경 프랍(스프라이트)이 각자의 현재 룩(평면/라이팅)을 유지한 채, width·color·cutoff 로 조정 가능한 외곽선이 실루엣을 따라 그려지는가?**

전 프랍 드롭인 가능한 **단일 스프라이트 셰이더**(`_MainTex` 기반 + Lit/Unlit 토글 + 외곽선)를 만든다. 사용자 결정(2026-06-29): **전체 프랍 · 현 룩 유지(토글)**, 외곽선 스타일 **내부 가장자리 스트로크(만화풍)**.

## 외곽선 기법 (결정 이력)

- 프랍은 전부 **평면 빌보드 스프라이트** → inverted-hull(노멀 외삽)은 부적합(cutoff 개념 없음) → 기각.
- 초기: **바깥 알파 팽창**(outside dilation). 문제 — 어두운 나무에 다크-온-다크 저대비로 안 보이고, 나무 텍스처에 그려진 **반투명 발밑 그림자**(< cutoff)를 배경과 구분 못 해 발밑에 링 아티팩트.
- 확정: **내부 가장자리 스트로크(alpha erosion)**. 보이는 아트 픽셀(a≥cutoff) 중 `_OutlineWidth` 이내에 배경(<cutoff) 이웃이 있으면 아트 가장자리를 `_OutlineColor` 로 덮음. 배경 무관하게 또렷 + 반투명 발밑 그림자는 아트로 안 쳐서 발밑 링 없음.

## feature-wide 계약

- **단일 스프라이트 셰이더 `Wassup/Prop Outline (Sprite)`.** 텍스처는 SpriteRenderer 가 공급하는 `_MainTex` + 정점 컬러(틴트). 전 프랍 드롭인.
- **외곽선 = 내부 가장자리 스트로크.** 실루엣 안쪽 가장자리를 `_OutlineColor` 로 덮음(아트 위, 알파는 아트 그대로 → 소프트 엣지 보존). 반투명 내부 영역(페인트 그림자 등)은 스트로크 안 함.
- **해상도 독립 두께.** `_OutlineWidth` = 짧은 변(min(w,h)) 기준 비율(0~0.2). 텍스처 해상도 132~1038px 편차에도 프랍 간 상대 두께 일관. 텍셀 고정 금지.
- **렌더 모드.** queue=Transparent, `Blend SrcAlpha OneMinusSrcAlpha`, `Cull Off`, `[Enum] _ZWrite`(기본 On — 컷아웃 나무/바위 깊이 정렬 보존).
- **Lit/Unlit 토글 `_LIT_ON`.** off=평면, on=Blinn-Phong. 머티리얼별 현 룩에 맞춤(3 cast Simple Lit → On, 나머지 → Off).
- **모든 수치는 머티리얼에서.** `_OutlineColor`, `_OutlineWidth`(상대), `_Cutoff`. 토글 `_OUTLINE_ON`/`_LIT_ON`/`_ZWrite`. 하드코딩 금지.
- **외곽선 off 시 현 룩 보존.** 토글로 컴파일 제외, 베이스는 `texColor*vcol*_BaseColor`.
- **변경마다 Play→게임뷰 스크린샷 육안 검증 후 확정.**

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_outline_shader.md` | `Prop_Outline_Sprite.shader` 작성 | `_MainTex` 베이스 + Lit/ZWrite 토글 + 내부 스트로크(해상도 독립) |
| 1 | `1_material_and_verify.md` | 전 프랍 머티리얼/프리팹 적용 + Play 검증 | 7 cast 인플레이스 + 공유 unlit 머티리얼 + 27 프리팹 재할당, 스크린샷 육안 |

## 후속 후보

- 스트로크 색/두께가 어두운 프랍에서 부족하면: 색을 더 진하게(순수 검정) 또는 두께↑, 또는 프랍별 머티리얼 미세조정.
- 거리 기반 외곽선 두께/페이드(원경 프랍 과강조 방지).
- 외곽선 outside/centered 모드 옵션(현 inside 외 선택지).
- 발밑 텍스처 페인트 그림자 vs 블롭 그림자 중복 정리(별도 spec).
