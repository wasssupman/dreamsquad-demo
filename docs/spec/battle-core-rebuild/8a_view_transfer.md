# 8a — 옛 화면에만 있는 것 이식 (조각 E · 1/3)

> unit 8 은 원래 파일 하나였다. 실측 복사·적응 대상이 **약 3.9k줄**(화면 누락분 ~1.9k · 판 진입·온보딩 ~1.6k · 폐쇄 검사·타입 이사·문서 ~0.4k)이라 한 커밋 단위가 못 된다(인계 §5 「~2.5k 넘으면 나눈다」). 그래서 **8a 화면 → 8b 판 진입·로비 교대 → 8c 폐쇄 증명**으로 나눈다. 화면이 먼저인 이유: 로비가 새 씬으로 들어오는 순간(8b) 플레이어가 옛 화면을 잃는다. 폐쇄 증명(8c)은 둘이 끝나야 참이 된다.
>
> ⚠ **8a·8b 동안 옛 씬은 계속 살아 있다** — 로비가 아직 옛 씬을 연다(8b 마지막 커밋 전까지). 그래서 이 unit 은 새 씬 쪽에 **더하기만** 하고 옛 씬의 배선(`GameManager` 구독)은 끊지 않는다. 끊는 것은 unit 9.

## 목적

**옛 `BattleScene` 에서 살아 있는데 새 `BattleCoreScene` 에 없는 화면 요소를 없앤다.** 장부는 「새 주인」을 적었지만 그 주인이 코드에 없는 행이 있다 — `bridge-fields.md` 1 `bonusPortalPrefab`(「unit 6 의 보너스 뷰」) · 31 `_gimmickPhaseView`(「5b」) · 55 `_bossWarning`(「5b 가 잇는다」)의 새 주인이 `BattleCoreUnity/` 에 0건이다(grep `BonusPortal|BossWarning|GimmickPhase` = 0). 장부 미정 0 은 **배정**의 끝이지 **실현**의 끝이 아니었다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 대조표(먼저) | 이 문서 「옛 씬 대조표」 |
| 당김 알약 | `BattleCoreUnity/Hud/CoreNextWaveDock.cs` ← `UI/NextWaveDock.cs`(593줄). 커맨드 `PullWave`(10)·`PullBonus`(13) 는 있다(`Match/Command.cs:45·57`), 입력이 없다. 상태원 = `WaveScheduler.PullsLeft`·`BonusOffered`(`Owners/WaveScheduler.cs:96·98`) |
| 보너스 포탈 | `BattleCoreUnity/View/CoreBonusPortalPresenter.cs` ← `Bridge/BattleBridge.BonusWave.cs:161~167` |
| 보스 경보 | `BattleCoreUnity/Hud/CoreBossWarning.cs` ← `UI/BossWarningView.cs`(240줄). 옛 구동 = 보스 스폰 순간(`BattleBridge.cs:10116~10121`) |
| 기믹 리빌 | `BattleCoreUnity/Hud/CoreGimmickReveal.cs` ← `UI/GimmickPhaseView.cs`(517줄) |
| 메뉴 웨이브 브리핑 | `CoreMenuPopup` + `WavePatternStripView` · **코어 `WavePlan` → 스트립 입력 어댑터**(구현 5) |
| 페이즈 먹이 | `Presentation/CoreCameraFeed.cs` → `BattleCoreUnity/View/CorePhaseFeed.cs`(이동·개명) — 카메라 + BGM 둘 다 민다 |
| BGM | `Audio/SoundManager.cs` 에 `SetPhase(GamePhase)` **추가**(`:133~151` 의 `GameManager` 구독은 둔다 — 옛 씬용) |
| HUD 게이팅 | `Hud/CoreHudUi.cs` — 결과 뒤 HUD 숨김(rules X19) |
| 결과 화면 | `CoreMatchOutcomePresenter.cs:165~173` 의 `MatchTally` 어댑터 제거 → `ResultScreen` 이 `MatchOutcome` 을 직접 받는다(5c 「고친 것」 약속). 옛 씬 경로가 아직 `MatchTally` 로 부르므로 **`ResultScreen` 은 두 입력을 한동안 다 받는다** — `MatchTally` 쪽은 unit 9 에서 지운다 |
| 손패 배경 | `UI/Dreamcatcher/DreamcatcherFluidBackdrop.cs` + `FluidPaintSim`(`:16`) — 새 씬에 오브젝트 둘을 세우고 `handView` 를 `CoreHandView` 상태로 |
| 브리지 static 미러 | `Presentation/BlobShadow.cs:31~37` · `Presentation/PropBillboard.cs:42~61` · `Editor/PropDataEditor.cs`(`TileToWorld`) |
| `GamePhase` | `Core/GameManager.cs:24` → `Core/GamePhase.cs`(값·순서 무변) |
| 장부 정정 | `ledgers/bridge-fields.md` 55 구동 신호 정정 — **완료**(critic 반영 커밋) |
| 테스트 | `Tests/PlayModeCore/` 뷰 테스트 |

## 옛 씬 대조표 (2026-09-25 실측 — 구현 첫 작업은 이 표의 재측정)

측정: `BattleScene.unity` 의 스크립트 GUID 56 − 패키지 스크립트 8 = 48 을 `BattleCoreScene.unity` 와 대조. 재측정할 수 있도록 **새 짝 열**을 둔다.

| 옛 컴포넌트 | 라이브? | 새 짝 | 처분 |
|---|---|---|---|
| `NextWaveDock` | ○ | 없음 | **이식** |
| 보너스 포탈 프리팹(브리지 필드 1) | ○ | 없음 | **이식** |
| `BossWarningView` | ○(5웨이브마다 보스) | 없음 | **이식** |
| `GimmickPhaseView` | **라이브 경로 없음** — 옛 `Data/Config/BattleConfig.asset:15`·새 `MatchMode_KillScore3Min.asset:27` 둘 다 `gimmickEnabled: 0` | 없음 | **이식** — 기믹 판은 7d 에서 살아났고 기믹 켠 모드에서만 보인다 |
| `MenuPopup` 공격 패턴 | ○ | `CoreMenuPopup`(정지만) | **이식** |
| `MenuPopup` dev 토글 「캐릭터/포스트」(`:172~178`) | dev 빌드 | 없음 | `IngameCharacterTest` 처분과 같이(8c) |
| `DreamcatcherFluidBackdrop` + `FluidPaintSim` | ○(`HandGated`, 켜짐) | 없음 | **이식**(오브젝트 2 + 상태원 교체) |
| `SoundManager` BGM | ○ | `SoundManager`(클립만) | **이식**(구동 — 5c 「아직 안 보이는 것」) |
| `TileHealthGaugeLayer` | ✕ — 표시 모드 `UnifiedOverhead`(bridge-fields 51) | `CoreUnitOverheadUiLayer` | 삭제(bridge-fields 49) |
| `DcActionFlipbookView` | ✕ — 재배치 진입구 꺼짐 | — | 삭제(`defender-clock-out/0` · tools 11) |
| `DraftController`·`DraftView`·`DraftCardFanView`·`SquadPrepView` | 뽑기 폴백·옛 준비 단계 | — | 삭제(계약 9) |
| `IngameCharacterTest` | 그림자 실험대(파일 헤더) | — | **8c 에서 확인** — 은퇴 근거를 옛 spec 에서 못 찾으면 사용자에게 묻는다 |
| `FirstRunTutorialController` + `OutgameTutorialOverlay`(`:78`) + `TutorialGuidanceView` | ○ | 없음 | **8b**(사용자 결정 ①) |
| `ReturnToMenuButton`·`MenuPopup` 나가기 | ○ | 없음 | **8b** |
| `UiSafeAreaFitter` | ○ | `UiCanvasSetup.Ensure` 가 런타임 부착 | 해당 없음 |

## 구현

1. **대조표 재측정이 먼저다.** 스크립트로 다시 뽑고 행이 늘면 추가한다. 5b 의 「은퇴」 오판(인계 §3-5) 때문에 **삭제 행은 옛 spec 인용이 있어야 한다.**
2. **당김 알약.** 판정 0 — 누르면 커맨드, receipt 거절이면 사유. 남은 횟수·보너스 제안은 읽기 모델만. 보너스 알약은 위에 쌓고 색만 가른다(옛 rev 9).
3. **보너스 포탈.** 열림 = 당김 + `portalAppearDelaySec`, 닫힘 = 마지막 스폰 + `portalLingerSec`. 시각은 **판의 시계**(틱 × 1/60) — `Time` 이면 슬로모에서 시계가 갈린다(옛 `BonusWave.cs:164` 주석). 마지막 스폰 시각은 코어 순수 함수 `BonusWaveSchedule.Build` 를 **호출만** 해서 얻는다. `portalLingerSec` 는 `BonusWaveData`(`Data/BonusWaveData.cs:37`)에 두고 뷰가 읽는다 — 코어 정의표에 싣지 않는다(뷰 타이밍이 `configHash` 에 들어가면 안 된다).
4. **보스 경보.** `Spawned` 의 `DefIndex` → 적 저작 `tier == Boss` 면 `Show()`(옛 판별 그대로).
5. **메뉴 브리핑.** 옛 경로는 `GameManager.BuildBriefingWavePlan`(브리지 경유, `MenuPopup.cs:87~89`)이고, 스트립의 덱 경로는 **옛 생성기**를 부른다(`WavePatternStripView.cs:69` → `WavePatternGenerator.Generate`). 그러니 스트립에 덱을 주면 옛 생성기의 웨이브가 그려진다. **코어 `WaveScheduler` 가 가진 그 판의 `WavePlan` 을 스트립 입력(`GeneratedWavePlan`, `:80`)으로 바꾸는 어댑터**를 새 층에 둔다. 덱 경로는 부르지 않는다 — 그래야 unit 9 가 옛 생성기의 소비처를 셀 수 있다.
6. **기믹 리빌.** 배치 **앞** 약 2초 — Battle 도메인 리스 0(메뉴 정지와 같은 기제)으로 판을 세운다. 기믹 없는 판은 건너뛴다.
7. **페이즈 먹이와 BGM — 커밋 순서가 있다.** 헤드리스 Check lane 은 `BattleCoreUnity/**` 를 Unity 가 굽은 `Wassup.Runtime.dll` 에 대고 컴파일한다(`BattleCoreUnity.Check.csproj:31·50`). 그래서 ① `SoundManager.SetPhase` 추가 커밋 → ② Unity 컴파일(dll 갱신) → ③ `CoreCameraFeed` 를 `BattleCoreUnity/View/` 로 옮기고 `SetPhase` 호출 추가. ①과 ③을 한 커밋에 두면 Check lane 이 거짓 빨강이 된다. `CameraDirector.SetPhase` 는 이미 dll 에 있다. `CameraDirector.cs:162~168` 이 「두 입력(구독 + push)이 공존해도 마지막에 민 쪽이 이긴다」를 계약으로 적어 뒀다 — `SoundManager` 도 같은 형이다.
8. **`GamePhase` 는 자기 파일로 옮기되 값 순서 무변.** `CameraDirectionConfig.breathPhases` 가 정수로 직렬화한다(rule-holders G2 · rules X16).
9. **HUD 게이팅.** `MatchEnded` 뒤 전투 HUD 를 숨긴다 — 옛 `GamePhase.Tally/Result` 게이팅과 같은 결과(5c ⚠).
10. **브리지 static 미러 3곳을 끊는다.** 새 씬에서 `BattleBridge.PropDistanceTiltFactor` 는 0 이라 **스테이지 프랍 37개의 거리 틸트가 꺼져 있다**(`PropBillboard.cs:43` — factor 0 = 비활성). 스테이지 블롭 40개는 옛 씬 색(bridge-fields 66)이 아니라 코드 기본값을 쓴다. ⚠ 스테이지는 `BattleDriver.cs:404` 가 `Instantiate` 하고 `BlobShadow` 는 **`Awake` 에서 값을 읽는다**(`:31~37`). 그러니 생성 뒤 주입은 늦다. 값의 주인(`BlobShadowConfig`·`CharacterViewConfig`)을 **컴포넌트가 직접 참조**하게 한다(제약 12 판단 순서 (a)·(b)). 옛 씬에서도 같은 SO 값이 읽히므로 무회귀다(그 SO 의 값 = 옛 씬 브리지 블록 복사, bridge-fields 머리말).

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 드롭 하마 키링 잔류물·`DragSwaySettings` 키링 칸 | 옛 라이브에 없었다(5b 「이식 제외」). 옛 드래그 컨트롤러와 함께 unit 9 에서 사라진다. `KeyringSim` 은 새 층 3곳이 쓰므로 존치 | 제거(선행) |
| `TileHealthGaugeLayer`·`DcActionFlipbookView`·드래프트 4종 | 위 표 | 제거(선행) |
| 공격음을 START 에 내기 | 5c 가 「플레이에서 어색하면」으로 미뤘다 | 후속 후보 |
| `WaveClear`·`TimeAttack` 결과 단위 표기 | 모드 선택 UI 와 같이(5c) | 후속 후보 |

## 파이프라인 커버리지

`object-pipeline-map.md` 「VFX (one-shot)」 대조 — 판 위 오브젝트는 보너스 포탈 하나다.

| 정거장 | 보너스 포탈 |
|---|---|
| 저작 | `BonusWaveData.portalAppearDelaySec`·`portalLingerSec` + 씬 배선 프리팹(bridge-fields 1) |
| 생성 신호 | 코어 `BonusPulled`(23) · 위치 = `MapSnapshot.BonusSpawns` |
| 뷰 생성 | `CoreBonusPortalPresenter` 풀 |
| 수명 | 판의 시계 · 판 경계에서 회수 |
| 정렬 | `ViewOrder` 유닛 동기 **앞** |

보스 경보·리빌·알약·브리핑은 UI 캔버스라 판 오브젝트 정거장 N/A(월드 생성→렌더 경로가 없다).

## 완료 기준

- [ ] 대조표 재측정을 이 문서에 반영 · 삭제 행 전부 옛 spec 인용.
- [ ] `grep -rn "BattleBridge\." Assets/_Project/Scripts/Presentation/BlobShadow.cs Assets/_Project/Scripts/Presentation/PropBillboard.cs Assets/_Project/Editor/PropDataEditor.cs` = 0.
- [ ] 옛 씬 무회귀: 로비 → 옛 씬 1판에서 카메라 페이즈 레시피·BGM·결과 화면이 전과 같다(`GameManager` 구독 존치).
- [ ] PlayMode 코어: 당김 → receipt → 웨이브 도착 · 보너스 → 포탈 열림/닫힘 틱 · 보스 스폰 → 배너 1회 · 결과 뒤 HUD 비활성 · 스테이지 프랍 틸트 factor = `CharacterViewConfig` 값 · 브리핑 웨이브 수 = 코어 `WavePlan` 웨이브 수. 기존 57 + 신규 전부 초록.
- [ ] EditMode 코어+Assets 선행 2 외 빨강 0 · 헤드리스 3종(build 0 · test · Check 0 — **커밋마다**, 구현 7 의 순서로) · 골든 11종 무변(코어 변경 0 이 기대값).
- [ ] Play 육안(옛 씬과 나란히): 당김 알약 · 보너스 포탈 · 보스 배너 · 메뉴 브리핑 · 손패 배경 · BGM · 프랍 틸트. 콘솔 에러 0.
- [ ] `core-reviewer` APPROVE.
