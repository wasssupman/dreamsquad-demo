# 2 — 스쿼드 편성 UI

## 목적

A의 SquadPanel placeholder 를 실제 편성 화면으로 채운다. 보유 유닛을 7슬롯에 배정·저장·선택.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs`
- 수정 `Assets/_Project/Scenes/OutgameScene.unity` — SquadPanel 내용 구성 (UnityMCP)
- (변경 없음) OutgameMenuController — `SquadBuilderView.OnEnable` 이 패널 활성 시 Refresh 하므로 별도 호출 불필요

## 구현

`SquadBuilderView` (MonoBehaviour, 씬 로컬):
- 참조: `DefenderCatalog catalog`, `PlayerProfileSO profileSO`.
- 상단: **7 슬롯** 행(빈 슬롯 = "＋", 채워진 슬롯 = 유닛명/아이콘 + 탭하면 해제).
- 하단: **보유 유닛 그리드**(`profile.ownedUnitIds` → catalog.ById). 유닛 탭 → 첫 빈 슬롯에 배정(이미 슬롯에 있으면 무시 또는 토글).
- 버튼: **저장**(→ `ProfileStore.Save`), 선택 스쿼드 표시. (스쿼드 1개뿐이므로 선택 UI 는 최소; 다중 스쿼드는 후속.)
- 변경은 메모리(`profileSO.profile.SelectedSquad().unitIds`)에 반영 후 저장 시 디스크 flush.
- 아이콘 리소스 없으면 유닛 `displayName` 텍스트로 표시(아이콘은 후속).

UnityMCP 로 SquadPanel 하위에 슬롯 7개 + 그리드 컨테이너 + 저장 버튼 생성, `SquadBuilderView` 부착·참조 wiring. 라벨 영문(한글 폰트 후속).

## 완료 기준

- SquadPanel 열기 → 7슬롯 + 보유 유닛 그리드 표시, 콘솔 에러 0.
- 유닛 탭 → 슬롯 배정, 슬롯 탭 → 해제 (Play 시각 확인).
- 저장 후 Play 재시작 → 배정 유지(profile.json 반영 확인).
- `profileSO.profile.SelectedSquad().unitIds` 가 UI 상태와 일치.

> 완료 확인 2026-06-02 — Play 검증: 7슬롯+15보유버튼 빌드, 보유 탭→배정(filled 3), 슬롯 탭→해제(filled 2), 저장 후 디스크 재로드 filled=2 유지. 에러 0.
> 구현 메모: SquadBuilderView 가 슬롯/그리드 버튼을 런타임 생성(컨테이너 2개 + statusText/save 만 씬 wiring). OnEnable 빌드는 동기.
