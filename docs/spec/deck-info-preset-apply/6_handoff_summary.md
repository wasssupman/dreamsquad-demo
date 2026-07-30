# 6 — Handoff Summary

## Commit

이 feature 커밋 직전 작성. 완료 커밋 해시는 후속 문서 스탬프에서 반영한다.

## Implemented

- 히스토리 덱보기 팝업의 탭별 프리셋 적용 버튼 활성화
- 한 슬롯 `PresetApply` 예약 채널, 이름 중복 해소, 유닛·드림스톤·카드 필터
- 히스토리 패널 → 메뉴 컨트롤러 → 스쿼드/드림캐쳐 페이지 라우팅
- 새 빈 프리셋 즉시 생성 + 랭커 편성을 미저장 작업본으로 적용
- 저장·되돌리기 규율, 상한/미로드 차단, 제외 항목 개수 안내
- 실계정 외부 덱 조건을 명시한 PlayMode live E2E 테스트

## Key Files

- `Assets/_Project/Scripts/Core/Profile/PresetApply.cs`
- `Assets/_Project/Scripts/UI/Outgame/DeckInfoPopup.cs`
- `Assets/_Project/Scripts/UI/Outgame/TournamentHistoryPanel.cs`
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`
- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPageController.cs`
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPageController.cs`
- `Assets/_Project/Tests/EditMode/Profile/PresetApplyTests.cs`
- `Assets/_Project/Tests/EditMode/Profile/PresetApplyPickupTests.cs`
- `Assets/_Project/Tests/EditMode/HistoryPresetApplyRoutingTests.cs`
- `Assets/_Project/Tests/EditMode/DeckInfoPopupTests.cs`
- `Assets/_Project/Tests/PlayMode/DeckInfoPresetApplyLiveE2ETest.cs`

## Verified

- Unity 스크립트 컴파일: 오류 0
- 신규/관련 EditMode: 46 pass
- 전체 EditMode: 1,738 total · 1,736 pass · 2 existing ignored · 0 fail
- 전체 PlayMode: 86 total · 72 pass · 14 fail
  - 실패 14개는 이 spec 경로가 아니라 기존 서버 계정 중복, Tween error 로그, 드래그 좌표, 드림캐쳐 전투 효과, 캐리인 상태, 배치 오라, 씬 전환 테스트다.
- 신규 `DeckInfoPresetApplyLiveE2ETest`는 로그인·히스토리 조회까지 진입했다. 현재 계정에 적용 가능한 외부 참가자 덱이 없어 실제 적용 assertion은 미완료다.
- 신규 `SerializeField` 및 씬 수정 0. `docs/reference/object-pipeline-map.md` 갱신 불요 확인.

## Notes

- 처음 전체 EditMode 실행은 Windows가 unsigned Burst JIT DLL을 차단(error 4551)해 비예상 Error 로그 1건으로 실패했다. Editor의 `Jobs/Burst/Enable Compilation`을 로컬에서 꺼 재실행했고 전체 EditMode가 통과했다. 프로젝트 소스/Player 설정 변경은 없다.
- `PresetApplyPickupTests`는 EditMode에서 일반 `MonoBehaviour.OnEnable`이 자동 실행된다고 가정해 13개가 실패했다. 실제 `OnEnable`을 호출하는 `Enter` 헬퍼로 테스트 하네스를 고쳤고 46개가 모두 통과했다.
- live E2E는 프로필을 JSON 복제하고 두 페이지의 `ProfileSaver`를 no-op으로 바꿔 사용자 디스크 프로필을 쓰지 않는다.

## Follow-up

1. 외부 참가자 덱이 있는 실계정에서 `DeckInfoPresetApplyLiveE2ETest`를 명시 실행한다.
2. 성공 후 수동으로 `[저장]`, `[되돌리기]`, dirty 경고와 내 덱 버튼 숨김을 육안 확인한다.
3. 공유 작업 트리에서 위 Key Files와 본 spec 문서만 선별해 Lore 프로토콜로 커밋한다.
