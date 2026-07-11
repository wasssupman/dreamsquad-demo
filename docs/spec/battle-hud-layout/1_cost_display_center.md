# 1 — 코스트 배지 중앙 동반 이동

## 목적

`CostDisplay` 에너지 배지를 중앙 스트립 바로 위 밀착 위치로 옮겨 "코스트 확인 → 슬롯 선택 → 드래그" 사이클이 한 시선 안에서 돌게 한다 (CR 엘릭서 바 관례). 선행: unit 0.

## 변경 대상

- `Assets/_Project/Scripts/UI/CostDisplay.cs` — `BuildCanvas()` 의 `CostPanel` RectTransform

## 구현

- 앵커/피벗: `anchorMin=anchorMax=(0.5, 0)`, `pivot=(0.5, 0)`
- 위치: 스트립 상단(y ≈ 32+120=152)에 밀착하되 겹치지 않게 `anchoredPosition = (0, 164)` 기준으로 시작, 시각 확인 후 미세조정
  - 중앙 정렬이 보드를 더 가리면 스트립 좌단 정렬(`x = -456 + PlateW/2`) 대안 허용 — 시각 판단으로 결정하고 결과를 이 문서에 기록
- 크기(363×112)·내부 구성(볼트/숫자/바 게이지)·페이즈 표시 로직 변경 없음
- 주석의 "bottom-left above the DefenderSelector" 서술을 실위치에 맞게 갱신

## 상태 관계 결정 (rev 2026-07-11)

핸드 오픈 시 배지(y164~276)가 핸드 패널(y32~264)과 세로 100px 겹침 확인 → **y 조정 대신 억제로 해결**. y 조정은 평상시 코스트-스트립 클러스터를 찢어 기각.

- `CostDisplay.SetSuppressed(bool)` 신설 — 표시 결정은 `_phaseVisible && !_suppressed` 로 CostDisplay 가 단독 소유 (PhaseChanged 구독 순서 경합 없음)
- `DreamcatcherHandView` 가 Open/Close/ForceClose 에서 신호 — "유닛 손패 + 유닛 재화" 한 세트 플립아웃
- 씬 배선: HandView.costDisplay SerializeField ← CostDisplay (BattleScene)
- Battle 슬림 시 배지 y 는 고정(스트립 미추종) — 갭이 벌어질 뿐 시각 무해, 단순 우선

## 완료 기준

- [x] 컴파일 클린
- [x] Play: 배지가 스트립 위에 밀착 표시, 스트립/핸드/웨이브 독과 겹침 없음
- [x] Play: 코스트 리젠 게이지·숫자 갱신 정상 (10/10 표시 확인)
- [x] 핸드 오픈 시 배지 동반 퇴장 / 클로즈 시 복귀 (스크린샷 검증 2026-07-11)
