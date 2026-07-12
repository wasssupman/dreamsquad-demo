# 0 — Tray Config와 외곽 셸

## 목적

유닛 스트립·코스트 레일·드림캐쳐 핸드가 같은 치수/색 계약을 소비할 데이터 원천을 만들고, 투명 슬롯 나열 뒤에 명확한 Action Tray 배킹을 추가한다. 선행: `mobile-ui-safe-area` 완료.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs`
- 신규 `Assets/_Project/Data/Config/BattleHudTrayConfig.asset`
- 신규 `Assets/_Project/Art/UI/BattleHudTrayPlate_v2.png`
- `Assets/_Project/Scripts/UI/DefenderSelector.cs`
- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

- Config에 safe edge를 제외한 트레이 전용 값만 둔다: anchored y, placement/battle/hand size, padding, slot spacing, energy rail size/overlap, name font 범위, tray/name/dim colors.
- `DefenderClass`별 presentation entry는 role, 짧은 glyph, 색을 직렬화한다. 중복 role이 있거나 누락되면 neutral fallback을 사용한다.
- `DefenderSelector`가 기존 `DefenderPanel` 내부에 raycast를 막지 않는 9-slice 배킹을 최하단 sibling으로 만든다.
- ~~프로덕션 배킹은 `BattleHudTrayPlate_v2.png`~~ → **계약 변경 (2026-07-12 시각 검증)**: 프로덕션 배킹 = **procedural 라운드 플레이트**(`UiRoundedSprite`, `trayFrame=null` 폴백 경로가 곧 프로덕션). v2 아트는 코너 장식(~150px)이 슬라이스 border(64px)를 넘어 "슬롯 틈의 골드 밧줄"로 왜곡 — uniform 축소+border 재조정(v3 실험)으로도 슬롯이 프레임을 가리는 구조 문제 잔존해 기각. 트레이 fill/border 색은 Config 소유(A안: 네이비 0.96 + 골드). v2 PNG 는 대형 패널용 후보로 잔존.
- 슬롯 container는 배킹의 padding 안을 채우며 기존 7슬롯 드래그 바인딩을 유지한다.
- Config가 누락되면 현행 치수/색으로 안전하게 폴백하되 경고는 1회만 남긴다.

## 완료 기준

- [x] Config 하나로 Placement/Battle/Hand 외곽 치수를 Inspector에서 조정 가능.
- [x] 7슬롯이 공통 plate 안에 정렬되고 portrait raycast/드래그가 유지됨.
- [x] 배킹이 맵 위 UI 레이어를 분리하지만 전장을 과도하게 불투명하게 가리지 않음.
- [x] config null에서 NRE 없이 fallback. (role 누락 fallback 은 role entry 가 unit 1 변경 대상이라 unit 1 에서 검증)
- [x] 컴파일 클린, BattleScene 배선 저장, 콘솔 에러 0.

확인 2026-07-12 — Placement(136)/Battle(104) 캡처 검증 + 프로그램 배치 3기 정상. v2 아트 슬라이스 왜곡(사용자 지적) → A/B/C 변형 캡처 비교 후 **procedural A안 채택**(사용자 확정, 위 계약 변경). 콘솔 에러/경고 0. 코드 커밋 `1d467a3b`(Codex) + 배킹 확정 커밋은 본 기록과 동반.
