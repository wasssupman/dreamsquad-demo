# 26 — 효과 타일 배치 안내 (두 번째 판 Placement)

## 목적

기믹 리빌(units 23~24)이 끝나고 들어가는 **두 번째 판 배치 구간**에서, 보드 위 빛나는 타일이
공짜 버프라는 것을 한 번 알린다. 지금은 아이콘이 "무엇"을 말하지만 **"놓으면 이득"이라는 사실
자체를 배울 자리가 없다**.

이 구간은 현재 안내가 하나도 없다 — core 는 첫 판 전용, 선물 홀드는 Gift, 리빌 홀드는 Gimmick,
각성 인트로는 Battle 에 있다. 배치 카운트다운(30초)은 안내 동안 게이트로 잡으므로 플레이어의 배치 시간을 먹지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` · `TutorialProgress.cs` — 진행 토큰
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 효과 타일 월드 앵커 조회
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.EffectTile.cs` — 신설 partial
- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceStyle.cs` · `TutorialGuidanceView.cs`
  — 전용 마커 색 · `HasVisibleWorldMarker` (표시 기구는 전부 기존 것을 쓴다)
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs` · `EffectTileAnchorTests.cs`(신설)

**씬 배선 없음** — `mapView`·`guidance`·`placementView` 는 BattleScene 에 이미 배선돼 있다(실측).

## 구현

**게이트는 둘이다**: `!_awakeningLockedThisMatch`(= 첫 판이 아니다) + 자기 토큰
`ShouldRunEffectTileHint`. **리빌 안내 완료를 체인하지 않는다** — unit 23 이 세운 계약
(선행 안내가 fail-open 경로를 타면 뒤 안내가 영영 발화하지 못한다). 순서는 페이즈가 이미
보장한다(`Gift → Gimmick → Placement`).

`_awakeningLockedThisMatch` 는 `OnPlacementReady` 초입에서 이미 판정된다. **`IsCorePending` 으로
첫 판을 가르려 하지 말 것** — units 19~20 과 같은 함정이다.

**진입은 `OnPlacementReady`**. 컨트롤러가 이미 구독하고 있으므로 신규 구독이 없다. core 안내가
발동하는 판(첫 판)에서는 위 게이트가 막으므로 두 안내가 겹치지 않는다.

**대상 좌표는 뷰가 준다.** `TilemapMapView` 에 효과 타일 셀의 월드 앵커를 돌려주는 조회를
추가한다(`SetEffectTile` 이 칠할 때 셀을 기억하고 `Clear`/`Initialize` 에서 비운다 —
`_spawnVisualAnchorsWorld` 와 같은 형태). **`BattleBridge._effectTilesByCell` 을 열지 않는다**:
컨트롤러에 `bridge` 참조를 새로 넣으면 BattleScene 배선이 필요하고, "어디에 빛나는 타일이
보이나" 는 뷰 지식이다.

**표시**: 타일 **하나**에 `ShowWorldMarker` + 말풍선 한 개. core 안내의 목표 비트가 쓰는 그
도구 그대로다. 3개를 다 지목하지 않는다(화면이 번잡해진다) — "여러 종류가 있다" 는 문구가 나른다.

**겹침은 두 장치가 함께 막는다(둘 중 하나만으로는 부족하다 — 실제로 두 번 겹쳤다).**

1. **전용 앵커** `MessageAnchor.EffectTile` / `effectTileHintMessageTopOffset`(기본 120) 으로
   말풍선을 카운트다운 플레이트 바로 아래까지 끌어올린다. 기본(184)·`WorldMarker`(320)는 둘 다
   보드 위라 링과 겹쳤다.
2. **화면상 가장 아래 효과 타일을 고른다**(`TryPickFarthestFromMessage`). 말풍선은 상단 고정인데
   링은 보드 어디든 뽑히므로, index 0 을 그대로 쓰면 상단 타일이 뽑힐 때 오프셋을 아무리 올려도
   겹친다. 카메라 뒤로 투영되는 후보는 제외한다.

값 조정은 `TutorialGuidanceStyle` 에셋에서 코드 없이 한다.

**마커에 라벨을 달지 않는다**(2026-08-02 화면 확인). `ShowWorldMarker` 의 라벨 플레이트는
링 위에 붙는데, 보드 상단 타일이 뽑히면 그게 상단 말풍선의 둘째 줄을 파고들어 글자를 가린다.
게다가 말풍선이 이미 "빛나는 타일" 이라고 부르므로 라벨은 같은 말을 두 번 하는 것이라 지울 때
잃는 정보가 없다. core 안내의 마커(`적 등장`·`방어 목표`)가 라벨을 다는 건 옆에 설명 문장이
없어서다 — **그 선례를 복붙하지 말 것.**

**마커 색은 전용 `effectTileMarkerColor`(민트)다. `goalMarkerColor` 를 재사용하지 말 것** —
그 노랑은 첫 판 core 안내가 `방어 목표` 라벨과 "노란색 베이스에 닿기 전에 막아주세요" 문구로
각인시킨 색이라, 두 번째 판에서 같은 링이 다른 뜻으로 뜨면 학습된 신호가 어긋난다. 색은
`TutorialGuidanceStyle` 이 소유한다(README 계약).

**문구(사용자 확정 2026-08-02 — 임의로 고치지 말 것)**:

```
"빛나는 타일 위에 배치하면 유닛이 강해집니다!\n칼=공격력 · 번개=공속 · 하트=체력회복"
```

지목한 타일의 구체 효과를 말하지 않고 **세 종류를 함께 소개한다**(사용자 결정) — 마커는 예시일
뿐이고, 나머지 둘도 같은 성격이라는 것이 이 안내의 요점이다. `재생` 이 아니라 **`체력회복`** 을
쓴다(사용자 결정). `EffectTileData.displayName` 은 `재생 (+1 HP/s)` 이지만 **어떤 코드도 읽지
않아** 화면에 나오지 않으므로 지금은 드리프트가 아니다 — 아래 후속 후보 참조.

**둘째 줄은 아이콘 모양 ↔ 효과 페어다**(사용자 결정 2026-08-02). 타일 위 글리프가 곧 그 효과라는
매핑을 문장이 직접 가르치므로, 색을 학습하지 않아도 처음 보는 타일을 읽을 수 있다 — 이건
`effect-tile-icons` 가 아이콘을 기하 기호에서 효과 그림으로 바꾼 이유와 같은 목적이다.
**매핑 출처는 그 spec 의 계약 1** (칼=공격력 +25% · 번개=공속 +20% · 하트=재생 +1 HP/s).
아이콘을 재저작하면 이 문구도 함께 고쳐야 한다 — 코드 주석에도 같은 경고를 남긴다.

실제 아이콘 **이미지**를 문장에 넣는 것은 이 unit 의 범위가 아니다: TMP Sprite Asset 이 필요한데
프로젝트에 하나도 없고(`<sprite=>` 사용처 0), 아이콘 PNG 로 에셋을 새로 저작해 말풍선 TMP 에
물려야 한다 — 문구 변경이 아니라 저작 작업이다(후속 후보).

**탭으로 넘긴다**(사용자 결정 2026-08-02 — 시간 소멸은 짧았다). **신규 기구를 만들지 않는다**:
`guidance.SetTapToContinue` + `ContinueTapped` + 본체 `OnContinueTapped` 우선순위 체인은 클래스
안내·스트레스 안내가 이미 쓰는 그대로다. 탭 소비자가 **셋**이 되므로 그 체인에 분기를 더한다
(셋은 실제로 배타적이다 — 클래스=1판 배치 · 스트레스=1판 전투 · 이 스텝=2판 이후 배치).

**카운트다운을 잡는다.** 탭 캐처가 배치 입력을 막는 동안 30초가 계속 흐르면 안내가 플레이어의
배치 시간을 먹는다. 기존 `PlacementPhaseView.BeginTutorialGate`/`EndTutorialGate` 를 그대로
쓴다 — 카운트다운 소유권은 그쪽에 남고 튜토리얼은 게이트만 요청한다(unit 2 계약).

**폴백은 `classHintFallbackSeconds`(12초)를 공유한다.** 같은 페이즈에서 같은 캐처가 만드는 같은
위험(탭 유실 → 배치 입력이 막힌 채 판이 진행 불가)이라 별도 knob 을 두지 않는다.

**완료 저장은 실제로 문구를 띄운 경로에만** 둔다. 정리 창구는 저장하지 않는다(unit 24 계약).

**fail-open — 이 spec 에서 가장 자주 밟는 경로다.** 맵에 효과 타일이 **0개일 수 있다**:
`desert.asset` 은 `effectTiles: []` 라 그 테마가 걸린 판에는 가리킬 타일이 없다. 그럴 땐 안내를
생략하고 **토큰을 저장하지 않는다** → 효과 타일이 있는 다음 판에서 정상 노출된다. 경고가 아니라
`Debug.Log` 다(정상 플레이 경로이지 배선 사고가 아니다 — unit 20 의 웨이브 버튼 부재와 같은 판단).

**마커가 실제로 보였을 때만 저장한다.** 월드 마커는 매 프레임 `SafeAreaRoot` 안쪽인지로 표시가
갈린다(`UpdateWorldPulse`). 화면 가장자리 셀이 뽑히고 인셋이 큰 기기에서는 **꺼진 채로** 대기가
지날 수 있고, 그때 저장하면 플레이어는 "빛나는 타일" 이 뭔지 못 본 채 계정당 1회를 잃는다.
대기 동안 `HasVisibleWorldMarker` 를 지켜보고 한 번도 안 보였으면 다음 판으로 미룬다.

**캐처와 게이트는 정리 창구 하나가 해제한다.** 잔류하면 배치 입력이 막힌 채 `배치 연습` 이
무기한 떠서 그 판을 플레이할 수 없다 — 클래스 안내가 같은 모양의 위험을 폴백으로 막은 것과
같은 이유이며, 이탈 경로를 늘릴 때 그 함수를 타는지 확인할 것.

**진입 시 한 프레임 양보하는 것은 카메라 대기가 아니다.** 월드 마커는 일회성 투영이 아니라
`TutorialGuidanceView.Update` 가 매 프레임 `WorldToScreenPoint` 를 다시 돌리므로, 카메라가 pitch 를
트윈해도 마커는 스스로 따라간다. 양보의 이유는 `OnPlacementReady` 호출 스택에서 빠져나와 같은
이벤트의 다른 구독자가 자기 UI 를 세울 틈을 주는 것뿐이다. **"카메라가 자리 잡을 때까지" 라는
근거를 복붙하지 말 것.**

**정리 단일 창구** `StopEffectTileHint`. **코루틴 중단과 UI 원복의 조건이 다르다** — 표시 전
양보 구간에 이탈하면 걷을 UI 는 없지만 코루틴은 죽여야 하므로, `_effectTileRoutine`(중단)과
`_effectTileHintActive`(원복)를 따로 본다. 후자는 형제 파일과 같은 의미("내가 guidance 에
무언가를 세웠다")여야 하므로 **실제 표시 직전에** 세운다. 호출처 3곳 — 정상 종료 ·
`OnPhaseChanged`(**분기 앞에서 무조건** — Battle 분기가 아래에서 조기 return 한다) · `OnDisable`.
앵커는 `Default` 를 쓰므로 원복할 것이 없다(`ClearWorldMarkers` 의 자동 원복은 `WorldMarker`
앵커 전용이다). 원복 대상은 **캐처와 게이트**다 — 위 항목 참조.

## 완료 기준

- 컴파일 0 (Runtime · Tests.EditMode)
- `TutorialProgressTests`: 토큰 pending/완료/멱등 · 형제 토큰 불변 ·
  `ResetAll`·`ResetAllInJson` 양쪽에서 **이 토큰만 비0일 때도** `changed == true`
- `EffectTileAnchorTests`: 셀 기록·중복 무시 · **빈 타일 페인트는 목록에서 제거**(안 하면 마커가
  아무것도 안 보이는 셀을 가리킨다) · `Clear` 리셋 · 인덱스 경계 · 앵커를 조회 시점에 푼다
- Play 확인(두 번째 판): 리빌 홀드를 탭해 배치로 들어가면 효과 타일 하나에 링 + 문구 2줄이
  뜨고 **탭할 때까지 유지된다**. 그동안 카운트다운은 `배치 연습` 으로 멈춘다. 탭하면 링·말풍선·
  dim 이 걷히고 카운트다운이 재개되며 배치가 정상 동작한다.
  **말풍선과 링이 겹치지 않는다**(문구 두 줄이 온전히 읽힌다).  콘솔 경고 0.
- Play 확인(첫 판): 배치 구간에 이 안내가 **뜨지 않는다**(core 안내만).
- Play 확인(효과 타일 0개 맵): 안내가 생략되고, 그 다음 효과 타일이 있는 판에서 뜬다.
  desert 테마 맵을 강제하거나 `forest.effectTiles` 를 임시로 비워 재현한다.

## 후속 후보

- **`displayName` 표기 드리프트 예약** — 자산은 `재생 (+1 HP/s)`, 이 안내는 `체력회복`. 지금은
  `displayName` 을 읽는 코드가 없어 충돌이 없지만, `effect-tile-icons` 후속 후보의 **툴팁/범례**가
  구현되면 화면에 둘이 동시에 뜬다. 그때 한쪽으로 통일한다.
- 안내가 지목한 타일 위에 실제로 배치했는지는 관측하지 않는다(노출만 보장).
- **아이콘 이미지를 문장에 인라인** — 지금은 `칼`·`번개`·`하트` 라는 **모양 이름**으로 페어링한다.
  실제 글리프를 넣으면 대조가 즉각적이지만 TMP Sprite Asset 저작이 선행돼야 한다(프로젝트에 아직
  하나도 없음). 같은 에셋이 생기면 `effect-tile-icons` 의 툴팁/범례 후속과 함께 쓸 수 있다.
- **`AddEffectTile` 의 그리드 경계 검사** — 같은 파일의 `SetTelegraphCells`/`SetZoneCells` 는
  `_gridSize` 를 검사하는데 효과 타일 경로는 안 한다. 현 호출자(`EffectTilePlacer`)는 항상
  유효하지만, 주석이 예고한 "후속 런타임 생성 루트(드림캐쳐/유닛 능력)" 가 생기면 보드 밖 셀이
  목록에 들어가 마커가 허공을 가리킬 수 있다. 이 spec 이 만든 결함은 아니다(코드 리뷰 LOW).
- **`TilemapMapView` 훅은 `eb3c3713`(`tune(ultimate-leap)`)에 선반영됐다** — 병행 세션이 미커밋
  편집을 함께 커밋했다. `git log -- Core/TilemapMapView.cs` 로는 이 unit 을 찾을 수 없으므로
  여기에 남긴다.

**확인 완료 2026-08-02** — 사용자 Play 확인. EditMode 1815 중 1813 통과(실패 0 · skip 2 는 기존
`[Ignore]`). 겹침은 두 번의 화면 확인을 거쳐 잡았다: ① 마커 라벨 플레이트가 말풍선 둘째 줄을
가림 → 라벨 제거, ② 링 자체가 말풍선 밴드에 들어옴 → 전용 앵커(120) + 가장 아래 타일 선택.
