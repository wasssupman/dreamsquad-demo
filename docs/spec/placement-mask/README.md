# placement-mask — 배치 가능 영역의 mask 정본화 (타일 종류 → per-cell 마스크)

상태: **units 0~3 구현·커밋 완료 2026-08-07 (EditMode 1894 그린 · 투트랙 코드 리뷰 반영) · unit 3 육안 축(Play 검증 맵·시나리오 4종·재검토 표 추기)은 대기**

## 상위 목표

배치 가능 위치가 `MapTileType.Place`(타일 종류)에 고정된 조건을 풀고, 타일 종류와 직교하는 **per-cell `placeMask`** 를 배치 가능성의 단일 정본으로 세운다. 마스크는 Walk 셀을 포함할 수 있다 — 이때 **적·방어 유닛의 행동 규칙은 일절 바꾸지 않는다**(적은 유닛 셀을 그대로 통과·공존, sim 변경 0). "어디에 놓을 수 있나"라는 제약만 데이터로 푼다 (2026-08-07 사용자 결정, B-1).

이 spec 은 후속 방향(footprint 오브젝트 맵 · 자유 이동)의 토대인 "논리 격자 = 마스크 묶음" 의 첫 레이어다. 단, `MapTileType` 은퇴·통행 규칙 변경은 **이 spec 범위 밖**이다.

## 작업 단위 목록

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| [0_place_mask_data_layer.md](0_place_mask_data_layer.md) | 데이터 레이어 | `placeMask` 직렬화·왕복·파생 폴백 (`propLayerId`/`goals` 선례 미러) |
| [1_predicate_and_curving.md](1_predicate_and_curving.md) | 판정 교체 | `SpatialPlacementCheck` 마스크화 + 런타임 커빙 재해석 + EffectTilePlacer |
| [2_map_painter_mask_brush.md](2_map_painter_mask_brush.md) | 저작 도구 | Map Painter 마스크 브러시·오버레이·베이크 |
| [3_walk_cell_placement_verification.md](3_walk_cell_placement_verification.md) | B-1 검증 | Walk 셀 배치의 Play 검증 + "배치칸=벽" 암묵 전제 6곳 재검토 |

## Feature-wide 계약

1. **`placeMask`(per-cell byte, 1=배치 가능)가 배치 가능성의 단일 정본.** `tiles` 는 시각(바닥/프랍 zone)·통행(walkMask 파생식 `tiles==Walk`) 정본으로 잔존한다. ECS 로 넘어가는 데이터는 불변 — sim 변경 0.
2. **폴백 사다리** (`goals`/`goalMaxStability` 폴백과 동형): doc 의 `placeMask` 부재·길이 불일치 → `tiles==Place` 파생. 빌더를 거치지 않은 직접 구성 픽스처 보호: `GeneratedMap.PlaceableAt` 이 마스크 미생성 시 `tiles==Place` 로 폴백. 기존 맵 6종·기존 테스트는 **무회귀**가 계약이다.
3. **배치 판정 술어의 단일 지점 유지**: 배치 가능성 판정은 `SpatialPlacementCheck` 하나로 수렴한다. 하이라이트·D&D·재배치·탭/클릭 배치는 술어 공유로 자동 추종하며, 병렬 스캔 재구현 금지 (placement-eligible-tile-highlight 계약 승계). (커빙 intent 비교·EffectTilePlacer 의 마스크 읽기는 배치 판정이 아니라 각각 저작 의도 감지·효과 타일 선정이다.)
4. **커빙(`ObstaclePlacer.DesignateDeco`) 재해석**: "doc 마스크가 파생값(`tiles==Place`)과 상이" = 수동 배치판 ⇒ 커빙 skip (authored-Deco skip 규칙과 동형·병렬). 커빙이 실행된 경우 종료 후 마스크를 tiles 에서 재파생해 동기를 유지한다. `ObstaclePlacer` 시그니처 불변.
5. **B-1 의미론**: 마스크는 Walk 셀을 포함할 수 있고, 그 위 유닛과 적은 겹침·통과를 수용한다(연출 이슈로 취급하지 않음). 통행(walkMask)·타겟팅·공격 규칙 무변경.
6. **EffectTilePlacer 는 마스크 기준으로 전환** — 효과 타일은 "그 칸에 유닛을 놓으면" 발동하는 배치 결합 시스템이므로 배치 정본을 따라간다. 픽업 후보(Walk∪Place)·프랍 zone·바닥 페인트는 tiles 기준 유지(범위 밖).
7. **`MapTileType.Place` 는 이 spec 에서 은퇴하지 않는다** — 판정의 데이터 소스만 교체한다. 타일 enum 정리는 후속(footprint 오브젝트 모델) 몫.

## 파이프라인 커버리지

**N/A** — 플레이 오브젝트 신설·생성→렌더 경로 변경 없음(배치 판정의 데이터 소스 교체 + 에디터 도구 확장). `docs/reference/object-pipeline-map.md` 대조 불요.

## 후속 후보

- **footprint 오브젝트 모델** [L] · 정본 반전 — 논리 격자는 마스크 묶음(placeMask + blockMask)만 유지, 맵 공간은 오브젝트(빌보드/메시)가 anchor+footprint(1×1·2×2·1×4)로 점유하고 blockMask 를 파생. `MapTileType` 은퇴. (2026-08-07 방향 탐색, 단계 2)
- **자유 이동** [L] · walkable 을 복도(`tiles==Walk`)에서 open field(`!blockMask`)로 재정의 + flow field 8방향·벡터 보간 일반화. navmesh 는 기각(footprint 가 타일 정렬이라 이득 없음 + 구조적 결정론 원칙과 상충). (단계 3)
- **픽업 후보·프랍 zone 의 마스크 정합** [S] · `PickupSpawnState.candidateCells`(Walk∪Place)·`BackgroundPropPlacer.OccludesPlay` 를 마스크 기준으로 볼지 — footprint 모델과 함께 결정.
- **Walk 셀 배치의 콘텐츠 활용** [M] · 실맵 6종에 마스크를 실제로 저작할지, 시각 어포던스(경로 위 배치 가능 표시)를 줄지 — 제품 결정.
- **페인터 Deco+mask=1 warning** [S] · spawn/골 셀 mask=1 은 warning 을 주는데 "장식물 위 배치 가능"(Deco+mask=1)은 침묵 — 마스크 브러시로만 만들 수 있는 의도 저작이지만 실수 가능성도 있어 warning 후보 (Track A 리뷰 MINOR-7).
