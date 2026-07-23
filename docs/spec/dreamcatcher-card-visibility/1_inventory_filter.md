# 1. 인벤토리에서 숨김 카드 제외

## 목적

`visible == 0` 인 카드가 덱 페이지 컬렉션 그리드에 나타나지 않게 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPageController.cs` — `BuildPool`

## 구현

`BuildPool` 에 이미 있는 제외 규칙(`CardCategory.Subconscious` = gift 전용이라 추가 불가) 옆에 한 줄을 더한다:

```csharp
if (c.visible == 0) continue; // 시트에서 숨긴 카드 — 인벤토리에 노출하지 않는다
```

`_pool` 이 곧 그리드 소스(`SortedPool` → `browser.ShowCards`)이자 추가 가능 목록이므로, 이 한 지점이 "보이지도 않고 넣을 수도 없다"를 동시에 만든다.

**덱에 남아 있는 숨김 카드는 이 필터가 지우지 않는다.** `_working` 은 저장 덱에서 그대로 읽고, `SortedPool` 은 `_pool` 교집합만 그리드에 얹으므로 숨김 카드는 그리드에 안 뜨면서 덱 스트립에는 남는다 — 실제 장착 해제는 유닛 2(로그인 prune)가 담당한다. 페이지에서 임의로 덱을 바꾸면 사용자가 저장하지 않은 편집을 만들어내므로(이 페이지는 명시적 Save 계약) 여기서는 건드리지 않는다.

옛 `DreamcatcherDeckBuilderView` 는 재설계 전 뷰라 이번 필터 대상이 아니다.

## 완료 기준

- [x] 컴파일 통과 (2026-07-23)
- [x] `BuildPool` 과 같은 규칙으로 재현 시 카드 하나를 `visible = 0` 으로 두면 풀이 25 → 24 로 줄고, 나머지 수 = 전체 − 숨김 − 무의식
- [ ] 덱 페이지를 실제로 열어 그리드에서 사라진 것을 눈으로 확인 (Play 필요)
