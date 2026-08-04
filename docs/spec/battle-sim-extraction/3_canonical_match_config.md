# 3 — canonical MatchConfig blob + configHash

## 목적

골든의 "같은 조건" 보장은 스탯 SO 스냅샷만으로 부족하다 — 씬 상주 gameplay knob(스폰 spread, `enableAdjacencySynergy` 등)도 결과를 바꾼다. 한 판의 조건 전체(맵·웨이브플랜 **생성 결과**·덱·seed·유닛/스킬/투사체/해저드/기믹 스탯·점수 룰·씬 knob)를 **불변 blob으로 물질화**하고 canonical 직렬화의 `configHash`를 만든다. 골든 diff 발생 시 "시트 드리프트 vs 코드 회귀"를 해시로 먼저 가르는 1차 판독 장치이자, 이후 AMR·커맨드로그의 공통 필드다. 셋업 단계는 이미 결정적이다(2026-08-03 교차검증 정정 — 웨이브 생성 `WavePatternGenerator`·기믹 선택 `GimmickSelection` 은 `Unity.Mathematics.Random` + `MatchSeed.Derive*` 파생. `UnityEngine.Random` 은 매치 시드 1회 생성 진입점뿐); 어느 쪽이든 생성 **결과**가 blob에 실리므로 셋업 난수는 sim 상류로 격리된다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/MatchConfigSnapshot.cs` (또는 Battle 하위) — 수집·canonical 직렬화·SHA 해시
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `StartBattle` 직전 수집 지점 (씬 knob 필드 목록화 포함)
- `Assets/_Project/Scripts/UI/Outgame/LoginAutoImport.cs` — 테스트/하네스 모드 차단 가드 (시트 임포트가 SO를 덮어 골든을 오염시키는 기존 함정 방어. 임포트는 비동기·논블로킹이라 같은 빌드·같은 시트에서도 판 시작 시점에 따라 스탯이 갈릴 수 있고 — 주석이 명시한 트레이드오프 — 릴리즈 빌드는 구독 자체를 안 한다: `_devEnabled = Debug.isDebugBuild || Application.isEditor`)

## 구현

수집 범위는 "게임 결과에 영향을 주는가"로 판정 — 뷰 전용 값(비주얼 스케일·그림자 등)은 제외. canonical 직렬화는 필드 순서 고정·문화권 불변 포맷(invariant)·부동소수 R 포맷으로 재현성 확보. 씬 knob 전수는 Bridge SerializeField 87개를 gameplay/presentation으로 분류해 목록을 이 unit에 기록(M1 salvage 판정의 입력 재활용). 해시는 골든 덤프(unit 4) 헤더에 동봉.

### BattleBridge SerializeField 87개 분류

2026-08-04 코드 기준 본체 72개 + partial 15개 = 87개다. `gameplay source`의 SO/서비스 참조 자체를 blob에 넣는 것이 아니라, `StartBattle` 직전의 **해결된 결과와 plain 값**(ActiveDeck, GeneratedMap/WavePlan, effect-tile 배치, 실제 loadout 등)을 넣는다.

| 분류 | 수 | 필드 |
|---|---:|---|
| gameplay source / knob (본체) | 14 | `deck`, `mapPool`, `endlessEncounter`, `fixedMapSeed`, `seasonRegistry`, `tileSize`, `scoreRules`, `defenderPool`, `spawnSpreadEnabled`, `spawnSpreadFraction`, `spawnSpreadTopScale`, `spawnSubLaneCount`, `stackModifierAuthoring`, `enableAdjacencySynergy` |
| gameplay timing (BossLeap partial) | 1 | `bossLeapTotalSeconds` — 이 시간이 끝날 때 landing slam을 생성하므로 단순 뷰 보간값이 아님 |
| presentation / adapter / service (본체) | 58 | `spawnHeight`, `resultScreen`, `draftController`, `skillRuntime`, `_placementPhaseView`, `_giftPhaseView`, `spineUnitPool`, `enemyViewPool`, `defenderFallbackViewPool`, `enemyDragDimAlpha`, `enemyDragDimFadeSpeed`, `spineDefenderYOffset`, `vfxSpawner`, `damageNumberSpawner`, `healthDisplayStyle`, `walkAnimSpeedStyle`, `enemyHitBarSpawner`, `statusFxSpawner`, `tileHealthGaugeLayer`, `dcIconStripSpawner`, `unitHealthPresentationMode`, `unitOverheadUiLayer`, `beamPresenter`, `scoreHud`, `scoreTallyView`, `_bossWarning`, `_projectileViewPool`, `placementInput`, `boardViewMode`, `tilemapMapView`, `tileSet`, `tilemapCameraPresetRect`, `tilemapCameraPresetIso`, `tilemapCharacterScale`, `tilemapBillboardTilt`, `propDistanceTiltFactor`, `propDistanceTiltMin`, `propDistanceTiltMax`, `blobShadowSprite`, `blobShadowSize`, `blobShadowColor`, `blobShadowGroundY`, `liftScalePerHeight`, `liftScaleMax`, `liftShadowFullHeight`, `liftShadowMinScale`, `liftShadowMinAlpha`, `useRealShadows`, `mobilePropBudgetScale`, `tilemapHiddenEnvironment`, `pickupViewPrefab`, `pickupViewHeight`, `pickupModelScale`, `pickupModelBaseY`, `pickupOverrideMaterial`, `resignationViewPrefab`, `resignationViewHeight`, `dcProcImpactMinIntervalSec` |
| presentation timing/curve (leap partials) | 14 | `bossLeapRecoilSeconds`, `bossLeapRecoilDip`, `bossLeapArcHeightFactor`, `bossLeapArcMinHeight`, `bossLeapLaunchControl`, `bossLeapLandingHeight`, `bossLeapHangPower`, `bossLeapLandingSquash`, `bossLeapLandingSquashSeconds`, `ultimateLeapAscendSeconds`, `ultimateLeapDescendSeconds`, `ultimateLeapHeight`, `ultimateLeapLandingSquash`, `ultimateLeapLandingSquashSeconds` |

합계: gameplay 15 + presentation/service 72 = **87**. `spawnHeight`는 모든 combat 좌표에 동일하게 더하는 Unity 공간 임베딩이라 현행 판정에는 영향이 없어 presentation으로 분류한다. UltimateLeap은 피해·텔레포트가 sim에서 이미 확정된 뒤 뷰만 투영하므로 전부 presentation이다.

### 구현 메모

- `MatchConfigSnapshot`은 schema/ruleset, seed, 생성 맵(장식 `propLayerId` 제외), 생성/작성 웨이브 결과, legacy deck, effect-tile 결과, 유닛·스킬·투사체·해저드·기믹 SO 그래프, 점수·코스트·드림스톤·드림캐처 및 위 gameplay knob를 고정 순서로 즉시 문자열화한다.
- 문자열/목록의 의미 순서는 보존하고, 비순서 effect-tile map은 `(y,x)`로 정렬한다. float/double은 invariant `R`, hash는 BOM 없는 UTF-8 SHA-256 소문자 64자리다.
- snapshot은 canonical 문자열과 hash만 보유하므로 이후 SO 변경이나 NativeArray dispose의 영향을 받지 않는다.
- 테스트/하네스 carry가 잡히면 `RuntimeImportsBlocked`를 match teardown까지 유지한다. `LoginAutoImport` 진입과 세 leaf refresher의 비동기 적용 직전을 모두 막아, 로비에서 이미 출발한 요청도 SO를 덮지 못한다.

## 완료 기준

- 같은 조건 2회 실행 → `configHash` 동일. 스탯 SO 값 1개 변경 → 해시 변경.
- 테스트/하네스 lock 동안 LoginAutoImport가 실행되지 않고, teardown 뒤에는 one-shot이 정상 실행 가능함을 확인(로그/테스트).
- gameplay/presentation knob 분류표가 이 unit 문서에 기록됨.

## 검증 결과 (2026-08-04)

- Unity MCP 스크립트 컴파일: 오류 0.
- 집중 EditMode: canonical config, test carry/import lock, 세 runtime refresher callback 경계 포함 **37/37 통과**.
- 전체 EditMode: **1,883건 중 1,881 통과, 실패 0, 기존 Ignore 2**.
- 실제 Play 하네스 2회: 각 306 ticks × 20Hz, `configHash=9293e3e11f7c023cdeaa5eb49644b0e540134ab617249b8630dcf926f50fe48e`, 7,727-byte digest 완전 동일.
- Track A common review: **APPROVE**. Track B `$ecs-reviewer`: **APPROVE**. 더 엄격한 최종 판정: **APPROVE**.
- 완료 커밋: `11902d32`.

## 골든 게이트 정지 사건 — 원인 2개 (2026-08-05)

**증상**: unit 13 진행 중 골든이 멎었다. `normal` 두 실행의 `configHash` 가 서로, 또 커밋된 골든과도
달랐고 **매번 새 값**이었다(`c85c3208`/`bbdbcfd0` → `7ccadf0d`/`c68c22bf`).

**진단 도구가 먼저다**: `LegacyTraceGoldenRunner` 가 이제 canonical blob 을
`Library/LegacyTraceV0/{scenario}.run{N}.blob.txt` 로 덤프한다. 해시가 갈리면 **두 블롭을 diff 하면
그 줄이 범인**이다. 이 결함은 정렬·반사 순회를 의심하며 세 번 오진했고 블롭 diff 는 한 번에 지목했다.
범인은 `skillLoadout` 이었다 — run1 `{power_surge, slow_field}` vs run2 `{slow_field, tornado}`.

### 원인 ① (근인) 세션 오염이 하네스의 **진입 경로**를 갈아탔다

Editor.log 증거: 정상 세션의 하네스는 `[DraftController] Roll 완료` 를 남긴다. 실패 구간
(687000~687600)에는 그 줄이 **0개**다 — 즉 draft 가 아니라 **squad/TestMode 경로**로 들어갔다.
그 경로만 `SetSkillLoadout(...)` 을 호출하므로, draft 경로에서는 `skillLoadout=null` 로 캡처되던
필드가 그 세션에서만 실제 스킬 2개로 채워졌다. 원인은 선행 PlayMode 스위트가 `PlayerProfileSO` 를
**메모리에서** 스쿼드 보유 상태로 만든 것이고, `Assets/_Project/Data` 강제 재임포트가 디스크 값으로
되돌려 치료된다(러너 `ReimportData` 모드). **도메인 리로드로는 낫지 않는다** — 에셋 인스턴스는
리로드를 넘어 산다.

⇒ **위생 규칙: 골든으로 검증할 세션에서 PlayMode 스위트를 먼저 돌리지 않는다.** 이미 돌렸으면
`ReimportData` 후 골든을 돌린다.

### 원인 ② (잠재) squad/TestMode 경로의 로드아웃이 **벽시계**로 굴렀다

`SkillLoadoutController.Roll()` 은 `_seed == 0` 이면 `DateTime.UtcNow.Ticks` 로 폴백하는데, 로드아웃을
굴리는 세 경로(Draft·Squad·TestMode) 중 **아무도 시드를 주지 않았다**. `MatchSeed` 의 다른 7계열
(map·wave·visual·pickup·gimmick·meteor·bomb)은 전부 `Derive*` 를 쓰는데 스킬만 빠져 있었다.
오염이 진입 경로를 갈아타자 이 결함이 캡처된 설정으로 드러난 것이다 — **오염이 원인 ②를 만든 게
아니라 노출했다.** squad 는 실제 게임 경로이므로 이건 라이브 결정론의 구멍이다.

**수리**: `MatchSeed.DeriveSkillSeed(matchSeed, rollIndex)` 신설 + `GameManager.NextSkillRollSeed()`
가 회차를 소유(매치 시드 확정 시 0 리셋) + 세 경로가 `Roll(seed)` 로 굴린다. **회차를 섞는 이유**:
REDRAFT 는 같은 매치에서 새 조합을 줘야 하므로 매번 같은 2개면 재드래프트가 무의미해진다. 회차를
넣어 조합은 갱신되지만 같은 `matchSeed` 의 롤 **순서 전체**가 재현된다.

### 결과 — 재기준선 없음

골든 7종 재생성: **two-run diff 0 · 커밋된 코퍼스와 byte 동일**(백업 대비 `cmp` 7/7). `normal` 블롭의
SHA-256 = `d3ded5d09609ff73` = 커밋된 헤더 `configHash` 그대로다. 로그 전체에서 이 해시는 **26회**
재현되고 흔들린 구간은 오염된 그 한 창뿐이다. ⇒ 골든을 다시 찍을 필요가 없었다. unit 13 C3 의
"골든 미검증" 도 이 실행으로 해소된다.

**주의 — 골든의 사각지대**: 코퍼스는 **draft 경로**를 녹음하고 거기서 `_skillLoadout` 은 null 이다
(하네스가 `ConfirmDraft` 를 거치지 않고 `BeginPlacement` 를 직접 부른다). 그래서 **골든은 로드아웃
결정론을 증인할 수 없다** — 원인 ②의 회귀 방지는 `MatchSeedTests`·`SkillLoadoutControllerTests` 의
EditMode 6건이 진다. squad 경로를 덮는 시나리오 추가는 후속 후보(코퍼스 변경 = unit 19 권한).
