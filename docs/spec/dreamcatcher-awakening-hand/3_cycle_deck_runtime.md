# 3 — DreamcatcherCycleDeck 12장 순환 큐 (순수 클래스 + EditMode)

## 목적

CR식 덱 순환의 전체 상태기계를 ECS/UI 무참조 순수 C# 클래스로 만들고 EditMode 로 고정한다. 소비자는 unit 4.

## 변경 대상

- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherCycleDeck.cs` (신규)
- `Assets/_Project/Tests/EditMode/DreamcatcherCycleDeckTests.cs` (신규 — 기존 EditMode 테스트 폴더 관례)

## 구현

1. **엔트리 단위**: 생성자 `(IReadOnlyList<DreamcatcherCard> cards, int seed)` — 호출자(unit 4)가 부착덱 10 + Active 2 를 합쳐 넘긴다. 엔트리 `{ int entryId, DreamcatcherCard card }` 목록을 만들고 **12장 전체를 시드 기반 Fisher-Yates 셔플 1회**. 같은 카드 SO 2장 = 독립 엔트리. (UnityEngine 참조는 `DreamcatcherCard` 타입뿐 — 테스트에서 `ScriptableObject.CreateInstance` 로 생성.)
2. **상태**: `_queue`(순환 큐) + `_attached`(entryId → 부착 중, 큐 밖). 총량 = queue + attached 불변(12).
3. **API**:
   - `Hand(int handSize)` — 큐 front N 조회(비파괴). 큐 크기 < N 이면 있는 만큼만(빈 슬롯은 뷰 책임).
   - `UseAndRecycle(entryId)` — Squad/Active 공용: 손패 검증 후 큐에서 제거 → **맨 뒤 append**.
   - `UseUnit(entryId)` — 손패 검증 후 큐에서 제거 → `_attached` 이동. (entity 매핑은 컨트롤러 레지스트리 소관 — 이 클래스는 entryId 만 안다.)
   - `Recover(entryId)` — `_attached` 에서 제거 → 큐 **맨 뒤 append** (사망 순 = 호출 순).
   - 손패 밖/미존재 entryId 는 false 반환(방어).
4. **결정론**: 같은 (cards, seed) → 같은 순서. `System.Random` 사용 (`UnityEngine.Random` 금지 — 문서 §11).

## EditMode 테스트 (최소)

- 셔플 결정론(동일 시드 동일 순서) + 12장 보존.
- front-N 손패: 사용 시 다음 카드 슬라이드 인.
- `UseAndRecycle` → 맨 뒤, 재등장 순서(문서 §8 규칙 A).
- `UseUnit` → 손패/큐에서 사라짐, `Recover` → 맨 뒤 복귀.
- 부착 초과로 큐 < handSize → `Hand` 축소, 전량 유출 → 빈 목록.
- 중복 카드 2장 = 독립 엔트리.
- 손패 밖 사용 거절.

## 완료 기준

- [ ] 컴파일 클린 + EditMode 전체 그린.
- [ ] ECS/Bridge/UI 참조 0 (순수 계층).

> 확인 2026-07-09 — 커밋 7019e928 (EditMode 7/7 그린)
