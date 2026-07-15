# 2 — 게이트 팝업 + START 배선

## 목적

unit 0 의 판정을 실제로 START 경로에 걸고, 미충족 시 **무엇이 모자란지 + 어디로 가야 하는지**를 팝업으로 안내한다. 이 unit 이 끝나면 검증 질문에 답할 수 있다.

선행: unit 1 (기본 덱 시딩) — 없으면 신규 유저가 첫 START 에서 막힌다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/LoadoutGatePopup.cs` (신규)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — SerializeField + `OnStartGame` 게이트
- `Assets/_Project/Scenes/OutgameScene.unity` — 팝업 GameObject 1개 + 배선

## 구현

### LoadoutGatePopup (뷰)

`MenuCanvas` 자식으로 두고 **코드로 빌드**한다 — 로비 팝업의 기존 idiom(`SquadBuilderView.OpenPicker:238`, `DreamcatcherDeckBuilderView.ShowCardPopup:322`)을 그대로 따른다.

```csharp
[SerializeField] private TMP_FontAsset font;   // 형제 팝업들과 동일. null -> TMP 기본
public void Show(IReadOnlyList<LoadoutShortfall> shortfalls, Action onGoSquad, Action onGoDeck)
public void Hide()
```

**활성화 규약 (명시 필수)**: 호스트 GameObject 는 **항상 활성**이고, `Show`/`Hide` 가 내부에 만든 `_root`(전체화면 스크림+패널)를 `SetActive` 로 토글한다. `SquadBuilderView` 의 `_pickerPanel`, `MenuPopup` 의 `_root` 와 같은 형태다. 호스트 GO 를 비활성으로 두면 `Awake` 가 안 돌고 `Show` 가 조용히 무반응이 된다 — 이 spec 이 없애려는 바로 그 실패.

빌드 절차:
1. 지연 빌드 `_built` + `BuildOnce()`. 부모는 **루트 캔버스**:
   ```csharp
   var host = GetComponentInParent<Canvas>();
   Transform parent = host != null ? host.rootCanvas.transform : transform.root;
   ```
   중첩 캔버스 + `overrideSorting` 은 **렌더 순서만** 올리고 `GraphicRaycaster` 우선순위는 못 올려서 탭이 아래 버튼으로 샌다 — `SquadBuilderView.cs:271-277` 에 기록된 실증. 같은 캔버스 last-sibling 이 렌더와 레이캐스트를 **둘 다** 이긴다. 새 캔버스를 만들지 않는 이유이기도 하다.
2. 스크림: 전체화면 `Image`(`UiOverlay.Dim`) + `Button` → `Hide` (탭-투-클로즈).
3. 패널: `UiRoundedSprite.Make(...)`.
4. 끝에 `UiLayer.Apply(gameObject)`.
5. `Show` 에서 `_root.transform.SetAsLastSibling()` → `_root.SetActive(true)`.

내용:
- 줄 문구: `have != need` → `"스쿼드 5/7"` / `"드림캐쳐 덱 6/8"`. 같으면 `reason` 을 그대로 (`"드림캐쳐 덱 — unknown card: xxx"`). **미충족 항목만** 줄로 만든다.
- 버튼: 미충족 대상의 이동 버튼만 + `닫기`. 이동 버튼은 `Hide()` **후** 콜백 호출.
- `Show` 재호출 시 이전 줄/버튼을 지우고 다시 만든다 (idempotent — `MenuPopup.Open/Close` 선례).
- **`shortfalls` 인자를 보관하지 않는다** — 호출 중 즉시 소비. 호출자가 재사용하는 리스트라 다음 `Check` 의 `Clear()` 가 팝업 밑을 판다.

팝업은 `OnOpenSquad` 를 모른다. 콜백만 받는다 (README 계약 — 패널 가시성 소유자는 컨트롤러).

### OutgameMenuController

- `[SerializeField] private LoadoutGatePopup gatePopup;` (`cardCatalog` 는 unit 1 에서 이미 추가됨)
- 재사용 리스트 `private readonly List<LoadoutShortfall> _shortfalls = new();` — `Check` 가 진입 시 Clear 하므로 호출 전 Clear 불필요.

```csharp
public void OnStartGame()
{
    // 배선 오류는 플레이어가 못 고치는 shortfall 로 위장시키지 않는다: cardCatalog 가
    // null 이면 게이트가 폴백 10장을 요구하는데 덱 빌더는 8에서 막아 영구 잠금이 된다.
    if (gatePopup == null || catalog == null || cardCatalog == null)
    {
        Debug.LogError("[OutgameMenuController] loadout gate refs unassigned — start blocked.", this);
        return;
    }
    var p = profileSO != null ? profileSO.profile : null;
    if (!LoadoutGate.Check(p, catalog, cardCatalog, _shortfalls))
    {
        gatePopup.Show(_shortfalls, OnOpenSquad, OnOpenDreamcatcher);
        return;
    }
    SceneTransition.Go(SceneNames.Battle);
}
```

`OnOpenSquad`/`OnOpenDreamcatcher` 는 이미 `RaiseExclusive` 로 패널을 열고 `menuRoot` 를 숨긴다 — 그대로 넘긴다.

### 씬 배선

- `LoadoutGatePopup` GameObject 를 **`MenuCanvas` 직속 자식**으로 신설한다. **`menuRoot` 자식으로 넣지 말 것** — `menuRoot` 는 `RaiseExclusive`/`ClosePanels`/`ApplyAuthGate` 가 통째로 토글한다(`OutgameMenuController.cs:55, 94, 105`). 그 안에 있으면 패널을 여는 순간 팝업도 같이 사라진다.
- `OutgameMenuController.gatePopup` ← 위 GO.
- `StartButton.onClick` 은 **건드리지 않는다** (이미 `OnStartGame` 을 가리킴).
- 씬 저장 전 `git diff` 로 무엇이 베이크되는지 확인 — 미저장 WIP 가 함께 박힌다.

## 완료 기준

- [x] compile clean, 콘솔 에러 0.
- [x] 구조 검증 (Play 중 프로브 — 라이브 프로필 무접촉, 합성 shortfall 로 `Show` 직접 호출):
  - `_root` 부모 = `MenuCanvas`, siblingIndex 4/5 = **마지막 sibling** (렌더 + 레이캐스트 우선순위 확보)
  - 미충족 2건 → 정확히 2줄(`스쿼드 5/7` · `드림캐쳐 덱 6/8`) + 버튼 3개(`스쿼드 편성`/`드림캐쳐 덱`/`닫기`). 한글 문자열 정상.
  - 버튼 총 5개 = 스크림 + 패널(no-op 소비용) + 위 3개
- [x] 스쿼드 7 + 덱 8 → START 가 기존대로 BattleScene 진입 (팝업 없음).
- [x] 씬 diff = 팝업 GameObject 신설 + `gatePopup`/`MenuCanvas` 자식 참조뿐. 무관한 WIP 미포함.
- [x] Play 육안 확인 — 사용자 확인 2026-07-16.
- [ ] `gatePopup`/`cardCatalog` 를 비우고 START → 차단 + LogError (fail-loud). **미실행** — 배선을 일부러 깨는 검사라 사용자 세션 중 하지 않았다. 코드 경로는 `OnStartGame` 최상단 3줄이라 자명하지만 실측은 아니다.
- [ ] 테스트 모드 경로 무게이트 확인 — **미실행**(코드상 `TestModePanelView` 는 `OnStartGame` 을 거치지 않으므로 구조적으로 보장).
- [ ] EditMode 전량 green — **unit 2 코드 상태에서 미실행** (에디터 Play 중이라 러너가 거부). 마지막 전체 실행은 unit 1 시점(854 중 852 passed / 0 failed). unit 2 는 테스트를 추가하지 않고 MonoBehaviour 배선만 바꿔 EditMode 커버리지 밖이며 compile 은 clean 이지만, 실행한 것으로 적지 않는다. Play 종료 후 1회 재실행 필요.

확인 2026-07-16 — 게이트 배선. **되돌리면 안 되는 것 3가지**: (1) 팝업 `_root` 는 **루트 캔버스의 마지막 sibling** 이어야 한다 — 중첩 캔버스 + `overrideSorting` 은 렌더만 이기고 레이캐스트는 못 이겨 탭이 아래로 샌다(`SquadBuilderView.cs:271-277` 실증). (2) 패널의 **no-op Button** 을 지우면 패널 안 클릭이 스크림의 `Hide` 로 버블링돼 팝업이 닫힌다. (3) 버튼 `transition = None` 을 지우면 기본 ColorTint 가 `image.color` 를 덮는다. 팝업 GO 는 **`MenuCanvas` 직속** — `menuRoot` 자식이면 패널 오픈 시 함께 사라진다.

> `DEFAULT LOADOUT` 은 dev 전용(`DevOnlyGroup`)이라 신규 설치를 대표하지 않는다. 신규 설치 검증은 unit 1 의 "`profile.json` 삭제 후 첫 진입" 기준이 담당한다.
