# 2 — 인스펙트 패널 뷰

## 목적

선택된 유닛 옆에 부착 카드 1~3장을 세로 스택으로 렌더한다. 손패 드래그 툴팁의 시각 문법을 계승하되, 위젯은 신규다(그 툴팁은 슬롯 인덱스/손패 생명주기에 하드 커플링돼 재사용 불가).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectPanelView.cs` (신규, namespace `Wassup.UI`)

## 구현

### 캔버스

`UiCanvasSetup.Ensure(gameObject, sortingOrder: 9)` — SquadPrep(8) 위, MenuPopup(960) 아래. 루트는 `SafeAreaRoot` 직속. 프리팹 없음, 런타임 구축(`DreamcatcherHandView.BuildCanvas` 관례).

### SerializeField (계약 12 — 하드코딩 금지)

`TMP_FontAsset labelFont`(Jua) + 레이아웃(`panelWidth`/`pad`/`rowGap`/`artHeight`/`headerBodyGap`/`gapFromUnit`/`edgeMargin`) + 색(`fill`/`squadBorder`/`unitBorder`/`costColor`).

보더는 **타입별**(Squad 골드 / Unit 청록 — `DcIconStripSpawner` 와 같은 색 언어). 손패 툴팁의 `trayConfig.fallbackBorder` 단색은 쓰지 않는다 — Squad/Unit 구분이 정보이기 때문. 따라서 `trayConfig` 참조 없음(배선 1점 감소).

### 행 구성

행 × N (N ≤ `maxAttachPerUnit` = 3):
- **아트**: `Image`, `card.art`. `art == null` → 아트 비활성 + 플레이트만(덱빌더 `_popupArtFallback` 선례).
- **헤더** TMP: `<b>{displayName ?? id}</b>  <color=#C9A6FF>{cost}</color>` — 손패 툴팁과 동일 포맷. cost 는 컨트롤러가 `hand.CostOf(card)` 로 해석해 넘긴다(뷰는 `DreamcatcherHandController` 를 모른다).
- **본문** TMP: `DreamcatcherCardText.Body(card)`.
- 행 배경: `UiRoundedSprite.Make(12f, 2f, fill, border)`, 타입별 보더(Squad 골드 / Unit 청록 — `DcIconStripSpawner` 와 같은 색 언어).

높이는 TMP `GetPreferredValues` 기반(툴팁 선례). 패널 높이 = Σ행높이 + 간격 + 패딩.

**전 Graphic `raycastTarget = false`** — 패널이 카드 드롭/조준 판정을 가로채면 안 된다(툴팁 계약과 동일). 패널 위 press 는 `IsPointerOverGameObject` 가 아니라 컨트롤러의 픽킹 실패(빈 보드)로 처리돼 닫힌다 — 의도된 동작(패널 밖 탭 = 닫기와 같은 결과).

### 위치 (계약 6)

**`LateUpdate` 에서 추종** (앵커 `Transform` 은 `Show` 로 받아 보관 — 뷰는 bridge/Entity 를 모른다):
```
LateUpdate: if (_root == null || !_visible) return; Follow();
Follow:
  if (_anchor == null || _camera == null) { _visible = false; return; }   // 앵커 파괴 방어
  sp = _camera.WorldToScreenPoint(_anchor.position);
  if (sp.z <= 0f) { _root.SetActive(false); return; }   // 카메라 뒤 — x/y 가 반전된다
  if (!_root.activeSelf) _root.SetActive(true);          // 복귀
  rect.position = FlipAndClamp(sp);
```

**`LateUpdate` 게이트에 `_root.activeSelf` 를 넣지 말 것** — `z<=0` 경로가 루트를 끈 순간 영구 early-return 이 되어 복귀선에 도달하지 못한다. 그러면 앵커가 화면 앞으로 돌아와도 패널은 사라진 채 컨트롤러만 슬로우 lease 를 쥔다(코드리뷰 M1). 활성/비활성 소유는 `Follow` 에 둔다.

- 유닛 **우측**에 붙이고, 우측이 safe area 를 넘으면 좌측 플립(툴팁 선례).
- 세로는 상/하단 클램프(툴팁은 좌우 플립만 있음 — 3행 스택은 세로가 길어 클램프 필요).
- ScreenSpaceOverlay 캔버스라 `RectTransform.position` 에 스크린 px 직접 대입 가능(`DreamcatcherCardDragSlot.cs:303` 주석).
- **`Update` 금지** — `CameraDirector(-90)` 가 LateUpdate 에서 포즈를 확정하므로 Update 추종은 1프레임 밀려 패널이 유닛에서 미끄러진다(`DcIconStripView.cs:81~88`).

### API

- `public void Show(Transform anchor, Camera cam, IReadOnlyList<DreamcatcherCard> cards, IReadOnlyList<int> costs)` — 행 리빌드 + 표시.
- `public void Hide()` — 멱등.

**뷰는 `Entity`/`BattleBridge` 를 모른다** — 컨트롤러가 `TryGetUnitViewAnchor` 로 앵커를 해석해 넘기고, 코스트도 `hand.CostOf` 로 해석해 넘긴다. `DcIconStripSpawner`→`DcIconStripView` 와 같은 역할 분담(뷰는 `Wassup.Data` 만 안다).

페이드/스케일 인은 툴팁 `TickTooltip` 문법(`Update` 에서 lerp, 신규 트윈 라이브러리 금지). **페이드는 `Update`, 위치는 `LateUpdate`** — 알파는 카메라 포즈와 무관하다.

## TMP 함정 (실측 2026-07-15 — 되돌리지 말 것)

행 TMP 는 **비활성 루트(`_root`) 밑에서 lazy 생성**된다. 비활성 계층의 `AddComponent` 는 **Awake 를 돌리지 않으므로** TMP 가 `TMP_Settings` 기본값을 로드하지 못한다. 그 결과 두 가지가 동시에 깨졌다:

1. `textWrappingMode` 가 enum 기본값 `0`(=`NoWrap`) 으로 남는다 → 본문이 행 플레이트 밖으로 흘러나간다. (새 TMP 의 정상 기본값은 `Normal`, 손패 툴팁도 `Normal` — 툴팁은 `BuildCanvas()` 에서 **활성 상태로 미리** 만들어져 이 함정을 피했다.)
2. 폰트 스케일이 서기 전이라 `GetPreferredValues` 가 **정답의 1/10** 을 답한다(헤더 2.21 vs 22.0, 본문 6.96 vs 69.5) → 본문이 헤더 위로 겹치고, 행 높이가 전부 아트 기둥 높이(112)로 고정된다.

**대응 2점**: (a) `BuildLabel` 에서 `textWrappingMode = Normal` 명시, (b) `Show` 가 측정 **전에** `_root.SetActive(true)` + 텍스트 대입 후 `ForceMeshUpdate()`. 페이드 인 초기화는 `wasHidden` 플래그로 분리(활성화를 앞당겼으므로 `!_root.activeSelf` 체크를 재사용할 수 없다).

## 완료 기준

- compile 클린 (콘솔 에러 0).
- 직렬화 필드가 씬에 베이크되지 않도록 코드 기본값을 source 로 유지(툴팁 선례 — 씬 diff 최소화).
- 시각 검증(unit 3 Play):
  - 부착 3장 유닛 → 행 3개, 헤더(이름+코스트)/타입 라벨/effects 색상/description 이 덱빌더 팝업과 동일 내용.
  - 본문이 플레이트 안에서 접힌다(넘침 없음).
  - 행 높이가 내용에 따라 달라진다(고정 아님).

확인 2026-07-15 — Play 스크린샷 육안 확인. 수정 후 실측: `wrap=Normal`, 헤더 h=22.0, 본문 h=69.5/69.5/169.3, 행 h=126/126/225(내용 반응), 패널 (460, 492.36). 수정 전에는 헤더/본문 겹침 + 본문 플레이트 밖 넘침이 스크린샷에 그대로 찍혔다.
