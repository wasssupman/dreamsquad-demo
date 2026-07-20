# ingame-ui-upgrade

> 상태: 완료 2026-07-09 (커밋 e25fb553)

> 위치 계약 후속 변경: `docs/spec/awakening-hud-resource-button/`에서 Placement Start 우하단은
> 유지하고, Battle의 NextWaveDock만 좌하단으로 이동하며 각성은 Battle 우하단에 배치한다.

## 목표

전투 화면의 두 UI 를 시각 업그레이드한다.

1. **StartButton**(배치 페이즈의 `START BATTLE` 버튼)을 **우하단**(타이머+NextWave dock
   코너)으로 옮기고, **Codex 생성 캐주얼 게임시작 버튼 배경 그래픽** + TMP 라벨 오버레이로
   바꾼다. 현재는 화면 중하단의 플랫 초록 사각 버튼이다. 배치 페이즈엔 START, 전투
   페이즈엔 타이머+NextWave 가 같은 우하단 코너를 이어받는다(시간상 배타 → 겹치지 않음).
2. **Cost UI**(좌하단 코스트 게이지)를 지금의 **단색 플랫 tint 세그먼트**에서
   **전용 아트 에셋**(에너지 크리스탈 아이콘 + 세그먼트 pip)을 쓰는 형태로 업그레이드한다.

검증 질문: *"전투 화면에서 START 버튼과 코스트 게이지가, 임시방편 단색이 아니라
게임의 캐주얼 아트 톤(포트레잇의 치비 가챠 톤)에 맞게 보이는가?"*

## 배경

- 스코어 HUD(`ScoreHudView`)는 외부 아트 없이 `UiRoundedSprite.Make(radius, border,
  fill, borderColor)` 로 **다크 플레이트 + 골드 테두리 + 골드 라벨 탭**을 절차적으로
  굽는다. StartButton 도 같은 헬퍼로 재현 가능 → **신규 에셋 불필요, 코드만.**
- Cost UI(`CostDisplay`)는 `_panel` 검정 반투명 + `Seg{i}` 회색 Image + `Fill` 초록
  Image(세로 스케일)로, 전부 스프라이트 미지정 기본 흰 사각을 tint 한 플랫 표현이다.
  사용자 요구: **별도 아트 에셋을 생성해 업그레이드.**
- 아트 톤 기준은 방어 유닛 **포트레잇**(`Assets/_Project/Art/DefenderPortraits/`)이다.
  → **치비 아니메 · 캐주얼 가챠 모바일** 스타일: 골드/크림 라운드 카드 프레임,
  반짝이·별·마법진 방사 배경, 밝고 채도 높은 팔레트 + 골드 악센트.
- 에셋 생성은 **Codex 에 위임**한다. Codex 에게 포트레잇 이미지를 직접 확인시키고
  "캐주얼 게임 스타일" 을 강하게 지시한다 (unit 1 브리프).

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_startbutton-reposition.md` | `PlacementPhaseView` StartButton 을 우하단 dock 코너로 이동 + 배경 이미지 슬롯 추가(미할당 시 절차 폴백). 코드만 |
| 1 | `1_ui-asset-brief.md` | UI 아트 에셋 Codex 브리프(START 버튼 배경 그래픽 + Cost 크리스탈 아이콘 + pip) 작성·생성 요청·Sprite import·START 슬롯 배선 |
| 2 | `2_cost-ui-upgrade.md` | `CostDisplay` 를 생성 에셋(크리스탈 아이콘 + pip)으로 재구성 |

## feature-wide 계약

- **StartButton = 배경 이미지 + TMP 라벨.** Codex 생성 배경 그래픽을 버튼 Image 로 쓰고
  `START BATTLE` 는 TMP 로 오버레이한다(문구 유연). 배경 이미지 슬롯이 비면 `UiRoundedSprite`
  절차 플레이트로 폴백.
- **UI 아트는 Codex 로 생성**한다. 아트 톤은 포트레잇(캐주얼 치비 가챠: 골드/크림 프레임,
  반짝이·글로시, 밝은 채도). 생성물은 투명 배경 PNG, `Assets/_Project/Art/UI/` 하위에 둔다
  (`UI/Cost/`, `UI/Buttons/`).
- 에셋 미할당(생성 전/누락) 시 **절차/플랫 폴백을 유지** — UI 가 깨지지 않는다.
- 두 UI 모두 순수 프레젠테이션. 게임 로직(코스트 계산/페이즈 전환)은 건드리지 않는다.
- 하드코딩 금지 원칙 유지: 스프라이트/색/치수는 SerializeField 또는 생성 헬퍼 인자로.
- 위치 계약: StartButton 은 우하단 코너(`NextWaveDock` 앵커와 정렬). 배치↔전투 페이즈가
  시간상 배타라 dock 과 시각 충돌 없음.

## 파이프라인 커버리지

**N/A** — 플레이 오브젝트(유닛/적/투사체/해저드/VFX)의 생성→렌더 경로를 신설·변경하지
않는다. UI 프레젠테이션 + UI 아트 에셋만 추가한다. (`object-pipeline-map.md` 대조 불필요.)

## 후속 후보 (현 spec 범위 밖)

- NextWaveDock 버튼 / PlacementPhase 배너의 동일 스타일 통일.
- 코스트 만땅/부족 시 크리스탈 아이콘 펄스·부족 시 레드 플래시 연동.
- StartButton 등장/호버 juice(PrimeTween pop).
