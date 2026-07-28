# 1 — UI 아트 에셋 Codex 브리프

> 2026-07-28 후속: 아래 브리프는 완료 당시 생성 이력이다. 현재 캐릭터 아트 기준은
> `Art/DefenderPortraits/spine/defender_portrait_*.png`이며, 동적 contact sheet는
> `Window > Wassup > Defender Portrait Baker > Preview All`에서 확인한다.

## 목적

전투 UI 두 곳에 쓸 캐주얼 아트 에셋을 Codex 로 생성한다. 아트 톤 기준은 방어 유닛
**포트레잇**(치비 아니메 · 캐주얼 가챠). Codex 에게 포트레잇을 **직접 확인**시키고
캐주얼 게임 스타일을 강하게 지시한다. 생성물은 투명 PNG 로 지정 경로에 저장 → Unity
에서 Sprite import → 컴포넌트 슬롯에 배선.

## 생성 대상 (최종 키트)

> rev 2026-07-09: 코스트 UI 는 사용자 레퍼런스(⚡ + 큰 숫자 + 짧은 바 게이지) 기준
> **에너지 배지**로 재설계됨. 초안의 크리스탈/젬 pip(`cost_crystal`, `cost_pip_*`)은
> **폐기**(미사용 — 정리 후보). START 버튼 배경은 그대로 사용.

| 키 | 용도 | 크기 | 경로 |
|---|---|---|---|
| START 버튼 배경 | `PlacementPhaseView.startButtonBackground` | 768×288 | `Art/UI/Buttons/start_battle_bg.png` |
| 코스트 HUD 패널 | `CostDisplay.costPanelSprite` | 216×236 | `Art/UI/Cost/cost_hud_panel.png` |
| 에너지 볼트 | `CostDisplay.costEnergyIcon` | 64×64 | `Art/UI/Cost/cost_energy_bolt.png` |
| 바 (채움) | `CostDisplay.costBarFilled` | 24×28 | `Art/UI/Cost/cost_bar_filled.png` |
| 바 (빈칸) | `CostDisplay.costBarEmpty` | 24×28 | `Art/UI/Cost/cost_bar_empty.png` |

## Codex 에 전달할 브리프 (그대로 복사)

> **[강한 지시]** 먼저 `Assets/_Project/Art/DefenderPortraits/spine/`의 방어 유닛
> 포트레이트 전수를 열거나 Defender Portrait Baker의 `Preview All` contact sheet로
> 현재 게임의 캐릭터 아트 스타일을 눈으로 확인하라.
>
> 이 게임은 밝고 친근한 **캐주얼 모바일 디펜스** 톤이다. 작은 크기에서도 읽히는 단순한
> 실루엣, 깨끗한 외곽선, 밝은 팔레트와 높은 대비를 우선한다. 사실적·그런지·어두운
> 하이판타지·수집형 RPG 키아트는 피하고, 아래 UI 에셋도 같은 캐주얼 게임 톤으로 만든다.
>
> 모두 **투명 배경 PNG**, 여백 최소, 지정 경로/크기로 저장.
>
> 1. **start_battle_bg.png** (768×288) — 전투 시작 버튼의 배경 그래픽. 골드로 트림된
>    글로시 라운드 버튼 판(캡슐/둥근 사각). 따뜻한 에너지 색(앰버~오렌지 또는 생기있는
>    그린) 글로시 바디, 은은한 하이라이트/베벨, 골드 리벳/코너 장식. **중앙은 텍스트가
>    올라가므로 비워둘 것**(중앙 1/2 영역에 큰 그림 요소 금지). "출발/돌격" 느낌.
> 2. **cost_crystal.png** (256²) — 코스트(에너지) 자원 심볼. 빛나는 파세트 크리스탈/젬
>    또는 오브. 골드 테두리, 내부 발광, 캐주얼 큐트. (색: 마나 느낌의 시안/블루 또는
>    에너지 느낌의 앰버 중 캐릭터 톤과 어울리는 쪽.)
> 3. **cost_pip_filled.png** (128²) — 코스트 게이지 한 칸(채워진 상태). 위 크리스탈과
>    같은 색 계열의 작은 발광 젬/보석. 또렷하고 밝게.
> 4. **cost_pip_empty.png** (128²) — 코스트 게이지 한 칸(빈 상태). 같은 젬의 어둡고
>    비어있는 소켓/윤곽. 채워진 것과 실루엣 일치, 채도/명도만 낮게.
>
> 4종이 **하나의 세트로 일관**되게(같은 골드 트림·같은 글로시 톤) 보이도록 하라.

## Unity import (에셋 수령 후 — Claude 가 수행)

- 4개 PNG 를 `textureType: Sprite (2D and UI)`, `spriteMode: Single`, `alphaIsTransparency`
  로 재import (`manage_texture as_sprite`).
- `PlacementPhaseView.startButtonBackground`, `CostDisplay.costCrystalIcon/costPipFilled/
  costPipEmpty` 슬롯에 각각 배선(BattleScene 컴포넌트).
- START 배경은 고정 크기 버튼이므로 `Image.Type.Simple` 로 충분(9-slice 불필요).

## 완료 기준

- 4개 에셋이 지정 경로에 존재하고 Sprite 로 import 됨.
- 슬롯 배선 후 Play: START 버튼이 캐주얼 그래픽으로, 코스트 게이지가 크리스탈+젬 pip 로
  보인다. 미할당 시 폴백(절차 플레이트 / 플랫 세그먼트)으로 안전.

---
완료 확인: 2026-07-09 · 커밋 e25fb553 (Codex 키트 수령·import·배선 완료)
