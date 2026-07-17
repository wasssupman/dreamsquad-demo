# 5 — Handoff Summary

## Commit

- `780810a1 feat: unify unit overhead health UI`
- Legacy 체력/드림캐쳐 경로는 삭제하지 않고 `UnitHealthPresentationMode`로 보존한다.

## Implemented

- 방어/적 공용 `UnitOverheadView`와 ScreenSpaceOverlay Layer.
- BattleScene 기본 모드는 `UnifiedOverhead`.
- 방어는 네이비 프레임·청록 캡슐, 적은 와인 프레임·코랄 절단형 bar.
- drop shadow/frame/track/fill/highlight 5층 구조와 피해 잔상.
- renderer top Y + visual pivot X 조합으로 머리 중앙 정렬.
- 1920×1080 height-match 기준 머리 위 5px, 카드 행 추가 5px.
- 방어유닛 드림캐쳐 최대 3장, 높이 28.8px·간격 4px·타일 폭 자동 축소.
- Layer 단위 절차 Sprite 공유 및 카드 프레임 지연 생성.
- Legacy 전환 시 EnemyHitBar/TileHealthGauge/DcIconStrip 중복 표시 방지.
- entity despawn, battle teardown, placement 재진입 때 View 회수.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Data/UnitOverheadUiStyle.cs`
- `Assets/_Project/Data/Config/UnitOverheadUiStyle.asset`
- `Assets/_Project/Scripts/Presentation/UnitOverheadUiLayer.cs`
- `Assets/_Project/Scripts/Presentation/UnitOverheadView.cs`
- `Assets/_Project/Scripts/Presentation/UnitOverheadSpriteSet.cs`
- `Assets/_Project/Scripts/Presentation/UnitOverheadLayout.cs`
- `Assets/_Project/Scenes/BattleScene.unity`

## Verified

- Unity 6000.4.3f1 domain reload 및 C# compile error 0.
- `git diff --check` 통과.
- 코드 리뷰 Track A 지적(알파 범위, Sprite 중복, HUD order, 테스트 공백) 반영.
- ECS review: BattleBridge read-only Health gateway 유지, 신규 ECS state/system/channel 없음.
- 실제 BattleScene 캡처를 기반으로 bar 크기·테두리·중앙 정렬 피드백 반영.

## Notes

- `ui_mockup.png`보다 실제 플레이 캡처를 우선한다.
- `SpineUnitView.TryGetScreenRect`는 기존 커밋 심볼이며 Quad에도 같은 계약을 추가했다.
- 가로 중심을 renderer bounds center로 되돌리면 무기·방패 방향에 따라 bar가 밀린다.
- 만피 alpha는 HealthBar에만 적용한다. 카드 루트에 CanvasGroup을 두지 않는다.
- Overhead Canvas order 3은 손패 5·점수 6보다 아래다.
- StatusFx는 이번 기능에서 통합하지 않았다.

## Follow-up

- EditMode/PlayMode Test Runner 실제 실행.
- Android 실기기에서 1920×1080 reference scaling과 safe-area 가장자리 확인.
- 최종 카드 1/2/3장 부착 캡처와 상태 아이콘 동시 표시 충돌 확인.
