# 2 — 패널 예약 + 메뉴 라우팅

## 목적

팝업의 요청을 받아 **예약**하고, 해당 페이지로 **이동**시킨다. 패널은 프로필을 모르고, 이동 권한은 계속 메뉴 컨트롤러가 갖는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/TournamentHistoryPanel.cs`
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`

## 구현

**패널** — 팝업이 페이로드를 되돌려주지 않으므로(unit 1), 마지막으로 연 행의 것을 패널이 들고 있어야 한다. 덱보기 핸들러(`row` → `Deserialize` → `Show`)에서 `_deckPayload` · `_deckOwnerName` 을 함께 기억한다.

구독은 `EnsureDeckPopup()` 안 **생성 직후 1회**. 팝업은 재사용되므로 `Show` 마다 구독하면 중복 발화한다.

```csharp
public event Action<PresetApply.Target> onPresetApply;   // onClose 선례
```

핸들러는 두 가지만 한다:

```csharp
PresetApply.Stage(new PresetApply.Request {
    target = PresetApply.Target.Squad,
    presetName = PresetApply.DeckName(_deckOwnerName),
    unitIds  = _deckPayload?.squad?.units,
    stoneIds = _deckPayload?.squad?.stones,
});
onPresetApply?.Invoke(PresetApply.Target.Squad);
```

드림캐쳐는 `cardIds = _deckPayload?.dc?.cards`. **필터는 여기서 걸지 않는다** — 카탈로그 판정과 상한은 픽업 시점의 프로필 상태에 달렸고, 그 판단의 소유자는 페이지다(계약 2). 패널은 원본 id 를 그대로 예약한다.

**메뉴 컨트롤러** — `_historyPanelView` 를 이미 들고 있으므로 `onClose` 와 같은 자리에서 구독/해제한다:

```csharp
private void OnPresetApply(PresetApply.Target target)
    => RaiseExclusive(target == PresetApply.Target.Squad ? squadPanel : dreamcatcherPanel);
```

`RaiseExclusive` 가 `ClosePanels()` 를 먼저 돌려 히스토리를 닫고 대상 패널을 켠다 → 페이지 컨트롤러의 `OnEnable` 이 그 프레임에 돈다 = 예약을 받을 주인. **`OnDisable` 에서 해제**를 빼먹지 않는다(패널 재활성마다 구독이 누적되면 적용 1회에 이동이 여러 번 요청된다).

대상 패널이 미배선(null)이면 **`PresetApply.Clear()` + `Debug.LogError` 후 이동하지 않는다.** "다음 진입에서 소멸" 규칙은 대상이 **다른** 페이지일 때만 유령을 죽인다 — 라우팅이 실패한 채 예약을 남기면, 한참 뒤 사용자가 그 페이지를 손으로 열었을 때 맥락 없는 프리셋이 불쑥 생긴다(spec 리뷰 2026-07-31 에서 발견). 배선 누락은 조용한 지연 동작이 아니라 즉시 에러다.

## 완료 기준

- [ ] 컴파일 그린
- [ ] `TournamentHistoryPanel` 에 `PlayerProfileSO`/`ProfileStore` 참조 0건 (프로필 무지 유지)
- [ ] EditMode: 패널의 적용 핸들러 호출 → `PresetApply.HasPending` true + `Request.target`·`presetName`·id 목록이 행 데이터와 일치. 팝업을 두 번 열고 각각 다른 행으로 적용해도 마지막 행의 것이 예약된다
- [ ] Play: 스쿼드 저장 → 히스토리가 닫히고 스쿼드 페이지가 열린다. 드림캐쳐 저장 → 드림캐쳐 페이지
- [ ] Play: 히스토리를 닫고 다시 열어 적용해도 이동이 **1회**만 일어난다(구독 누적 없음)
