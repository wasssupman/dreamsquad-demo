# 첫 실행 튜토리얼 B5 생존·시간 안내

## 목적

드림캐쳐 부착 안내가 끝난 뒤 플레이어가 남은 60초 경기의 목표를 놓치지 않도록,
좌상단 TIME 배지를 가리키며 다음 문구를 보여준다.

> 유닛 배치, 드림캐쳐등 기능을 활용하여 튜토리얼 1분을 버텨보세요!

이 단계는 새 행동을 강제하는 구간이 아니라, 배운 기능을 자유롭게 조합하는 구간으로
넘어갔음을 알리는 전환이다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs`
- `Assets/_Project/Scripts/UI/ScoreHudView.cs`
- `Assets/_Project/Scripts/Data/FirstRunTutorialConfig.cs`
- `Assets/_Project/Data/Config/FirstRunTutorialConfig.asset`
- `Assets/_Project/Scenes/BattleScene.unity`
- `Assets/_Project/Tests/EditMode/TutorialDragGuidanceTests.cs`

## 구현 계약

1. B5는 B4가 실제로 완료된 뒤에만 시작한다.
2. B5 진입 시 Battle 정지 lease와 튜토리얼 차단막을 먼저 해제한다.
3. 문구·포커스 링·포인터만 표시하며 이들은 raycast를 받지 않는다.
4. 포커스 대상은 `ScoreHudView`가 소유한 실제 TIME 배지다. 튜토리얼이 배지를 복제하거나
   자식 이름으로 탐색하지 않는다.
5. 노출 시간은 `FirstRunTutorialConfig.survivalHintSeconds`가 소유한다.
6. TIME 배지가 없거나 비활성이면 경고 후 B5를 미완료로 남긴다. 온보딩 완료 플래그도
   기록하지 않아 잘못된 씬 배선을 조용히 소비하지 않는다.
7. 문구 노출이 끝나면 포커스와 문구만 정리한다. 전투는 그동안에도 계속 진행된다.

## 완료 기준

- B4 직후 요청 문구가 표시되고 좌상단 TIME 배지에 포커스 링이 생긴다.
- 문구가 떠 있는 동안 유닛 배치와 드림캐쳐 입력이 가능하며 전투 시간이 흐른다.
- 문구 종료 후 포커스가 제거되고 기존 자유 플레이가 이어진다.
- B3·B4·B5가 모두 완료된 경우에만 `firstRunTutorialDone`을 기록한다.
- 관련 EditMode 회귀 테스트와 Unity 스크립트 컴파일이 통과한다.
