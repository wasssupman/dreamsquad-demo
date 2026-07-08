# 3 — 팝업에 공격패턴 스트립 통합 + "!" 토글 제거

## 목적

메뉴 팝업이 열릴 때 기존 `WavePatternStripView`(incoming waves = 공격 패턴)를 화면 중상단에 노출한다. 배틀 중 온디맨드 "!" `WaveToggle` 은 제거하고, 공격패턴 열람 창구를 메뉴 팝업으로 일원화한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/MenuPopup.cs` — Open/Close 에서 스트립 구동
- `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs` — "!" 토글 제거
- `Assets/_Project/Scripts/UI/Outgame/SquadPrepView.cs` — `SetToggleEnabled(true)` 호출 정리
- `BattleScene.unity` — MenuPopup 에 WavePatternStripView 참조 주입

## 구현

### MenuPopup ↔ 스트립 연동
- MenuPopup 에 SerializeField `WavePatternStripView wavePatternStrip` 추가(씬 주입).
- `Open()`: 백드롭/버튼 활성 후 `wavePatternStrip.FadeIn()` 호출. (스트립은 `SquadPrepView` 에서 이미 `RebuildFromDeck()+SnapHidden()` 로 준비돼 있음 — 배틀 진입 시 최신 deck 반영 상태. 필요 시 `RebuildFromDeck()` 재호출 여부는 구현자 판단: deck 이 배틀 중 불변이면 생략.)
- `Close()`(재개): `wavePatternStrip.Roll()` 로 퇴장(자동 `SnapHidden()` 안착).
- **레이아웃**: 스트립은 자체 Canvas 에서 중상단 렌더. 팝업 버튼([재개]/[나가기])은 스트립 아래 영역이라 공간상 겹치지 않음. 스트립 overlay(dim)와 팝업 백드롭 중복 dim 이 과하면 팝업 백드롭 알파를 낮추거나 스트립 overlay 에 위임(구현자 판단, Play 육안).

### "!" 토글 제거
- `WavePatternStripView.cs`:
  - `Build()` 내 `WaveToggle` GameObject 생성 블록(약 321~347) 제거.
  - `_toggleButton` 필드, `OnToggleClicked()` 핸들러 제거.
  - `SetToggleEnabled(bool)` 은 **호출부가 사라지면 제거**. `_toggleEnabled` 필드도 함께 정리. (단 `FadeIn()/Roll()/SnapHidden()/RebuildFromDeck()/Unroll()` public API 는 드래프트 모드/팝업이 계속 쓰므로 보존.)
- `SquadPrepView.cs`:
  - `wavePatternStrip.SetToggleEnabled(true)` 호출 제거. `SnapHidden()` 준비는 유지(팝업 FadeIn 전제).

### 회귀 주의
- **드래프트 모드**: draft 시작 시 `Unroll()` 자동 인트로는 이 spec 범위 밖 — 건드리지 않는다. draft 의 스트립 동작 회귀 없는지 확인.
- 스트립을 두 소비자(SquadPrep 준비 + MenuPopup)가 공유 — 상태 머신(`State`)이 FadeIn↔Roll 로만 오가므로 충돌 없음. 팝업 열림 중 draft 진입 같은 동시성은 실제 플로우상 발생 안 함.

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러 없음(미참조 심볼 없음).
- [ ] 배틀 중 좌상단/우상단 "!" 토글 버튼 미표시.
- [ ] 메뉴 팝업 오픈 → 중상단에 공격패턴 스트립 FadeIn, [재개] → 스트립 Roll 퇴장.
- [ ] 드래프트 모드 스트립(Unroll 인트로/스와이프) 회귀 없음.
- [ ] 스쿼드 준비 → 배틀 진입 플로우 정상(deck 반영된 스트립).
