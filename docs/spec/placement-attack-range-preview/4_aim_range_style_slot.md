# 4 — 조준 페이즈 전용 범위 스타일 슬롯 (주황 solid)

## 목적

지금은 `rangeTile`/`rangeColor` **한 쌍**을 5개 호출처가 공유한다 — 드래그 중 사거리 프리뷰
(`SetPlacementRange`), 조준 페이즈 레인(`PaintLanes`), 폭탄 착지셀(`PaintLandingCells`),
스킬 조준(`SetSkillAimRange`), 스킬 텔레그래프(`PinSkillTelegraph`). 그래서 **드롭 전(배치)과
드롭 후(공격방향 지정)가 똑같은 주황 프레임**이고, 단계가 바뀐 걸 표시가 말해주지 못한다
(사용자 지적 2026-07-28).

조준 페이즈에만 **두 번째 스타일 슬롯**을 준다. 형태는 **solid 채움**(테두리 rim + 안쪽 fill),
색은 **배치 단계와 같은 주황**(사용자 결정 2026-07-28, 시안 시안(試案)을 거쳐 확정) —
**두 단계를 가르는 신호는 색이 아니라 형태다**(배치 = outline / 조준 = 채움). 색이 같으니
"사거리를 말하는 표시"라는 정체성은 한 색으로 유지되고, 단계 전환만 형태로 읽힌다.

> `aimRangeColor` 는 현재 `rangeColor` 와 같은 값 `(1, 0.55, 0.12)` 이지만 **별도 knob 이다** —
> 두 표시가 언제든 갈라질 수 있게 슬롯을 나눠둔 것이고, 한쪽을 바꿔도 다른 쪽이 따라가지 않는다.
> 스프라이트가 흰색이라 이 값이 곧 최종 렌더색이다(배치 하이라이트 슬랩은 스프라이트 자체가
> 시안이라 tint 값 ≠ 최종색인 것과 다르다 — 그쪽 색을 참고할 일이 있으면 곱을 역산해야 한다).

> **2026-07-04 결정 뒤집기 (사용자 재승인 2026-07-28)**: README 계약은 "solid fill 은 맵을 과하게
> 가려 폐기(사용자 결정 2026-07-04)" 였다. 사용자가 그 결정을 인용받은 뒤 **solid 로 진행**을 지시했고,
> 이어서 **"투명 넣지 말고 진하게"** 로 완전 불투명(`aimRangeAlpha` 1)까지 지시했다. 즉 조준 페이즈는
> 아래 맵을 **가리는 것이 사양**이다. 원 결정이 지켜지는 구간은 **적용 범위를 좁힌 것** — 배치 단계
> (드래그 프리뷰)·스킬 조준/텔레그래프는 outline 그대로다.

## 변경 대상

- `Assets/_Project/Scripts/Data/TileSetData.cs` — `aimRangeTile` / `aimRangeColor` / `aimRangeAlpha`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 범위 페인트/틴트가 스타일을 선택
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SetAimGuide` 경로만 aim 스타일 요청
- Asset 신규: `Assets/_Project/Data/TileSets/tile_range_solid.png` + `tile_range_solid.asset`
- Asset 배선: `Generated/Tiles/AutoTileTest/TileSet_AutoTileTest.asset`(라이브) · `Data/TileSets/TileSet_Desert.asset`

## 구현

- **폴백 = 단일 게이트**: `aimRangeTile == null` 이면 타일·색·알파 전부 기존 `range*` 를 쓴다.
  미할당 타일셋은 **바이트 동일 동작**(무회귀). 스타일 분리를 끄고 싶으면 슬롯을 비우면 된다.
- **스타일은 페인트 시점에 고정**: `SetPlacementCells(cells, alphaMul, aimStyle)` 가 어떤 `TileBase`
  를 깔지와 `_rangeAimStyle` 를 함께 정하고, `Update()` 의 틴트가 같은 플래그로 색/알파를 고른다.
  **알파 소유권은 그대로 `Update()`** — 호출부가 색을 직접 박으면 다음 프레임에 덮인다(unit 9 계약).
- **조준 표시는 세기 배율을 쓰지 않는다**(전부 배율 1 = 불투명). 미선택/선택은 알파가 아니라
  **몇 개를 그리느냐**로 갈린다 — 미선택은 4레인 전부, 선택은 그 레인 하나만. 드래그 프리뷰의
  dim(`AimLaneDimAlpha` 0.7)은 outline 표시라 그대로 둔다.
- **조준 화살표는 같은 슬롯 + 명도 상향**: `SetAimArrows` 는 조준 페이즈 전용이라 `aimRangeColor` 에서
  색을 받되 `AimArrowLighten`(0.72) 만큼 흰쪽으로 민다. **solid 채움 위에서 같은 색 화살표는 사라진다** —
  색상(hue)을 공유해 "레인과 한 몸"이라는 신호는 지키고, 값 대비로 읽히게 한다.
- **호출처 분기는 `SetAimGuide` 안에서만**: `PaintLanes`/`PaintLandingCells` 에 `aimStyle` 파라미터를
  더하고, 드래그 프리뷰(`SetPlacementRange`)·스킬 조준·텔레그래프는 인자 없이 기존 경로(false).
- **solid 스프라이트**: 64px, PPU 64(=1셀), pivot Center, Bilinear + **Uncompressed**(압축이면 격자선
  유령 — 기존 교훈). **전 픽셀 알파 255 — 투명도를 쓰지 않는다**(사용자 결정 2026-07-28).
  칸 경계는 알파가 아니라 **테두리 2px 의 명도를 낮춰**(RGB 0.70) 낸다 — 틴트가 곱해져 "진한 주황
  테두리 + 밝은 주황 채움"이 되고, 레인 3칸·착지 N칸을 세는 정보가 살아남는다. 색은 여전히
  `aimRangeColor` 가 입힌다(스프라이트 RGB 는 흰색/회색 = 밝기만 담당,
  `placement-eligible-tile-highlight/3_range_dark_liner.md` 의 tint 상호작용 주의 승계).
- 신규 시스템·큐·순수 함수 0. 순수 Presentation, ECS 접근 없음(feature 계약 승계).

## 완료 기준

- [x] compile 0 · EditMode 1541 중 실패 1(`DreamSquadMobileBuildCliTests.Preflight_AcceptsTrackedSerializedScreenAutoRotation`
      — 모바일 빌드 orientation preflight, 이 변경과 무관한 사전 실패). 회귀 0.
- [x] 스타일 분기 실측(오프스크린 렌더, 실제 바닥 타일 위): 같은 셀 집합에 대해
      `aimStyle=false` → `tile_grid_outline`(outline) / `aimStyle=true` → `tile_range_solid`(채움) ·
      틴트 RGBA(1, 0.55, 0.12, **1**). 색은 같고 타일만 갈린다.
- [x] 불투명 확인: 스프라이트 최소 알파 255 · 틴트 알파 1 · 배율 1 → 아래 바닥이 비치지 않는다.
      칸 경계는 어두운 테두리로 남는다(레인 3칸이 통짜 막대로 뭉치지 않음).
- [x] 화살표가 불투명 채움 위에서 판독됨(명도 상향 적용 후).
- [x] (Play) 드래그 중 = 주황 **테두리** → 드롭 → 조준 페이즈 = 같은 주황 **채움**.
      머신거너 4레인 십자·폭탄맨 착지셀 4개 둘 다 채움.
- [x] (Play) 스킬 조준/텔레그래프 표시 무변경.

확인 2026-07-28 (사용자 Play).
