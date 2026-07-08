# 5 — Handoff Summary

## Commit

- `c423be4c` feat(defender-portraits): DefenderUnitData.portrait 필드 + 클래스 포트레이트 배정 (unit 0,1)
- `95e1099b` feat(defender-portraits): 스쿼드/인게임 선택 UI 포트레이트 표시 + 크기·레이아웃 조정 (unit 2,3,4)
- 문서 커밋은 이 handoff 포함해 별도.

## Implemented

- `DefenderUnitData.portrait` (Sprite) 추가 — nullable, 순수 프레젠테이션(ECS 미참조).
- DefenderPortraits 아트(bishoujo/modern) 리포 편입, 사용 16장 Sprite 재import.
- 16개 방어 SO 에 클래스별 포트레이트 배정(매핑 키 = 유닛 id, bruiser→fighter).
- SquadBuilderView: 편성 슬롯 + 유닛 피커에 포트레이트, 슬롯 165/피커 셀 225,
  SELECT UNIT 그리드를 타이틀 24px 아래로.
- DefenderSelector: 배치 스트립 포트레이트, 패널 20% 확대(912x120), 상시 테두리/딤
  제거 + 선택 슬롯만 골드 프레임.
- DreamcatcherDeckBuilderView: MY DECK 프레임 하향(-196)으로 타이틀 가림 해소, 컬렉션
  높이 430→400.
- OutgameScene: SquadPanel/SlotsRow 1000x140 → 1260x190 (확대 슬롯 수용, 씬 저장됨).

## Key Files

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — portrait 필드
- `Assets/_Project/Data/Defenders/Defender_*.asset` — 배정된 포트레이트 참조
- `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs` — 슬롯/피커 포트레이트·크기
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 배치 스트립 포트레이트·선택 프레임
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs` — 타이틀 가림 수정
- `Assets/_Project/Scenes/OutgameScene.unity` — SlotsRow 확대

## Verified

- 컴파일/콘솔 클린(각 변경 후 refresh + read_console error 0).
- 육안 확인은 사용자가 Play 스크린샷으로 진행(스쿼드/SELECT UNIT/배치 스트립/드림캐쳐).
- 주의: ScreenSpaceOverlay UI 는 `manage_camera screenshot`(game_view)에 안 잡히고
  Play 중 MCP GameObject 조작 불가 → 에이전트 자동 시각검증 불가, 사용자 육안 확인 필요.

## Notes

- 매핑 키는 유닛 **id** (role enum 6값은 16 포트레이트와 1:1 아님). 되돌리지 말 것.
- portrait == null 이면 UI 는 기존 텍스트/단색 폴백. 계약 유지.
- DefenderSelector 선택 표시는 골드 프레임(선택 슬롯만). 상시 테두리/딤은 의도적으로 제거.
- 스쿼드 상단 편성 슬롯 크기는 SlotsRow(씬) 폭에 종속 — 더 키우려면 씬 크기도 함께.

## Follow-up

- 포트레이트 `_test_01` 접미사/최종 네이밍 정리, 루트 ranger 중복본 정리.
- 미사용 스타일 16장 활용(스킨 토글 등), AttackUnitData(적)·ResultScreen 확장.
- 선택 프레임 완전 제거 옵션(사용자 요청 시) 및 편성 슬롯 추가 확대(씬 폭 확장).
