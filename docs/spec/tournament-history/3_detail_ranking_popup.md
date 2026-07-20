# 3 — 상세 랭킹 팝업

## 목적

히스토리 행 클릭 시 해당 토너먼트의 참가자 랭킹을 모달 팝업으로 보여준다. 조회 API/DTO 는 기존 `TournamentApi.GetResult` + `ResultData` 재사용, 렌더링은 unit 1 의 `LeaderboardList` 재사용.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/TournamentDetailPopup.cs` (신규)
- (연결) `TournamentHistoryPanel.CreateRow` 의 행 클릭 → `Show(entryId)` (unit 2 에서 이미 배선)

## 구현

- `TournamentDetailPopup`(MonoBehaviour): 자체 캔버스(`UiCanvasSetup.Ensure`, sorting 3000, overrideSorting) — 히스토리 패널(2500) 위 모달. dim(클릭 시 닫기) + 패널(제목/리스트/닫기).
  - `Show(string entryId)`: `_epoch` 증가 → 로딩 표시 → `TournamentApi.GetResult(baseUrl, idToken, entryId, …)`. 콜백 epoch 가드(이전 entry/닫힘 응답 폐기).
  - 성공: 제목 = `data.name`, `LeaderboardList.BuildRows(data.entries, data.maxEntryCount, UserSession.Current?.userId)` → `Render`. 실패/빈 목록 상태 문구.
  - `Hide()`: epoch 증가(in-flight 폐기) + 비활성. dim/닫기 버튼 모두 Hide.
- 랭킹 행 룩은 결과창과 100% 동일(공용 `LeaderboardList`). 본인 행 골드 강조는 `UserSession.Current.userId` 매칭.

## 완료 기준

- [x] compile: 오류 0.
- [ ] Play: 히스토리 행 클릭 → 팝업에 참가자 랭킹(점수 내림차순, 본인 강조, WAITING 슬롯), 닫기/dim 로 복귀. 실서버 상세 왕복 로그 확인.
- [ ] 결과창 리더보드와 시각 일치 육안 확인.
