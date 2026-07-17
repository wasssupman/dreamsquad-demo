# 7 — 끈적함 시각화: 액체 타일 (히스테리시스 시각화)

**작업 구분**: feature · 의존: unit 1(히스테리시스), unit 4(팝) · **rev 2026-07-18**: 오버레이 블롭 → **하이라이트 자체가 액체**(사용자 결정)

## 목적

히스테리시스(margin)는 **보이지 않는다**. margin 을 올리면 "손가락은 옆 칸인데 하이라이트가 안 따라옴 →
무시당했다"로 읽힌다. **끈적함을 눈에 보이게** 해서 그 lag 을 버그가 아닌 **피드백**으로 바꾼다.
부수 효과: margin 의 사용 가능 상한이 올라간다(늘어나는 게 보이면 0.5 도 "엿가락 맛").

**rev 배경**: 1차 구현(타일 위 오버레이 블롭)은 같은 셀에 시각 개체 2개(초록 타일+청록 액적)가 겹쳐
신호가 죽었다. 최종 구조 = **포커스 셀 하이라이트 자체가 액체**: 테두리는 셀에 고정(계약 표시),
내부 fill 이 손가락 방향으로 번지다 테두리를 넘는다(전환 예고). 하이라이트 개체는 하나.

## 물리 모델 (레퍼런스 근거)

- **표면장력(Young–Laplace)**: 액체는 뭉치려 함 = 붙잡는 힘 = `margin`. 경계는 **또렷해야** 액체(흐리면 글로우)
- **Plateau–Rayleigh**: 목이 가늘어지면 급격히 파열 → `t` 선형 금지, `t^p` 후반 가속
- **부피 보존**: 당김 쪽으로 번는 만큼 반대쪽 fill 이 빠진다(lean/배수) — 없으면 "이동하는 원"으로 읽힘(실측)
- **점성 관성**: 신호를 스프링으로 지연 추종 — 늦게 따라오고, 멈추면/셀이 넘어가면 출렁이며 이완
- 우리 `t=1` 이 곧 파열점 — 전환 순간의 punctuation 은 unit 4 확정 팝이 담당

## 변경 대상

- `Assets/_Project/Shaders/PlacementLiquidTile.shader` — **신규**. SDF 셰이더(둥근사각 테두리 + smin 액체 fill)
- `Assets/_Project/Data/TileSets/PlacementLiquidTile.mat` — **신규**. 모양 튜닝의 단일 소스(인스펙터, Play 중 라이브)
- `Assets/_Project/Scripts/Data/TileSetData.cs` — `placementLiquidMaterial` 슬롯(전 TileSet 에셋 배선)
- `Assets/_Project/Scripts/UI/PlacementCellSnap.cs` — `EvaluateStretch` (+ 테스트)
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — `PlacementBlobOrder`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 액체 쿼드 렌더 + 팔레트/관성 SerializeField
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SetPlacementStretch(cell, dir, t, valid)` / `ClearPlacementStretch`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 신호 산출·전달·수명, hover 대체 게이팅
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — `stickyBlobEnabled`(마스터 토글)

## 구현

**① 신호 — 순수 함수 (`PlacementCellSnap.EvaluateStretch`, load-bearing)**
```
EvaluateStretch(committed, frac, stickMargin) → (dir, t)
  margin = clamp(stickMargin, 0, MaxMargin)      // Resolve 와 같은 clamp
  d      = frac − committed
  dir    = normalize(d)
  t      = clamp01( max(|d.x|, |d.y|) / (0.5+margin) )   // ★ 체비쇼프
```
**`t` 는 체비쇼프(축별 max).** `Resolve` 밴드가 축별 박스라 파열은 `max(|dx|,|dy|) ≥ 0.5+margin`.
유클리드면 대각선에서 거짓말. 분모·clamp 를 `Resolve` 와 공유(같은 파일) → 드리프트 0.

**② 전달** — `bridge.SetPlacementStretch(cell, dir, t, valid)` / `ClearPlacementStretch()`. 오브젝트 불가지(defender 모름).
`stickyBlobEnabled` 이면 컨트롤러가 `SetPlacementHover` 를 **건너뛴다**(액체 타일이 하이라이트 본체, 이중 표시 금지).
끄면 기존 타일 하이라이트 폴백. valid → 초록/빨강 팔레트.

**③ 렌더 (`TilemapMapView` + 셰이더)** — 셀 4배 쿼드 1장(grid 자식, `PlacementBlobOrder`), 회전 없음(테두리 축 정렬).
- 프래그먼트: `boxD = sdRoundBox(p)` → 테두리 밴드(고정). fill = `smin(boxD+inset−lean·t·proj, 타원팁D, k(t))`
  - **lean**: 당김 쪽 팽창 + 반대쪽 배수(질량 보존) · **타원 팁**: 당김 축 신장(원판 금지) · **k(t)**: 메니스커스, t↑ 수축
- **관성(뷰 소유)**: 표시용 당김 = `dir×t` 를 스프링(`liquidSpring`/`liquidDamping`) 추종, unscaled dt,
  재표시 시 스냅 리셋, 오버슈트 1.2 까지 셰이더 허용(출렁 가시화)
- **z-fight**: Ground 가 ZWrite On — 쿼드·팝 모두 `PropGroundLift` 리프트 필수(코플레이너면 깜빡임)
- **캔버스**: 쿼드 크기(`BlobQuadCells`=4)가 혀 최대 도달(≈1.9셀)보다 좁으면 옆 타일 위에서 칼로 잘린다.
  셰이더 `_QuadCells` 와 C# 이 단일 소스 동기

**④ 수명** — 온보드 드래그 중 표시. `ClearHover` → `ClearPlacementStretch`, `TilemapMapView.Clear` → 쿼드+머티리얼 파괴.

**⑤ 계약 (load-bearing)**
> **테두리는 확정 칸에 고정.** 번짐은 *긴장*일 뿐, 배치되는 곳은 확정 칸 — 테두리가 움직이면 "프리뷰가 거짓말".
> **머티리얼은 반드시 TileSetData 에셋 참조.** Shader.Find 는 기기 빌드 스트리핑에서만 죽는다(2026-07-15 사고 전례).
> **Unity `Mathf.SmoothStep` ≠ HLSL `smoothstep`** — 인자 의미가 다르다(1차 구현 유령 블롭의 원인).

## 파라미터

- **SO(`DragSwaySettings`)**: `stickyBlobEnabled` — 마스터 토글(끄면 기존 하이라이트)
- **`.mat` 인스펙터** = 모양: `_BorderWidth` `_CornerRadius` `_Inset` `_Reach` `_StretchPow` `_TipR` `_NeckK` `_TipElong` `_Lean` `_Feather`
- **`TilemapMapView` SerializeField** = 팔레트(`liquid{Valid,Invalid}{Border,Fill}`) + 관성(`liquidSpring` `liquidDamping`)

## 완료 기준

- 컴파일 클린. EditMode: `EvaluateStretch` — 중심 `t=0`, `Resolve` 전환점에서 `t≈1`(체비쇼프 정합), clamp01, dir 정규화, 대각선.
- Play: 테두리는 고정된 채 내부 액체가 손가락 쪽으로 번지고 테두리를 넘으며, 옆 타일 위에서 잘리지 않고,
  멈추면 출렁이며, 셀 전환 순간 되감김+확정 팝. 깜빡임(z-fight) 없음. invalid 셀은 빨강 팔레트.
- margin 0.2↔0.5 에서 번지는 양이 눈에 띄게 달라짐(= margin 이 시각적으로 읽힘).
- 사용자 Play 확인: **2026-07-18 통과** ("느낌 좋네" — 액체 타일 + 스프링 관성 + 잘림 수정 포함). 커밋 대기.
