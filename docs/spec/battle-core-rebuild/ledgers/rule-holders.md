# 장부 — 브리지 밖 규칙 보유자 (unit 0 · 항목 6)

> 생성 2026-09-23. 규칙 문장 단위. 「미정」은 조각 E 진입 전 0 이어야 한다.

`BattleBridge` 만 지우면 되는 것이 아니다. 아래 11개 파일이 **판의 규칙·판정·상태**를 브리지 밖에서 들고 있다(README 계약 12). 각 행은 규칙 문장 하나이고, 반드시 함수 이름을 인용한다. 배선만 하는 메서드(규칙 없음)는 행을 만들지 않고 파일 절 끝에서 수만 센다.

**새 주인 어휘** — 담당자 8(`MatchClock`·`WaveScheduler`·`CostLedger`·`PlacementService`·`HeartMeter`·`ScoreLedger`·`HandDeck`·`GimmickHost`) · `MatchDefinitionBuilder`(SO→정의표, 판 밖) · `입력`(입력→커맨드) · `뷰`(프레젠테이션 전용 — **코어 밖의 아웃게임·앱 셸도 여기**) · `삭제`(제거 확정 기능) · `유지(예외)`(`TimeManager`) · `미정`(질문 동반).

## 요약

| 파일 | 행 | 규칙 문장 수 | 담당자 분포 |
|---|---|---|---|
| `Core/TilemapMapView.cs` | 1,608 | 20 | 뷰 20 |
| `Core/GameManager.cs` | 656 | 24 | MatchDefinitionBuilder 8 · 뷰 7 · MatchClock 2 · WaveScheduler 2 · 입력 2 · 삭제 2 · CostLedger 1 |
| `Core/Dreamcatcher/DreamcatcherHandController.cs` | 551 | 24 | HandDeck 19 · MatchDefinitionBuilder 2 · 입력 1 · 뷰 1 · 삭제 1 |
| `Core/DraftController.cs` | 252 | 9 | 삭제 8 · 뷰 1 |
| `Core/SkillLoadoutController.cs` | 143 | 7 | MatchDefinitionBuilder 6 · 삭제 1 |
| `Core/TimeControl/TimeManager.cs` | 140 | 7 | 유지(예외) 7 |
| `Core/CostRuntime.cs` | 110 | 10 | CostLedger 9 · **미정 1** |
| `Core/PlacementInput.cs` | 103 | 8 | 입력 5 · PlacementService 2 · 삭제 1 |
| `Core/SkillRuntime.cs` | 99 | 6 | HandDeck 4 · 뷰 1 · **미정 1** |
| `Core/PlacementCooldownRuntime.cs` | 97 | 7 | PlacementService 6 · 뷰 1 |
| `Core/MatchTally.cs` | 69 | 11 | MatchClock 4 · ScoreLedger 4 · HeartMeter 2 · WaveScheduler 1 |
| **합계** | **3,828** | **133** | 뷰 31 · HandDeck 23 · MatchDefinitionBuilder 16 · 삭제 13 · CostLedger 10 · PlacementService 8 · 입력 8 · 유지(예외) 7 · MatchClock 6 · ScoreLedger 4 · WaveScheduler 3 · HeartMeter 2 · **미정 2** |

규칙 아님(배선만): **67 메서드** (파일별 수는 각 절 끝).

---

## `Core/TilemapMapView.cs` — 판을 그리는 면

이 파일은 「거의 전부 뷰」다. 그런데 한 곳에서 **전투 도달 판정을 직접 부른다**(T1) — 코어 밖에 남는 유일한 도달 판정 소비처이고, 제약 13 이 요구한 「정본 진입점을 호출만」의 실제 사례다.

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| T1 | 사거리 안으로 밝히는 칸은 **전투에서 실제로 닿는 칸과 같은 자를 쓴다** — 화면이 모양을 다시 그리지 않는다 | `SetPlacementRange` → `AttackReach.InReach` | 뷰 | 도달 산식 자체는 순수 함수라 그대로 salvage(UML 머리말) · 제약 13 · **코어 밖 유일 판정 호출** |
| T2 | 사거리 링의 반지름 = 사거리 + 내 몸. **상대의 몸은 더하지 않는다** — 그건 적의 그림자가 말한다 | `SetPlacementRange` → `ShowRangeRing` | 뷰 | 그래야 「적 그림자가 링에 닿으면 사거리 안」이 판정식과 정확히 동치 |
| T3 | 칸 채움은 표준 잡몹 크기를 가정한다 — 링보다 최대 0.25칸 바깥까지 칠해지는 것을 **감수한다**(칸은 크기를 표현 못 한다) | `SetPlacementRange` | 뷰 | 칸은 배치 안내, 링은 판정 — 서로 다른 것을 말한다 |
| T4 | 조준·착탄 예고의 링 반지름은 **판정 입력의 복사본**이고 표준 상대 항을 더하지 않는다 | `SetAreaRange` | 뷰 | 형제 경로(T2)와 같은 계약 |
| T5 | 칸↔월드 정합의 권위는 `Grid` 하나다 — 셀 중심 기준(0.5) · 바닥에 눕힌 90° 회전 | `ConfigureGrid` · `CellCenterToWorld` | 뷰 | `BoardSpace.ToSim/ToView/RaycastPlane` 가 이 회전을 추종 · P4(입력)가 같은 평면을 읽는다 |
| T6 | 격자의 위치를 옮기는 곳은 **한 곳뿐**이다(스테이지 정렬) | `AlignGridTo` | 뷰 | writer 가 둘이면 프랍-논리 정렬이 조용히 깨지고 격자 기준 검증은 전부 통과한 채로 깨진다 |
| T7 | 카메라가 맞출 「판」은 렌더러 실측이 아니라 **격자 4코너**로 만든다(외곽 링·데코 제외) | `TryGetPlayfieldWorldBounds` | 뷰 | 실측을 쓰면 20×12 맵이 35×32 로 잡혀 판이 화면 중앙의 작은 조각이 된다 |
| T8 | 바닥 표시는 언제나 유닛 아래다 — 드래그 중에만 **일괄로** 위로 올라온다 | `ConfigureGrid` · `SetPlacementHighlightAboveUnits` | 뷰 | ⚠ 상승 목록에서 빠진 타일맵은 옛 값에 굳는다(궁극기 예고 선례) |
| T9 | 바닥은 그림자를 **받기만** 한다 — 드리우는 것은 유닛·프랍뿐 | `SetRendererCastShadows` · `ConfigureGrid` | 뷰 | |
| T10 | 배치 미리보기와 확정 팝은 손끝 한 칸이 아니라 **유닛이 실제로 먹을 자리 전체**를 덮는다 | `SetPlacementHover(anchor,size,valid)` · `PulsePlacementHover(anchor,size,valid)` → `FootprintMath.Cells` | 뷰 | 점유 자리의 소유자는 `PlacementService` — 뷰는 받기만 |
| T11 | 「여기 놓을 수 있나」의 표시를 바꾸는 곳은 **한 함수뿐**이고 전이에만 반응한다 | `SetPlacementRangeValidity` | 뷰 | ⚠ 순서 의존: `SetPlacementRange` 가 내부에서 `ClearPlacementRange` 를 먼저 부르므로 거기서 리셋하면 무효 영역을 훑는 동안 플래시가 연발한다 |
| T12 | 놓을 수 없을 때 사거리는 **붉어지지 않고 채도만 떨어진다** — 빨강은 자리 충돌 전용 | `RangeTintColor` · `ApplyRingTint` | 뷰 | 한 색에 「왜 안 되나」와 「어디까지 닿나」를 겹치면 무효인 동안 사거리를 못 읽는다 |
| T13 | 채움의 진하기는 링이 있느냐가 정한다(링 있으면 투명). **타일은 투명해도 계속 칠한다** — 「어느 칸이 사거리 안인가」를 묻는 소비자가 있다 | `RangeFillAlpha` · `IsPlacementRangeCell` | 뷰 | 자리 고스트가 사거리 칸을 비켜 가는 read seam · ⚠ 되돌리면 채움이 두 겹이 된다 |
| T14 | 마음(골)과 스폰 칸의 마커는 맵 정의에서 칠한다 — 마음은 목록 순회, 없으면 단일 폴백 | `PaintMarkers` | 뷰 | 원천은 `MapSnapshot.goals/spawns`(UML §3) |
| T15 | 효과 타일이 칠해진 칸 목록은 **미러**다 — 소유권은 브리지에 있고 여기는 「보이는 곳」만 | `SetEffectTile` · `TryGetEffectTileAnchor` | 뷰 | 새 소유자 = `MapRuntime.EffectTiles`(조각 C) · **중복 8** |
| T16 | 착지 예고는 배치 미리보기와 **채널을 공유하지 않는다** — 예고 중 유닛을 빼는 것이 그 스킬의 놀이라서 둘이 서로를 지우면 안 된다 | `SetTelegraphCells` · `EnsureTelegraphTilemap` | 뷰 | 동시 예고가 없어서(궁극기는 생존당 1회) refcount 를 두지 않는다 |
| T17 | 예고 타일이 저작에 없으면 폴백하되 **한 번은 시끄럽게** 알린다 — 예고가 안 뜨면 회피 불가 = 불공정 | `SetTelegraphCells` | 뷰 | 조용한 열화가 「색이 안 먹는다」로 위장했던 사례 |
| T18 | 손끝 셀의 끈적 하이라이트는 **꺼져 있다** — 다칸 유닛에서 「릴리즈하면 이 한 칸」이 거짓말이 되기 때문 | `SetPlacementStretch` | 뷰 | 되살리려면 액체가 점유 자리를 따라가야 한다 |
| T19 | 「사거리 안에 누가 있나」 마크는 **판정 결과를 받기만 한다** — 뷰는 거리 계산을 갖지 않는다 | `SetRangeTargetMarks` | 뷰 | 대상 선정은 전투 코어 · T1 과 같은 규율 |
| T20 | 모든 표시(링·마크·가이드·고스트·예고)는 맵 리빌드·판 경계에서 회수된다 — 살아남으면 다음 판에 남아 있다 | `Clear` · `ClearPlacementRange` | 뷰 | |

규칙 아님(배선만): **51 메서드** (`Ensure*Tilemap` 6 · `Ensure*/Place*/Hide*` 링·마크·팝 9 · 코루틴·플래시 4 · 도형 가이드 5 · sorting/파괴/변환 헬퍼 5 · Clear/Set 짝의 나머지 12 · 1칸 호환 오버로드 2 · 그 외 8)

---

## `Core/GameManager.cs` — 판의 입구

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| G1 | 판은 한 번에 한 국면에만 있고, 같은 국면으로 다시 들어가는 것은 무시한다 — **배치와 전투** 두 국면이 판 안의 상태다 | `SetPhase` | MatchClock | `MatchClock.phase`(UML §1) |
| G2 | 판 밖 국면(뽑기·기믹 리빌·결과 연출·결과)은 씬 흐름이다 | `SetPhase` · `GamePhase` | 뷰 | ⚠ `GamePhase` 는 `CameraDirectionConfig.breathPhases` 에 **정수로 직렬화**된다 — 값 순서를 바꾸면 같은 커밋에서 그 에셋도 옮겨야 한다 |
| G3 | 판의 시드는 판당 한 번 정해진다 — 고정 노브가 0 이 아니면 그 값, 아니면 새 난수. **맵·웨이브·기믹이 전부 여기서 갈라진다** | `EnsureMatchSeed` → `MatchSeed.GenerateRandom` | MatchDefinitionBuilder | `MatchDefinition.seed` · ⚠ 순서: 맵을 짓기 **전에** 확정돼야 한다 |
| G4 | 기믹은 판당 한 번 배정된다 — 같은 시드면 같은 기믹, 스위치가 꺼졌거나 풀이 비면 기믹 없는 판, 판 안 재시작은 처음 배정을 유지 | `AssignGimmick` → `GimmickSelection.PickIndex` · `MatchSeed.DeriveGimmickSeed` | MatchDefinitionBuilder | 선택은 정의표가, 부착은 `GimmickHost` 가 · ⚠ 순서: 시드 → 기믹 → 맵 |
| G5 | 판에 들어가는 길은 우선순위가 있다 — 테스트 모드 > 저장한 편성 | `Start` | MatchDefinitionBuilder | |
| G6 | 편성이 없으면 **뽑기로 떨어진다** | `Start` | 삭제 | 계약 9 「뽑기 폴백 진입」 제거 · 로비 게이트가 이미 막고 있어 남은 도달 경로는 게이트 우회뿐 |
| G7 | 편성 반입은 저장된 그대로다 — **랜덤 채움 없음**, 카탈로그가 못 찾는 id 는 그 슬롯만 빠진다 | `StartSquadMatch` · `ResolveSquadDefenders` → `SquadDraw.Resolve` | MatchDefinitionBuilder | |
| G8 | 편성이 유닛 0으로 풀리면 뽑기로 떨어진다 | `StartSquadMatch` | 삭제 | G6 과 같은 통로 |
| G9 | 장착한 돌 중 **코스트 계열이 아닌 것만** 유닛 버프로 들어간다 | `ResolveEquippedStones` | MatchDefinitionBuilder | |
| G10 | 코스트 계열 돌은 코스트가 차는 속도 배율(1 + 합계%/100)이 되고, **판에 들어갈 때만** 설정된다 | `ResolveCostRateMultiplier` → `CostRuntime.SetRegenRateMultiplier` | CostLedger | ⚠ 배치 진입마다 도는 초기화가 이 값을 건드리면 판 안 재시작이 플레이어의 돌 버프를 조용히 지운다 · **중복 4** |
| G11 | 온보딩 판은 쉬운 저작 웨이브로 돌고 첫 손패가 정해져 있다 — 판정 소비처를 늘리지 않으려고 **여기서** 둘 다 밀어 넣는다 | `StartSquadMatch` → `FirstRunTutorialConfig.ShouldRun` | MatchDefinitionBuilder | 온보딩 콘텐츠는 철거됐고 도구만 남아 있다 — 이식 대상인지 `rules.md` 에서 재판정 |
| G12 | 보너스 당김 억제 스위치는 **조건 밖에서 무조건** 설정한다 — 조건 안에 두면 온보딩 다음 판이 켜진 값을 물려받는다 | `StartSquadMatch` → `SetBonusPullSuppressed` | WaveScheduler | 순서 의존: 분기 앞 |
| G13 | 테스트 모드는 뽑기·편성을 건너뛰고 저작 웨이브 + 저장 편성(비면 프리셋)으로 배치에 들어가며, 그 문맥은 **한 번만** 소비된다 | `StartTestModeMatch` → `TestModeContext.Clear` | MatchDefinitionBuilder | |
| G14 | 스킬은 유닛과 독립이다 — 판마다 새로 굴린다 | `StartSquadMatch` · `StartTestModeMatch` → `skillLoadout.Roll` | MatchDefinitionBuilder | **중복 5**(세 번째 호출처는 `DraftController.BeginDraft`) |
| G15 | 「한 판 해봤다」로 세는 기준은 **히스토리에 남는 판**이다 — 판당 한 번(래치), 종료 통로는 결과 진입과 나가기 둘, 이번 세션에 읽은 프로필일 때만 저장 | `RecordMatchPlayed` | 뷰 | 코어 밖(아웃게임) · 나가기도 0점으로 마감돼 히스토리에 남기 때문 |
| G16 | 조준 모드는 서로 배타다 — **마지막 클릭이 이긴다**(각 구독자가 자기 상태를 지운다) | `RaiseAimCanceled` · `IsAiming` · `SelectedDefender` | 입력 | **중복 11** |
| G17 | 앱 전역: 60프레임 고정 + 수직동기 끔 · 세로 1080 캡에 기기 가로비 유지 · 트윈 풀 400 예약 | `ApplyFrameRateCap` · `Awake` · `ReserveTweenCapacity` | 뷰 | 앱 셸 — 코어 밖 · 가로비를 고정하면 모든 오브젝트가 가로로 찌그러진다 |
| G18 | 탭과 드래그를 가르는 거리는 화면 밀도에 맞춰 올린다 — **절대 낮추지 않는다** | `CalibrateDragThreshold` | 입력 | 기본 10px 는 고DPI 에서 0.6mm 라 탭이 드래그로 오인식된다 |
| G19 | 판의 수명은 씬의 수명이다 — 로비로 나가면 통째로 파괴돼 다음 판이 깨끗하게 시작한다 | `Awake` · `OnDestroy` | 뷰 | 계약 12 「매니저를 두지 않는다」 — 새 코어에서 판 수명은 `BattleMatch` 가 든다 |
| G20 | 씬이 꺼지면 전투를 멈추고 기록 세션을 닫는다 | `OnDisable` | MatchClock | 로그 종료는 뷰 |
| G21 | 토너먼트 참가는 **로비 게이트가 발행한 것만 채택**한다 — 에디터 직접 진입은 상태만 리셋되고 참가가 생기지 않는다 | `OnEnable` → `TournamentMatchReporter.BeginMatch` | 뷰 | 아웃게임 |
| G22 | 반입 시점의 편성·돌을 **미리** 기록에 적어 둔다 — 배치 전에 앱이 죽어도 그 판이 편성 없이 마감되지 않게 | `PersistTournamentDeckSnapshot` | 뷰 | **중복 6** |
| G23 | 카탈로그가 못 찾는 돌 id 는 **버리지 않고 id 만 기록**한다 — 4개 장착한 판이 2개로 남지 않게 | `LogDreamstoneCarryIn` | 뷰 | 유닛의 raw id 정책과 같은 계약 |
| G24 | 일시정지 메뉴의 웨이브 브리핑은 그 판의 실제 웨이브를 다시 만들어 보여준다(만들 수 없으면 마지막 것 유지) | `BuildBriefingWavePlan` | WaveScheduler | 읽기 모델 |

규칙 아님(배선만): **3 메서드** (`RequestPlacement` · `LogSquadCarryIn` · `LogSkillLoadout`)

---

## `Core/Dreamcatcher/DreamcatcherHandController.cs` — 각성과 손패

`HandDeck` 과 가장 크게 겹친다. 19행이 통째로 그쪽으로 간다.

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| D1 | 덱은 **배치에 들어갈 때마다 새로 구성한다**(캐시 없음) | `OnPhaseChanged` · `BuildDeck` | HandDeck | 순서 의존: 덱 구성 → 부착 등록부 비움 → 게이지 초기화 → 손패 리셋 → 기록 |
| D2 | 덱 = 저장한 부착 카드 10 + 이 판에 굴린 공용 액티브 2 = 12장. 섞는 것은 **한 곳뿐**이고 시드는 판의 시드 하나 | `BuildDeck` → `DreamcatcherCycleDeck` 생성자 | HandDeck | |
| D3 | 저장한 덱이 없거나 검증에 실패하면 부착 덱은 **비어 있다** — 기본 덱 폴백은 없앴다 | `ResolveAttachDeck` → `DeckRules.Validate` | MatchDefinitionBuilder | 기본 덱이 모든 배치 유닛을 상시 버프하던 미의도 동작이 강화 오라 오작동의 근본 원인이었다 |
| D4 | 굴린 스킬을 감싸는 액티브 카드가 없으면 경고하고 **그 장만 빠진다** — 손패와 순환은 장수를 모른다 | `AppendActiveCards` · `FindActiveCard` | MatchDefinitionBuilder | |
| D5 | 온보딩 첫 손패는 **덱에 실제로 든 카드만** 앞으로 끌어온다 — 없는 카드를 끼우면 저장 덱을 조작하는 셈 | `PinTutorialFirstHand` · `SetTutorialFirstHand` | HandDeck | 저작 목록은 정의표 · 밀어 넣는 쪽은 G11 |
| D6 | 각성 게이지는 상한을 넘은 만큼 **소멸**하고(그 손실을 화면에 알린다), 판 시작값은 저작값을 상한으로 자른다 | `GainAwakening` · `OnPhaseChanged` | HandDeck | |
| D7 | 각성은 **처치와 사망의 보상**이다 — 퇴근·배치 취소·적 소멸에는 주지 않는다 | `OnDefenderDied` · `OnDefenderRetired` · `OnDeploymentCancelled` · `OnEnemyGone` | HandDeck | 주면 배치→퇴근 반복이 게이지 파밍이 된다 |
| D8 | 카드를 얹고 있던 유닛이 판을 떠나면(사망·퇴근·배치 취소·적 소멸) 그 카드는 전부 큐로 돌아간다 — 기본은 **맨 뒤**(떠난 순서 = 돌아오는 순서) | `RecoverCardsHostedBy` | HandDeck | **중복 1** — `HandDeck.Recover` |
| D9 | 예외 하나: **퇴근**이고 그 유닛에 「인수인계」가 붙어 있었으면 나머지가 **부착 순서 그대로 큐 맨 앞**으로 온다(선언한 카드 자신은 맨 뒤) | `RecoverCardsHostedBy` · `DeclaresRetireRecall` · `CompareByAttachSeq` | HandDeck | 앞 삽입은 손패 창을 밀어내는 유일한 연산이라 **플레이어가 누른** 퇴근에만 붙인다 — 사망 경로에 얹지 말 것 · **중복 1** |
| D10 | 「인수인계」 판정 세 조건(유닛 카드 · 앞당김 페이로드 · 퇴근 트리거)은 브리지의 부착 판정과 **완전히 같아야** 한다 | `DeclaresRetireRecall` ↔ `BattleBridge.ApplyDreamcatcherCardToUnit` | HandDeck | 한쪽만 넓히면 「붙는데 무효」 또는 「검증 없이 발동」 · 계약 8 의 정적 표가 이 이중 판정을 **한 표**로 접는다 · **중복 2** |
| D11 | 부착 결과 규약: 실패(차감 없음) / 회수 불필요(유닛과 함께 사라짐) / 회수 필요(유닛이 죽으면 되돌림) 셋 | `CommitAttach` · `RecoverCardsHostedBy` | HandDeck | 새 코어에선 `Binding.InstanceId` 가 이 자리를 대신한다(UML §5) |
| D12 | 부착은 **먼저 적용하고 나서 값을 치른다** — 실패한 부착은 차감도 순환도 하지 않는다 | `CommitAttach` · `AttachAndSpend` | HandDeck | |
| D13 | 유닛 하나에 붙일 수 있는 카드 수는 한정(기본 3)이고 **유닛 카드와 편성 카드가 함께 센다** | `AtAttachCap` · `CountAttachedTo` · `CanAttachMore` | HandDeck | |
| D14 | 적에게 붙이는 표식에는 그 상한을 **적용하지 않는다** — 표식은 적당 1개라는 자기 상한이 있고, 부착 상한은 방어유닛 슬롯 개념 | `CommitMarkEnemy` → `bridge.ApplyBountyMark` | HandDeck | 적당 1개 판정은 브리지 preflight → 코어 `BindingRegistry` |
| D15 | 카드를 쓸 수 있는 조건 = 손패에 있고 각성이 그 카드 값 이상. 값은 **카드 종류가 정한다** | `CanUse` · `CostOf` · `TryGetUsable*` | HandDeck | |
| D16 | 액티브 카드는 부착 경로로 못 가고, 부착 카드는 시전 경로로 못 간다 | `TryGetUsableAttach` · `TryGetUsableActive` | HandDeck | |
| D17 | 액티브는 시전에 성공해야 값을 치르고 **덱 뒤로 재활용**되며, 부착 카드는 **풀에서 이탈**한다(돌아올 때만 복귀) | `CommitActiveTile` · `CommitActivePortal` · `SpendAndRecycle` · `AttachAndSpend` | HandDeck | |
| D18 | 값 지불은 각성에서 깎고 0 밑으로 내려가지 않는다 | `Spend` | HandDeck | |
| D19 | 「몽마의 계약」 카드는 유출 허용치를 **선불**로 먹는다 — 남은 허용치가 1 미만이 되면 붙일 수 없고, 한 번 치른 값은 유닛이 죽어도 돌아오지 않는다 | `CommitAttach` → `bridge.RemainingLeakAllowance` · `TryPayLeakAllowance` | 삭제 | 계약 9 「유출 한도·몽마의 계약」 제거 · 카드 문안(`DreamcatcherCardText`)도 같은 시점에 따라간다 |
| D20 | 부착 목록을 읽을 때는 **부착 번호 오름차순**으로 준다 — 사전 순회 순서는 제거가 섞이면 보장이 없다 | `GetAttachments` | HandDeck | 결정론(계약 5) |
| D21 | 표식을 떨어뜨릴 적은 손끝에서 일정 반경 안에서 고른다 | `EnemyPickRadiusTiles` | 입력 | 값은 저작 노브 |
| D22 | 덱이 확정되면 「고른 덱」과 「액티브 포함 최종 덱」을 **둘 다** 남기고 대기 기록을 갱신한다 — 이전 세션 마감이 읽는 유일한 출처 | `LogDeck` → `TournamentMatchReporter.PersistMatchDeck` | 뷰 | 아웃게임 기록 · **중복 6** |
| D23 | 손패는 **정지도 감속도 걸지 않는다** — 각성 손패는 실시간이 계약이고, 감속은 손패 화면의 몫 | 클래스 계약(`OnEnable` 머리말) | HandDeck | 감속은 `BattleDriver` 의 틱 발행률로(계약 5) — 미정 2 와 같은 축 |
| D24 | 부착한 **순서 자체가 기능**이다 — 인수인계가 그 순서를 그대로 손패에 싣는다 | `AttachAndSpend` · `CompareByAttachSeq` | HandDeck | 회수가 「맨 뒤로 몰아넣기」이던 시절엔 순서가 안 보였다 |

규칙 아님(배선만): **5 메서드** (`OnEnable` · `OnDisable` · `Hand` · `FindActiveCard` · `CountAttachedTo`)

---

## `Core/DraftController.cs` — 뽑기(제거 확정)

계약 9 의 「뽑기 폴백 진입」이 이 파일 전체다. 살아남는 행은 배치 국면으로 넘어가는 **순서 계약** 하나뿐이고, 그건 편성 경로에 이미 같은 모양으로 있다.

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| R1 | 뽑기 풀 = 기본 3 + 메타 2 + 에고 1 + 컬렉션 4 = 10장, 3장 버리고 7장 반입 | 상수 · `PoolSize` · `PickCount` · `DiscardCount` | 삭제 | |
| R2 | 슬롯 검증: 기본 3 · 메타 2 · 에고 필수 · 고정 슬롯 중복 금지 · 컬렉션은 고정 유닛을 뺀 유니크 후보가 4 이상 | `ValidateSlots` · `AddFixedUnit(s)` | 삭제 | |
| R3 | 뽑기 시드는 벽시계와 공용 난수를 섞어 만든다 — 같은 틱에 연속으로 들어가도 갈리게 | `GenerateSeed` | 삭제 | |
| R4 | 「새 뽑기」에 들어갈 때 스킬을 새로 굴린다 — 판 안 재시작은 이 길을 안 타서 이전 스킬을 유지한다 | `BeginDraft` | 삭제 | 스킬 롤 규칙 자체는 편성 경로에 남는다(G14) · **중복 5** |
| R5 | 확정은 고른 장수가 다 차야 성립한다(모자라면 아무 일도 안 일어난다) | `TryConfirm` | 삭제 | |
| R6 | 뽑기로 시작한 판은 편성 돌을 **하나도** 반입하지 않는다 — 유닛 버프도, 코스트 배율도 초기값으로 | `TryConfirm` → `SetDreamstones(null)` · `SetRegenRateMultiplier(1f)` | 삭제 | 코스트 배율을 건드리는 세 곳 중 하나가 사라진다 · **중복 4** |
| R7 | 스킬은 굴린 결과를 쓰고, 굴리는 쪽이 없을 때만 인스펙터 배열로 폴백 | `TryConfirm` | 삭제 | |
| R8 | 확정은 **전투를 바로 시작하지 않는다** — 배치 국면 화면이 카운트다운을 돌린 뒤 시작시킨다 | `TryConfirm` · `DraftConfirmed` | 뷰 | 편성 경로의 `PlacementRequested` 와 동형 — 이 순서 계약은 새 코어에서도 유지 |
| R9 | 확정 전에 풀·고른 것·버린 것·시드를 먼저 기록한다 — 즉시 패배로 세션이 잘려도 뽑기가 남게 | `TryConfirm` → `logger.SetDraft` | 삭제 | |

규칙 아님(배선만): **3 메서드** (`ToggleDiscard` · `HasSlotConfiguration` · `AddFixedUnits`)

---

## `Core/SkillLoadoutController.cs` — 판마다 굴리는 액티브 2장

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| S1 | 판마다 스킬 풀에서 정해진 수(2)를 **시드 기반 부분 셔플**로 뽑는다 — 같은 시드면 같은 결과 | `Roll` | MatchDefinitionBuilder | |
| S2 | 시드가 0이면 벽시계로 새로 만들고 홀수로 맞춘다 | `Roll` | MatchDefinitionBuilder | ⚠ 이식 시 **판의 시드에서 파생**시켜야 한다 — 그래야 「같은 modeId+seed 로 판이 끝까지 돈다」(검증 질문)가 액티브 2장까지 포함한다 |
| S3 | 풀이 비었거나 뽑을 수가 0 이하면 결과는 빈 목록이되 「굴렸다」로 표시한다 | `Roll` | MatchDefinitionBuilder | |
| S4 | 숨긴 카드는 풀에서 뺀다 — 그 스킬을 감싸는 액티브 카드가 있고 **전부** 숨김일 때만. 감싸는 카드가 없으면 남긴다 | `FilterHiddenSkills` | MatchDefinitionBuilder | 순수 함수(제약 10) — 그대로 salvage |
| S5 | 거르는 것은 뽑기가 아니라 **풀 자체**다 — 기록에 남는 풀과 실제 뽑기 대상이 같아야 한다 | `Configure` · `FilterHiddenSkills` | MatchDefinitionBuilder | |
| S6 | 판 안 재시작은 다시 굴리지 않는다(같은 조건 재도전) · 다시 뽑기는 새 시드로 굴린다 | `ResetRollState` | MatchDefinitionBuilder | 「다시 뽑기」는 뽑기와 함께 사라진다 · 「재시작」 축은 **미정 1** |
| S7 | 인스펙터 풀이 비면 에디터가 스킬 폴더 전량을 폴백 풀로 채운다 | `PopulateEditorFallbackPool` | 삭제 | 에디터 전용 · 계약 6 「값의 정본은 판 밖」 |

규칙 아님(배선만): **2 메서드** (`Awake` · `Configure(SkillData[])`)

---

## `Core/TimeControl/TimeManager.cs` — 의도된 예외

7행 모두 `유지(예외)`. 다만 **판의 시간만은 빠져나간다**(M7) — 새 코어에서 판의 델타는 `BattleDriver` 의 틱 발행률이 되고, 여기엔 화면·연출 시간만 남는다.

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| M1 | 시간 스케일의 단일 소유자 — 전역 시간배율은 **항상 1** 로 둔다 | 클래스 계약 · `DeltaTime` | 유지(예외) | 제약 5 의 명시적 예외 |
| M2 | 한 영역에 요청이 여럿이면 우선도 높은 쪽이 이기고, 같으면 **더 느린 쪽**이 이긴다 | `ScaleOf` | 유지(예외) | |
| M3 | 요청이 없으면 배속 1 | `ScaleOf` | 유지(예외) | |
| M4 | 요청 해제는 몇 번을 해도 안전하다 — 번호를 재사용하지 않기 때문 | `Release` · `TimeLease.Dispose` | 유지(예외) | |
| M5 | 실제로 배속이 바뀔 때만 알린다 | `RaiseIfChanged` | 유지(예외) | |
| M6 | 판 경계에서 모든 요청을 비운다(고아 요청 누수 방지) | `ResetAll` | 유지(예외) | |
| M7 | 델타의 원천은 **한 줄**이 정한다 — 고정 스텝 하네스면 스텝, 아니면 벽시계 | `DeltaTime` | 유지(예외) | 소비처를 하나씩 고치면 하나 빠뜨렸을 때 「대부분 결정론」이 되고 그건 결정론이 아니다 · 새 코어에선 판의 시간이 이 줄을 떠난다(계약 5) · **중복 10** |

규칙 아님(배선만): **1 메서드** (생성자)

---

## `Core/CostRuntime.cs` — 코스트

`CostLedger` 와 통째로 겹친다.

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| C1 | 코스트는 시작값에서 출발해 최대치까지 초당 일정 속도로 찬다(배율이 곱해진다) — 최대치에서 멈춘다 | `Tick` · `Configure` | CostLedger | **중복 3** — `CostLedger.regen` |
| C2 | 차는 것에는 스위치가 있다 — 배치 진입에서 초기화(꺼짐) · 전투 진입에서 켬 · 결과/정리에서 끔 | `ResetToStart` · `BeginRegen` · `StopRegen` | CostLedger | 순서 의존: 세 호출의 순서가 곧 국면 순서 |
| C3 | 시작값은 0~최대치로 자르고, 최대치는 최소 1, 차는 속도는 음수 불가 | `Configure` · `ResetToStart` | CostLedger | |
| C4 | 모자라면 **지불이 거부되고 행동 자체가 일어나지 않는다**. 0 이하 금액은 항상 성공 | `TrySpend` · `CanAfford` | CostLedger | **중복 3** — `CostLedger.TryPay` |
| C5 | 화면에 보이는 수는 **내림**이다 — 판정은 실수로 한다 | `CurrentInt` | CostLedger | 「9.9인데 10짜리를 못 놓는다」의 근거 · 이식 시 실수 유지 |
| C6 | 되돌려 주는 코스트는 최대치를 넘지 않는다 | `RefundSpend` · `AddCost` | CostLedger | |
| C7 | 돌 버프 배율은 **초기화가 절대 건드리지 않는다** — 배치 진입마다 도는 초기화가 손대면 판 안 재시작이 플레이어의 돌 버프를 조용히 지운다 | `SetRegenRateMultiplier` | CostLedger | 설정 호출처는 판 진입 2곳 + 뽑기 확정 1곳(삭제 예정) · **중복 4** |
| C8 | 코스트는 **배치를 막는 판 상태**다 — 판의 시계 밖에서 화면 프레임을 따라 자라면 같은 시점의 같은 입력이 두 판에서 다른 판정을 받는다 | `Update` · `Tick` | CostLedger | 새 코어에선 `CostLedger.Step` 이 틱 파이프라인 안 — `Update` 가 통째로 사라진다 · **중복 10** |
| C9 | 코스트는 판의 시계를 따른다 — 메뉴로 멈추면 차지 않고, 드래그 감속에선 비례해 느려진다 | `Update` → `TimeManager.DeltaTime(Battle)` | CostLedger | 틱 발행률로 자동 성립(계약 5) |
| C10 | **판 안 재시작**(같은 씬에서 판을 다시 돌림)이 새 코어에 있는가 — C7·S6·K5 가 전부 이 기능을 전제로 쓰여 있다 | `SetRegenRateMultiplier` 계약 주석 · `BattleBridge.OnRestartRequested`(현재 미구독) | **미정** | 미정 1 — 아래 「미정 목록」 |

규칙 아님(배선만): **0 메서드**

---

## `Core/PlacementInput.cs` — 탭 배치(대부분 은퇴)

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| P1 | 탭으로 놓는 배치는 **은퇴했다** — 배치는 드래그-드롭 전용이고 이 경로는 기본으로 꺼져 있다 | `Update` 첫 가드 · `SetClickPlacementEnabled` | 삭제 | 드래그 시작 시 다시 끄는 호출(`DefenderDragPlacementController.Configure`)도 함께 사라진다 |
| P2 | 조준 중에는 배치 입력을 받지 않는다 — 이 처리가 시전 처리보다 **먼저** 돌아, 한 번의 탭이 스킬과 배치를 둘 다 먹는 경합을 막는다 | `Update` · `[DefaultExecutionOrder(-50)]` | 입력 | 이 순서 계약의 현재 상속자는 `DcInspectController`(같은 -50) · **중복 11** |
| P3 | 손가락이 UI 위에 있으면 배치로 치지 않는다 | `Update` → `EventSystem.IsPointerOverGameObject` | 입력 | 타일이 버튼 뒤에 있으면 탭이 통과해 배치돼 버린다 |
| P4 | 손가락이 짚은 곳은 **판의 평면**으로 되돌린 뒤 칸으로 바꾼다 | `Update` → `BoardSpace.RaycastPlane` · `BoardSpace.ToSim` · `bridge.DebugWorldToCell` | 입력 | 칸 변환의 권위는 격자(T5) · **중복 12** |
| P5 | 코스트가 모자라면 배치 시도 자체를 거부하고 빨갛게 튕긴다 | `Update` → `costRuntime.CanAfford` · `bridge.FlashPlacementReject` | PlacementService | 새 코어에선 커맨드 영수증의 거절 사유가 이 분기를 대신한다(계약 7) |
| P6 | 코스트는 **배치가 성사된 뒤에** 깎는다 — 실패하면 차감 없이 거절 | `Update` | PlacementService | 순서 의존: 성공 판정 → 차감 · **중복 3** |
| P7 | 고른 유닛이 없으면 아무 일도 하지 않는다 — 옛 랜덤 배치 폴백은 제거됐다 | `Update` | 입력 | |
| P8 | 맵과 타일 크기는 브리지가 **한 곳에서** 주입한다 | `Initialize` | 입력 | 새 코어에선 `MapSnapshot` 을 뷰와 입력이 함께 읽는다 |

규칙 아님(배선만): **1 메서드** (`Start`)

---

## `Core/SkillRuntime.cs` — 스킬 쿨다운

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| K1 | 스킬은 남은 쿨다운이 없을 때만 쓸 수 있다 — 기록 자체가 없으면 준비된 것 | `IsReady` | HandDeck | |
| K2 | 시전이 성사되면 쿨다운을 저작값만큼 다시 채운다. **준비 확인은 호출부 책임**이다(준비→시전은 두 쪽 거래) | `Consume` | HandDeck | 순서 의존 · 브리지 시전 경로 2곳이 확인·커밋을 짝으로 부른다 |
| K3 | 「쿨다운 감소」 효과는 **모든 스킬에 일괄** 적용되고 0 이하가 된 것은 지워진다 | `ReduceAllCooldowns` | HandDeck | `SkillIntent.ReduceSkillCooldown` 의 소비처 — 새 코어의 `MetaIntent(쿨다운)`(UML §5) |
| K4 | 남은 비율은 저작 쿨다운 대비이고, 쿨다운 0짜리 스킬은 항상 0 | `GetRemainingNormalized` | 뷰 | 읽기 모델(레이디얼) |
| K5 | 전투 시작·판 안 재시작·다시 뽑기에서 **전량 소거** — 옛 쿨다운이 판을 넘지 않게 | `ResetAll` | HandDeck | **중복 7** · 「재시작」 축은 **미정 1** |
| K6 | 쿨다운은 **벽시계**로 흐른다 — 판의 시계로 옮기는 것은 「감속하면 쿨다운도 느려진다」는 **별개의 게임 결정**이라 의도적으로 안 옮겼다 | `Update` · `Tick` | **미정** | 미정 2 — 아래 「미정 목록」 |

규칙 아님(배선만): **1 메서드** (`GetRemainingSeconds`)

---

## `Core/PlacementCooldownRuntime.cs` — 재배치 대기

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| L1 | 유닛을 놓으면 **그 유닛 종류**에 재배치 대기가 걸린다 — 같은 종류를 다시 놓으면 처음부터 다시 | `StartCooldown` | PlacementService | |
| L2 | 대기 시간이 0인 유닛은 **등록조차 안 된다**, 그리고 대기 중인 유닛이 하나도 없으면 아무것도 돌지 않는다 | `StartCooldown` · `Update` · `AnyActive` | PlacementService | 「0 = 없는 것과 같다」 |
| L3 | 놓을 수 있는가 = 남은 시간이 0 이하인가 | `IsReady` · `RemainingFor` | PlacementService | |
| L4 | 배치 진입과 판 정리에서 전부 지운다 | `ResetAll` | PlacementService | **중복 7** |
| L5 | 배치를 **취소**하면 그 배치가 건 대기만 지운다 — 같은 자리를 사망·퇴근 대기가 공유하므로 무조건 지우면 남의 것을 지운다. 구분은 「건 순간의 길이」로 하는 **근사**다 | `ClearCooldownUpTo` | PlacementService | 오식별해도 비용은 대기 하나가 일찍 풀리는 것뿐 · 새 `PlacementService.cooldowns` 는 대기의 **출처**를 키에 넣을 수 있어 이 근사가 없어질 자리 — 조각 A 설계 입력 |
| L6 | 대기는 판의 시계를 따른다 — 감속하면 느려지고 메뉴로 멈추면 언다. 이것도 **배치를 막는 판 상태**다 | `Update` · `Tick` | PlacementService | `CostRuntime` 와 같은 계약(C8) · **중복 10** |
| L7 | 남은 비율 1→0 은 아이콘 위 레이디얼용(없거나 길이가 0이면 0) | `Fraction` | 뷰 | 읽기 모델 |

규칙 아님(배선만): **0 메서드**

---

## `Core/MatchTally.cs` — 판이 끝난 시점의 성적

이미 엔진을 참조하지 않는 순수 값이라 **그대로 salvage 된다**(계약 4). 조립 지점은 `IMatchGoal.BuildOutcome` → `MatchOutcome`(UML §1)으로 옮기고, 재료는 각 담당자가 든다.

| # | 규칙 문장 (게임 언어) | 코드 포인터(함수) | 새 주인 | 비고 |
|---|---|---|---|---|
| Y1 | 결과 라벨은 셋뿐이다 — 시간 완주 · 유저 제출 · 스트레스 100. **승패를 담는 자리는 없다**(자리를 남기면 조용히 되살아난다) | `MatchTally.Outcome` | MatchClock | 종료 통로의 소유자 |
| Y2 | 라벨이 셋이라고 판이 끝나는 길이 셋인 것은 아니다 — 규칙상의 길은 둘이고 제출은 절차 밖 탈출구다. **UI 가 제출을 「게임을 끝내는 방법」으로 승격시키지 않는다** | `Outcome` 계약 주석 | MatchClock | 새 모드에서도 통로는 그대로이고 **의미만 목표가 붙인다**(`match-mode-design.md` 계약 2) · 제출 어휘는 그 모드에만 |
| Y3 | 점수 = 잡은 마리 수. **1킬 = 1점이고 예외가 없다** — 보스도 분열체도 1 | `Kills` · `Total` | ScoreLedger | 티어 가중 축은 은퇴 |
| Y4 | 흘려보낸 적은 점수에 들어가지 않는다 — 「못 잡은 적 = 못 번 점수」가 유일한 페널티다 | `Kills` 계약 주석 | ScoreLedger | 그쪽은 처치 사건을 내지 않는다 |
| Y5 | 서버에 올리는 수는 **총점 그대로**다 — 가공이 없다 | `SubmissionScore` | ScoreLedger | 남은 안정도를 값에 실어 동점을 가르던 인코딩은 폐기 · unit 0 항목 8(v1 제출은 한 모드만) |
| Y6 | 음수 처치는 0으로 자른다 | 생성자 | ScoreLedger | |
| Y7 | 도달 웨이브 = 마지막으로 큐에 올린 웨이브 번호 | `WaveReached` | WaveScheduler | |
| Y8 | 마음의 남은 안정도와 최대치는 판이 끝난 시점의 값이다 | `Stability` · `StabilityMax` | HeartMeter | 마음 체력은 `HeartMeter` 가 든다(UML §1) |
| Y9 | 「놓쳤다」 = **돌격형이 마음을 치고 산화한 수**다. 옛 뜻(부서진 마음으로 적이 흘러듦)은 첫 붕괴에 판이 끝나므로 구조적으로 발생하지 않는다. ⚠ 화면에 「유출」이라 쓰면 거짓말이고, 점수와는 무관하다 | `Leaks` | HeartMeter | 공성형은 마음 앞에서 아직 잡을 수 있으므로 놓친 것이 아니다 |
| Y10 | 성적의 **조립 지점은 하나뿐**이다 — 예전엔 재료 다섯이 흩어져 있고 종료 경로 다섯이 각자 조립해 한 곳만 빠뜨려도 조용히 어긋났다 | 생성자 ↔ `BattleBridge.BuildTally` | MatchClock | 새 조립 지점 = `IMatchGoal.BuildOutcome` · 담당자들은 재료만 읽힌다 |
| Y11 | 성적은 아키텍처를 모르는 **순수 값**이다 | 클래스 선언 | MatchClock | 계약 4 — 그대로 salvage |

규칙 아님(배선만): **0 메서드**

---

## 미정 목록

조각 E 진입 전에 0 이 되어야 한다. 둘 다 **플레이어가 겪는 규칙**을 바꾸므로 에이전트가 정하지 않는다(CLAUDE.md 워크플로우 0).

| # | 행 | 질문 | 왜 지금 물어야 하나 |
|---|---|---|---|
| 미정 1 | C10 (`CostRuntime.SetRegenRateMultiplier` 계약 주석 · `BattleBridge.OnRestartRequested`) | **판 안 재시작이 새 코어에 있는가?** 같은 씬에서 판을 처음부터 다시 돌리는 기능. 현재 구현은 **잠들어 있다**(`OnRestartRequested` 가 아무 데서도 구독되지 않는다). | 세 계약이 이 기능을 전제로 쓰여 있다 — 코스트 배율 소유 계약(C7, 「재시작이 돌 버프를 지우면 안 된다」) · 스킬 재굴림 계약(S6, 「재시작은 같은 조건 재도전」) · 쿨다운 소거 시점(K5). 재시작이 없으면 **셋 다 근거가 사라지고** C7 의 「초기화는 절대 건드리지 마라」라는 미묘한 금칙을 새 코어로 옮길 이유가 없다. 있으면 `BattleMatch.Begin` 의 재진입 계약을 조각 A 에서 함께 설계해야 한다. |
| 미정 2 | K6 (`SkillRuntime.Update` · `Tick`) | **스킬 쿨다운이 감속·정지에 같이 느려져야 하는가?** 지금은 벽시계라 드래그 감속 중에도, 메뉴로 멈춘 동안에도 계속 돈다. 새 코어는 모든 것이 고정 틱이라(계약 5) **가만두면 자동으로 판의 시계로 옮겨간다.** | 그 이관은 원 구현이 「별개의 게임 결정」이라며 **의도적으로 미룬 것**이라 자동으로 넘기면 규칙이 조용히 바뀐다. 같은 축의 이웃 계약이 이미 명시적이다 — 각성 손패는 「실시간이 계약」(D23)이고, 코스트·재배치 대기는 반대로 「판의 시계를 따른다」(C9·L6). 셋 중 쿨다운만 답이 없다. |

## 중복 목록

같은 규칙이 두 곳 이상에 살아 있는 것. 새 코어는 **한 곳만** 남긴다.

| # | 규칙 | 지금 사는 곳 | 새 코어의 한 곳 |
|---|---|---|---|
| 1 | 떠난 유닛의 카드 회수 · 인수인계 앞당김 | `DreamcatcherHandController.RecoverCardsHostedBy` ↔ `DreamcatcherCycleDeck.Recover`/`RecoverToFront` | `HandDeck.Recover` / `HandDeck.RecallOthersToFront` |
| 2 | 「인수인계」인가의 판정 | `DreamcatcherHandController.DeclaresRetireRecall` ↔ `BattleBridge.ApplyDreamcatcherCardToUnit`(부착 화이트리스트) | 계약 8 의 정적 (트리거,페이로드)→(concrete,형) 표 **한 장** |
| 3 | 코스트 지불 · 모자람 거절 | `CostRuntime.TrySpend`/`CanAfford` ↔ `PlacementInput.Update` 선검사 ↔ 브리지 배치 경로 | 커맨드 → `Receipt.RejectReason`(계약 7) |
| 4 | 돌 코스트 배율 설정 | `GameManager.ResolveCostRateMultiplier`(2 호출처) ↔ `DraftController.TryConfirm`(1) ↔ `CostRuntime.SetRegenRateMultiplier` | `MatchDefinitionBuilder` 가 값을 굳혀 `CostLedger` 에 실어 보낸다(호출처 0) |
| 5 | 판마다 스킬 굴리기 | `GameManager.StartSquadMatch` ↔ `GameManager.StartTestModeMatch` ↔ `DraftController.BeginDraft` | `MatchDefinitionBuilder.Build` 안 한 곳 |
| 6 | 대기 기록에 덱 쓰기 | `GameManager.PersistTournamentDeckSnapshot` ↔ `DreamcatcherHandController.LogDeck` (같은 `TournamentMatchReporter.PersistMatchDeck`) | 뷰(아웃게임) — 한 호출처로 접는다 |
| 7 | 판 경계에서 쿨다운 전량 소거 | `SkillRuntime.ResetAll` ↔ `PlacementCooldownRuntime.ResetAll` ↔ 브리지 정리 2곳 | 담당자가 각자 자기 `Step`/`Begin` 에서 — 「판 경계」를 부르는 한 함수를 만들지 않는다(계약 12) |
| 8 | 효과 타일이 칠해진 칸 | `TilemapMapView._effectTileCells`(미러) ↔ `BattleBridge._effectTilesByCell`(소유) | `MapRuntime.EffectTiles` 하나 · 뷰는 이벤트만 받는다 |
| 9 | 전투 도달 판정 | `TilemapMapView.SetPlacementRange` → `AttackReach.InReach` (코어 밖 유일 호출) | 순수 함수 `AttackReach` 를 뷰가 **호출만** — 제약 13 대로 자를 새로 만들지 않는다 |
| 10 | 「판의 시계인가 화면 시계인가」 게이트 | `CostRuntime.Update` · `PlacementCooldownRuntime.Update` · `SkillRuntime.Update` 가 각자 하네스 여부를 본다 | 셋 다 틱 파이프라인 단계가 되어 **게이트 자체가 사라진다**(계약 5) |
| 11 | 조준 모드 상호 배타 | `GameManager.IsAiming`/`AimCanceled` ↔ `PlacementInput.Update` 가드 ↔ `DcInspectController`(-50 순서 상속) | `BattleInput` 한 곳 — 마지막 입력이 이긴다 |
| 12 | 칸↔월드 변환 | `TilemapMapView.CellCenterToWorld`/`Grid` ↔ `BoardSpace.ToSim`/`ToView`/`RaycastPlane` ↔ `PlacementInput.Update` | 격자가 권위, `BoardSpace` 가 유일 진입점 — 뷰·입력 둘 다 그것만 부른다 |
