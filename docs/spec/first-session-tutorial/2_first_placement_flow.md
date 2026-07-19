# 2 — 첫 배치와 전투 시작

## 목적

첫 BattleScene의 Placement에서 플레이어가 설명을 읽는 대신 실제로 유닛 한 명을 배치하고 전투를
시작하게 한다. 이것이 유일한 강제 튜토리얼이다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs` (신규)
- `Assets/_Project/Scripts/UI/DefenderSelector.cs`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs`
- `Assets/_Project/Scripts/UI/GimmickGuideView.cs`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs`

## 구현

`PlacementPhaseView`는 비용 초기화·`bridge.BeginPlacement`·패널 활성화가 모두 끝난 뒤 `PlacementReady`
이벤트를 발행한다. 컨트롤러는 이 신호와 현재 Placement 페이즈를 함께 확인하고
`TutorialProgress.ShouldRunCore`가 true일 때만 시작한다. 시작 시 배치 카운트다운 hold, Start 숨김,
`GimmickGuideView` suppress를 요청한다.

현재 비용으로 살 수 있는 슬롯이 하나도 없거나 필수 참조가 빠졌으면 튜토리얼을 시작하지 않고 hold를
걸지 않는다. 구매 불가능한 슬롯을 추천하는 폴백은 없다.

진행은 네 상태다.

1. **Goal**: `적이 노란색 베이스에 닿기 전에 막아주세요.` 기존 `TilemapMapView.VisualPlan`의 spawn들에
   `적 등장`, goal에 `방어 목표` 지속 마커를 순서대로 연다. 전체 beat는 4~6초(기본 5초)이며 전반부는
   spawn을, 후반부는 spawn과 goal을 함께 읽게 한다. 입력은 막지 않으며,
   마커는 Pick에서도 유지하다가 유닛 arm 또는 실제 D&D 시작 시 제거한다. 위치는 바닥 셀을 재계산하지 않고
   `TilemapMapView`가 실제 생성된 spawn/goal 구조물의 renderer 중심을 제공하며, 구조물이 없는 테마에서만 셀
   중심으로 폴백한다. 데이터가 없으면 문구만 보이고 계속한다.
2. **Pick**: `캐릭터를 배치하는 방법 두가지 방법!\n1. 캐릭터 터치! 원하는 위치에 터치!\n2. 캐릭터를 터치한 상태로 드래그! 원하는 위치에 드랍!` `DefenderSelector`가 현재 비용으로 살 수 있는 비방향 유닛 중 가장 저렴한
   슬롯 Rect를 추천한다. 비방향 affordable이 없으면 affordable 중 가장 저렴한 슬롯을 추천한다. 추천과
   무관하게 다른 affordable 슬롯의 탭·드래그도 그대로 허용한다.
3. **Place**: arm 상태가 되면 `하늘색으로 빛나는 곳을 터치해보세요!`, 실제 슬롯 드래그가 시작되면
   `하늘색으로 빛나는 곳에 D&D 해보세요!`로 교체한다. 탭 배치의 내부 simulated drag는 물리 D&D로 오인하지 않고,
   arm 취소 단계도 발생시키지 않는다. 기존 `ShowPlacementHighlight`가 유효 타일을 표시하며 튜토리얼은
   타일 판정을 복제하지 않는다.
4. **Start**: 공용 배치 commit 성공 후 방향 조준이 끝났다면 Start를 표시·활성화하고
   `좋습니다! 더 배치해보세요.\n준비되면 전투 시작!`으로 이동한다. 추가 배치는 허용하되 카운트다운은
   계속 hold한다. 실제 Start 탭으로 `GamePhase.Battle`에 진입한 것이 완료 신호다.

`DefenderDragPlacementController`에는 arm 변경, 실제 슬롯 D&D 시작, 성공 commit 읽기 이벤트만 추가한다. 탭과 드래그가
합류하는 `CommitPlacementAt`의 성공 분기에서 unit payload와 함께 1회 발화한다. 방향 유닛은
`DirectionAimController.Begin` 뒤 이벤트를 발행해 `IsAiming=true`를 먼저 관측할 수 있게 하며, false가 될
때까지 Start를 잠근다. 비용·스폰 권한은 계속 BattleBridge에 있다.

실제 Start 탭·Skip·OnDisable·Placement 이탈 모든 경로에서 countdown hold, Start 가시성,
GimmickGuide suppress를 반드시 원복한다. Skip은 핵심 안내를 완료 저장하고 즉시 기존 Placement로
돌려보내며 정상 30초 카운트다운과 Start를 복구한다.

## 완료 기준

- [ ] 신규 프로필 첫 Placement에서 Gift 종료 후에만 안내가 시작된다.
- [x] 카운트다운이 첫 배치 전 자동으로 줄지 않고 Start 버튼이 눌리지 않는다.
- [x] 핵심 안내 동안 카운트다운 초가 숨고 Start는 첫 배치 성공 전 조용히 숨는다.
- [ ] 탭→탭과 드래그 어느 쪽으로 배치해도 Start 상태로 진행한다.
- [ ] 무효 타일/비용 부족은 기존 reject 피드백만 내고 단계가 진행되지 않는다.
- [ ] 배치 후 Start 탭 → Battle 진입, 프로필 완료 저장, 안내 UI 제거.
- [ ] 배치 후 추가 유닛 배치가 가능하며 기다려도 자동으로 Battle에 진입하지 않는다.
- [x] affordable 슬롯이 없으면 안내/hold 없이 정상 Placement로 fail-open한다.
- [ ] Skip·컴포넌트 disable·씬 이탈에서 hold/버튼 상태가 원복된다.
- [ ] 완료 프로필과 profile null/direct Play에서는 기존 흐름이 픽셀·타이밍 회귀 없이 유지된다.
