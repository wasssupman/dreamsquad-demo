# 1 — 손패: 부착 순서 기록 + 앞으로 회수

## 목적

퇴근한 유닛에 붙어 있던 드림캐쳐를 **붙인 순서 그대로 큐 맨 앞**으로 돌려보낸다.
이 카드가 실제로 일하는 유일한 지점이다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherCycleDeck.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`
- `Assets/_Project/Tests/EditMode/DreamcatcherCycleDeckTests.cs`
- `Assets/_Project/Tests/PlayMode/DefenderRetireTest.cs` (통합 1건)

## 구현

### 1) 덱 — 앞 삽입 API

```csharp
// 퇴근 회수: 준 순서 그대로 큐 맨 앞에 꽂는다(0,1,2...). 미부착 id 는 건너뛴다.
public int RecoverToFront(IReadOnlyList<int> entryIds)
```

`inserted` 카운터 하나로 `_attached` 에서 꺼내 `_queue.Insert(inserted++, entry)`.
`Recover(entryId)`(맨 뒤)는 **그대로 둔다** — 사망·적 소멸이 계속 쓴다.
인덱스 계산이 여기 있는 이유는 `Hand()` 가 "큐 앞 N" 이라는 사실의 주인이 이 클래스이기
때문이다(계약 13). 컨트롤러에 인덱스를 노출하지 않는다.

### 2) 컨트롤러 — 부착 순서 기록

`_attachedTo` 값 튜플을 `(Entity host, int handle)` → `(Entity host, int handle, int seq)` 로
넓히고, `AttachAndSpend` 에서 단조 증가 카운터 `_attachSeq++` 를 같이 저장한다.
카운터는 `OnPhaseChanged(Placement)` 의 `_attachedTo.Clear()` 옆에서 0 으로 되돌린다.

> **왜 필요한가**: `Dictionary` 순회 순서는 제거가 섞이면 보장이 없다(그래서 아이콘 스트립
> `GetAttachments` 가 entryId 오름차순으로 **정렬**한다). 기존 회수는 "맨 뒤로 몰아넣기"라
> 순서가 안 보였지만, 이 카드는 **순서가 곧 기능**이다(계약 3).
> `GetAttachments` 는 무변경 — entryId 순서는 스트립의 안정성 축이지 부착 순서가 아니다.

### 3) 컨트롤러 — 회수 정책

`RecoverCardsHostedBy(Entity host)` 에 `bool retired` 를 추가하고 호출처 3개를 갱신한다
(사망 `false` · 적 소멸 `false` · 퇴근 `true`).

```
1. host 의 entryId 를 모아 seq 오름차순 정렬
2. retired && 그중 하나라도 「인수인계」 선언 → 앞당김 활성 (여럿이어도 결과는 같다)
3. 각 항목: handle>0 이면 RevokeDreamcatcherEffects, _attachedTo 에서 제거
   - 앞당김 활성 && 자신이 선언 카드가 아님 → front 리스트에 append
   - 그 외 → _deck.Recover(entryId)   // 기존 맨 뒤
4. front 가 비지 않으면 _deck.RecoverToFront(front)
5. 기존 그대로 HandChanged(Recovered) + AttachmentsChanged
```

**선언한 카드 자신은 맨 뒤다**(계약 2 — 사용자 결정). 인수인계만 붙어 있으면 앞으로 오는 것이
0장이고 자기만 뒤로 간다 — 정상이며 경고를 내지 않는다.

**선언 판정은 bake 와 완전히 같은 조건을 본다** — `type == Unit` · payload · trigger 셋 다.
`type` 을 빠뜨리면 구멍이 생긴다(코드리뷰 지적): Squad 카드는 `mechanics` 를 아예 읽지 않는 다른
apply 경로로 가므로 bake 의 트리거 화이트리스트를 **안 탄다**. 그 카드가 컨트롤러에서만 동작하면
**검증을 통과하지 않은 발동**이 된다.

> ⚠ **플래그 파라미터에 대하여.** defender-clock-out 계약 3 은 공유 함수에 `bool playDeathAnim`
> 같은 플래그를 넣지 말라고 적었다. 여기서 갈리는 것은 **회수 목적지 하나**이고 나머지 절차
> (revoke · detach · 통지)가 완전히 동일하다. 갈라 두면 그 절차가 복제되고 한쪽만 고쳐지는 게
> 이 함수가 원래 3벌을 하나로 합친 이유였다. 그래서 여기서는 파라미터가 맞다.

> ⚠ **계약 12 를 여기서 지킨다.** 이 함수는 퇴근(플레이어 탭)에서만 앞 삽입을 한다.
> 사망·적 소멸 경로에 앞 삽입을 얹지 말 것 — 조준 중 비동기 재정렬이 되고, `CommitAttach` 는
> 브리지 적용 뒤 손패 창 밖이면 **롤백 없이** 실패한다.

## 완료 기준

- 컴파일 0 에러 · 콘솔 경고 0.
- **EditMode `DreamcatcherCycleDeckTests` +3**:
  ① `RecoverToFront([a,b])` 후 `Hand(5)` 의 0·1번이 a·b (순서 보존)
  ② 미부착 id 는 건너뛰고 나머지가 밀리지 않는다
  ③ `Recover`(맨 뒤)와 섞어 불러도 `TotalCount` 가 보존된다
  ⚠ **부착 순서 ≠ entryId 순서인 케이스를 반드시 포함**한다. 둘이 우연히 같으면 이 테스트는
  이 unit 이 새로 만든 축(seq)을 아무것도 증명하지 않는다.
- **PlayMode 통합 1건**(`DefenderRetireTest` 확장 — 이미 손패 컨트롤러를 잡는 퇴근 회수 단정이
  있다): 인수인계 + 다른 Unit 카드를 한 host 에 붙이고 `RetireDefender` → `Hand()` 맨 앞이
  **다른 카드**이고 인수인계는 앞에 없다.
  (부착 구동은 `PlacementAuraTest` · `DreamcatcherAttachRequirementE2ETest` 의 컨트롤러 주입
  셋업을 따른다 — 새 하네스를 만들지 않는다.)
- **대조군**: 같은 구성을 **사망**시키면 앞으로 오지 않는다(기존 맨 뒤). 계약 12 의 회귀 방어선.
