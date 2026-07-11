# 0 — Tray Config와 외곽 셸

## 목적

유닛 스트립·코스트 레일·드림캐쳐 핸드가 같은 치수/색 계약을 소비할 데이터 원천을 만들고, 투명 슬롯 나열 뒤에 명확한 Action Tray 배킹을 추가한다. 선행: `mobile-ui-safe-area` 완료.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs`
- 신규 `Assets/_Project/Data/Config/BattleHudTrayConfig.asset`
- `Assets/_Project/Scripts/UI/DefenderSelector.cs`
- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

- Config에 safe edge를 제외한 트레이 전용 값만 둔다: anchored y, placement/battle/hand size, padding, slot spacing, energy rail size/overlap, name font 범위, tray/name/dim colors.
- `DefenderClass`별 presentation entry는 role, 짧은 glyph, 색을 직렬화한다. 중복 role이 있거나 누락되면 neutral fallback을 사용한다.
- `DefenderSelector`가 기존 `DefenderPanel` 내부에 raycast를 막지 않는 9-slice/procedural 배킹을 최하단 sibling으로 만든다.
- 슬롯 container는 배킹의 padding 안을 채우며 기존 7슬롯 드래그 바인딩을 유지한다.
- Config가 누락되면 현행 치수/색으로 안전하게 폴백하되 경고는 1회만 남긴다.

## 완료 기준

- [ ] Config 하나로 Placement/Battle/Hand 외곽 치수를 Inspector에서 조정 가능.
- [ ] 7슬롯이 공통 plate 안에 정렬되고 portrait raycast/드래그가 유지됨.
- [ ] 배킹이 맵 위 UI 레이어를 분리하지만 전장을 과도하게 불투명하게 가리지 않음.
- [ ] config null/role 누락에서 NRE 없이 fallback.
- [ ] 컴파일 클린, BattleScene 배선 저장, 콘솔 에러 0.
