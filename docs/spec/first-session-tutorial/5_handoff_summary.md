# 5 — Handoff Summary — first-session-tutorial

상세 계약은 README와 unit 0~4 문서가 우선한다. 구현·자동 검증과 Editor 사용자 Play 조정은 완료했다.
Android 가로 실기기 터치 QA는 후속 확인으로 남긴다.

## Commit

- `da398417` — `feat(tutorial): 첫 세션 행동형 온보딩 추가`

## 구현 결과

- 첫 정상 로비 로드에서만 실행되는 버전형 진행 상태를 추가했다. 직접 BattleScene Play는
  `IsLoadedThisSession` 가드로 안내와 프로필 저장을 모두 막는다.
- Placement 초기화가 끝난 뒤 안내를 시작한다. 첫 배치 전에는 카운트다운을 `배치 연습`으로 hold하고
  Start를 숨긴다.
- 화면에는 한 번에 짧은 문장 하나와 다음 대상 하나만 강조한다. 순서는 목표 beat → 추천 유닛 →
  기존 배치 가능 타일 → Start이며 별도 `다음` 버튼은 없다. 추천 단계에서 탭과 D&D를 함께 알리고,
  실제 입력을 관찰해 탭이면 `타일을 탭`, 드래그면 `타일에 놓기` 문구로 분기한다.
- 목표 beat는 기본 5초로, spawn만 2.5초 보여준 뒤 spawn과 goal을 함께 2.5초 읽게 한다. 마커는 첫 배치
  조작까지 유지되며 바닥 셀 중심이 아니라 실제 생성된 구조물 renderer 중심을 따라간다. 구조물 없는
  테마에서만 셀 중심으로 폴백한다.
- 탭/드래그의 공용 배치 성공 신호를 관찰하고, 방향 유닛은 조준 종료까지 Start를 열지 않는다.
- 핵심 안내 중 기믹 카드는 숨기고 종료·Skip·이탈에서 복구한다. 필수 참조 또는 affordable 슬롯이 없으면
  hold를 걸지 않고 기존 게임으로 fail-open한다.
- Battle 중 실제로 쓸 수 있는 드림캐쳐 카드가 생긴 경우에만 각성 버튼을 판당 1회 알린다. 손패가 실제로
  열린 뒤 첫 usable 카드 한 곳만 가리키고 완료 저장한다.

## 주요 파일

- 진행 상태: `Core/Profile/PlayerProfile.cs`, `PlayerProfileSO.cs`, `TutorialProgress.cs`
- 반복 QA: `Editor/FirstSessionTutorialMenu.cs` — `Wassup > Tutorial > Reset First Session Tutorial`
- 공통 안내: `UI/Tutorial/TutorialGuidanceStyle.cs`, `TutorialGuidanceView.cs`
- 오케스트레이션: `UI/Tutorial/FirstSessionTutorialController.cs`
- 월드 표시 앵커: `Core/TilemapMapView.cs`
- 성공 신호/게이트: `PlacementPhaseView.cs`, `DefenderSelector.cs`,
  `DefenderDragPlacementController.cs`, `AwakeningGaugeView.cs`, `DreamcatcherHandView.cs`
- 씬/스타일: `Scenes/BattleScene.unity`, `Data/Config/TutorialGuidanceStyle_Default.asset`

## 검증 완료

- Unity compile clean, 런타임 콘솔 error 0.
- EditMode `TutorialProgressTests` + `TutorialDragGuidanceTests`: **11/11 pass** — 신규/레거시 JSON,
  독립 완료, 버전, null, 세션 로드 가드, round-trip, 탭 비행의 선택 취소 오인 방지.
- EditMode `TilemapMapViewTests`: **4/4 pass** — 셀 좌표 정합, 재초기화, 구조물 renderer 중심 앵커와
  구조물 미사용/clear 폴백.
- PlayMode `FirstSessionTutorialSmokeTest`: **4/4 pass** — PlacementReady 이후 hold, 카운트다운 정지,
  첫 배치 전 Start 숨김, world marker 지속/정리, Skip 복구와 완료 저장.
- 정상 Outgame 프로필 로드 후 Battle 진입: core pending, Pick 단계 활성, `배치 연습`, Start 숨김,
  추천 슬롯 1곳 강조를 1920×1080에서 확인했다. 상태 신호 스모크로 Place 문구와 Start 단계 전환도 확인했다.
- 추천 슬롯의 실제 물리 drag 세션을 시작해 두 가지 배치 방법 안내 →
  `하늘색으로 빛나는 곳에 D&D 해보세요!` 전환, 배치 하이라이트 유지, console error 0을 1920×1080에서 확인했다.
- 검증 중 Start/Skip은 누르지 않았고 기존 `profile.json`에는 신규 버전 필드가 추가되지 않아 디스크 저장이
  발생하지 않았음을 확인했다.
- BattleScene 직접 Play: `loaded=false`, `ShouldRunCore=false`, overlay/hold 비활성, console error 0.
- 사용자 Play 피드백으로 Step 1 구조물 중심 앵커와 기본 5초 템포까지 조정한 뒤 2026-07-19 마무리 승인.

## 후속 확인

- Android 가로 실기기에서 실제 탭→탭 배치, 드래그 배치, 방향 유닛 조준 후 Start 노출.
- Skip 시 즉시 일반 Placement 복구 및 다음 판 미노출, 실제 Start 시 핵심 완료 저장.
- 각성 비용 충족 → 버튼 안내 3~4초 → 손패 open → usable 카드 안내와 다음 판 미노출.
- 20:9 safe area, 노치/컷아웃, 하단 Action Tray 및 각성 버튼과의 겹침. 에디터 1920×1080 캡처는 통과했으나
  `Screen.SetResolution` 기반 20:9 에디터 캡처는 GameView 도킹 크기로 축소되어 판정 자료로 쓰지 않았다.
- 실제 Battle 카메라에서 `적 등장`/`방어 목표` 링이 빨간/노란 구조물의 보이는 중심과 일치하는지 최종 눈검증.

반복 확인은 로비 개발 버튼 `RESET TUTORIAL`을 누르면 재질문 없이 즉시 가능하다. 두 버전만 초기화하고 원본
프로필 백업을 남긴다. Editor에서는 `Wassup > Tutorial > Reset First Session Tutorial`도 쓸 수 있다. 이후
OutgameScene에서 Play해야 하며 BattleScene 직접 Play는 안내가 꺼진다.

## 작업 트리 주의

- 작업 트리는 다른 기능 WIP와 함께 dirty다. `BattleScene.unity`의 기존 `mapDocument` 변경과
  `GimmickGuideView` 접힘 작업을 되돌리지 말 것.
- 구현 커밋 `da398417`에는 튜토리얼 관련 hunk만 선별했다. 씬의 카메라·맵·기타 UI WIP는 제외했다.
