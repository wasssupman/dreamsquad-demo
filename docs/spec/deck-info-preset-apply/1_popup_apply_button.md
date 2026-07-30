# 1 — 팝업 적용 버튼 활성화

## 목적

자리만 잡혀 `interactable = false` 였던 하단 버튼을 살린다. 라벨과 대상이 활성 탭을 따르고, 누르면 요청을 raise 하고 팝업이 스스로 닫힌다. **팝업은 여전히 프로필을 모른다.**

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DeckInfoPopup.cs`

## 구현

이벤트는 **탭별로 둘**이다:

```csharp
public event Action SquadApplyRequested;
public event Action DreamcatcherApplyRequested;
```

`Action<int>` 한 개로 탭 인덱스를 넘기지 않는 이유는 호출측에서 `0` 이 무엇인지 읽히지 않기 때문이고, `PresetApply.Target` 을 넘기지 않는 이유는 팝업이 프리셋 어휘를 몰라도 되기 때문이다(계약 2). **페이로드는 넘기지 않는다** — 그건 호출자(히스토리 패널)가 이미 갖고 있다.

- 라벨: 스쿼드 탭 `"이 스쿼드를 프리셋으로"` / 드림캐쳐 탭 `"이 덱을 프리셋으로"`. `RenderTab()` 에서 갱신한다(탭 전환 경로가 이미 그것 하나다).
- `interactable` = 그 탭에 표시 항목이 **1개 이상**일 때만(계약 9). `BuildSections(tab)` 의 항목 총수로 판정한다 — `_payload == null`, 빈 배열, 그리고 "스쿼드 탭은 비었지만 카드는 있는" 혼합 케이스까지 한 식으로 덮인다.
- 클릭 → 활성 탭에 맞는 이벤트 raise → `Hide()`. 닫는 이유: 적용은 완료된 동작이고, 뒤이어 페이지로 이동하므로 팝업이 남아 있을 자리가 없다. **`Hide()` 를 이벤트보다 먼저 부르지 않는다** — 구독자가 이동을 유발하고 그 과정에서 팝업의 부모가 비활성화되므로, 순서를 뒤집으면 이미 죽은 오브젝트에서 이벤트가 나간다.
- `allowPresetApply == false`(내 덱)면 기존대로 영역 통째 숨김 + 목록이 그 자리까지 확장. 이 경로는 변경하지 않는다.
- `OnDestroy` 에서 리스너 정리 — `_closeButton` 선례를 따른다.

`Show()` 는 매번 `_tab = SquadTab` 으로 되돌리므로 라벨 초기화가 자동으로 따라온다.

## 완료 기준

- [ ] 컴파일 그린
- [ ] `DeckInfoPopup` 에 `PlayerProfile|SquadPreset|DreamcatcherPreset|ProfileStore` 참조 0건 유지 (순수 프레젠테이션 계약)
- [ ] EditMode(`DeckInfoPopupTests` 확장):
  - 스쿼드 탭에서 클릭 → `SquadApplyRequested` 1회, `DreamcatcherApplyRequested` 0회, 팝업 비활성
  - 드림캐쳐 탭으로 전환 후 클릭 → 반대로
  - `payload == null` → 두 탭 모두 버튼 비활성, 클릭해도 이벤트 0회
  - 카드만 있고 스쿼드가 빈 페이로드 → 스쿼드 탭 비활성 / 드캐 탭 활성
  - `allowPresetApply: false` → 영역 비표시 (기존 테스트 유지)
- [ ] Play: 탭 전환 시 라벨이 바뀌고 자리는 뛰지 않는다
