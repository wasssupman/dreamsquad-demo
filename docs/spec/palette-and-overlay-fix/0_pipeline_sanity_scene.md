# 0. Pipeline Sanity Scene

## 목적

`RuntimeMaterialFactory` + `Tile_Unlit` / URP Unlit 셰이더 path 가 단독으로 (MapView 캐시 / theme rule / region mesh 와 무관하게) 정상 동작하는지를 격리된 새 씬에서 확인. 본 단계 결과로 후속 작업의 출발점이 갈린다.

- 통과 → factory + shader path 정상 → 본 게임에서 안 보이는 이유는 `MapView` 측 (Bug A 캐시, Bug B mask, Bug C alpha) 임. 작업 단위 `1` (red-tint) 로 진행.
- 실패 → factory 또는 shader path 자체가 막혀있음 → 본 spec 의 추가 작업 단위로 root cause 파헤치고 수정.

이전 사후 코드 트레이스에서 factory 와 shader 가 모두 정상이라고 결론 냈지만, 그건 **코드 레벨 판단**일 뿐 실제 화면 검증이 아님. 22 palette pass 의 가장 큰 교훈은 "코드 트레이스 ≠ 화면 검증". 그래서 본 작업 단위를 spec 화한다.

## 변경 대상

- `Assets/_Project/Scripts/Rendering/PaletteSanityProbe.cs` (이미 생성됨) — `RuntimeMaterialFactory` 의 6개 호출 결과를 한 줄에 quad 로 시각화하는 진단 MonoBehaviour.
- 새 Unity 씬 — `Assets/_Project/Scenes/PaletteSanity.unity` (사용자가 Editor 에서 생성).

## 절차

1. Unity Editor 에서 **File → New Scene → Empty** 로 새 씬 생성. 저장 위치 `Assets/_Project/Scenes/PaletteSanity.unity` (이름은 자유).
2. Hierarchy 에서 **빈 GameObject** 하나 생성 (이름 `Probe`).
3. Probe 에 **`PaletteSanityProbe` 컴포넌트 추가** (Inspector → Add Component → "PaletteSanityProbe").
4. Inspector 에서 두 텍스처 슬롯에 forest 테마 텍스처 드래그:
   - `Opaque Texture` ← `Assets/_Project/Art/Theme/forest/tile_forest_grass1.png` 또는 `tile_place.png`
   - `Transparent Texture` ← `Assets/_Project/Art/Theme/forest/tile_forest_place_grass_edge.png` (또는 placeEdgeTexture 로 지정된 GUID `5ebd872665b074c91a57d0eb7bdd9711`)
5. Camera 위치 조정 (top-down 또는 약간 기울인 각도). Quad 들이 X 축으로 6개 보이도록 카메라 X=0, Y=2~4, Z=-3 정도. Orthographic 추천.
6. Play 진입.
7. 6개 quad 색을 관찰하고 결과 보고.

## 기대 결과

| Quad | 호출 | 기대 외관 |
|---|---|---|
| 0 | `CreateOpaqueTexture(tex, Color.white)` | 텍스처 원색 (tint 없음) |
| 1 | `CreateOpaqueTexture(tex, Color.red)` | 텍스처가 빨강 톤으로 곱해짐 (확실히 빨강) |
| 2 | `CreateOpaqueTexture(tex, Color.blue)` | 텍스처가 파랑 톤으로 곱해짐 |
| 3 | `CreateTransparentTexture(tex, white α0.4)` | 흐릿하게 보이고 뒤 backdrop 색이 비침 |
| 4 | `CreateTransparentTexture(tex, red α0.4)` | 빨강 + 흐릿 + backdrop 비침 |
| 5 | composite (3 위에 4 같은 빨강 overlay 5b) | opaque 위에 빨강 overlay 가 얹힘 |

## 결과 분기

| 관찰 | 결론 | 다음 작업 |
|---|---|---|
| 0/1/2 모두 기대대로 (white/red/blue) + 3/4 모두 backdrop 비침 + 5 빨강 overlay 보임 | factory + shader 정상 | 작업 단위 `1_red_tint_decision_test.md` 진행 |
| 1/2 가 white 와 구분 없음 (tint 무시) | opaque tint path 막힘 — 기존 진단 (DOTS_INSTANCING) 재의심 | 본 spec 안에서 새 work unit `0a_opaque_tint_diagnose.md` 작성 |
| 3/4 가 backdrop 안 비침 (opaque 처럼 보임) | transparent surface mode 안 먹힘 — `RuntimeMaterialFactory.CreateTransparentTexture` 또는 셰이더 fallback path 의심 | 본 spec 안에서 `0b_transparent_path_diagnose.md` |
| 5 의 overlay 가 안 보이거나 backdrop 위로만 보임 | overlay 전용 sortingOrder/depth 이슈 가능 | 본 spec 안에서 `0c_overlay_layering_diagnose.md` |
| 4 vs 3 이 모두 backdrop 비치는데 빨강이 안 보임 | transparent 의 tint 만 별도로 막힘 | `0a_opaque_tint_diagnose.md` 와 같은 부류 |

## 완료 기준

- `PaletteSanityProbe.cs` 가 컴파일 통과 (Unity console error 0).
- 새 씬 `PaletteSanity.unity` 가 저장되고 Play 가능.
- 6 quad 의 외관이 사용자에 의해 확인되어 위 결과 분기 표에 따라 분류됨.
- 결과가 "factory + shader 정상" 이면 본 작업 단위 종료, `1_red_tint_decision_test.md` 진행.
- 결과가 비정상이면 본 spec 안에 추가 진단 work unit (`0a` / `0b` / `0c`) 가 spec 화 되고 사용자 승인 대기.

## 주의

- 본 씬은 **진단 전용**. 게임 빌드 / 본 게임 씬에 영향 없음.
- 작업 종료 시점에 `PaletteSanityProbe.cs` 와 sanity 씬을 그대로 둘지, 진단이 끝나면 정리할지 결정. 우선 둔다 (재발 시 다시 활용).
- forest.asset 은 **건드리지 않는다**. red-tint 변경은 작업 단위 `1` 의 일.

확인 일자: 2026-04-27 — 통과. 6 quad 모두 기대 외관 일치. factory + shader path 단독 정상. 본 게임 미반영 원인은 MapView 측 (Bug A/B/C) 으로 확정. 사용자 Play 캡처 첨부 (스크린샷 2026-04-27 오후 4.13.25.png).
