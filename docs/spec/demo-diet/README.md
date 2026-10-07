# demo-diet — 전투 로직만 남기는 Demo 정리

상태: 진행 중 2026-10-06 (경계·결정 4건 승인 — 단위 0 부터)

## 목표

Demo 리포에서 **전투 로직 외 전부를 제거**해 정리본을 만든다. 이 정리본이 somnia-client 로 옮겨 가는 payload 다(사용자 결정 2026-10-06: *「전투 로직 외에 다른 것들을 모두 제거하여 정리된 버전을 메인 리포로 옮기는 게 낫다」*). 아웃게임(로그인·로비·프로필·토너먼트 API·결과 화면)은 somnia 가 이미 갖고 있으므로 버린다.

## 경계 — 플레이 흐름 기준 (`ingame-flow.md` §2 · blueprint §3)

```
로비 → LoadoutGate → 참가 신청(시드)                  아웃게임  → REMOVE
→ 판 조립(MatchDefinitionBuilder) → 전투 180초         전투      → KEEP
→ 종료 판정 → MatchOutcome 값                          전투      → KEEP
→ 결과 통보·랭킹·히스토리·프로필 저장 → 로비 복귀      아웃게임  → REMOVE
```

전투는 바깥과 **값으로만** 통한다 — 입구 `MatchEntryInput`(스쿼드·드림스톤·덱·플랜·로스터·맵 인덱스·시드), 출구 `MatchOutcome` 사건. 이 두 seam 이 정리의 축이다.

## 사용자 결정 (2026-10-06)

| # | 결정 |
|---|---|
| Q1 | 배치 컷신 프레임 327장 + `deployCutscene*` 필드 → **제거** |
| Q2 | 시트 **에디터 임포터**(DTO 14 · `Editor/UnitStatImport` 9 · `SheetSync` 2) → **당분간 유지**(후속 제거 요청 예정). 런타임 덮어쓰기 그룹(`*RuntimeRefresher` 4 · `IRuntimeRefresher` · `LoginAutoImport`)은 제거 |
| Q3 | 결과 화면(`ResultScreen` · `LeaderboardList` · `NoticePopup` · `UiOverlay`) + `CoreMatchOutcomePresenter` → **통째 제거**. 전투는 `MatchOutcome` 사건만 발행 |
| Q4 | 개발 statics(`TestModeContext` · `DevMapOverride` · `TestModeConfig`) → **`MatchEntryInput` 값으로 접어 제거** |
| 순서 | ~~다이어트 먼저~~ → **2026-10-07 정정: `unity-6-6-upgrade` 를 단위 2 보다 먼저**(사용자: 「에디터 에러부터 잡는 게 맞지 않나」 — 에디터가 살아야 단위 2 의 씬·에셋 정리를 눈으로 검증한다). 단위 2 부터는 6.6 위에서 |

## KEEP / REMOVE 요약 (전수표는 조사 기록 — 세션 스크래치 `battle-only-boundary.md`, 수치는 재측정)

- **KEEP** ≈ 755 .cs: `BattleCore` 147 · `Skills` 43 · `UnitAi` 4 · `BattleCoreUnity` 74 · `Presentation` 40 · `Core/{MapStage,BoardSpace,GamePhase,TimeControl,MatchConfigSnapshot}` · `Audio/SoundManager` · `Rendering/RuntimeMaterialFactory` · 전투 UI 18(`UI/Dreamcatcher` 11 · `WavePatternStripView` · `Placement*` 3 · `UiLayer` · `UiRoundedSprite` · `KeyringSim` 수학) · `Data` 정의 타입 ~118 + SO ~560 · 전투 아트/VFX/오디오/Spine/셰이더 · `BattleCoreScene` · 전투 테스트 · 헤드리스 lane · 시트 에디터 임포터(Q2).
- **REMOVE** ≈ 150 .cs + 에셋 ~630: `Core/Api` 10 · `Core/Profile` 8 · `SquadDraw` · `MobileScreenOrientation` · `GimmickSelection`(코어 정본 중복) · 시트 런타임 6 · `UI/Outgame` 35 · `UI/Dev*` 3 · `CostWellMath` · 결과 화면 4 + `CoreMatchOutcomePresenter` · `Editor/MobileBuild` 3 · `LayerLabPresetImporter` · 아웃게임 테스트 ~50 · 로비 스프라이트 281 · 컷신 327 · 씬 3 · SO 3 · 로비 셰이더 5 · `Layer Lab` 벤더 33 MB.
- **벤더 추림**: GabrielAguiar(피참조 115) · PixPlays(57) · Hovl · KayKit · VFXPACK · Spine Examples(적 5종 스켈레톤) — 참조 폐포만 남긴다.

## 작업 단위 (각 커밋은 컴파일 초록을 유지한다)

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_seams.md` | 전투 입구·출구를 값/사건으로 — `MatchEntryInput` · `BattleDriver` 사건 · statics 접기 · `KeyringSim` 이동 · 나가기 알림 |
| 1 | `1_remove_outgame_code.md` | 아웃게임 코드·테스트 삭제(≈150 .cs). **단위 0 과 한 커밋으로 실행** — 아웃게임이 새 seam 을 쓰도록 바꾸는 중간 어댑터는 다음 단위에서 통째로 지워질 코드라 만들지 않았다(2026-10-07) |
| 2 | `2_remove_assets.md` | 아웃게임 에셋·컷신·필드·씬 삭제. GUID 참조 0. **Layer Lab 은 삭제가 아니라 단위 3 추림으로 이관**(디펜더·적 42 유닛의 Spine 스켈레톤이 거기 있다 — 2026-10-07) |
| 3 | `3_vendor_prune.md` | 벤더 6개를 참조 폐포로 추림(에디터 스크립트 1회) |
| 4 | `4_docs_and_close.md` | CLAUDE.md·reference·spec 색인 현행화 · 전체 검증 · 종료 |

## 공통 원칙

- **각 커밋이 컴파일된다.** 삭제가 KEEP 쪽을 깨뜨리면 그 seam 은 **앞 단위**에 있어야 한다. 단위 0 이 그래서 먼저다.
- 전투 입구는 `MatchEntryInput` **하나**로 모은다. 스쿼드·덱·플랜·로스터·맵 인덱스·시드 — 프로필·static·PlayerPrefs 를 전투 코드가 직접 읽지 않는다. Demo 안에서 BattleCoreScene 을 바로 Play 할 수 있도록 **기본 입력은 직렬화 에셋**(`MatchEntryConfig` SO 또는 `ModeSelection` 확장)에서 온다.
- 전투 출구는 사건이다 — `MatchStarted` · `DeckLocked` · `MatchFinished(MatchOutcome)`. 통보·저장·화면·씬 복귀는 구독자(지금은 없음, somnia 에선 App) 몫.
- 지우는 것의 git 이력은 그대로 남는다. 「되살릴 때 git 에서」가 보존 전략이다.
- **검증 환경**: 단위 0+1 은 4.7 검증 워크트리 `wt47` 배치로 검증했다(사용자 에디터가 6.6 이라 메인에선 못 돌렸다). 단위 2 부터는 6.6 위다 — 배치는 사용자 에디터를 닫고 메인 리포(또는 6.6 워크트리 `wt66`)에서, Play 육안은 사용자 에디터에서.
- 커밋은 경로 지정. Layer Lab meta·Spine 아틀라스·png meta 재임포트 churn 은 **스테이징하지 않는다**(단위 2 가 Layer Lab 을 지운다). 6.6 핀(`ProjectVersion` · `manifest` · `lock` · `ProjectSettings`)은 `unity-6-6-upgrade` 가 실었다.

## 분류 정정 (단위 0+1 구현 중 확인 — 조사표와 다른 점)

- **KEEP 으로 재분류**: `Core/AppBootstrap`(60fps 캡·PrimeTween 용량 — 앱 전역 훅, 아웃게임 참조 0) · `UI/UiOverlay`(딤 상수 — 전투 브리핑 스트립이 읽음) · `UI/Layout/` 4(전투 HUD 가 씀) · `Data/{KeyringStyle,DragSwaySettings,UnitKitSummary,UnitLabels,UnitStatReadout}`(전투 뷰·카드 문안이 씀) · `Data/StatImport/SheetFetcher` + `*Applier`(에디터 임포터가 씀 — Q2) · `Core/Dreamcatcher/{CycleDeck,AttachEval}`(코어·빌더가 씀).
- **이동**: `UI/Outgame/CardCategoryStyle` → `UI/Dreamcatcher/`(손패 카드면이 씀) · `Core/Api/ApiEnvelope` → `Data/StatImport/`(시트 임포터의 서버 프록시 응답 파서, 네임스페이스 `Wassup.Data.StatImport`) · `UI/KeyringSim` → `Presentation/MotionMath`.
- **Layer Lab 은 KEEP(추림)**: 조사표의 「피참조 0」은 틀렸다 — `Casual Character_SkeletonData.asset` 이 디펜더 23 · 적 19 의 `skeletonDataAsset` 이다. `Editor/LayerLabPresetImporter` 도 그 외형 임포트 도구라 유지. 단위 3 에서 스켈레톤 세트 + `LayerLab.ArtMaker` 스크립트만 남긴다(2026-10-07).
- **키링 → KEEP**: `CoreRetireFlightPresenter` 가 `DragSwaySettings.style`(ringSprite · worldRing/CordMaterial) 을 읽는다. 홀로그램 셰이더·머티리얼·스프라이트·`KeyringStyleHologram.asset` 유지(2026-10-07).
- **옛 `Core/StressMath`**: 코드 소비처가 `UI/ResultScreen` 뿐이라 함께 삭제(데이터 파일의 언급은 주석). 테스트 1건은 코어 `Wassup.BattleCore.StressMath` 로 재조준.
- **옛 `Core/GimmickSelection`**: 코어 `Owners/GimmickSelection` 과 API 동일 → 테스트를 코어로 재조준하고 옛 파일 삭제.

## 후속 후보

- 시트 에디터 임포터 제거(Q2 유보 — 사용자 요청 시).
- `Logging/BattleLogger`(주석에서만 언급, 사망 추정) · `Rendering/PaletteSanityProbe` · `Core/Dreamcatcher/{CycleDeck,AttachEval}` · `Art/{UI,Season,Dreamstones}`(참조 0 다수) — 단위 1·2 에서 참조 재확인 후 처분, 미확인이면 여기로.
- `.claude/skills/catchup` 을 정리본 기준으로 재작성.
- Unity 6.6 전환(`unity-6-6-upgrade`) — 원인 5종·수정 15줄은 메모리/프로브에 기록.
