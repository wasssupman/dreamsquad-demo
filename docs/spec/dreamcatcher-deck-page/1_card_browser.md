# 1 — 카드 그리드 브라우저 (DreamcatcherCardBrowser)

## 목적

우 2/3 스크롤 그리드 — 카드 셀(art + `CardCategoryStyle.Frame` 프레임 + 이름 + **"편성중" 불리언 뱃지**(유니크)). 셀 탭 → `CardSelected(id)`. 무의식 제외는 caller(orchestrator)가 풀 필터.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/DreamcatcherCardBrowser.cs` (`Wassup.UI`)

## 구현

- `ShowCards(IReadOnlyList<DreamcatcherCard>)` — 엔트리 셀(art fill + 프레임색 + 이름 + 편성중 뱃지). SquadRosterBrowser의 ScrollRect+Grid 기계 미러(사본, 뱃지 방식이 유일 차이 → 형제로).
- `SetSelected(id)`(스케일+오버레이) / `SetBadged(ISet<string>)`(편성중 토글) / `CardSelected` 이벤트.

## 완료 기준

- [x] 컴파일 클린. `ShowCards`/`SetBadged`/`SetSelected`/탭 발화.
- [x] Play: 전 카드 타로 art 그리드 + 덱 카드에 "편성중" 뱃지 + 선택 하이라이트 확인. (rev 2026-07-18: 유니크 전제로 카운트→불리언 뱃지)
- [x] 프레임색 = `CardCategoryStyle`(unit 0과 공용). 무의식은 orchestrator가 풀에서 제외.

> 구현 2026-07-18 · 커밋 `30d882cf`. (SquadRosterBrowser 확장 대신 형제 — 뱃지 방식 차이, 커밋 위생상 스쿼드 코드 무변경.)
