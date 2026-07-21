# 4 — 씬 배선 + 코너 겹침/화면비 검증

## 목적

통합 트레이가 **실기 화면비에서 다른 UI 를 덮지 않고 슬롯 드래그가 살아 있는지** 확인한다. 실측 결과 제약은 세로(맵 가림)가 아니라 **가로(하단 코너 위젯)** 다.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity`
- `Assets/_Project/Data/Config/BattleHudTrayConfig.asset` (실측 후 치수 확정)
- `Assets/_Project/Tests/EditMode/TrayCornerOverlapTests.cs` (신규)

## 최대 위험 — 하단 코너 위젯 겹침

하단 코너는 이미 세 위젯이 점유 중이고 **전부 트레이(canvas order 4)보다 위**다.

| 위젯 | 앵커·좌표 | 안쪽 모서리 | y 범위 | order | 페이즈 |
|---|---|---|---|---|---|
| 전투시작 버튼 | `(1,0)` pos`(-40,40)` `280×104` (`PlacementPhaseView.cs:283-287`) | 우측 **320** | 40~144 | 7 | Placement |
| 각성 게이지 | `(1,0)` pos`(-24,20)` `244×244` (`AwakeningGaugeView.cs:516-519`) | 우측 **268** | 20~264 | 7 | Battle |
| NextWaveDock | `(0,0)` pos`(40,40)` `250×150` +backing 10 (`NextWaveDock.cs:176-189`) | 좌측 **300** | 40~190 | 7 | — |

트레이 가장자리 = `S/2 − W/2` (S = SafeAreaRoot 폭). `W = 1324` 일 때:

| 화면비 | S | 트레이 가장자리 | vs 시작버튼(320) | vs 각성(268) | vs Dock(300) |
|---|---|---|---|---|---|
| 16:9 | 1920 | 298 | **겹침 22** | 여유 30 | **겹침 2** |
| 19.5:9 | 2340 | 508 | 여유 188 | 여유 240 | 여유 208 |
| 20:9 | 2400 | 538 | 여유 218 | 여유 270 | 여유 238 |

**시각 문제로 끝나지 않는다.** 각성 패널은 `Image(alpha 0.001) + Button` 이라 **244×244 불가시 히트영역**이고(`AwakeningGaugeView.cs:522-524`), order 7 > 4 이므로 겹치는 구간의 **슬롯은 드래그가 아예 안 된다**. 16:9 에서 7번 슬롯, 그 이하 화면비에서 6~7번이 죽는다.

### 폭 상한

```
W ≤ S − cornerReservedWidth        (cornerReservedWidth = 2 × 320 = 640)
```

구속 조건은 가장 안쪽인 시작 버튼(320)이다. 백분율 클램프(`논리폭 × 0.85`)는 **틀린 형태**다 — 코너 위젯은 고정 픽셀 오프셋(24/40/40)이라 화면이 넓어져도 그대로인데 백분율 여유만 커진다. 16:9 에서 `1920 × 0.85 = 1632` 는 실제 한계 1280 을 **352 초과**한다.

가능하면 상수 640 대신 `PlacementPhaseView.StartButtonRect` / `AwakeningGaugeView.HitRect` 를 실측해 산출한다(두 rect 모두 이미 public).

### 클램프 시 배분

축소분은 코스트 셀과 슬롯이 **같은 비율로** 나눈다. 셀만 `flexibleWidth = 0` 으로 154 를 지키면 4:3 에서 슬롯이 140 까지 줄어 **셀이 슬롯보다 넓어지고 위계가 뒤집힌다**(자원 표시가 행동 대상보다 큼). 계약: `costCellWidth ≤ slotWidth` 는 클램프 후에도 성립.

## safe area

트레이는 캔버스 직속이 아니라 **SafeAreaRoot 자식**이다(`DefenderSelector.cs:143`, `UiSafeAreaFitter.cs:44-48`). 따라서 기준은 "논리 폭"이 아니라

```
S = (safeArea.width / Screen.width) × 화면비 × 1080
```

이고, `y = 32` 도 화면 밑변이 아니라 SafeAreaRoot 밑변 기준이다.

16:9 에서 각성 여유가 30 units 뿐이라, **좌우 합계 60 논리 units(화면폭의 3.1%) 이상의 cutout inset 이면 16:9 도 겹침으로 넘어간다.** Android 랜드스케이프 cutout 에서 흔한 값이다. 실기 계측 시 `Screen.safeArea` 를 함께 로깅한다.

## 슬롯 수

`n = 7` 이 표준이지만(스쿼드 7슬롯 강제 · 드래프트 10−3), **`BattleScene.unity:3979-3986` 의 직렬화 `defenderPool` 은 8개**다. 에디터에서 BattleScene 을 직접 Play 하는 개발 워크플로에서는 8슬롯이 뜬다.

→ 트레이 폭을 상수로 박지 말고 `costCellWidth + n×slotWidth + gap + padding` 으로 **슬롯 수에서 유도**한 뒤 상한 클램프. 1324 는 `n = 7` 에서만 성립하는 값이다.

## 맵 가림 (재평가 — 위험 아님)

정적 계산: `gridSize {20,10}`·`tileSize 1`·FOV 40°·pitch 55°·`perspectiveFitMargin 1.12` → 보드 최하단 모서리가 화면 밑변에서 **약 435 논리 units**. 트레이 상단 190 대비 **245 units 여유**. 페이즈 pitch 델타(±8°)를 최대로 적용해도 414 로 여유 유지.

또한 레일이 삭제되므로 클러스터 상단은 `198 → 190` 으로 **8 units 낮아진다**(레일이 y154~198 이었다). 확대가 아니라 축소다.

→ Play 로 확인은 하되(드래그 줌아웃 knob 이 있으므로), 세로는 이 spec 의 제약이 아니다. **Battle 슬림 축소 부활은 하지 않는다** — 상쇄할 증가가 없고, `battleSize == placementSize` 는 사용자 product 결정이다(README 결정 기록).

## 배선 점검

- `DefenderSelector.costDisplay` 참조가 씬에 실제로 할당돼 있는지 확인. 비어 있으면 `AttachToTray` 가 안 불려 **코스트 셀이 통째로 사라진다**.
- `CostDisplay` GameObject 에 남은 `Canvas` / `CanvasScaler` / `GraphicRaycaster` 제거.
- 씬 인스펙터 구값 `placementSize {912,120}` / `battleSize {912,88}` 를 config 와 맞춰 둔다(혼동 방지).

> 씬 저장 주의: `SaveScene` 은 사용자의 미저장 in-memory WIP 까지 함께 베이크한다. 저장 전 `git diff` 로 무엇이 딸려가는지 확인하고 내 변경 hunk 만 스테이징한다.

## 검증 방법

**스크린샷 비교로는 코너 겹침을 못 잡는다** — 각성 히트영역은 alpha 0.001 이라 안 보이고(가시 아트 220 vs 히트 244), 시작 버튼 aura 는 `raycastTarget = false` 라 보이는 것과 먹히는 것이 다르다. 게다가 각성은 Battle 에서만, 시작 버튼은 Placement 에서만 뜨므로 한 페이즈만 봐도 놓친다.

→ **좌표 기반 단언**을 쓴다. 사각형 겹침 판정을 EditMode 순수 함수로 빼고 화면비 파라미터 테이블 테스트로 고정한다(CLAUDE.md 제약 10 (c) 회귀 가치).

1. **EditMode** — 화면비 × 슬롯 수 행렬에서 트레이 rect ∩ (StartButton / AwakeningHit / Dock) == ∅
2. **오프스크린 선튜닝** — 색·대비·폰트 크기 다회 조정
3. **Play 실측** — 최종 판정. 오프스크린은 EditMode 가 관대해 실 Play 에서만 드러나는 결함(TMP 초기화 NRE 등)을 놓친 전례가 있다

## 완료 기준

- [ ] **EditMode — 화면비 행렬(16:9 / 19.5:9 / 20:9) × 슬롯 수(7, 8)에서 트레이가 코너 위젯 3종과 겹치지 않는다**
- [ ] Play(Placement) — 트레이 우측 끝과 전투시작 버튼이 겹치지 않고, **가장 오른쪽 슬롯이 드래그된다**
- [ ] Play(Battle) — 트레이 우측 끝과 각성 게이지가 겹치지 않고, 가장 오른쪽 슬롯이 드래그된다
- [ ] Play — 좌측 끝과 NextWaveDock 이 겹치지 않는다
- [ ] 16:9 / 20:9 에서 슬롯 n개가 균등하고 셀 폭 ≤ 슬롯 폭이 유지된다
- [ ] 실기 `Screen.safeArea` 로그를 기록하고, cutout 이 있는 기기에서 위 항목을 재확인
- [ ] Placement / Battle 양쪽에서 보드 최하단이 트레이에 가리지 않는다 (여유 units 기록)
- [ ] **실기 그립 확인** — 좌/우 엄지로 첫 슬롯·마지막 슬롯을 각각 드래그하는 중에 `현재/최대` 숫자가 손에 가리지 않는가. 가리면 코스트 셀을 트레이 우측 끝 또는 상단 중앙으로 옮기는 선택지를 연다
- [ ] **사용자 Play 판정 — 물통 학습 리스크**: `10/10` 에서 1코스트 유닛을 놨을 때 물통이 가득→빔 으로 떨어지는 것이 "코스트가 다 없어졌다"로 오독되지 않는가 (unit 2 의 알려진 리스크)
- [ ] 손패 플립 후 복귀 시 레이아웃이 어긋나지 않고, 플립 중간 프레임에 구멍이 없다
- [ ] 콘솔 에러/경고 없음, EditMode 전체 통과
