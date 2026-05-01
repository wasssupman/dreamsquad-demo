# Zone Transition Rules (rev3)

## 문제 정의

각 타일이 텍스처를 따로 보여줘 경계가 뚜렷하거나, 반대로 전체를 한 장으로 덮어 zone 이 흐려지는 문제. 해결은 cell random 이 아니라 **zone 간 전이 규칙 + inner/outer corner 분리**.

## 기본 zone

- `Walk`: 적 이동 경로
- `Place`: 배치 가능
- `Env`: 일반 배경

## 전이 우선순위

1. `Walk` 가장 먼저 읽힌다.
2. `Place` 는 배치 영역으로 구분된다.
3. `Env` 는 둘을 받쳐주는 연결 배경이다.

gameplay readability 가 우선. 자연스러움은 edge / corner 규칙으로 달성.

## Shape 집합 (16종, shape class)

- `Isolated`
- `End` × 4 (N/E/S/W)
- `Straight` × 2 (NS/EW)
- `OuterCorner` × 4 (NE/NW/SE/SW)
- `TJunction` × 4 (N/E/S/W)
- `Cross`

## Inner corner 는 overlay-only

별도 shape class 가 아니라 **4-bit `innerCornerMask`** 로 표현. 각 비트가 세워진 방향마다 같은 셀 위에 overlay quad 를 하나씩 올린다. shape class 와 공존 가능:

예:
- `Place` 셀이 `StraightEW` 이고 `innerCornerMask` 의 NE 비트가 서면 → Straight base + NE inner corner overlay.

## Inner / Outer corner 판정

셀이 zone `Z` 에 속한다고 가정.

- **Outer corner** (shape class): cardinal 기준 두 이웃이 `Z`, 나머지 두 이웃이 `¬Z`. shape 가 `OuterCorner*` 로 분류된다.
- **Inner corner mask** (overlay): 해당 셀이 `Z` 내부이고, 대각 방향이 `¬Z` 인데 그 대각과 공유하는 두 cardinal 이웃이 모두 `Z` 인 지점. 해당 대각 bit 를 `innerCornerMask` 에 세움.

두 조건은 독립적으로 평가된다.

## 전이 표현 레이어 (v0)

1. `Env` base (region 내부 noise multi-texture)
2. `Env` detail scatter
3. `Walk` / `Place` base top (shape class 기반)
4. `Place` outer corner sprite (OuterCorner* shape)
5. **`Place` inner corner overlay** (innerCornerMask 비트마다 1 quad)
6. zone edge fringe overlay
7. decor props

검은 outline 금지. 내부 톤/fringe 로 읽히게.

## v0 Transition Scope

- `Walk → Env` outer corner/edge
- `Place → Env` outer corner + inner corner overlay + edge
- `Walk` shape 4방향 회전
- `Place` inner corner overlay 는 4 방향 분리 sprite 또는 1장 + yaw 회전 모두 허용

미지원 유지:
- `Walk → Place` 전용 규칙
- `Blocked` 전이
- 47-tile full coverage
- diagonal sub-cell smooth blend

## Rule Direction

### Walk
- shape 별 전용 texture + 회전 재사용
- `Env` 와 맞닿는 변에 optional path shoulder

### Place
- 단일 slab 반복이 아니라 연결된 plate 느낌
- **outer corner 는 shape class, inner corner 는 overlay mask 로 분리**
- 모든 셀 경계를 같은 강도로 보여주지 않는다

### Env
- 개별 셀이 아니라 **region 내부 noise-driven variation** (`11`)
- region 간 경계는 1 셀 폭 blend (`11`)
- detail 은 셀 중앙보다 zone fringe 와 코너 위주

## Transition Band Rules

- band width 1 셀 (v0)
- overlay 는 base 위에 얹힌다
- outer corner 는 straight 보다 우선
- inner corner overlay 는 outer corner 와 독립. 같은 셀에 둘 다 올 수 있음
- edge overlay 가 상대 zone 을 과하게 침범하지 않는다

## Sprite 최소 요구

카테고리별:

- Walk: `single`, `straight_ns`, `straight_ew`, `outer_corner`, `end`, `t_junction`, `cross` (7)
- Place base: `base`, `base_variants[]`
- Place transition: `outer_corner`, `inner_corner` (overlay 용), `edge_straight`, `end_cap`
- Walk ↔ Env transition: `shoulder_edge` (optional)

corner / end / T-junction 은 기준 방향 1장 + renderer yaw. inner corner overlay 는 별도 sprite (4 방향 또는 1장 + yaw).

`placeInnerCornerTexture` null fallback: overlay 를 그리지 않는다. outer corner 로 회귀 fallback 금지 (`14`).

## v0 성공 기준

- `Walk` 의 직선/코너가 명확히 구분된다.
- `Place` 는 slab 그리드가 아니라 묶인 바닥처럼 읽힌다.
- 같은 zone 이 L자로 맞닿는 지점에 inner corner overlay 가 들어가 꺾임감이 이어진다.
- `Env` 는 패치워크가 아니라 variation 있는 연속 배경.
- zone 사이 경계가 딱 잘린 컷이 아니라 의도된 전이처럼 보인다.

## RuleTile 과의 관계

Unity `RuleTile` 채택 안 함. 개념만 차용: 이웃 mask, shape 분류, 회전 가능 sprite reuse, inner corner overlay (RuleTile 에 없는 본 spec 고유).
