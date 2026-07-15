# 5 — 개발용 버튼 트레이 토글

## 목적

로비 우상단 dev 버튼이 5개(TESTMODE / IMPORT UNIT / IMPORT DREAMCATCHER / IMPORT ALL / RESET ACCOUNT)로 늘어 화면을 잠식한다. **토글 버튼 하나만 상시 노출**하고 나머지는 그 아래로 접는다. 기본값은 접힘.

## 변경 대상

- `Assets/_Project/Scripts/UI/DevTrayToggle.cs` (신규)
- `Assets/_Project/Tests/EditMode/DevTrayToggleTests.cs` (신규)
- `Assets/_Project/Scenes/OutgameScene.unity` (컨테이너 신설 + 6개 reparent + 토글 버튼)

## 구현

`DevTrayToggle : MonoBehaviour` — `DevButtons` 에 부착하고 content 를 접었다 편다.

```
DevButtons (DevOnlyGroup + CanvasGroup + DevTrayToggle)   ← 기존 2개는 미변경
├── DevToggleButton    y=-48                              ← 상시 노출
└── DevTrayContent     (anchor/pivot (1,1), pos 0, size 0) ← 토글 대상, 기본 비활성
    ├── TestModeButton              y=-136
    ├── StatRefreshButton           y=-224
    ├── DreamcatcherRefreshButton   y=-312
    ├── ImportAllButton             y=-400
    ├── ResetAccountButton          y=-488
    └── StatRefreshResult           y=-576
```

1. `[SerializeField] Button toggleButton` · `GameObject content` · `TMP_Text label`.
2. `Awake` 에서 접힘으로 초기화 + 리스너 등록. 씬에도 content 를 **비활성으로 직렬화**해 Awake 순서와 무관하게 기본이 접힘이 되게 한다.
3. 라벨은 **ASCII 만** — `DEV +`(접힘) / `DEV -`(펼침). 로비 폰트에 한글·기호 글리프가 없다(`StatRefreshButtonView.cs:19`).
4. 빌드 게이트는 건드리지 않는다. `DevOnlyGroup` 이 `DevButtons` 를 통째로 끄므로 릴리즈에서는 토글 버튼도 함께 사라진다.
5. `OutgameMenuController.devButtonsGroup`(패널 열릴 때 트레이 숨김)도 `DevButtons` 를 가리키므로 그대로 동작한다 — 수정하지 않는다.

`content` 가 비활성인 동안 그 안의 `StatRefreshButtonView.Awake` 는 실행되지 않는다. 펼치는 순간 실행되며 리스너를 등록하므로 동작에 영향이 없다(현재도 로그인 게이트가 `menuRoot` 를 끄고 있어 같은 경로를 탄다).

`OutgameMenuController` / `DevOnlyGroup` / `StatRefreshButtonView` 는 수정하지 않는다. 새 인터페이스·매니저·설정 SO 를 만들지 않는다.

## 완료 기준

- [x] 로비 진입 시 `DEV +` 토글 버튼 하나만 보이고 나머지 5개 + 결과 라벨은 숨겨져 있다. — Play 실측 `IsExpanded=False`, 트레이 내 활성 버튼 0, 토글만 active.
- [x] 토글 클릭 → 5개 버튼 + 결과 라벨 노출, 라벨이 `DEV -` 로 바뀐다. 다시 클릭 → 접힘 복귀. — 1회 후 버튼 5개/`DEV -`, 2회 후 0개/`DEV +`.
- [x] 펼친 상태에서 각 버튼이 정상 동작한다 (`IMPORT ALL` 클릭 → 기존 경로 완주). — 클릭 후 `RequestInFlight=True`.
- [x] 패널(스쿼드/드림캐쳐/테스트모드)을 열면 토글 버튼까지 함께 숨는다 (기존 `devButtonsGroup` 경로 유지). — `OnOpenSquad` → alpha 0 / blocksRaycasts false / GO 비활성, `OnClosePanels` → alpha 1.
- [x] 릴리즈 빌드에서 토글 버튼도 숨는다 (`DevOnlyGroup` 승계 — 코드 변경 0). — 토글이 `DevButtons` 자식이라 컨테이너 비활성에 함께 사라짐. 릴리즈 빌드 실측은 미실시.
- [x] `OutgameMenuController` / `DevOnlyGroup` / `StatRefreshButtonView` diff 0.
- [x] EditMode 테스트 green: 초기 접힘, 토글 1회 → 펼침 + 라벨, 2회 → 접힘. — 전체 820 passed / 0 failed.
- [x] compile clean, 에디터 Play 검증.

확인 2026-07-15 — 트레이 토글. 라벨은 ASCII(`DEV +`/`DEV -`) — 로비 폰트에 한글·화살표 글리프가 없다. content 는 씬에도 `m_IsActive: 0` 으로 직렬화해 `DevTrayToggle.Awake` 가 안 돌아도(릴리즈에서 `DevOnlyGroup` 이 컨테이너를 먼저 끄는 경우) 접힘이 기본이 되게 했다. 로비 캔버스가 Screen Space Overlay 라 카메라 스크린샷에는 UI 가 안 잡힌다 — 검증은 활성 상태·라벨·버튼 수를 직접 읽는 방식으로 했다.
