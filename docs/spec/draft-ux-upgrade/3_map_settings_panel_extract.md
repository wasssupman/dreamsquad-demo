# 3. MAP SETTINGS 패널 추출

## 목적

`TimelineBriefingView` 안의 MAP SETTINGS UI(path shape / map size / obstacle density / spawn lanes) 를 별도 컴포넌트로 추출하여 좌상단 작은 토글로 이관한다. 추출이 끝나면 원본 `TimelineBriefingView.cs` + .meta + 씬 GameObject 도 본 task 의 완료 기준으로 함께 제거한다 (task 1 의 보존 윈도우 종료).

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs`
- (씬) DraftCanvas 또는 DraftView 의 자식 GameObject `MapSettingsPanel`
- 삭제: `Assets/_Project/Scripts/UI/TimelineBriefingView.cs` + `.meta`
- 삭제: 씬의 TimelineBriefing GameObject (있다면)

## 구현

1. 새 `MapSettingsPanelView` MonoBehaviour 생성. 상위 DraftView Canvas 자식 전제 — 자체 Canvas/Scaler/GraphicRaycaster 추가하지 않음.
2. UI 빌드는 기존 `TimelineBriefingView` 의 다음 메서드를 그대로 옮긴다 (코드 그대로 읽어 옮김 — task 1 이 원본 파일 보존했으므로 가능):
   - `BuildMapSettingsPanel`
   - `RefreshMapSettingsButtons`
   - `SetSelectedButton`
   - `AddRow`, `AddPanelLabel`, `CreateInput`, `CreateBriefingButton` (helper)
   - `ReadMapGenerationOptions`, `ParsePositiveInt`
3. 위치/크기 변경:
   - 토글 버튼: anchor `(0,1)-(0,1)`, anchoredPosition `(40, -40)`, sizeDelta `(220, 50)`, fontSize 22.
   - 펼친 패널: anchor `(0,1)-(0,1)`, anchoredPosition `(40, -100)`, sizeDelta `(360, 360)`.
   - 펼친 상태 우측 끝 X = 40 + 360 = 400px → 카드 fan / 공격 패턴 strip / SkillLoadout 영역과 비충돌.
4. API:
   - `void Initialize(DraftController controller)` — 보관.
   - 옵션 변경 즉시 `controller.SetMapGenerationOptions(ReadMapGenerationOptions())` 호출 (briefing confirm 콜백 개념 사라졌으므로 즉시 push).
   - 토글 버튼 onClick → 펼친 패널 SetActive 토글.
5. WavePatternGenerator 미리보기 호출은 본 컴포넌트가 하지 않는다 (task 4 의 strip 이 자체 처리).
6. 트윈 없음. 단순 SetActive(true/false) — 개발 옵션이라 시각 연출 불필요.
7. 추출 검증 후 다음을 일괄 정리:
   - `TimelineBriefingView.cs` + `.meta` 삭제.
   - 현재 씬에서 TimelineBriefing GameObject 검색 후 제거. Missing Script 가 남으면 깨끗이 정리 후 씬 저장.
   - `using` / 참조 grep 으로 잔여 0 확인.

## 완료 기준

- DraftView active 시 좌상단 `MAP SETTINGS` 버튼 표시.
- 클릭 시 옵션 패널 펼침/접힘. path/size/density/spawn-lanes 변경 즉시 `DraftController.SelectedMapGenerationOptions` 반영.
- 펼친 상태에서 카드 fan(하단) / wave strip(상단) / SkillLoadout(우측 중앙) 영역과 시각 겹침 없음.
- `TimelineBriefingView.cs` + `.meta` 삭제 완료.
- 씬에 Missing Script 경고 없음.
- 컴파일 에러 0, Console 경고 0.
