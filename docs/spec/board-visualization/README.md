# Board Visualization Spec

**작성일**: 2026-04-25 (rev4)
**상태**: **종료 (wrap) 2026-04-27** — rev4 palette pass 까지 코드 반영. Play 검증에서 시각 개선 체감 없음 + 엣지/코너 overlay 미표시 잔존. 본 spec 의 추가 시각 튜닝은 ROI 부족으로 **중단**. baseline 은 그대로 유지하고 다음 작업은 별도 spec 에서 시작한다. 인계는 `29_final_handoff.md` 참조.
**목표**: 정형화된 grid 기반 전투 보드를 **보드게임 타일 스타일**로 일관되게 렌더. Walk / Place / Env / 프랍 / 캐릭터가 같은 시각 언어 위에서 읽히면 된다. 격자감과 셀 경계는 **디자인 의도**로 수용한다.

## 컨셉 재정의 (rev4)

rev2/rev3 는 "Enter the Gungeon 수준의 연결감" 을 타겟으로 잡았으나, 맵 생성 방식이 **cell-by-cell grid** 이고 방 기반 procedural 이 아니라 구조적 불일치가 반복해서 드러났다. Place 파편화를 region mesh 로 묶어도 원본이 흩어져 있으면 효과가 제한됨이 28 에서 확증.

따라서 시각 참조를 다음과 같이 재설정한다:

- 참조: **Warhammer Underworlds / Gloomhaven / 보드게임 타일**
- "격자 타일이 명확히 보이는 보드" 가 목표. 셀 경계가 드러나도 **일관된 시각 언어**면 통과.
- Enter the Gungeon 의 연속된 방 바닥은 포기.
- 47-tile / room-based generation / 방 시뮬 — 전부 범위 밖.

## 남은 목표 (rev4)

격자 수용 이후 실제로 가치 있는 작업:

1. **팔레트 일관성** — Walk / Place / Env / 프랍의 톤 / 명도 / 채도가 같은 계열에서 움직임. 현재 흰 Place slab 과 녹색 Env 의 대비가 너무 강한 문제 해결.
2. **프랍 유기 분포** — 여전히 V-001 (Poisson 축약) 잔존. 같은 prop family cluster 가 더 읽히도록.
3. **sorting** — 이미 26 으로 해소. 유지 관리만.

## 최상위 구조 (유지)

```text
GeneratedMap
  -> BoardVisualPlan (cells, regions, anchors, goal, spawns)
  -> MapView                 (plan only, region mesh + overlay)
  -> BackgroundPropPlacer    (plan only)
```

## 현재 구현 baseline

- 6~15: 아키텍처 정의 + 초기 구현 (plan, placer plan consumer, shape mask, place rendering, env variation, anchor, prop distribution, theme contract, verification, visual audit)
- 16: visual audit → 결함 카탈로그
- 17: prop distribution proper pass (Poisson 축약 구현 — 부분 달성)
- 17b: prop visualScale 이중 적용 hotfix
- 17c: DEPRECATED (전제 오류)
- 24: enemy ECS → Mono quad view
- 25: fallback defender Mono 수렴
- 26: sort order unification (V-010 해소)
- 27: place seam light reduction
- 28: place region mesh refactor (부분 달성 — cardinal 연결된 region 만 묶임)

모두 rev4 에서도 **유지**. 버리지 않는다. 컨셉 재정의는 시각 참조의 상한 조정이지 구조 변경이 아니다.

## rev4 의 남은 spec

| 번호 | 파일 | 상태 |
|---|---|---|
| 22 | `22_theme_palette_pass.md` | **우선순위 1** — 쓴다 (rev4 에서 재정의) |
| 17r | `17r_prop_distribution_retry.md` | 우선순위 2 (선택) — V-001 해소 원하면 |
| 20 | `20_env_variation_tuning.md` | 낮은 우선순위 (optional) |
| 21 | `21_walk_shape_polish.md` | 낮은 우선순위 (optional) |
| 23 | `23_volcano_theme_fill.md` | 낮은 우선순위 |

**rev3 때 계획했던 18 / 19 / 28 / 29 같은 추가 seam 완화 spec 은 rev4 에서 불필요.** 격자감 수용으로 자연스럽게 해소.

## 작업 순서 (rev4)

1. **현 baseline 수용** — 28 (`1bc73f9`) 까지가 기술적 완성.
2. **22 palette pass** — 시각 언어 통일. rev4 의 핵심.
3. (선택) **17r Poisson 재작업** — 프랍 유기성 완성.
4. (선택) **20 / 21** — 미세 튜닝.
5. **23 volcano** — 두 번째 테마 완성. 12 번째 우선순위여도 됨.

## 공통 계약 (rev3 에서 유지)

- `BoardVisualPlan` 출력: `cells[x,y]`, `regions[]`, `decorAnchors[]`, `goal: int2`, `spawns: int2[]`.
- `BoardVisualCell` 필드: `sourceTileType`, `zoneType`, `regionId`, `sameZoneMask (8-bit)`, `transitionMask (4-bit)`, `innerCornerMask (4-bit)`, `shapeClass`, `surfaceNoiseHash`, `decorBudgetBias`, `pathProximity`, `borderProximity`.
- `BoardShapeType` 16종. inner corner 는 mask + overlay.
- 5종 decor anchor.
- renderer / placer 는 plan 만 소비.
- Mono 렌더 통일 (ECS RenderMesh 는 projectile / healthBar 만).

## 성공 기준 (rev4)

- **보드 전체가 하나의 시각 언어**로 읽힘 (톤 / palette 가 한 계열).
- 같은 seed 에서 동일한 plan + placement 재현.
- forest ↔ volcano 테마 교체 시 렌더 오류 없음.
- 프랍-캐릭터 sorting 정상.
- 격자감 / seam 은 **허용**. "이 보드는 타일로 구성된다" 를 시각이 명시.

**Enter the Gungeon 수준의 연속 바닥, 자연스러운 room 표현은 rev4 의 목표가 아니다.**

## legacy 폴더

`docs/spec/background-props/` 는 legacy. rev4 기준 문서는 `docs/spec/board-visualization/` 만.
