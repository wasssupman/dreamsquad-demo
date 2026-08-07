# 5. Handoff Summary — placement-mask (units 0~4)

## Commit

- `6845a692` spec 초안(critic 리뷰 반영) · `dd26dfa7` unit 0 데이터 레이어 · `77ed95dd` unit 1 판정·커빙 · `f7095db6` unit 2 페인터 · `3eca983e` unit 3 B-1 검증 · `da73d387`·`9dd81ee4` units 0~3 투트랙 리뷰 반영
- `408372cc` unit 4 층 비트필드 · `77b16a4a` README 편입 · `588a99c4` unit 4 리뷰 반영
- `74a6f470` 가디언 경로 층 저작 · `c6666e0e` 옛 문서 층 채우기 + `MapDocument_Test` 마이그레이션

## Implemented

- 배치 가능성의 정본이 `MapTileType.Place` → per-cell `placeMask`(층 비트필드)로 이동. 판정 = `(셀 층 & 유닛 층) != 0`, **코드는 `DefenderClass`/role 을 보지 않는다**.
- 층: `Ground`(배치지면, Place 파생) / `Path`(경로, Walk 파생) / `All`(유닛 전용). 파생·정규화는 `PlacementLayers.Derive/Sanitize` 단일 정의.
- 폴백 사다리 3단(문서 부재·길이 불일치 → 파생 / 마스크 미생성 struct → 타일 파생 / 유닛 `None` → `Ground`) — **아무것도 저작 안 하면 기존과 동일**(옵트인).
- 커빙(`DesignateDeco`)이 마스크와 어긋나지 않게: `HasAuthoredMaskIntent`(저작 감지 → skip) + `RederivePlaceMask`(실행 후 동기).
- 하이라이트가 드는 유닛의 층으로 스캔(유닛+표시상태 래치로 자기치유). 재배치는 옮기는 유닛의 층.
- 스폰·골 칸은 라이브 빌드 마지막에 전 층 폐쇄(런타임 불변식) — 문서/커빙 의미는 불변.
- 효과 타일은 `Ground` 층 고정. 전방 배치기 `FindNearestPathDirection` 자기 셀 제외(Walk 위 배치 시 고정 +x 발사 결함).
- Map Painter: Mask 브러시(지면/경로 층 선택), **테두리=손댄 칸 / 색=열린 층** 오버레이, 파생 리셋 + **빠진 층 채우기**(비파괴), 스폰/골 경고(파생과 상이 기준), 베이크 로그.
- 저작 사례: `Defender_Guardian.placementLayers = Path`. `MapDocument_Test` 는 층 이전 문서라 경로 비트 0 이었고 마이그레이션함(지면 264칸 보존 + 도로 900칸 경로 개방).

## Key Files

`Scripts/Data/PlacementLayer.cs` · `Data/GeneratedMap.cs`(`LayersAt`/`PlaceableAt`) · `Data/MapGrid/{MapDocument,MapDocumentBuilder}.cs` · `Data/{ObstaclePlacer,EffectTilePlacer,BattleMapBuilder,DefenderUnitData}.cs` · `Bridge/BattleBridge.cs`(`SpatialPlacementCheck`·커빙 블록·하이라이트·스폰/골 폐쇄) · `Bridge/BattleBridge.Relocation.cs` · `UI/DefenderDragPlacementController.cs` · `Editor/MapPainterWindow.cs`

## Verified

- EditMode **1917/1919 그린**(실패 0, skip 2 = 이 spec 이전부터의 의도적 Ignore). 신규 테스트 ~30케이스.
- 투트랙 적대 리뷰 2라운드(units 0~3 / unit 4). unit 4 리뷰에서 **하이라이트 유닛 고착(CRITICAL)** 을 잡아 수정 — 두 리뷰어가 독립적으로 같은 결함 지목.
- 라이브 경로(mapPool→BuildMapForBattle) 테스트로 마스크·층·스폰/골 폐쇄 검증.

## Notes (되돌리면 안 되는 의도)

- **파생·정규화는 `PlacementLayers` 단일 정의를 지날 것.** 빌더·커빙·폴백·페인터가 공유한다 — 한 곳만 복제해도 커빙 skip 판정이 어긋난다.
- **하이라이트 래치는 유닛과 표시 상태를 포함해야 한다.** bool 하나로 되돌리면 arm 갈아타기에서 이전 유닛 층이 남아 판정과 갈린다.
- **Path 층의 blast radius = 맵 전체**(파생이 `Walk→Path`). 의도된 의미이며, 특정 도로 칸만 닫으려면 페인터로 그 칸의 Path 비트를 지운다.
- **옛 문서에 `Mask=파생 리셋` 금지** — 저작이 통째로 날아간다. 층이 늘면 `빠진 층 채우기`(OR)를 쓴다.
- `MapTileType` 은 은퇴하지 않았다. 시각·통행(walkMask)의 정본으로 잔존한다.

## Follow-up

- **unit 3 육안 축 미완**: Play 시나리오 4종(경로 위 배치 시 적 통과 공존 / 어그로 / 보스 필드 / 도약 착지) 확인 + 재검토 표 6행 결과 추기. 가디언(경로)·`MapDocument_Test` 로 바로 가능.
- **하이라이트 유닛 전환의 자동 커버 없음**(UI 상태 전이라 EditMode 밖) — Play 로 arm 갈아타기 확인 필요.
- 적 이동의 대칭 설계 → `docs/spec/traversal-layers/`(작성됨·승인 대기).
- 나머지 후보는 README "후속 후보" 참조(footprint 모델, 자유 이동, 픽업/프랍 zone 정합, Deco+mask 경고, `None` sentinel, 경로 개방 도로의 시각 어포던스).
