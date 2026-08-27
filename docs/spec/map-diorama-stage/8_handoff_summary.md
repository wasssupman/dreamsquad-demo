# 8 — Handoff Summary (2026-08-19)

## Commit

브랜치 `feature/map-diorama-stage` (main 팁 기반, behind 0). 주요 커밋:
`7c7bf005`(설계+spec) → `27d383b5`(critic 반영) → `c05d1993`(unit 0) → `d8b82ed1`(unit 1+2 코드) → `6e60af32`(unit 2 에셋/씬) → `1caffef2`(unit 3) → `d41b5c11`+`a60bf3c5`(unit 4) → US-004b/5 커밋들 → unit 7(68파일 은퇴) + 아키텍트 정정.

## Implemented

- 디오라마 스테이지 파이프라인 전면 교체: `MapStage`+마커 저작 → `MapStageScanner`/`DioramaMapBuilder`(순수) → `GeneratedMap` 무변경 합성(열림=Walk/차단=Deco, placeMask 직접 조립)
- 브리지 스테이지 경로: 폴백 리니어·시드 커빙 은퇴, 연결성 실패 = 하드 실패, `AlignGridTo` 단일 grid writer, 스테이지 수명 = `TeardownGeneratedMap`
- 바닥 페인팅/절차 프랍 은퇴 · 오버레이 7채널 존치 · `BoardSortOrder` 폭 종속 stride + 대역 4000
- 골 균열/붕괴/앵커 = 마커 뷰 훅(브리지 등록부), 튜토리얼 브리지 앵커 교체
- e2e 스테이지 이관: `MapSlot` 포트, KayKit 스테이지 9종(픽스처·파일럿 포함), 덱/플랜 짝 승계, 이격 배치 정규화
- 구 파이프라인 68파일 은퇴 (후계 매핑 기록)
- unit 9 (`07ebea0c`): `BonusSpawnMarker` → `GeneratedMap.bonusSpawns` 투영, 규칙은 main `BonusSpawnAuthoringRules` 재사용. Duel(6,1)/(6,6)·DuelClassic(10,3)/(10,8) 저작 — main 의 보너스 웨이브가 스테이지에서 살아난다

## Verified

- EditMode 두 lane **2397 그린** · PlayMode 스모크 Passed · PlayMode 전체 148 pass / 18 잔존(분류 기록) · 아키텍트 **APPROVED**(정정 6건 반영)

## Notes (되돌리면 안 되는 의도)

- 계약 11: 공성·본능·적 마음·Env **비가용** — 병합 = StructureMarker 후속까지 공성 기능 부재 (사용자 결정 필요, 아키텍트 지적)
- `grid.transform` writer 는 `AlignGridTo` 하나 — Initialize **앞** 호출 순서 불변
- BattleScene 커밋에 타 spec 발 재직렬화 churn 포함(NextWaveDock orphan 키 정규화 — 무해, 기록됨)
- Ralph 러너 2종(`RalphTestRunner`/`RalphEditorTasks`)은 검증 채널로 잔존 — 삭제 판단은 사용자

## Merge (2026-08-21)

`e6129466` 에서 origin/main(87커밋)을 병합. 충돌 5 + 수선 2(TryGetGoalViewAnchor 재지향 · 카메라 상한 ≤23×12 를 StagePoolBuildabilityTests 로 이식). 검증: 컴파일 0 에러 · EditMode 2396 중 1 실패(malphite 텍스트 폭 — main 상속, 바이트 동일 실증) · PlayMode 168 중 13 실패 **전수 분류: 머지 유발 0** — 기존 US-007 잔존 9 · main 상속 1(DragPlacementReach: ResolveFocusAndTarget 3→2인자인데 main 이 테스트 미수정) · 순서 의존 2(단독 green) · 환경 누수 1(`dev_forceMapIndex` PlayerPrefs 잔존 → 비-pin 테스트가 dev 맵에서 돎, 키 제거 후 green). 사용자 BattleScene 실험(FluidBackdrop off + Hello 배치)과 ProjectSettings 는 stash 보관.

2차 병합 `4d780e25` (main +18: instinct-wreck·spawn-point-visual·카메라 셰이크 등). 충돌 1
(BoardSortOrder — 우리 RowStride·대역 4000 + main 잔해 상수 병기). 검증: dotnet 0 에러 ·
EditMode 2413 중 1 실패(malphite — 동일 main 상속) · 스모크 Passed · 풀 PlayMode 168 중
16 실패 = 기존 US-007 잔존 9 + main 상속 1(DragPlacementReach) + 순서 의존 6(전부 단독
green 실증 — suite 구성이 바뀌면 오염 패턴이 이동) + MovementIntegrity 는 오버라이드 제거로
해소 유지. **머지 유발 0.** 포탈 스폰 프랍·본능 잔해는 structures 휴면이라 스테이지 경로
비활성(StructureMarker 후속에서 활성화).

3차 병합 `277121d8` (main +61: bonus-wave-pull·heart-stress-axis·battle-sim-extraction M0·duel-route-tours(자체 철회) 등). 충돌 7 — 은퇴 파이프라인 위의 main 수정 6건 삭제 유지 + TilemapMapView 의 heart-stress 골 틴트를 `GoalMarker.SetStressTint` 로 이식(렌더러 캐시 승계) · 브리지 `SetGoalStressTint`/`MarkGoalCollapsed` 2곳 마커 재지향. **남는 기능 격차 = `GeneratedMap.bonusSpawns` 스테이지 저작 부재 → unit 9 제안**(스테이지 맵에서 보너스 버튼 미등장, 크래시 없음). dotnet 4어셈블리 0 에러(Ralph csproj 를 글롭 기반으로 전환 — 이후 병합에서 파일 주입 불필요). Unity 검증: 컴파일 0 에러 · EditMode 2506 통과 / 3 실패 — malphite(main 상속) · `LegacyTraceV0Tests.StoredCorpus`(**CRLF 환경**: 골든 코퍼스가 index LF·작업본 CRLF 로 체크아웃되고 파서가 `lines[0]=="LTV0"` 엄격 비교 — main 몫: trim 또는 `.gitattributes eol=lf`) · `DreamcatcherCardAssetTextTests`(boomerang — 테스트·에셋 main 과 바이트 동일 = 상속) · 스모크 Passed. 머지 유발 0.

## US-007 결론 (2026-08-25)

unit 9 트리 풀 PlayMode 178 중 162 통과 / 16 실패. 실패 전수 원인 규명 — **머지·unit 9 유발 0**:
- **+1.2% 오염의 정체 = 장착 드림스톤 반입.** GameManager 테스트 모드 경로(직접 BattleScene 진입, `GameManager.cs` ~L475)가
  `StartSquadMatch` 를 미러해 `profileSO.profile.CommittedSquad()` 의 스톤을 `SetDreamstones` 로 반입한다(dreamstone-loadout
  unit 3 — 제품상 의도). 이 머신의 커밋 스쿼드 = 1.2% 스톤 4개(Stone_016/032/048/064) → 스톤 0 을 가정한 테스트에
  ×1.012 가 붙는다(PlacementAura 1.012 · DreamcatcherEffect 0.87→0.859 · AttachRequirement 1.10→1.112). profile.json 을
  비켜도 재현 — `profileSO` 가 에디터 메모리에 프로필을 들고 있어서다. 예전의 «순서 의존»은 프로필/로그인 상태가 런 중
  언제 채워지느냐의 차이였다. **수선 위치 = 테스트 하네스(main 몫)**: PlayMode 셋업에서 `SetDreamstones(빈)` 또는
  프로필 픽스처 주입.
- Auth E2E = dev 서버 닉네임 중복(서버 상태) · DeckInfoPreset = 그 하류 NRE · DragPlacementReach = main 시그니처 미갱신 상속 ·
  나머지(DragCancelZone·DropDismount·FlyingEnemy·SceneTransition·SlimeSplit·Tween·ExecutionStrike 0.43)는 병합 전 18건에
  있던 기존 항목 — ExecutionStrike 는 프로필 반입(스쿼드 디펜더/스톤) 의심.
- `dev_forceMapIndex` PlayerPrefs 가 다시 생겨 있었다(값 0 = 라이브 Duel 이라 이번엔 무해). 스테퍼 사용 후 OFF 습관 필요.

## Follow-up

- **사용자**: 육안 검증 축 5종(spec 5) · OutgameScene dev 패널 `pool` 수동 배선+저장 · 공성 부재 병합 판단 · push 승인
- **US-007(병합 게이트)**: 순서 의존 10(기제 미확인) · SceneTransition(브랜치 씬 편집 의심) · 환경 의심 4 · FlyingEnemy/SlimeSplit/Tween — main 기준선 대조
- spec 후속 후보: 접근 C · 물 영역 · LOS · 웨이브 재밸런스 · 라이브 맵 재저작

## units 10~12 (2026-08-26) — 본능 마커 · Duel 재저작 · 레거시 은퇴

- **원인 발견**: 풀 정리 후 dev 3맵이 `deck null` → BattleScene 레거시 폴백 `WaveA`(생성기 v2·컨셉 0) 로 돌았다. 라이브 3맵 + `Deck_Duel` 짝, 폴백 덱 `Deck_Duel`, 등록 버튼 = `entries[0].deck` 상속(`EditorRegisterDevStage_InheritsLiveDefaultDeck`). 보너스 포탈은 세 맵 모두 미저작 → `AuthorBonusPortals` 로 (15,1)/(15,9)·(15,1)/(15,7). `Portal` 헬퍼가 `gridOriginLocal` 을 안 더하던 버그 수정(원점 밀린 Subway/StreetDay 에서 한 칸 어긋남).
- **unit 10 `StructureMarker`**: 브리지 `SpawnStructureEntities/Views` 의 `docStructures = null` 자리에 `_stageStructures`(스캔, (y,x) 사전순, 맵 수명). Core 는 빌더가 «계약 11» 거부. EditMode 6 케이스.
- **unit 11 `MapStage_Duel`** (`Art/Theme/duel/`): main 현행 `MapDocument_Duel` 23×10 디코드 — 분리대 x=11 {0,3~6,9} · 골 (2,4) · 스폰 (20,3)/(20,5) · 본능 Guard (4,2)/(4,7) · Watch (18,2)/(18,7) · 포탈 (11,2)/(11,7) · 적 진영 BlockZone (17,0) 6×10 · 마음 자리 = BlockZone 1 + 장식. 생성기 `MapStageDuelGenerator`(Street 에셋 placeholder, 볼륨 프로파일 `Scenes/BattleScene/Duel.asset` = Street 복사). 풀 `entries[0]`. **DuelClassic(21×12 강·벽)은 옛 스냅샷이라 폐기.**
- **unit 12**: `Prefabs/Maps` 11종 삭제(Fixture 포함) · `MapStageDummyGenerator` → `MapStageAuthoringTools`(KayKit 의존 제거) · PlayMode `DefaultMap` Serpent→**Street** · `DioramaStagePlayTests` Duel 재지정(+본능 4기 스폰 단언) · Coil/Zig/Tutorial/MovementLab pin 테스트 Ignore(사유 명시) · `SiegeDevSlot` Values("Duel").
- **원격 육안 검증 도구**: `MapStageCameraFraming.RenderPrefabPreview` — 프리뷰 씬 + Battle 카메라 포즈 + 논리 셀 오버레이 PNG(러너 태스크 `preview_duel`/`preview_street`). MCP 없이 스테이지 정렬을 이미지로 확인하는 경로.
- **검증**: dotnet 0 에러 · EditMode 두 lane 2512 통과 / 3 실패(기존 셋: LegacyTrace CRLF·Dreamcatcher boomerang·UnitKitCatalog) · PlayMode 3그룹 1차 13 통과 / 3 실패 → PrimeTween 언로드 로그(스위트 관례 `ignoreFailingMessages`)·`WaypointRoutingLiveTest` SetUp 의 MovementLab pin(DefaultMap 으로) 수정 후 재실행 → **4 통과 / 0 실패 / 4 Skip(사유 명시)**. 기본판 Street 재지정 영향 4파일(DreamcatcherGate·OnPlaceStun·OnPlaceTaunt·Whirlpot): 11 통과 / 3 실패 — `FlyingEnemy_IsNotTaunted`(기존 실패 목록) · `Stun_FreezesEnemiesInRange_ButNotOutside`(«entity does not exist») · `Whirlpot_WalksIn_ThenEngages`(최소접근 2.98타일). 뒤 두 건은 **Serpent 판형 가정**(원점 (0,0)부터 배치칸 스캔 → Street 에선 골 (1,5) 옆이라 더미가 즉시 골 도달 / 첫 배치칸 +5타일 텔레포트 → Street 흐름장이 그 칸을 비껴감)이 깨진 테스트 하네스 이슈 → **ⓐ 로 해소**(사용자 결정): `BattleBridgeTestAccess` 에 흐름장 헬퍼(`TryGetFlowField`·`DistToGoal`·`FlowPathFrom`), Stun 은 골 거리 내림차순 배치칸 + 워커 칸 골 거리 ≥8, Whirlpot 은 스폰0→골 경로 옆 배치 + 경로 5칸 상류 출발. 재실행 13/14 — 남은 1 = `FlyingEnemy_IsNotTaunted`(기존).
- **critic 리뷰(2026-08-26, 방법론)** → REVISE 3건 반영: ⓐ 회귀 가드 부재 → `StagePoolBuildabilityTests` 라이브 엔트리 `deck != null` 단언 ⓑ `MinWalkerDistToGoal=8` 단위 착오(흐름장 dist 는 ×10 비용, 실제 0.8칸) → `CellsToGoal` 셀 단위 ⓒ `FlowPathFrom` 이 dist 하강을 재구현(빌더가 경고한 대각 편향) → 심이 소비하는 `FlowSlot` 방향장 추종. 부수: `EditorUpsertLiveEntry` 의 null 슬롯 제거를 경고 로그로, Duel 생성기 메뉴 덮어쓰기 확인, `DioramaStagePlayTests` 차단 셀을 `GeneratedMap.tiles` 파생으로(이중 정본 제거). **절차 지적(수용, 되돌릴 수 없음)**: 판형 21×12→23×10 교체·적 마음 장식·dev→live 승격을 사용자 확인 전에 확정했고, `df1a117a` 가 unit 11+12+풀 재구성+사용자 미커밋 풀 편집(live 1+dev 9 → live 4+dev 0)을 한 커밋에 담아 이력에서 사용자 손과 에이전트 손이 구분되지 않는다.
- **사용자 결정 3건(2026-08-26 저녁)**: ⓐ Duel 적 마음 장식 제거(생성기에서 `enemy_heart` 호스트 삭제, 재생성) ⓑ 맵마다 다른 덱 — Street=Deck_Serpent · Subway=Deck_Zig · StreetDay=Deck_Coil(현행 세대 맵 덱 재배정, 보스 J/N/M/N) ⓒ 세 맵 live 유지. `docs/reference/map-wave-balancing.md` 의 풀 표를 스테이지 풀로 갱신.
- **5차 병합 준비 — 기준선 A (2026-08-27, `506136c7` 기준 전수)**: EditMode 2512 통과 / 3 실패(기존 셋) · PlayMode 155 통과 / 17 실패 / 9 Skip. 17 중 14 = US-007 기존 목록(Auth 서버·DeckInfoPreset·DragCancelZone·DragPlacementReach·DreamcatcherAttachRequirement·DreamcatcherEffect×2·DropDismount·FlyingEnemy·PlacementAura×3·SceneTransition·SlimeSplit) · 1 = `DefenderCatalog_BakesPathOnly…`(PrimeTween 언로드 로그 플레이크) · **2 = 새 Duel 판형이 드러낸 테스트 전제** → 고침: `MovementIntegritySmokeTest`(스폰 근처 차단 셀 전제 → Walk 셀 폴백, 가디언 전 마당 배치 결정 2026-08-18) · `SpawnGuideMatchesWalkTest.Duel`(레인 0 예고선 하나에 전 적 대조 → 레인별 예고선 전부 중 최근접). 재실행 2/2 통과 → 기준선 A' = PlayMode 실패 15(기존 14 + 플레이크 1). main 기준선 B = `skill-layer-complete` 문서의 216 중 14.
- **5차 병합 `687c56b8` (main +96 = skill-layer-migration, 태그 `pre-skill-layer`→`skill-layer-complete`; 2026-08-27)**: `git merge-tree` 드라이런 충돌 0 → 실제 머지 충돌 0(공동 수정 6파일 auto-merge). 머지 전 이 워크트리의 camera-direction unit 18 미커밋분(다른 세션, 미진행)은 `stash@{0}` 로 보관(pop 으로 복원). dotnet 5어셈블리 0 에러(Skills 소스는 Runtime.Ralph 의 `Scripts/**` glob 에 포함). EditMode 2608 통과 / 3 실패(기존 셋). PlayMode 216 중 186 통과 / 21 실패 / 9 Skip → **분류: 기존(A) 14 · main 신규 테스트의 판형 전제 2(`OnPlaceBindNearbyTest` 원점 스캔·`ActiveTornadoTest.FindWalkCell` 원점+margin → 골 거리 기준으로 고침; Tornado 는 main 기준선에서도 빨강이던 것) · 순서 의존 플레이크 5(`DreamcatcherCombatDamageTest`×2·`DreamcatcherSleepDamageTest`×2·`OnPlaceSkyStrikeTest` — 코드 변경 0, 격리 재실행 15/15 통과) · 진짜 상호작용 0**. 검증 공백이던 «본능 사격 × 스킬 레이어»에 라이브 그물 추가(`Duel_AllyInstinct_DamagesEnemyWalkingToGoal_WithoutAnyDefender`, 방어유닛 0 판에서 Guard 가 적을 때림) → 통과. main 의 새 PlayMode 테스트 19파일이 기본판(이제 Street)을 타지만 격리 실행에서 전부 통과 — «Serpent 유사 테스트 마당» 은 지금은 불필요.
- **주의**: `CameraDirectionConfig` 가 다른 세션에서 바뀌었다(fovMin 31→24, Battle 레시피 역산) — 프리뷰 fov 24. Street 프리팹의 장식 5개가 존재하지 않는 머티리얼 guid(`9dfc825a…`)를 참조한다(렌더는 기본 스프라이트 머티리얼로 폴백 — 사용자 에셋, 손대지 않음).
