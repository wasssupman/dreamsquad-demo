# 22. Theme Palette Pass (rev4)

## 목적

rev4 컨셉 (보드게임 타일) 기준으로 Walk / Place / Env / 프랍 / 캐릭터가 **같은 시각 언어** 위에 놓이도록 팔레트를 통일한다. 격자감은 허용하되 톤/명도/채도가 한 계열에서 움직여 "한 세트의 보드" 로 읽히게 한다.

audit V-007 은 rev4 에서 N/A 로 무력화됐지만, **남아있는 실질 시각 문제**는 zone 간 색상 대비가 너무 강해 서로 다른 tile atlas 를 합친 듯한 인상. 본 spec 이 그것을 마감.

## 전제

- `24`, `25`, `26`, `27`, `28` 완료. Baseline 커밋 `1bc73f9`.
- rev4 컨셉 재정의 확정 (README rev4).

## 변경 대상

- `Assets/_Project/Map/Theme/forest/forest.asset` — 모든 color / opacity / material tint 필드
- 필요 시 `Assets/_Project/Scripts/Rendering/RuntimeMaterialFactory.cs` — tint 파라미터 확장 (최소 변경)
- 프랍 `PropData.spriteColor` / `visualMaterial` 중 palette 에서 튀는 항목 보정
- 새 asset 제작 최소화. 기존 texture 에 material tint 적용으로 해결.

## 문제 분석 (현 screenshot 기반)

`스크린샷 2026-04-25 오전 12.23.23.png` 관찰:

- **Walk**: 갈색 / 베이지 계열. 채도 중간.
- **Place**: 거의 무채색 흰/회색. 명도 매우 높음 → zone 중 가장 튐.
- **Env**: 녹색 단색. 채도 높음. Walk 와 대비 큼.
- **수정 프랍**: 선명한 파란 / 시안. 팔레트에서 혼자 차가움.
- **일반 프랍**: 갈색/녹색 계열 (OK).

즉 **Place 흰색 + 수정 파란 + Env 녹색 3 조합이 보드 안에서 서로 다른 시각 언어로 충돌**. rev4 가 수용하는 "격자 타일" 은 OK 지만 **세 요소가 같은 팔레트 안이어야** 보드로 읽힘.

## 팔레트 방향 (보드게임 참조)

Warhammer Underworlds / Gloomhaven 의 전형적 보드 팔레트:

- **Base 톤**: warm earth (beige ~ muted brown)
- **Contrast**: 채도 낮은 색으로 zone 구분 (mossy green, dusty cream, weathered stone)
- **Accent**: 프랍 / 유닛에 소량의 선명한 색 (포인트만)
- **avoided**: 순백색 / 순원색 / 네온

적용 방침:

| 요소 | 현재 | 타겟 |
|---|---|---|
| Place base tint | 흰 (거의 #FFFFFF) | warm cream / beige (#E5D8B7 ± tone) |
| Walk base tint | 갈색 베이지 | 유지 또는 약간 따뜻하게 |
| Env base tint | 선명 녹색 | desaturated mossy green |
| Edge fringe | 흰색 광 | Place tint 와 유사한 톤 (fringe 가 seam 강조 안 하도록) |
| 수정 프랍 spriteColor | 선명 파란 | 채도 20~40% 낮춤 (dusty teal) |
| 일반 프랍 | 갈색/녹색 | 유지 (이미 palette 안) |
| 캐릭터 | 각자 색 유지 | 단 배경과 대비가 과도하면 tint 소량 |

## 구현 가이드

### Step 1. Inspector 튜닝 기반 반복

`forest.asset` 에 color / tint 필드가 직접 있으면 Inspector 에서 조정. 없으면 `MapThemeData` 에 tint 필드 추가 (최소).

추가 검토 필드:
- `placeBaseTint` (신규, Color, 기본 `(0.9, 0.85, 0.72, 1)` 베이지)
- `envBaseTint` (신규, Color, 기본 `(0.68, 0.74, 0.55, 1)` 모스 그린)
- `walkBaseTint` (신규 또는 기존 walk tile tint 활용)
- `placeEdgeTint` (이미 material 에 녹아있으면 건드리지 않음)

`RuntimeMaterialFactory.CreateOpaqueTexture(tex, tint)` / `CreateTransparentTexture(tex, tint)` 는 이미 tint 파라미터를 받음. `MapView` 의 `CreateTileTopMaterials` 가 현재 `Color.white` 로 하드코딩되어 있다면 `theme.<zoneTint>` 를 넘기도록 교체.

### Step 2. 프랍 palette 보정

PropData 의 `spriteColor` 가 선명한 원색이면 `(r*0.85, g*0.85, b*0.85, 1)` 정도로 채도 감. 특히 수정 계열 프랍이 대상.

대량 asset 을 수정해야 하면 `RuntimeMaterialFactory` 또는 `MapView.InstantiateBackgroundProps` 에서 global prop tint 를 추가로 곱하는 방법도 가능 (theme param `propGlobalTint`).

### Step 3. 검증

- Play 재캡처 (Unity MCP 가능하면 `20260425_22/`)
- 같은 seed 3개. UI off, 동일 해상도.
- `VISUAL_AUDIT.md` 에 rev4 기준 최종 시각 평가 한 문단 기록.

## 완료 기준

- Play 재캡처에서 Walk / Place / Env / 프랍이 **같은 계열 팔레트** 로 읽힘.
- 흰 Place slab / 선명 파란 수정 같은 **혼자 튀는 요소** 가 사라짐.
- zone 구분은 유지 (gameplay readability).
- sorting / inner corner / 프랍 분포 회귀 0.
- Unity console error 0.
- `VISUAL_AUDIT.md` 최종 시각 평가 기록.

## 주의

- **새 asset 제작 최소화**. tint 와 기존 asset 재조합이 1차 범위.
- 너무 낮은 채도로 가면 보드가 우중충해짐. Inspector 반복 필수.
- 프랍 spriteColor 를 대량 수정하려 하면 `propGlobalTint` 한 필드로 일괄 적용이 안전.
- rev4 scope 기준: **Enter the Gungeon 연속감 목표 아님**. 팔레트 조화만 만족하면 종료.

## 다음 단계 분기

- 팔레트 통일 만족 + 사용자 OK → board-visualization rev4 **완성 선언**
- V-001 (프랍 cluster 약함) 이 여전히 거슬리면 `17r` 드래프트 → Poisson 제대로 재구현
- 테마 확장 요구가 있으면 `23_volcano_theme_fill.md` 로

확인 일자: 2026-04-25 — Unity MCP unavailable로 Play 재캡처 없음. 코드 레벨 material factory trace 완료. 커밋: 6c88007
