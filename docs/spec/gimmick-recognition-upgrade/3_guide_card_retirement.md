# 3 — 배치 안내 카드 은퇴

## 목적

리빌(unit 1)이 안내 역할을 가져갔으므로 배치 페이즈의 확장 카드·칩은 중복이다. 걷어내 배치 화면을 원래대로 깨끗하게 되돌린다(계약 6: 리빌 후 흔적 0).

**이 유닛이 별도인 이유**: 삭제를 신설과 같은 커밋에 섞으면 롤백이 안 된다. 리빌을 실기로 보고 판단한 뒤 이 커밋을 진행한다. 리빌이 기대에 못 미치면 이 유닛을 **하지 않거나** 되돌려 기존 카드를 되살린다(계약 10).

## 선행 조건

unit 1 리빌을 실기에서 확인하고 **사용자가 카드 은퇴를 승인**한 뒤에 착수한다. 순서를 뒤집으면 안내가 잠시 사라진 상태로 남는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/GimmickGuideView.cs` — 삭제
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — `gimmickGuide` 필드 + `BindPlacementActivity` 호출 제거
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — `gimmickGuide` 필드 + `SetTutorialSuppressed` 호출 2곳 제거
- `Assets/_Project/Scenes/BattleScene.unity` — `GimmickGuideView` GO + 배선 제거

## 구현

`GimmickGuideView` 를 통째로 삭제한다. 리빌이 그 역할을 전부 흡수했고, 부분만 남기면 소비처 없는 코드가 된다.

**건드리면 안 되는 것** — `DefenderDragPlacementController` 의 `DragBegan` / `Armed` **이벤트 선언과 invoke 는 유지**한다. 이 이벤트는 first-session-tutorial 과 **공용**이라 제거하면 튜토리얼이 깨진다. 없어지는 건 `GimmickGuideView` 의 **구독**뿐이다.

`FirstSessionTutorialController` 의 첫 판 억제는 리빌 쪽으로 이미 옮겨갔다(unit 1 스킵 조건 2 — `TutorialProgress.ShouldRunCore`). 여기서 지우는 건 죽은 호출이다. 튜토리얼의 다른 억제 대상(`placementView.BeginTutorialGate` 등)은 손대지 않는다.

씬에서 GO 를 지울 때 **다른 컴포넌트의 참조가 남지 않게** 확인한다. 실제 참조는 `DefenderSelector` / `FirstSessionTutorialController` 두 곳이 전부다. 그 외 `GameManager.cs:60`(AssignedGimmick 필드 주석)과 `UI/Outgame/LobbyNeonCta.cs:126`(fontSharedMaterial 선례로 인용)은 **주석 언급**이라 코드 동작과 무관 — GameManager 쪽은 문구를 갱신하고, LobbyNeonCta 는 선례 인용이라 그대로 둔다.

## 완료 기준

- 컴파일 에러 0, EditMode 회귀 없음.
- `rg "GimmickGuideView" Assets/_Project/Scripts` 결과가 `LobbyNeonCta.cs` 주석 1건만 남는다(문서 제외).
- Play: 배치 페이즈에 기믹 카드·칩이 뜨지 않는다. 리빌은 정상.
- 배치 드래그·arm 이 정상 동작한다(`DragBegan`/`Armed` 유지 확인).
- 첫 세션 튜토리얼이 정상 진행된다(억제 호출 제거가 튜토리얼을 깨지 않았는지 — 배치 게이트·안내 문구 확인).
- 씬에 dangling 참조 경고가 없다.
- **이 커밋 단독 revert 시** 기존 배치 카드가 리빌과 공존 상태로 되살아난다.

## 2026-08-01 착지

사용자가 리빌을 실기 확인하고 카드 은퇴를 승인해 진행했다.

- `GimmickGuideView.cs`(+meta) 삭제. `DefenderSelector` 의 필드·`BindPlacementActivity` 호출, `FirstSessionTutorialController` 의 필드·`SetTutorialSuppressed` 2곳 제거. 씬에서 GO + 참조 2건 제거(54줄 삭제, insertions 0).
- `DefenderDragPlacementController` 의 `DragBegan`/`Armed` **선언·invoke 는 유지**했다 — first-session-tutorial 과 공용이라 지우면 튜토리얼이 깨진다. 없어진 건 구독뿐.
- 첫 판 억제는 리빌이 `TutorialProgress.ShouldRunCore` 로 스스로 판정하므로(unit 1 스킵 조건 2) 튜토리얼 쪽 호출은 죽은 코드였다.
- 잔존 참조는 `LobbyNeonCta.cs:126` 주석 1건뿐(선례 인용이라 타 스펙 파일을 건드리지 않고 남김).

**검증**: 컴파일 에러 0, 씬 missing-script 슬롯 0, Play 스모크에서 `GameManager`/`GimmickPhaseView`/`DefenderSelector`/`FirstSessionTutorialController` 전부 생존하고 리빌 정상 동작.

⚠ **미검증**: 첫 세션 튜토리얼 실제 진행. 신규 프로필이 필요해 이 세션에서 못 돌렸다 — 제거한 게 죽은 호출뿐이라 위험은 낮지만, 첫 판 플로우는 사용자 확인이 필요하다.

⚠ **Play 모드 중 씬 편집 주의**: `Undo.DestroyObjectImmediate` 는 Play 중에도 "성공"하지만 런타임 씬에만 적용되고 정지 시 사라진다(`MarkSceneDirty` 가 던지는 예외로만 알아챘다). 씬 편집 전 `Application.isPlaying` 을 반드시 확인할 것.

## 완료 확인

**2026-08-01** — 커밋 `921829a4`. **사용자가 리빌 실기 확인 후 카드 은퇴를 명시 승인**("기존배치카드는 제거해")해 진행했다.

이후 원격 머지에서 같은 자리가 충돌했다 — 원격이 guidance API 를 `SetWorldMarkerLayout` → `SetMessageAnchor` 로 개명했고 내 쪽은 `gimmickGuide` 호출을 지웠다. 양쪽을 합쳐 해소(`8b25fdbb`). 머지 후 EditMode 1777 재실행에서 이 변경에 기인한 실패는 없다.

**미확인**: 첫 세션 튜토리얼 실제 진행(신규 프로필 필요). 배치 페이즈에 카드가 안 뜨는지도 사용자 재확인 대기 — 은퇴 후 Play 는 스모크만 돌렸다.
