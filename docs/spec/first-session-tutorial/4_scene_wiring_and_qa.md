# 4 — BattleScene 배선과 모바일 QA

## 목적

첫 세션 컨트롤러를 BattleScene에 영속 배선하고, 신규·완료·Skip·중단 경로와 모바일 가로 비율을 실제
플레이로 검증한다.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity`
- `Assets/_Project/Tests/PlayMode/FirstSessionTutorialSmokeTest.cs` (신규)
- 필요 시 tutorial style asset의 시각 노브만 조정

## 구현

BattleScene에 단일 `FirstSessionTutorial` GameObject를 만들고 아래 실제 소유자를 직렬화한다.

- `PlayerProfileSO`, `GameManager`, `PlacementPhaseView`
- `DefenderSelector`, `TilemapMapView`, Main Camera
- `DreamcatcherHandController`, `AwakeningGaugeView`, `DreamcatcherHandView`
- `GimmickGuideView`
- `TutorialGuidanceStyle_Default`

씬 이름/전역 Find를 런타임 정상 경로로 사용하지 않는다. 런타임 생성되는 drag controller와 slot은
각 소유자의 공개 read-only seam으로 받는다. 참조가 빠지면 오류를 남기고 **hold를 걸지 않은 채** 기존
게임을 진행한다.

씬에는 이미 사용자 WIP가 있을 수 있으므로 저장 전 `git diff`를 확인하고, 튜토리얼 배선 delta만
격리한다. UI 캡처는 `ScreenCapture`로 1920×1080과 2400×1080을 남겨 safe area와 대상 가림을 확인한다.

## 완료 기준

- [ ] PlayMode smoke: pending 프로필 → Placement hold → 배치 성공 → Start → 완료 저장.
- [ ] PlayMode smoke: 완료 프로필 → 튜토리얼 UI/hold 없이 기존 Placement.
- [ ] PlayMode smoke: Skip → 상태 원복 + 완료 저장.
- [x] PlayMode smoke: direct BattleScene Play / profile not loaded → 안내·파일 저장 없음.
- [x] PlayMode smoke: affordable 슬롯 없음 → hold 없이 정상 Placement.
- [ ] PlayMode smoke: usable 카드 발생 → 각성 버튼 힌트 → 손패 open 완료 저장.
- [ ] PlayMode smoke: 각성 버튼 미입력 → 3~4초 자동 숨김 + 같은 판 재노출 없음.
- [ ] 16:9/20:9에서 말풍선·Skip·펄스가 safe area 안이며 Action Tray/Start/각성 버튼을 가리지 않는다.
- [ ] Gift 연출, 배치 reject, 전투 시작, 손패 슬로모/드래그에 회귀가 없다.
- [x] 콘솔 오류 0, 관련 EditMode/PlayMode 테스트 green.
- [ ] Android 실기기에서 탭→탭 배치, Skip, 각성 버튼 터치 1회 확인.
- [ ] 사용자 Play 확인 전 feature 완료 처리/커밋하지 않는다.
