# 0 — Attachments Read API

## 목적

`DreamcatcherHandController` 의 부착 레지스트리(`_attachedTo`)를 프레젠테이션이 읽을 수 있는 공개 API + 변경 통지 이벤트로 노출한다. 기존 부착/회수/리셋 로직 변경 0.

## 변경 대상

- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`

## 구현

1. **`public event System.Action AttachmentsChanged;`** — 부착 목록이 실제로 바뀌는 3지점에서 발화:
   - `AttachAndSpend` 성공 (부착)
   - `OnDefenderDied` 회수 루프 후 (회수가 1건 이상일 때만)
   - `OnPhaseChanged` Placement 리셋 (`_attachedTo.Clear()` 후)
   - `HandChanged` 를 재사용하지 않는 이유: Active 카드 사용(`SpendAndRecycle`)도 `Used` 를 발화하지만 부착은 안 바뀐다 — 전용 이벤트가 뷰 리빌드 횟수를 정확히 만든다.
2. **`public void GetAttachments(List<(Entity host, DreamcatcherCard card)> results)`** — 호출자 제공 리스트에 채움(할당 없음):
   - `_attachedTo` 키를 entryId 오름차순 정렬 후 `_deck.TryGetCard(entryId)` 로 카드 해석.
   - entryId 정렬 = 딕셔너리 순회 순서 비결정성 제거(리빌드 간 스트립 순서 안정). 부착 시각순 정렬은 레지스트리 구조 변경이 필요해 채택 안 함(후속 튜닝 여지).
   - 카드 해석 실패 엔트리는 스킵(방어적 — 정상 흐름에선 발생 안 함).

## 완료 기준

- compile 통과 (Unity 콘솔 에러 0).
- 기존 부착/회수/사이클 플로우 로직 diff 0 (이벤트 발화 라인 추가만).
- 통합 동작(이벤트 타이밍·목록 내용)은 unit 2 Play 검증에서 확인.

확인 2026-07-12 — compile 에러 0, 기존 로직 diff 0 (이벤트 3지점 + 읽기 API 추가만). 사용자 진행 승인.
