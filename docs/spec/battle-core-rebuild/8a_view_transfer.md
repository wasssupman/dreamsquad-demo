# 8a — 옛 화면에만 있는 것 이식 (조각 E · 1/3)

> unit 8 은 원래 파일 하나였다. 실측 복사·적응 대상이 **약 3.9k줄**(화면 누락분 ~1.9k · 판 진입·온보딩 ~1.6k · 폐쇄 검사·타입 이사·문서 ~0.4k)이라 한 커밋 단위가 못 된다(인계 §5 「~2.5k 넘으면 나눈다」). 그래서 **8a 화면 → 8b 판 진입·로비 전환 → 8c 브리지 폐쇄**로 나눈다. 순서 이유: 로비가 새 씬으로 들어오는 순간(8b) 플레이어가 옛 화면을 잃으므로 화면이 먼저다. 폐쇄 검사(8c)는 둘이 끝나야 참이 된다.

## 목적

**옛 `BattleScene` 에서 살아 있는데 새 `BattleCoreScene` 에 없는 화면 요소를 없앤다.** 장부는 「새 주인」을 적었지만 그 주인이 코드에 없는 행이 있다 — `bridge-fields.md` 1 `bonusPortalPrefab`(「unit 6 의 보너스 뷰」) · 31 `_gimmickPhaseView`(「5b」) · 55 `_bossWarning`(「5b 가 잇는다」)의 새 주인이 `BattleCoreUnity/` 에 0건이다(grep `BonusPortal|BossWarning|GimmickPhase` = 0). 장부 미정 0 은 **배정**의 끝이지 **실현**의 끝이 아니었다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 대조표(먼저) | 이 문서 「옛 씬 대조표」 — `BattleScene.unity` 에 붙은 MonoBehaviour 중 새 씬에 짝이 없는 것 |
| 당김 알약 | `BattleCoreUnity/Hud/CoreNextWaveDock.cs` ← `UI/NextWaveDock.cs`(593줄). 커맨드 `PullWave`(10)·`PullBonus`(13) 는 이미 있다(`Match/Command.cs:45·57`), 입력만 없다. 상태원 = `WaveScheduler.PullsLeft`·`BonusOffered`(`Owners/WaveScheduler.cs:96·98`) |
| 보너스 포탈 | `BattleCoreUnity/View/CoreBonusPortalPresenter.cs` ← `Bridge/BattleBridge.BonusWave.cs:161~167`(열림 = 당김 + `portalAppearDelaySec` · 닫힘 = 마지막 스폰 + `portalLingerSec`) |
| 보스 경보 | `BattleCoreUnity/Hud/CoreBossWarning.cs` ← `UI/BossWarningView.cs`(240줄). 옛 구동 = 보스 스폰 순간(`BattleBridge.cs:10121`) |
| 기믹 리빌 | `BattleCoreUnity/Hud/CoreGimmickReveal.cs` ← `UI/GimmickPhaseView.cs`(517줄) |
| 메뉴 웨이브 브리핑 | `CoreMenuPopup` 에 `WavePatternStripView` 재사용(옛 `MenuPopup.cs:82~95`, rule-holders G24) |
| 페이즈 먹이 | `Presentation/CoreCameraFeed.cs` → `BattleCoreUnity/View/CorePhaseFeed.cs`(이동·개명) — 카메라 + **BGM** 둘 다 민다 |
| BGM | `Audio/SoundManager.cs:135~143` — `GameManager.PhaseChanged` 구독을 **밀어 넣기**(`SetPhase`)로 |
| HUD 게이팅 | `Hud/CoreHudUi.cs` — 결과 뒤 HUD 숨김(rules X19) |
| 결과 화면 | `CoreMatchOutcomePresenter.cs:165~173` 의 `MatchTally` 어댑터 제거 → `ResultScreen` 이 `MatchOutcome` 을 직접 받는다(5c 「고친 것」 약속) |
| 손패 배경 | `UI/Dreamcatcher/DreamcatcherFluidBackdrop.cs` — `handView` 를 `CoreHandView` 상태로 |
| 브리지 static 미러 | `Presentation/BlobShadow.cs:36~37` · `Presentation/PropBillboard.cs:42~61` · `Editor/PropDataEditor.cs`(`TileToWorld`) |
| `GamePhase` | `Core/GameManager.cs:24` → `Core/GamePhase.cs`(값·순서 무변) |
| 테스트 | `Tests/PlayModeCore/` 대조 스모크 + 뷰 테스트 |

## 옛 씬 대조표 (2026-09-25 실측 — 구현 첫 작업은 이 표의 재측정)

측정: `BattleScene.unity` 의 스크립트 GUID × `BattleCoreScene.unity` 의 GUID. 옛 씬에만 붙은 것 48 중 새 씬에 `Core*` 짝이 있는 것은 제외하고 남은 것:

| 옛 컴포넌트 | 라이브? | 처분 |
|---|---|---|
| `NextWaveDock` | ○(당김·보너스 알약) | **이식** |
| 보너스 포탈 프리팹(브리지 필드 1) | ○ | **이식** |
| `BossWarningView` | ○(5웨이브마다 보스) | **이식** |
| `GimmickPhaseView` | 기믹 켠 모드만(라이브 모드 `gimmickEnabled: 0` — `MatchMode_KillScore3Min.asset:27`) | **이식** — 기믹 판은 7d 에서 살아났다 |
| `MenuPopup` 의 공격 패턴 | ○ | **이식**(`WavePatternStripView` 재사용) |
| `DreamcatcherFluidBackdrop` | ○(`HandGated`, 켜짐) | **이식**(배선만) |
| `SoundManager` BGM | ○ | **이식**(구동만 — 5c 「아직 안 보이는 것」) |
| `TileHealthGaugeLayer` | ✕ — 표시 모드가 `UnifiedOverhead`(bridge-fields 51) | 삭제(bridge-fields 49 「레거시 표시 모드」) |
| `DcActionFlipbookView` | ✕ — 재배치 진입구가 꺼짐 | 삭제(`defender-clock-out/0` · tools 11 은퇴) |
| `DraftController`·`DraftView`·`DraftCardFanView`·`SquadPrepView` | 뽑기 폴백·옛 준비 단계 | 삭제(계약 9 「뽑기 폴백 진입」) |
| `IngameCharacterTest` | 그림자 실험대(파일 헤더) | **확인 후** 8c 퇴역 집합 — 은퇴 근거를 옛 spec 에서 찾지 못하면 사용자에게 묻는다 |
| `FirstRunTutorialController` | ○(계정 첫 판 온보딩) | **8b**(사용자 결정 ①) |
| `ReturnToMenuButton`·`MenuPopup` 나가기 | ○ | **8b**(퇴장 사슬) |

## 구현

1. **대조표 재측정이 먼저다.** 위 표를 스크립트(GUID 대조)로 다시 뽑고 행이 늘면 여기 추가한다. 5b 가 옛 기능을 「은퇴」로 오판한 함정(인계 §3-5) 때문에 **삭제 행은 옛 spec 인용이 있어야 한다.**
2. **당김 알약.** 판정 0 — 누르면 커맨드, receipt 거절이면 사유 표시. 남은 횟수·보너스 제안은 읽기 모델만. 보너스 알약은 위에 쌓고 색만 가른다(옛 rev 9 계약).
3. **보너스 포탈.** 열림·닫힘은 **판의 시계**(틱 × 1/60)로 잰다 — `Time` 을 쓰면 슬로모에서 두 시계가 갈린다(옛 `BonusWave.cs:164` 주석). 마지막 스폰 시각은 코어 순수 함수 `BonusWaveSchedule.Build` 를 **호출만** 해서 얻는다(자를 새로 만들지 않는다). `portalLingerSec` 는 `BonusWaveData`(`Data/BonusWaveData.cs:37`)에 그대로 두고 뷰가 읽는다 — 코어 정의표에 싣지 않는다(뷰 타이밍이라 `configHash` 에 들어가면 안 된다).
4. **보스 경보.** `Spawned` 사건의 `DefIndex` → 적 저작 `tier == Boss` 면 `Show()`. 판별은 옛과 같은 `tier`(elite-enemy-tier unit 0).
5. **기믹 리빌.** 배치 **앞**의 약 2초 — 그동안 Battle 도메인 리스 0(메뉴 정지와 같은 기제)으로 판을 세운다. 기믹이 없는 판은 건너뛴다.
6. **페이즈 먹이.** `CoreCameraFeed` 를 `BattleCoreUnity/View/` 로 옮긴다 — 검사 lane 이 옛 dll 을 보던 이유(파일 헤더)는 `CameraDirector.SetPhase` 가 이미 dll 에 있어 사라졌다. 같은 자리에서 `SoundManager.SetPhase` 도 민다. `GamePhase` 는 자기 파일로 옮기되 **값 순서 무변** — `CameraDirectionConfig.breathPhases` 가 정수로 직렬화한다(rule-holders G2 · rules X16).
7. **HUD 게이팅.** `MatchEnded` 뒤 전투 HUD 를 숨긴다 — 옛 `GamePhase.Tally/Result` 게이팅과 같은 결과(5c 완료 기준의 ⚠).
8. **브리지 static 미러 3곳을 끊는다.** 새 씬에서 `BattleBridge.PropDistanceTiltFactor` 는 0 이라 **스테이지 프랍 37개의 거리 틸트가 꺼져 있고**(`PropBillboard.cs:43` — factor 0 = 비활성), 스테이지 블롭 40개는 옛 씬 색(`bridge-fields` 66)이 아니라 코드 기본값을 쓴다. 값의 주인 `CharacterViewConfig`·`BlobShadowConfig` 를 `CoreStructurePropLayer` 가 스테이지 인스턴스에 넣는다(제약 12 판단 순서 (a)).

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 드롭 하마 키링 잔류물(`DetachKeyringRemnant`)·`DragSwaySettings` 키링 칸 | 옛 라이브에 없었다(5b 「이식 제외」). 옛 드래그 컨트롤러와 함께 unit 9 에서 사라진다. 궤적 수학 `KeyringSim` 은 새 층 3곳이 쓰므로 **존치** | 제거(선행) |
| `TileHealthGaugeLayer`·`DcActionFlipbookView`·드래프트 4종 | 위 표의 근거 | 제거(선행) |
| 공격음을 START 에 내기 | 5c 가 「플레이에서 어색하면」으로 미뤘다 — 이 unit 의 질문이 아니다 | 후속 후보 |
| `WaveClear`·`TimeAttack` 결과 단위 표기 | 모드 선택 UI 와 같이(5c) | 후속 후보 |

## 파이프라인 커버리지

`object-pipeline-map.md` 「VFX (one-shot)」 아키타입 대조 — 보너스 포탈만 판 위 오브젝트다.

| 정거장 | 보너스 포탈 |
|---|---|
| 저작 | `BonusWaveData.portalAppearDelaySec`·`portalLingerSec` + 씬 배선 프리팹(bridge-fields 1) |
| 생성 신호 | 코어 `BonusPulled`(23) · 위치 = `MapSnapshot.BonusSpawns` |
| 뷰 생성 | `CoreBonusPortalPresenter` 풀 |
| 수명 | 판의 시계 · 판 경계에서 회수 |
| 정렬 | `ViewOrder` 유닛 동기 **앞**(포탈이 적보다 먼저 선다) |

보스 경보·리빌·알약은 UI 캔버스라 판 오브젝트 정거장 N/A(생성→렌더 경로가 월드에 없다).

## 완료 기준

- [ ] 대조표 재측정 결과를 이 문서에 반영 · 삭제 행 전부 옛 spec 인용.
- [ ] `grep -rn "BattleBridge\." Assets/_Project/Scripts/Presentation/BlobShadow.cs Assets/_Project/Scripts/Presentation/PropBillboard.cs Assets/_Project/Editor/PropDataEditor.cs` = 0.
- [ ] `grep -rn "GameManager" Assets/_Project/Scripts/Audio Assets/_Project/Scripts/Presentation/CameraDirector.cs` 비주석 = 0.
- [ ] PlayMode 코어: 당김 → receipt → 웨이브 도착 · 보너스 → 포탈 열림/닫힘 틱 · 보스 스폰 → 배너 1회 · 결과 뒤 HUD 비활성 · 스테이지 프랍 틸트 factor = `CharacterViewConfig` 값. 기존 57 + 신규 전부 초록.
- [ ] EditMode 코어+Assets 선행 2 외 빨강 0 · 헤드리스 3종(build 0 · test · Check 0) · 골든 11종 무변(코어 변경 0 이 기대값).
- [ ] Play 육안(옛 씬과 나란히): 당김 알약 · 보너스 포탈 · 보스 배너 · 메뉴 브리핑 · 손패 배경 · BGM · 프랍 틸트. 콘솔 에러 0.
- [ ] `core-reviewer` APPROVE.
