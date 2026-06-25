# Tilemap Real Shadows (XZ 바닥 + 퍼스펙티브)

> 상태: 진행 중 (2026-06-25 착수)
> 전제: `tilted-billboard` (퍼스펙티브 카메라 + XZ 바닥 + 빌보드). 커밋 `47e7925`.
> 대상: `Assets/_Project/Scenes/BattleScene.unity` (Tilemap, URP). Legacy3D 불변.

## 목표 / 검증 질문

> **타일맵 바닥이 진짜 방향광 그림자를 받고(플랫 색 유지), 빌보드 캐릭터가 실루엣 그림자를 드리우는가? 모바일은 블롭으로 폴백되는가?**

블롭(가짜)에 더해, 데스크톱에서 **진짜 캐스트 그림자**를 바닥에 떨군다. CotL/DST 처럼 바닥은
플랫하게 두되 그림자만 실제로 진다.

## 접근 (선택: 옵션 1 — 플랫 유지)

- **바닥 receive**: 라이팅 없이 **그림자 감쇠만** 곱하는 커스텀 URP 스프라이트 셰이더. 바닥 색은 플랫 유지.
- **빌보드 cast**: Spine/Quad 렌더러 `shadowCastingMode=TwoSided` + 그림자 패스 alpha-clip(실루엣).
- **모바일 폴백**: `useRealShadows` 토글 — ON=진짜 그림자(캐스터 ON, 블롭 OFF), OFF=블롭(현행).

## feature-wide 계약

- **셰이더 베이스 = URP Sprite-Unlit 패턴**: `[PerRendererData] _MainTex` + vertex color 로 TilemapRenderer
  스프라이트 바인딩 호환. (`Tile_Unlit` 의 `_BaseMap` 방식은 타일맵 텍스처 안 잡힐 수 있음 → 먼저 타일 렌더 확인 후 그림자 추가.)
- **receive = 그림자 감쇠만**: `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE` +
  `GetShadowCoord`/`MainLightRealtimeShadow`. 노멀/NdotL 라이팅 없음(플랫 유지). 그림자 영역만 어둡게.
- **노멀 무관**: 그림자 감쇠만 쓰므로 XZ 회전 후 타일 노멀 방향과 무관.
- **캐스터 alpha-clip 필수**: 평면 빌보드라 클립 없으면 사각형 그림자. Spine=ShadowCaster 보유, Quad=URP/Unlit `_ALPHATEST_ON`.
- **토글은 데이터에서**: `useRealShadows`(serialized→static). 기본 데스크톱 ON / 모바일 OFF(블롭). 하드코딩 금지.
- **블롭과 상호배타**: 진짜 그림자 ON 이면 BlobShadow 부착 스킵.
- **CAST/RECEIVE 역할 분리**: **타일/맵 = receive only (cast OFF)**, **유닛·프랍 = cast** (프랍 cast 는 4b/terrain 에서). 바닥 TilemapRenderer 는 `receiveShadows=true` + `shadowCastingMode=Off`. receive 셰이더엔 ShadowCaster 패스 없음(이중 안전).
- **범위**: Tilemap 만. Legacy3D 불변. 모바일 RP 에셋 그림자 비용 유의(1024/1캐스케이드).

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_receive_shader.md` | 셰이더+머티리얼 | 바닥이 그림자 받는 스프라이트 셰이더 + 타일맵 적용 |
| 1 | `1_billboard_casters.md` | 캐스터 | Spine/Quad 가 실루엣 그림자 드리움 |
| 2 | `2_quality_toggle.md` | 토글/폴백 | useRealShadows + 블롭 상호배타 + 모바일 폴백 |
| 3 | `3_handoff.md` | 인계 | 구현 종료 요약 |

## 후속 후보

- **Soft/contact 그림자 품질 튜닝** (bias, soft 샘플). 
- **추가광 그림자**(현재 main light 만).
- **그림자 + 블롭 하이브리드**(접지점 블롭 + 방향 진짜)로 두 마리.
