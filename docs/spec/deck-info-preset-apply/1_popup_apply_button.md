# 1 — 팝업 적용 버튼 활성화

## 목적

하단에 `스쿼드 프리셋 저장`과 `드림캐쳐 프리셋 저장`을 분리한다. 각 버튼은 활성 탭과 무관하게 자기 요청만 raise 하고 팝업이 스스로 닫힌다. **팝업은 여전히 프로필을 모른다.**

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DeckInfoPopup.cs`

## 구현

이벤트는 **대상별로 둘**이다:

```csharp
public event Action SquadApplyRequested;
public event Action DreamcatcherApplyRequested;
```

`Action<int>` 한 개로 탭 인덱스를 넘기지 않는 이유는 호출측에서 `0` 이 무엇인지 읽히지 않기 때문이고, `PresetApply.Target` 을 넘기지 않는 이유는 팝업이 프리셋 어휘를 몰라도 되기 때문이다(계약 2). **페이로드는 넘기지 않는다** — 그건 호출자(히스토리 패널)가 이미 갖고 있다.

- 라벨: `"스쿼드 프리셋 저장"` / `"드림캐쳐 프리셋 저장"`. 두 버튼은 동일 폭으로 항상 분리해 표시한다.
- 각 버튼의 `interactable` = 해당 종류에 표시 항목이 **1개 이상**일 때만(계약 9). `BuildSections(target)` 의 항목 총수로 판정한다 — `_payload == null`, 빈 배열, 그리고 "스쿼드는 비었지만 카드는 있는" 혼합 케이스까지 독립적으로 덮는다.
- 클릭 → 두 버튼을 먼저 잠금 → 그 버튼에 맞는 이벤트 하나만 raise → `Hide()`. 닫는 이유: 적용은 완료된 동작이고, 뒤이어 페이지로 이동하므로 팝업이 남아 있을 자리가 없다. 먼저 잠그므로 같은 프레임 재진입도 두 번째 종류를 요청하지 못한다.
- `allowPresetApply == false`(내 덱)면 기존대로 영역 통째 숨김 + 목록이 그 자리까지 확장. 이 경로는 변경하지 않는다.
- `OnDestroy` 에서 리스너 정리 — `_closeButton` 선례를 따른다.

`Show()` 때 두 버튼의 활성 상태를 각 payload 섹션에서 다시 계산하므로 이전 팝업의 상태가 남지 않는다.

## 완료 기준

- [x] 컴파일 그린
- [x] `DeckInfoPopup` 에 `PlayerProfile|SquadPreset|DreamcatcherPreset|ProfileStore` 참조 0건 유지 (순수 프레젠테이션 계약)
- [x] EditMode(`DeckInfoPopupTests` 확장):
  - 스쿼드 저장 클릭 → `SquadApplyRequested` 1회, `DreamcatcherApplyRequested` 0회, 팝업 비활성
  - 드림캐쳐 저장 클릭 → 반대로
  - `payload == null` → 두 버튼 모두 비활성, 클릭해도 이벤트 0회
  - 활성 탭과 무관하게 스쿼드 버튼은 스쿼드 이벤트만, 드림캐쳐 버튼은 드림캐쳐 이벤트만 1회
  - 카드만 있고 스쿼드가 빈 페이로드 → 스쿼드 버튼 비활성 / 드캐 버튼 활성
  - `allowPresetApply: false` → 영역 비표시 (기존 테스트 유지)
- [x] Play: 두 버튼이 동일 폭으로 분리되고 탭 전환 시 자리와 라벨이 바뀌지 않는다
  - 2026-07-31 임시 PlayMode 시각 스모크 캡처 후 `$visual-verdict` 96/100 pass. 임시 테스트와 캡처 파일은 검증 후 제거했다.
