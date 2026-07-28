# 2 — UI 소비처 smoke + 기존 AI 포트레이트 제거

## 목적

새 투명 상체 Sprite가 실제 최소 크기와 상태 오버레이에서 읽히는지 확인하고,
승인된 cut-over 뒤 기존 AI 포트레이트 38장과 stale 문서 포인터를 정리한다.

## 변경 대상

- 삭제: `Assets/_Project/Art/DefenderPortraits/{bishoujo,modern,generated}/`
- 삭제: `Assets/_Project/Art/DefenderPortraits/defender_portrait_ranger_test_01.png`
- 갱신: `docs/spec/defender-portraits/README.md` — 후속 대체 포인터
- 갱신: `docs/spec/ingame-ui-upgrade/README.md`
- 갱신: `docs/spec/ingame-ui-upgrade/1_ui-asset-brief.md`
- 검증 대상:
  - `SquadBuilderView`, `SquadRosterBrowser`, `SquadHeaderStrip`, `PresetUnitCell`
  - `DefenderSelector`, `DreamcatcherFocusPresenter`

## 구현

1. unit 1 사용자 승인 후 OutgameScene Play에서 다음을 확인한다.
   - 스쿼드 상단 슬롯과 유닛 피커
   - 캐릭터 페이지 로스터와 92px 헤더 슬롯
   - 프리셋 유닛 셀
2. BattleScene Play에서 7슬롯 배치 트레이를 확인한다.
   구매 가능/불가 dim, 비용 chip, 이름 밴드, 배치 쿨다운 액체/숫자가 투명 컷아웃 위에서
   모두 읽혀야 한다. 드림캐쳐 방어유닛 포커스 아이콘도 같은 캐릭터로 보여야 한다.
3. 문제가 포트레이트 crop/여백이면 UI별 임시 보정을 넣지 않고 unit 0 profile을 고쳐
   재베이크한다. 공통 UI 배경 자체가 없어서 읽히지 않는 구조 문제라면 이 unit을 임의로
   넓히지 말고 새 번호 문서와 사용자 승인을 먼저 받는다.
4. 전 소비처 통과 뒤 기존 AI PNG와 `.meta`, 비게 된 하위 폴더 `.meta`를 삭제한다.
   새 `spine/` 출력과 루트 `DefenderPortraits.meta`는 유지한다.
5. 과거 spec은 구현 이력으로 보존하되 README 상단에
   `defender-spine-portraits`가 현재 아트 source임을 명시한다. UI asset brief의
   bishoujo/modern contact sheet 참조는 새 spine 출력/contact sheet 포인터로 교체한다.
6. 삭제된 경로와 GUID를 `rg`로 전수 검색해 SO·씬·프리팹·문서의 잔존 참조가 0인지 확인한다.

## 완료 기준

- 위 Outgame/Battle 소비처 전부에서 캐릭터 식별, aspect, 여백, 라벨/오버레이가 정상이다.
- 가장 작은 92px 헤더 슬롯과 실기기 배틀 트레이에서도 얼굴/헤어 또는 헬멧이 읽힌다.
- 기존 AI 포트레이트 38장과 `.meta`가 삭제되고 참조 잔존 0.
- 새 Spine 포트레이트 수가 유효 DefenderCatalog 수와 일치하며 전 SO가 새 Sprite를 참조한다.
- Unity 컴파일/Console error 0, Outgame→Battle 진입 smoke 통과.
- 사용자 확인: 실제 UI 크기와 기존 AI 원본 삭제 범위 통과.

- 확인 2026-07-28: Editor UI smoke·38장 삭제·GUID 참조 0 통과
  (`5c4a3bdb`, 사용자 “모든 유닛 진행” 승인). Android/iOS 실기기 QA는 대기.
