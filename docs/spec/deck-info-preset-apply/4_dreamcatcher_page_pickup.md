# 4 — 드림캐쳐 페이지 픽업

## 목적

unit 3 과 같은 형태를 카드에 적용한다. 다르게 다뤄야 하는 것은 **가변 길이 작업본**과 **덱 규칙**뿐이다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPageController.cs`
- `Assets/_Project/Tests/EditMode/Profile/PresetApplyPickupTests.cs` (unit 3 파일에 이어 씀)

## 구현

`OnCreatePreset` 의 생성부를 `private DreamcatcherPreset CreatePreset(string name)` 로 추출(unit 3 과 대칭).

`OnEnable` 에서 `LoadWorking()` 뒤, 브라우저 구성 **앞**에 픽업한다 — `SortedPool()` 이 `_working` 을 읽어 편성된 카드를 앞으로 보내므로, 작업본이 채워진 뒤에 그리드를 만들어야 진입 화면부터 정렬이 맞는다:

```csharp
LoadWorking();
if (PresetApply.TryConsume(PresetApply.Target.Dreamcatcher, out var req)) ApplyStaged(req);
if (browser != null) browser.ShowCards(SortedPool());
```

`ApplyStaged` 는 unit 3 과 같은 순서(가드 → 상한 → 필터 → 생성 → 작업본 → 안내)이고 차이는 셋:

- `PresetApply.FilterCards(req.cardIds, catalog, out int dropped)` 하나만 쓴다.
- 작업본은 **가변 리스트**다. `_working.Clear()` 후 필터 결과를 `AddRange` — 슬롯 패딩이 없다.
- `AddCard` 를 거치지 않는다. 필터가 이미 `CanAdd` 와 같은 규칙(상한·타입 제한·중복·숨김·`Subconscious`)을 적용했고, `AddCard` 는 매 장마다 브라우저 재정렬과 `RefreshAll` 을 돈다 — 10장이면 10번이다.

상한 가득 알림 문구는 `"덱 프리셋이 30개로 가득 차..."` 로 대상만 바꾼다(`OnDeletePreset` 의 스쿼드/덱 문구 구분과 같은 결).

**유효하지 않은 덱도 저장할 수 있다는 기존 계약을 건드리지 않는다.** 필터가 상한을 넘기지 않으므로 결과는 항상 규칙에 맞지만, 원본이 9장이었으면 9장으로 들어간다 — `START` 는 `LoadoutGate` 가 막는다.

## 완료 기준

- [ ] 컴파일 그린
- [ ] EditMode:
  - 예약 후 진입 → `dreamcatcherDecks.Count` +1, 이름 규칙, 작업본 = 필터 결과, 저장본은 빈 리스트, `IsDirty()` true
  - `[저장]` → 저장본 반영 · `[되돌리기]` → 빈 덱
  - 상한 초과 카드 목록(12장) 예약 → `EffectiveDeckSize` 만큼만, dropped 안내
  - 타입 제한 위반 목록 → 초과분 제외
  - 상한 가득 / 미로드 → 미증가 + 예약 소멸 (unit 3 과 동형)
  - 예약 없이 진입 → 기존 동작 무변경
- [ ] Play: 남의 덱 → 드림캐쳐 탭 적용 → 카드가 채워진 새 프리셋 + dirty + 그리드에 편성 카드가 앞으로 정렬됨
