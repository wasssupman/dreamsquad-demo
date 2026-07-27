# 0 — 편집 즉시 저장 · 저장 버튼 제거

## 목적

`_working` 을 바꾸는 모든 편집이 즉시 `ProfileStore.Save` 를 타게 하고, 덱 스트립의 `[저장]` 버튼을 제거한다. 유효성 검사는 저장 경로에서 빼고 출전 게이트에만 남긴다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPageController.cs`
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckStrip.cs`
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPage.cs` — `SaveHost` 생성·주입(`:86-93`)과 이제 죽는 `saveButtonSize` / `saveButtonMargin` SerializeField
- `Assets/_Project/Tests/EditMode/DreamcatcherDeckAutosaveTests.cs` (신규)
- `docs/spec/README.md` — Follow-up Backlog "스쿼드 저장 게이트" 항목 정정 (아래 참조)

`saveButtonSize` / `saveButtonMargin` 은 씬(`OutgameScene.unity`)의 `DreamcatcherDeckPage` 에 직렬화돼 있을 수 있다. 필드를 지우면 orphan YAML 키가 남지만 무해하다(기존 관측 사항) — 씬을 손으로 고치지 않는다.

## 구현

### 컨트롤러

`OnSave()` → `PersistWorking()`. `DeckRules.Validate` 게이트를 제거하고, 덱 find-or-create · `deck.cardIds` 갱신 · `selectedDeckId` 고정 · 저장 호출은 유지한다. 진입 가드:

```
if (profileSO == null || profileSO.profile == null) return;
if (!profileSO.IsLoadedThisSession) return;   // README 계약 4
```

`AddCard` / `RemoveOccurrence` 가 `_working` 을 바꾼 직후 `PersistWorking()` 을 호출한다. 두 메서드의 `// in-memory edit; persists only via Save button (parity)` 주석은 사실이 아니게 되므로 갱신한다.

저장 호출은 테스트 seam 을 경유한다 — `FirstSessionTutorialController.cs:66` 선례:

```
[NonSerialized] internal Action<PlayerProfile> ProfileSaver = ProfileStore.Save;
```

`WireOnce` 의 `deckStrip.SaveClicked += OnSave` 구독을 제거한다.

`BuildPool` 의 숨김 카드 주석에서 근거로 쓰인 "이 페이지는 명시적 Save 계약이라" 를 갱신한다. 정책 자체는 유지 — 페이지 진입은 저장을 유발하지 않으므로(README 계약 5) 숨김 카드는 다른 편집이 일어날 때까지 디스크에 남고, 해제는 로그인 prune 담당이다.

### 덱 스트립

`SaveClicked` 이벤트, `_saveButton`, `_saveBg`, `SaveOn`/`SaveOff` 색, `saveHost` SerializeField, `EnsureBuilt` 의 Save 생성 블록과 `UiLayer.Apply(saveGo)` 를 제거한다. `Refresh` 의 `_saveButton.interactable` / `_saveBg.color` 두 줄도 함께 제거하되, **`valid` 계산과 상태 라인은 남긴다** (README 계약 6).

`saveHost` 는 `DreamcatcherDeckPage` 가 주입하므로 그쪽 주입 코드와 호스트 RectTransform 생성도 같이 걷어낸다. 클래스 헤더 주석의 `+ Save button (gated on DeckRules.Validate)` 도 갱신한다.

### 백로그 정정

`docs/spec/README.md` Follow-up Backlog 의 **"스쿼드 저장 게이트"** 항목은 "덱 빌더처럼 스쿼드도 유효할 때만 저장" 을 제안한다 — 이 spec 이 채택한 방향("항상 저장 + 출전 게이트에서 판정")과 반대다. 항목을 정정해 반대 방향으로 끌려가지 않게 한다.

### 테스트

`DreamcatcherDeckAutosaveTests` — `ProfileSaver` 를 주입하고 reflection 으로 컨트롤러를 구동한다(`_working`, `AddCard`, `RemoveOccurrence` 는 private).

1. `AddCard` 1회 → saver 1회 호출, 전달된 프로필의 선택 덱 `cardIds` 가 `_working` 과 일치
2. `RemoveOccurrence` 1회 → saver 호출, 덱 장수 감소
3. 덱이 무효(예: 정원 미달)여도 저장된다 — 게이트 제거 회귀
4. `IsLoadedThisSession` 이 false 면 saver 미호출 — 계약 4 회귀

## 완료 기준

- [ ] compile 통과, 콘솔 에러 0
- [ ] `DreamcatcherDeckAutosaveTests` 4케이스 green. 3·4번은 변경 전 코드에서 실제로 red 임을 확인
- [ ] 전체 EditMode 스위트에 신규 실패 없음 (기존 실패 `MobileBuild...CapturedProjectOrientation_IsLandscapeOnlyAutoRotation` 제외)
- [ ] Play 검증: 로비 → 드림캐쳐 → 카드 1장 교체 → 페이지 나가기 → 재진입 시 교체가 남아 있다
- [ ] Play 검증: 저장 버튼이 화면에 없고, 상태 라인은 `10/10` 및 미달 시 사유를 계속 표시한다
- [ ] Play 검증: 9/10 로 나간 뒤 START → 로드아웃 게이트 팝업이 뜨고 "드림캐쳐 덱" 버튼으로 복귀한다
