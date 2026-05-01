# Scope And Goals (rev4)

## In Scope

- grid 기반 전투 보드 시각화
- `Walk`, `Place`, `Env` 영역의 시각 규칙
- zone 간 edge / corner 표현 (inner corner overlay 포함)
- 배경 프랍 배치 규칙 (Poisson + cluster + jitter — 부분 달성)
- theme 교체 가능한 아트 파이프라인
- Env 내부 sub-tile variation
- **팔레트 / 톤 일관성** — rev4 의 핵심
- Mono 렌더 통일 + sortingOrder 체계

## Out Of Scope

- **Enter the Gungeon 수준의 연속된 방 바닥** — rev4 에서 포기
- 방 기반 procedural 맵 생성 (room generator)
- 오픈월드형 biome simulation
- 47-tile full autotile authoring pipeline
- moisture / fertility / ecology 같은 고차 필드
- Unity Tilemap 기반 전체 전환
- pathfinding / battle rules 변경

## Visual Target (rev4)

참조: **Warhammer Underworlds / Gloomhaven / 보드게임 타일맵**.

- 격자 타일이 **명확히 구분**되어도 된다. 셀 경계는 디자인 의도.
- Walk / Place / Env 가 같은 계열 톤/명도/채도로 움직여 **하나의 보드**로 읽힌다.
- 프랍은 타일 위에 놓인 board piece 처럼 일관된 시각 언어.
- 캐릭터는 프랍과 같은 depth/sort 규칙.
- 인접 Place region 이 묶일 수 있으면 plate 로 보이고, 파편화된 작은 Place 는 **독립된 타일**로 자연스럽게 보인다 (seam 을 강조하지 않는 팔레트면 OK).

## Non-Goals For v0

- 연속된 방 바닥 표현
- 자연 생태계 시뮬
- `Blocked` 전용 시각 규칙
- 물리 기반 머티리얼 / 그림자 정밀화
- 고급 decor profile 시스템

## Acceptance (rev4)

- 같은 seed 에서 동일한 `BoardVisualPlan` + placement 재현.
- `Walk`, `Place`, `Env` 가 같은 시각 언어로 묶임 (톤 일관성).
- 같은 바닥이 L자로 맞닿을 때 inner corner overlay 가 들어감 (rev3 유지).
- Env 내부에서 2 종 이상 surface variation 관찰.
- 프랍이 scatter / cluster 로 배치 (완전 random 아님).
- 프랍 rotation/scale jitter 로 복붙 인상 없음.
- renderer / placer 가 plan 만 읽음.
- `BattleBridge.StartBattle` 100 회 반복 leak 0.
- forest / volcano 교체 시 렌더 오류 0.
- **격자감은 허용**. Enter the Gungeon 같은 연속감은 평가 대상 아님.

## Language Guardrail

- `pathProximity`, `borderProximity` 등은 plan 파생값이지 월드 상태 아님.
- `decorBudgetBias` 는 장식 밀도 제어값이지 생태 필드 아님.
- region 은 연결된 zone 덩어리.
- `cluster` 는 프랍 배치 Pass 의 이름.
- `inner corner / outer corner` 는 mask 분류.
- **"연속된 바닥" / "room" 같은 용어는 rev4 에서 사용하지 않는다.** 표현이 필요하면 "묶인 plate" 선에서.

## rev3 → rev4 의 축소된 목표 차이

rev3 Acceptance 중 rev4 에서 **완화된 항목**:

- ~~"같은 바닥이 L자로 맞닿을 때 inner/outer corner 가 시각적으로 구분된다"~~ → 유지 (rev3 구현이 그대로 통과)
- ~~"Place 내부 seam 이 눈에 띄지 않음"~~ → **삭제**. 격자감 수용.
- ~~"Enter the Gungeon 참조와 비교" 관련 항목~~ → **삭제**.
- ~~"전체 화면이 보드 한 장으로 읽힘"~~ → "**보드게임 타일 한 세트**로 읽힘" 으로 수정.
