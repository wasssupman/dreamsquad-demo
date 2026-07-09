# 3 — Handoff (ingame-ui-upgrade)

## Commit

- e25fb553 — feat(ui): 인게임 START 버튼/코스트 UI 업그레이드 (ingame-ui-upgrade units 0~2)

## Implemented

- **StartButton**(`PlacementPhaseView`) 중하단 → **우하단**(dock 코너) 이동.
- START 배경 = Codex 캐주얼 그래픽(`start_battle_bg`), 라벨 = **Bangers SDF** + 다크 외곽선.
  미할당 시 `UiRoundedSprite` 절차 플레이트 폴백. 아우라 + 아이들 펄스 juice.
- **CostDisplay** 전면 재작성: 가로형 컴팩트 에너지 배지(363×130) — ⚡ 볼트 + 인라인
  `N/Max`(상단) + 10칸 바 게이지(하단). Codex 키트(패널/볼트/바) 사용.
- 코스트 총량 `DefaultCostConfig.maxCost` 15 → **10**.
- Codex 에셋 4종 Sprite import + BattleScene 컴포넌트 슬롯 배선.

## Key Files

- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs`
- `Assets/_Project/Scripts/UI/CostDisplay.cs`
- `Assets/_Project/Art/UI/Buttons/start_battle_bg.png`, `Art/UI/Cost/{cost_hud_panel,cost_energy_bolt,cost_bar_filled,cost_bar_empty}.png`
- `Assets/_Project/Data/Config/DefaultCostConfig.asset` (maxCost 10)
- `Assets/_Project/Scenes/BattleScene.unity` (슬롯 배선)

## Verified

- 컴파일/콘솔 에러 0. 사용자 Play 육안 확인(START 버튼·코스트 배지) 통과.

## Notes

- **패널 PNG 여백 크롭이 핵심**: Codex `cost_hud_panel` 원본은 투명 여백(L8 T7 R8 B11)이
  있어 9-slice 시 콘텐츠가 박스 밖으로 밀렸다. 불투명 영역(200×218)으로 크롭 후 정상화.
  → 향후 UI 패널 에셋은 **여백 없이 꽉 채워** 받는 게 안전.
- 9-slice 테두리 `cost_hud_panel.png.meta` spriteBorder 30. 세로 스프라이트를 가로로 늘려도
  코너 유지. 콘텐츠는 코드 레이아웃(패딩 18, 상단행 50 / 바행 34).
- START 배경은 장식 코너라 `Image.Type.Simple`(9-slice 금지). 코스트 패널은 Sliced.
- 초안의 크리스탈/젬 pip 3종은 폐기·**삭제**(미사용). 스코어 배지 스타일 START 안(초기
  방향)도 폐기 — 최종은 캐주얼 그래픽 + 우하단.

## Follow-up

- START 버튼 문구 `START BATTLE` → `BATTLE!`/`START!` 한 단어 검토(캐주얼 톤).
- 코스트 max 10 기준 밸런스(시작값 10=풀) 재검토 여지.
- NextWaveDock/PlacementPhase 배너 동일 톤 통일(README 후속 후보).
