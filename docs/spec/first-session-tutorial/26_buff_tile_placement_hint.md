# 26 — 효과 타일 배치 안내 (두 번째 판 Placement)

## 목적

기믹 리빌(units 23~24)이 끝나고 들어가는 **두 번째 판 배치 구간**에서, 보드 위 빛나는 타일이
공짜 버프라는 것을 한 번 알린다. 지금은 아이콘이 "무엇"을 말하지만 **"놓으면 이득"이라는 사실
자체를 배울 자리가 없다**.

이 구간은 현재 안내가 하나도 없다 — core 는 첫 판 전용, 선물 홀드는 Gift, 리빌 홀드는 Gimmick,
각성 인트로는 Battle 에 있다. 배치 카운트다운은 30초라 비차단 한 줄이 들어갈 여유가 있다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` · `TutorialProgress.cs` — 진행 토큰
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 효과 타일 월드 앵커 조회
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.EffectTile.cs` — 신설 partial
- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceStyle.cs` · `TutorialGuidanceView.cs`
  — 노출 시간 knob · 전용 마커 색 · `HasVisibleWorldMarker`
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

**표시**: 타일 **하나**에 `ShowWorldMarker` + `MessageAnchor.WorldMarker` 로 말풍선 한 개.
core 안내의 목표 비트가 쓰는 그 도구 그대로다. 3개를 다 지목하지 않는다(화면이 번잡해진다) —
"여러 종류가 있다" 는 문구가 나른다.

**마커 라벨은 `빛나는 타일`**(= 말풍선의 어휘). `버프 타일` 같은 게임 용어를 넣으면 같은 4초
동안 한 물건이 두 이름으로 불려, 이 안내가 하려던 "저게 뭔지" 연결이 도로 끊긴다.

**마커 색은 전용 `effectTileMarkerColor`(민트)다. `goalMarkerColor` 를 재사용하지 말 것** —
그 노랑은 첫 판 core 안내가 `방어 목표` 라벨과 "노란색 베이스에 닿기 전에 막아주세요" 문구로
각인시킨 색이라, 두 번째 판에서 같은 링이 다른 뜻으로 뜨면 학습된 신호가 어긋난다. 색은
`TutorialGuidanceStyle` 이 소유한다(README 계약).

**문구(사용자 확정 2026-08-02 — 임의로 고치지 말 것)**:

```
"빛나는 타일 위에 배치하면 유닛이 강해집니다!\n공격력 · 공속 · 체력회복 세 종류가 있어요."
```

지목한 타일의 구체 효과를 말하지 않고 **세 종류를 함께 소개한다**(사용자 결정) — 마커는 예시일
뿐이고, 나머지 둘도 같은 성격이라는 것이 이 안내의 요점이다. `재생` 이 아니라 **`체력회복`** 을
쓴다(사용자 결정). `EffectTileData.displayName` 은 `재생 (+1 HP/s)` 이지만 **어떤 코드도 읽지
않아** 화면에 나오지 않으므로 지금은 드리프트가 아니다 — 아래 주의점 참조.

**비차단이다.** `BeginTutorialGate` 를 부르지 않는다 — 배치 카운트다운을 잡지 않고
`effectTileHintSeconds`(SerializeField/Style, 기본 4초) 경과로 스스로 걷힌다. 첫 판의 core 안내와
달리 여기서 강제할 행동이 없다(효과 타일 위 배치는 권장이지 필수가 아니다).

**완료 저장은 실제로 문구를 띄운 경로에만** 둔다. 정리 창구는 저장하지 않는다(unit 24 계약).

**fail-open — 이 spec 에서 가장 자주 밟는 경로다.** 맵에 효과 타일이 **0개일 수 있다**:
`desert.asset` 은 `effectTiles: []` 라 그 테마가 걸린 판에는 가리킬 타일이 없다. 그럴 땐 안내를
생략하고 **토큰을 저장하지 않는다** → 효과 타일이 있는 다음 판에서 정상 노출된다. 경고가 아니라
`Debug.Log` 다(정상 플레이 경로이지 배선 사고가 아니다 — unit 20 의 웨이브 버튼 부재와 같은 판단).

**마커가 실제로 보였을 때만 저장한다.** 월드 마커는 매 프레임 `SafeAreaRoot` 안쪽인지로 표시가
갈린다(`UpdateWorldPulse`). 화면 가장자리 셀이 뽑히고 인셋이 큰 기기에서는 **꺼진 채로** 노출
시간이 지날 수 있고, 그때 저장하면 플레이어는 "빛나는 타일" 이 뭔지 못 본 채 계정당 1회를 잃는다.
노출 동안 `HasVisibleWorldMarker` 를 지켜보고 한 번도 안 보였으면 다음 판으로 미룬다.

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
`ClearWorldMarkers` 가 `WorldMarker` 앵커일 때만 `Default` 로 되돌리므로 남의 앵커를 훔치지 않는다.

## 완료 기준

- 컴파일 0 (Runtime · Tests.EditMode)
- `TutorialProgressTests`: 토큰 pending/완료/멱등 · 형제 토큰 불변 ·
  `ResetAll`·`ResetAllInJson` 양쪽에서 **이 토큰만 비0일 때도** `changed == true`
- `EffectTileAnchorTests`: 셀 기록·중복 무시 · **빈 타일 페인트는 목록에서 제거**(안 하면 마커가
  아무것도 안 보이는 셀을 가리킨다) · `Clear` 리셋 · 인덱스 경계 · 앵커를 조회 시점에 푼다
- Play 확인(두 번째 판): 리빌 홀드를 탭해 배치로 들어가면 효과 타일 하나에 마커 + 문구 2줄이
  뜨고, 4초 뒤 스스로 걷힌다. 배치 카운트다운이 **멈추지 않는다**. 콘솔 경고 0.
- Play 확인(첫 판): 배치 구간에 이 안내가 **뜨지 않는다**(core 안내만).
- Play 확인(효과 타일 0개 맵): 안내가 생략되고, 그 다음 효과 타일이 있는 판에서 뜬다.
  desert 테마 맵을 강제하거나 `forest.effectTiles` 를 임시로 비워 재현한다.

## 후속 후보

- **`displayName` 표기 드리프트 예약** — 자산은 `재생 (+1 HP/s)`, 이 안내는 `체력회복`. 지금은
  `displayName` 을 읽는 코드가 없어 충돌이 없지만, `effect-tile-icons` 후속 후보의 **툴팁/범례**가
  구현되면 화면에 둘이 동시에 뜬다. 그때 한쪽으로 통일한다.
- 안내가 지목한 타일 위에 실제로 배치했는지는 관측하지 않는다(노출만 보장).
- **`AddEffectTile` 의 그리드 경계 검사** — 같은 파일의 `SetTelegraphCells`/`SetZoneCells` 는
  `_gridSize` 를 검사하는데 효과 타일 경로는 안 한다. 현 호출자(`EffectTilePlacer`)는 항상
  유효하지만, 주석이 예고한 "후속 런타임 생성 루트(드림캐쳐/유닛 능력)" 가 생기면 보드 밖 셀이
  목록에 들어가 마커가 허공을 가리킬 수 있다. 이 spec 이 만든 결함은 아니다(코드 리뷰 LOW).
- **`TilemapMapView` 훅은 `eb3c3713`(`tune(ultimate-leap)`)에 선반영됐다** — 병행 세션이 미커밋
  편집을 함께 커밋했다. `git log -- Core/TilemapMapView.cs` 로는 이 unit 을 찾을 수 없으므로
  여기에 남긴다.
