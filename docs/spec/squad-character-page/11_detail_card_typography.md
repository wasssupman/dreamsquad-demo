# 11 — 상세 카드 타이포그래피: 설명문 확대 + 스탯 2열

## 목적

좌측 상세 카드의 **설명문이 작고 잘린다**. 폰트를 24→30 으로 키우되, 카드 높이 예산이 이미 초과 상태이므로 스탯 5행을 2열 3행으로 압축해 공간을 회수한다. 카드 크기·Spine 백드롭은 건드리지 않는다.

## 현재 상태 (측정값)

`cardRoot` 실제 높이 = `(cardHeight 0.56 − cardBottomMargin 0.03) × 1080` = **572px**
(캔버스 `matchWidthOrHeight = 1` → 화면비와 무관하게 높이 1080 고정 — `UiCanvasSetup.cs:39`)

| 항목 | 높이 |
|---|---|
| 패딩(18+40) + 간격(12×8) | 154 |
| 이름 54 + 배지 42 + 스탯 5×38 + 설명 76 + 버튼 84 | 446 |
| **필요 합계** | **600px → 28px 초과** |

설명란 76px 는 font 24(줄높이 ~28) 기준 2.6줄. 최장 desc(Guardian 62자)는 3줄 필요 → **현재도 잘린다**.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadUnitDetailView.cs` — `EnsureCardBuilt()` 스탯 행 구성 + `_summaryText` 인자, `_statRowGos` 배열 크기, `MakeStatCell` 신설

## 구현

- **스탯 2열 3행**: 행 하나에 (라벨 left + 값 right) 셀 2개를 `HorizontalLayoutGroup`(`childForceExpandWidth=true`)으로 절반씩. 5번째(각성보상)는 홀수라 3행 = 셀 1 + **빈 스페이서 셀**로 좌측 정렬 유지. 스탯 영역 190 → **114px**.
- **설명란**: `MakeText(cardRoot, "", 24, TopLeft, 76)` → `(…, 30, TopLeft, 148)`. `enableWordWrapping = true` 유지.
- **예산 재계산**: 비설명 자식 = 54+42+114+84 = 294. 패딩 58 + 간격(자식 7개 → 12×6=72) = 130. 572 − 424 = **설명 148px**. font 30 줄높이 ≈ 35 → 4줄. 폭 = `detailWidth 0.34 × 1920 − 패딩 44` = 609px → 한글 전각 30px 기준 한 줄 ~20자 → Guardian 62자 = 4줄(140px) ≤ 148 ✓
- **`_statRowGos` 는 행 단위**: 배열 5 → **3**. `SetUnitPartsActive()`(101행)가 스톤 모드에서 스탯을 숨기는 데 쓰므로 행 기준으로 바뀌어야 한다. `_statValues[5]` 는 셀 단위로 유지(`SetStat(index, value)` 계약 불변).
- **스톤 상세도 자동 수혜**: `ShowStone`(77행)이 같은 `_summaryText` 를 쓴다. 스톤 모드는 배지·스탯 행이 숨겨져 공간이 남으므로 확대는 이득만 있다.

## 완료 기준

- compile 클린.
- Play(유닛 모드): 최장 desc(Guardian 62자)가 잘림 없이 전부 보이고, 최단(Artillery 11자)에서 카드 하단이 비어 보이지 않는다. 스탯 5개 값이 2열로 정확히 매핑(데미지/체력 · 사거리/공격주기 · 각성보상/빈칸).
- Play(스톤 모드): 배지 행 + 스탯 3행이 **모두** 숨겨진다(행 잔상 없음). 스톤 요약문이 확대된 폰트로 표시.
- 카드가 `cardRoot` 경계를 넘지 않는다(하단 [출전] 버튼이 잘리지 않음).

> 검증 시 주의: 이 캔버스는 `ScreenSpaceOverlay` 라 `manage_camera` 게임뷰 스크린샷에 안 잡힌다. Play 중 잠깐 `ScreenSpaceCamera` 로 플립(저장 금지).
