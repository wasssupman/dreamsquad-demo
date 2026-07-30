# 2 — 장판 점등 (전용 zone 타일맵 + 셀 refcount)

## 목적

효과가 화면에 남게 한다. 지금은 시전 후 근거가 사라져 "왜 저 유닛이 강한지" 를 알 수 없다.
이 unit 은 **마감이 아니라 검증 질문을 성립시키는 부품**이다(README 순서 주의 참조).

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 전용 zone 타일맵 추가
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 장판 스폰/만료 시 점등 등록·회수

## 구현

1. **점등 채널 = 전용 zone 타일맵 신설**(결정 완료 — 재사용 가능한 기존 경로는 **없다**).
   해저드 존·회오리·포탈의 표현은 타일 점등이 아니라 **월드 VFX 프리팹**이다
   (`vfxSpawner` + `HazardVisualLifetime` self-destroy, `BattleBridge.SpawnHazardWithVisual`).
   신설 비용은 작다 — `EnsureEffectTilemap`/`EnsureRangeTilemap`/`EnsurePlaceableTilemap` 세 템플릿을
   복사하고, 타일은 `_tileSet.rangeTile` 재사용 + 타일맵 `color` 로 아군색(**새 에셋 0**,
   `_placeableTilemap.color` 틴트 선례).
   - **조준 프리뷰 채널(`RangeDisplayOwner.SkillAim`)은 절대 쓰지 않는다** — 단일 owner 라 다음
     조준이 장판을 지우거나 그 반대가 된다(선행 spec 에서 실제로 겪은 사고).
   - **맵 효과 타일(`_effectTilesByCell` / `_effectTilemap`)도 쓰지 않는다** — 상시 타일과 수명이 다르다.
   - **빠뜨리면 새는 등록 지점 3개**: `TilemapMapView.Clear()` · 유닛 위 소팅 갱신
     (`SetPlacementHighlightAboveUnits` 계열) · `StopBattle`/teardown 회수.

2. **셀 refcount 필수.** 장판은 동시에 여러 장 존재할 수 있다(`SpawnTornadoField` 의 "independent
   field, multiple coexist" 선례와 같은 성격). 기존 타일 채널은 전부 단일 owner set/clear
   (`ClearPlacementRange` 가 `_rangeCells` 전부 null, `SetPlacementHighlight` 는 `ClearAllTiles`)라
   그 형태를 복사하면 **먼저 만료된 장판이 겹친 칸을 지운다** — 살아 있는 장판의 발자국이 사라지고
   복구 경로가 없다. zone→cells 등록부 + 칸별 참조수로 회수한다.

3. **수명 페이드는 채널 전체로 하거나 생략한다.** `TilemapRenderer` 는 타일맵당 머티리얼/컬러 1개라
   칸별 색은 `SetTileFlags(TileFlags.None)` + `SetColor` 경로가 필요하고, 그러면 겹친 칸에서 두
   장판의 남은 시간이 매 프레임 서로를 덮어쓴다. 이번 범위에서는 **페이드 없음**으로 둔다.

4. **장판 위 아군 하이라이트는 이 unit 에서 하지 않는다**(후속 후보로 이관).
   `SetDefenderHoverHighlight` → `SpineUnitView.SetHoverHighlight` 는 refcount 가 아니라 **단일 슬롯
   저장/복원 래치**다: 장판이 켠 뒤 조준 스윕이 같은 유닛을 덮고, 스윕이 풀릴 때 **원래 색**으로
   돌아가 장판이 살아 있는데도 하이라이트가 사라진다(장판이 매 프레임 재점등하면 반대로 조준
   스윕이 죽는다). 이 API 로는 소유권 분리가 불가능하다.
   정식 경로는 프레임 재조정 채널이다 — `StatusFxKind` append + `ReconcileStatusFx` 의
   `StatModifierSlot` 스캔에 `header.origin == ModifierOrigin.Skill` 분기 추가
   (`BeginFrame`/`EndFrame` 이 꺼짐을 자동 처리, 틴트 래치와 충돌 없음). 프리팹 1개가 필요하므로
   아트가 준비되면 별 unit.

5. **VFX 는 있으면 얹고, 없어도 성립**해야 한다. 아트 의존으로 기능이 막히지 않게 한다.

## 완료 기준

- [ ] 장판이 지속시간 동안 보이고 만료 시 사라진다.
- [ ] 두 장판이 겹친 뒤 하나가 만료돼도 나머지 발자국이 온전하다(refcount 확인).
- [ ] 다음 카드 조준을 시작해도 장판 점등이 유지되고, 조준 프리뷰가 장판을 지우지 않는다.
- [ ] 맵 효과 타일과 같은 칸에서 서로를 지우지 않는다.
- [ ] `StopBattle`/재시작 후 장판 점등 잔존 0.
- [ ] 콘솔 에러/워닝 0.
