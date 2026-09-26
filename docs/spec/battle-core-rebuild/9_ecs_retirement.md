# 9 — ECS 은퇴: 옛 전투를 지우고 문서를 새 코어 기준으로 (조각 E)

## 목적

**README 부수 질문에 답한다 — 「전투 코드 어디에도 `Unity.Entities` 가 없고, `manifest` 에서 Entities 계열이 사라져도 asmdef 가 초록인가.」** 8c 가 「지워도 남는 것은 아무것도 안 부른다」를 컴파일로 증명했다. 여기서는 **8c `ledgers/retire-set.md` 가 퇴역으로 판정한 것만** 지운다. 동결 장치·옛 리뷰 도구·옛 lane 을 걷고, 옛 구조를 가리키는 문서와 CLAUDE.md 를 새 코어 기준으로 바꾼다. 규칙 변경은 0 이고, 골든 11종 무변이 그 증언이다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 코드·자산 삭제 | `ledgers/retire-set.md` 의 목록 **그대로**(`Scripts/Battle/**`·`Bridge/**`·`Editor/Battle/**` 포함 — tools 1~3·5 는 코어판 완료, 4 `SimOrderDumpMenu` 은퇴). 규칙 보유자 10 이라도 8c 가 이사시킨 부분(`FilterHiddenSkills` 등)은 이미 밖에 있다 |
| 씬·설정 | `Scenes/BattleScene.unity`(8b 가 빌드 목록에서 뺐다 — 폴더 안 볼륨 4개는 8c 가 옮겼다) · `Tests/Golden/**`(옛 코퍼스 — 새 것은 `Tests/GoldenCore/`) · `ProjectSettings/EntitiesClientSettings.asset` |
| 패키지 | `Packages/manifest.json` 에서 `com.unity.entities`·`com.unity.entities.graphics` 제거 |
| asmdef | `Wassup.Runtime` −`Unity.Entities`·`Unity.Entities.Graphics`·`Unity.Transforms`·`Unity.Burst` · `Wassup.Tests.EditMode`·`.Assets`·`.PlayMode` −`Unity.Entities`·`Unity.Transforms`(·`Unity.Burst`) · `Wassup.Skills`·`Wassup.UnitAi` −`Unity.Burst` |
| 헤드리스 lane | `tools/battle-core-rebuild/headless/BattleCoreUnity.Check.csproj:56~57`(`Unity.Entities.dll`·`Unity.Transforms.dll`) — `:55 Unity.Burst.dll` 은 패키지가 남으니 둔다 |
| 옛 씬 경로가 남은 곳 | `ResultScreen` 의 `MatchTally` 입력(8a 가 남긴 이중 입력) · `CameraDirector`·`SoundManager` 의 `GameManager` 구독(8a 가 옛 씬용으로 남겼다) — **8c 가 `*.OldBattle.cs` 부분 파일로 떼어 `retire-set.md` 에 올렸다: 파일 삭제만 하면 된다**(스트립의 덱 경로 `WavePatternStripView.OldBattle.cs` 도 같다) |
| 동결 훅 | `.githooks/commit-msg` 삭제 · `git config --unset core.hooksPath` · CLAUDE.md 「동결 장치」 줄 |
| 리뷰 도구 | `.claude/agents/ecs-reviewer.md` · `.claude/skills/ecs-reviewer/` · `.claude/skills/two-track-review/` · `.codex/skills/ecs-reviewer/` · `.codex/hooks/two-track-review-stop-gate.mjs` · `.codex/hooks/ecs-review-detector.mjs` · `.codex/hooks.json` · `.claude/hooks/ecs-review-detector.mjs` → 옛 분기를 빼고 `core-review-detector.mjs` 로(`.claude/settings.json` 경로 갱신) · `.claude/agents/core-reviewer.md:8` 「use ecs-reviewer」 안내 |
| 네임스페이스 | `Wassup.Battle.Units`(`Faction.cs`·`FactionRelation.cs`) → `Wassup.Skills` · 8c 가 이사한 저작 타입의 `Wassup.Battle.*` → `Wassup.Data` |
| 잔여 이중화 | `Data/WavePatternGenerator.cs`·`Data/BonusWaveSchedule.cs`·`Data/GeneratedWavePlan.cs`·`Data/BattleConfig.cs`(+ 자산) — 8a 뒤 소비처 0 을 컴파일로 확인하면 삭제 |
| 문서 | `docs/reference/` 18편 · 스킬 5 · `CLAUDE.md` — 아래 표 |

## 구현 (커밋 순서)

1. **삭제는 동결 훅이 살아 있는 동안 `[old-battle]` 태그로 한다.** 훅을 먼저 지우면 「옛 전투를 지운 커밋」이 태그 없이 남아 이력에서 안 보인다. 훅 해제는 **마지막 커밋**이다. `core.hooksPath` 는 워크트리 **공용 config** 라서 `--unset` 하면 main 워크트리의 훅도 같이 꺼진다. 그러니 브랜치가 main 에 머지된 뒤(10 의 경계 2) 한 번만 한다. 그 전에 main 에서 옛 전투 경로를 커밋할 일은 없다(폴더가 이미 없다).
2. **테스트 삭제에는 짝이 있어야 한다.** 옛 lane 테스트를 지울 때 파일마다 「같은 규칙을 증언하는 코어 테스트」 한 줄을 `retire-set.md` 에 적는다. `rules.md` 필수 행의 테스트 열이 그 지도다. 짝이 없으면 코어 테스트로 먼저 옮긴다. 비교가 본질인 테스트(`AttackReachParityTests` — 옛 `AttackReach` ↔ 새 것 2만 건)는 짝 없이 은퇴한다. 근거는 계약 3 「옛 러너와 병존하는 동안만 A/B」.
3. **옛 PlayMode lane 은 없애지 않고 아웃게임 lane 으로 줄인다(에이전트 결정 — 초안의 「PlayMode.Core 로 합친다」 철회).** 진입 테스트 6건은 8b 가 이미 새 씬 대상으로 다시 썼다. 남는 것은 아웃게임 테스트다(`AuthE2ETest`·`DeckInfoPresetApplyLiveE2ETest` — 둘 다 `[Explicit]` 라이브 서버 · `PresetBarPopupLayerTest`). 이것들을 「Core」 이름의 lane 에 섞으면 lane 의 뜻이 흐려진다. 그래서 `Wassup.Tests.PlayMode` asmdef 에서 Entities·Transforms 참조만 빼고 존치한다. `test-procedure.md` 에 「아웃게임 PlayMode · 씬 부팅 없음」으로 적는다.
4. **남기는 패키지.** `com.unity.burst`·`com.unity.collections` 는 **지우지 않는다**. URP 의 `com.unity.render-pipelines.core` 가 둘 다 의존하고(`Packages/packages-lock.json`), `Data/GeneratedMap.cs`·`DioramaMapBuilder.cs`·`ProjectilePatternData.cs:33` 이 `NativeArray`·`FixedList128Bytes` 를 쓴다. `Unity.Burst` asmdef 참조는 `[BurstCompile]` 0 건을 확인한 뒤 뺀다. `Wassup.UnitAi` 가 Burst 를 참조하던 이유는 Burst 로 컴파일된 옛 시스템이 그 어셈블리를 불렀기 때문이고, 그 호출자가 사라진다.
5. **네임스페이스 정리는 삭제 뒤 한 커밋이다**(8c 구현 8의 결정). 기계적 치환이라 규칙 diff 가 섞이지 않게 따로 둔다. 파일 수는 삭제 뒤 재측정한다(2026-09-25: 전체 313 · 옛 폴더·옛 PlayMode 제외 157 · 옛 EditMode 까지 제외 87). 직렬화 영향은 0 이다(열거형 필드는 정수, 스크립트는 GUID, 자산에 이 네임스페이스 문자열 0).
6. **덱 타이머 폴백은 「이미 해소 — 필드 존치」로 닫는다**(README 작업 표 unit 4 행 「unit 9 에서 제거」 정정). 판 길이는 이미 모드 단독이다(`MatchDefinitionBuilder.cs:340` `MatchSeconds = m.durationSec`, 덱 폴백 코드 없음). 덱의 `timerDurationSec` 는 웨이브 간격에 들어가는 조건이 `MaxWaveIntervalSec ≤ 0` 일 때뿐이다(`BattleCore/Wave/WaveGenerator.cs:70~75`). 라이브 덱 12종은 전부 `maxWaveIntervalSec: 20` 이라 영향이 없다. 반면 그 값은 `configHash` 정준 문자열에 들어간다(`WaveDefs.cs:173·249`). 필드를 지우면 골든 11종 해시가 전부 바뀌므로 **지우지 않는다.**
7. **옛 웨이브 생성기 삭제는 스킬 갱신과 같은 커밋이다.** `enemy-wave-integration` 스킬이 `WavePatternGenerator.*` 를 규칙 근거로 인용하고(`SKILL.md:36~39·79·104`), 스킬의 「갱신 트리거」 표가 같은 커밋을 요구한다. 인용 대상을 코어 `WaveGenerator` 의 같은 함수로 바꾼다. 생성기 소비처가 남아 있으면(8a 구현 5 가 스트립을 끊지 못했으면) 지우지 않고 8c 표의 `:639` 행을 이사로 처리한다.
8. **옛 씬 경로로 남긴 이중 입력을 걷는다.** `ResultScreen` 의 `MatchTally` 입력 → `MatchTally.cs` 삭제. `CameraDirector`·`SoundManager` 의 `GameManager` 구독 → push 만 남긴다.

### 문서 목록 (2026-09-25 grep `BattleBridge|Unity.Entities|ISystem|EntityManager|NativeQueue|Scripts/Battle/|BattleScene|SpineUnitView` — README 의 「14건」은 18편이 됐다)

| 문서 | 적중 | 처분 |
|---|---|---|
| `battle-core-architecture.md` | 24 | §1 유지 · **§2~§10 을 코어 구조 지도로 교체**(담당자 8 · `TickPipeline` 단계 · `EventOrder`/`ViewOrder` · 드라이버/뷰 풀) — 원천 `class-diagram.md` |
| `object-pipeline-map.md` | 38 | 8c 에서 완료 — 삭제 뒤 심볼 존재만 재확인 |
| `test-procedure.md` | 1 | lane 표: `EditMode`·`.Assets`·`.Core`·`PlayMode.Core`·`PlayMode`(아웃게임) + 헤드리스 |
| `weapon-trail-authoring.md` | 5 | 호출처 `SpineUnitView.AttachWeaponTrail`(`:159`) → `CoreSpriteUnitView.cs:360` 등 새 호출처 |
| `shadow-quality-settings.md` | 1 | `BattleScene` 의 조명 기술(`:10`) → 새 씬 |
| `enemy-movement-algorithm.md` · `map-wave-balancing.md` · `score-formula.md` · `ingame-flow.md` · `map-stage-authoring.md` · `dreamcatcher-card-schema.md` · `dreamcatcher-portability.md` · `review-skill-comparison.md` | 3·3·4·3·4·3·5·3 | 심볼·경로를 코어로. `ingame-flow.md` 1축 「지지 않는다」 → **모드별**(`match-mode-design.md` 사용자 판정 2) |
| `lessons/01·02·03·04·README` | 2·2·5·4·1 | 옛 경로는 「이력」 표기, 교훈 문장은 유지 |
| `docs/spec/unit-stats-and-modifiers/1·2` | — | 상단에 은퇴 표기(고정소수점 scale 1000 미채택 — README 후속 후보) |
| 스킬 `unity-vfx-integration`(SKILL 12 + `ecs-bridge-pattern.md` 12) · `unity-feature-wiring`(10) · `unity-vfx-authoring`(3) · `unity-prop-tile-authoring`(2) · `enemy-wave-integration` | | 브리지·NativeQueue 절 → 「코어 사건 → 뷰 풀 구독」. `ecs-bridge-pattern.md` 삭제 |
| `docs/spec/**` 옛 포인터 ~1,157 | | **두는 것이 규칙**(역사서 — README 후속 후보) |

### CLAUDE.md 편집 목록 (줄 = 2026-09-25 기준)

| 자리 | 편집 |
|---|---|
| `:7~13` 「⚠ 현재 상태 — 전투는 전환 중」 | 삭제. 기술 스택 앞에 한 줄: 「전투 = 순수 C# 전투 코어(`battle-core-rebuild` 완료 YYYY-MM-DD)」 |
| `:36~38` 기술 스택 | 아키텍처 = 코어 + Mono 드라이버/뷰 · 필수 패키지에서 Entities·Entities Graphics·Jobs 제거(Burst·Collections 는 URP 의존으로 남는다고 적는다) · 「ECS 버전 기준」 줄 삭제 |
| `:41~57` 「옛 전투 — ECS 맥락 분리」 | 절 전체 삭제(채널 31 목록 포함) |
| 절대 제약 `:61·62·64·81`(1·2·4·12) | 한 줄 은퇴 표기 — **번호는 보존**(제약 8 이 같은 관례) · 3 은 「네트워크 코드 완전 금지」만 |
| `:80` 제약 11 끝 문장 「Transition 문서를 근거로 ECS 경계나 네트워크 금지를 우회할 수 없다」 | 「전투 코어 제약이나 네트워크 금지」 |
| 제약 10 | 「ECS 시뮬」 → 「전투 코어」 · 모범 `ModifierMath.CombineMul` 경로를 코어(`BattleCore/Effects/ModifierMath.cs`)로 |
| 제약 13 | 인용 심볼(`SkillMath.CellHalfWidthTiles`·`SkillFiredEvent`·`ProjectileHitSystem`·`DeathSiteBlastSkill`)을 코어 정본 진입점으로(존재를 grep 으로 확인). 규칙 문면은 무변 |
| 추가 제약 `:141·145·148` | `[옛 전투]` 반절 삭제 · 「전투는 ECS 시스템에서만」 → 「전투 코어에서만」 · 「UI 가 ECS Component 를」 → 「UI 는 코어 읽기 모델·사건만」 · 매니저 예외 2건에 「판 밖 전역이라 코어 제약 1 과 충돌하지 않는다」 |
| `:150~159` 「새 전투 코어 — 절대 제약」 | 꼬리표 제거 · 제목 「전투 코어 — 절대 제약」 · 「절대 제약」 바로 뒤로 이동 · 적용 범위 인용문의 「전환 중」 삭제 |
| `:178` 참조 표 설계도 행 | 「§1 설계 아웃라인 · §2~ 코어 구조 지도」 · test-procedure 행 lane 수치 · 이동 알고리즘 행의 「시스템 순서·MovementSystem」 → 코어 단계 이름 |
| `:179` 참조 표 「전투 전환은 어디까지 왔나」 행 | 「완료 — 이력은 `docs/spec/battle-core-rebuild/`」 |
| `:231` 「변경 대상」 예시 경로 `Scripts/Bridge/BattleBridge.cs` | 새 층 경로(예: `Scripts/BattleCoreUnity/BattleDriver.cs`) |
| `:302` 자가 점검 | `[옛 전투]` 반절 삭제 |
| `:305~320` 「ECS 설계의 불확실성 대응」 | 「전투 코어 설계의 불확실성」으로 교체 — 질문할 가치가 있는 것: 담당자 소속 · 커맨드 vs 사건 · 틱 단계 위치 · 결정론 영향 |
| `:350·353` 테스트 절 「ECS 시스템」 | 「전투 코어」 |
| `:361` 맥락 폴더 금지 | 삭제 |
| `:368` 「방법은 맥락 분리 + …」 | 「담당자 분리 + …」 |

## 이식 제외 — 일부러 안 지우는 것

| 남기는 것 | 이유 |
|---|---|
| Burst·Collections 패키지 | 구현 4 |
| 8c 도달성이 「남는다」로 판정한 옛 폴더 밖 공유 부품(`KeyringSim`·`BlobShadow`·`DcInspectPanelView`·`DreamcatcherCardText` 등) | 새 층이 부른다 |
| ~~`DefenderRetireFlight`~~ | **퇴역**(장부 묶음 3 — 옛 뷰·옛 UI) · 후계 `CoreRetireFlightPresenter`(`BattleCoreUnity/View/`) |
| `TimeManager`·`SoundManager` | 추가 제약의 의도된 예외 |
| 덱·플랜의 `timerDurationSec` 필드 | 구현 6 |
| `docs/spec/**` 의 옛 포인터 | 역사서 |

## 파이프라인 커버리지

N/A — 새 정거장이 없다. 지우는 정거장은 8c 가 맵에서 이미 이력으로 내렸다. 확인할 것은 하나다: 맵의 모든 심볼이 삭제 뒤에도 존재한다(grep).

## 고친 것 (구현 2026-09-25 — spec 과 다른 점)

- **퇴역 목록 밖의 Entities 의존 잎 7 을 3c 로 더했다**(`Presentation/` 의 `BeamPresenter`·`DcAuraVisualPool`·`EnemyHitBarSpawner`·`EnemyHitBarView`·`StatusFxSpawner`·`StatusFxView`·`UnitView` · 939줄 · 리드 기본값 승인). 8c 의 `Retire.Check` 가 `Library/ScriptAssemblies/*.dll`(Entities 포함)을 참조해 「남는 코드가 Entities 를 부르나」를 못 봤다 — Exclude 에 Entities·Transforms·Serialization·Hybrid dll 을 더했다. 증거: 가지치기 export 를 Entities dll 없이 빌드하면 오류 56 이 이 7 파일에만 · 지우면 0 · 사용처는 서로끼리 · 자산 참조는 옛 씬뿐.
- **구현 2(짝 없는 테스트 먼저 옮기기)는 「규칙 누락 의심」 34 중 33 을 옮겼다**(31 `TilemapMapViewTests` 는 대상 자체가 퇴역) — `7482f7ba6`, 짝 지도 `ledgers/retire-test-pairs.md`. **옛 규칙과 코어가 다른 4건**은 코어를 고치지 않고 `[Ignore("unit 9 — 옛 규칙과 다름: …")]` 로 남겼다: ⑴ 방향탄이 관통을 다 쓰면 호밍으로 바꿔 다음 적에게 튕긴다(옛 `ProjectileHitSystem.cs:648~676` · 코어는 관통 소진 = 소멸) ⑵ 감지 후보의 직업 필터(옛 `EnemyTargetFilter.classMask` · 코어 `ReachProbe.IsLegalDetectionTarget` 는 진영·층만) ⑶ 같은 입구 다른 종의 예고선 병합(옛 = 종마다 한 줄 · 코어 `WaveScheduler.CollectForecast` = 입구×경로로 접는다) ⑷ 예보 경로 해석(옛 = 스폰과 같은 해석 · 코어 = 컨셉 슬롯 경로만). 「부분 공백」 51 은 README 후속 후보.
- **남는 테스트가 옛 소스를 런타임에 읽고 있었다**(`Tests/EditMode/ReachEntryPointGuardTests.cs` 의 그물 10 — `Path.Combine(…, "Battle")` 라 `--retire-assets` 의 문자열 검사도 못 잡았다). 대상과 함께 은퇴시키고 `Wassup.Skills` 쪽 그물 4 는 남겼다. 코어의 원점 항은 행동 테스트가 증언한다.
- **네임스페이스 이사의 목적지를 `Wassup.Data` → `Wassup.Data.Authoring` 으로 바꿨다**(구현 5). `Wassup.Data` 로 합치면 `DotElement`·`StatKind`·`StackKind`·`CombineOp` 가 코어 `Wassup.BattleCore.Effects` 의 같은 이름과 부딪혀 두 using 을 다 가진 파일이 CS0104 로 깨진다(실측 `CoreStatusFxSpawner`). 하위 네임스페이스는 이름 해석을 옛 것과 똑같이 둔다. 「자산에 이 네임스페이스 문자열 0」은 틀렸다 — 해저드 자산·프리팹 5개의 `m_EditorClassIdentifier` 가 옛 이름을 든다(에디터 정보용 · 바인딩은 GUID · 영향 0).
- **`check_ledgers.py` 를 삭제 뒤 상태에 맞췄다** — 브리지가 없으면 두 장부를 이력으로 동결(「미정 0」만 본다) · 퇴역 목록이 다 지워지면 총계 대조 생략.
- **패키지 제거 중 Unity 가 `ProjectSettings/EntitiesClientSettings.asset` 을 한 번 다시 만들었다**(첫 refresh 에서 Entities 가 아직 로드된 채) — 재삭제. 첫 refresh 뒤에는 옛 Entities 어셈블리가 도메인에 남아 있었고, 두 번째 refresh 에서 사라졌다.
- **인스펙터 문구 5곳**(Tooltip·Header)이 `BattleBridge` 를 주인으로 적고 있어 새 주인으로 고쳤다(완료 기준 첫 줄).
- **이식 제외 표가 `DefenderRetireFlight` 를 「남긴다」에 적고 있었다**(unit 9 감사 2026-09-25) — 실제로는 장부 묶음 3 으로 퇴역했고 후계는 `CoreRetireFlightPresenter` 다. 표를 정정했다.
- 동결 훅 `.githooks/commit-msg` 는 마지막 커밋에서 파일만 지웠다. `core.hooksPath` unset 은 main 머지 뒤(리드 몫).
- **9d — 삭제 뒤 잔여 정리**(설계 리뷰 2026-09-25 · `0666ce139` 삭제 · `bdc798ffd` 주석). 참조 0 인 파일 16(+`.meta` · 빈 `Core/Trace`) 을 지웠다 — `LegacyTraceRecorder` · `BattleMapBuilder`(+테스트) · Draft 5종(+테스트) · `DamageNumberSpawner` · `DcIconStripView` · `DcActionFlipbookView` · `DeployCutscenePlayer` · `ScoreBurstPool` · `BeamPulse` · `SimHarnessClock`(켜는 곳이 테스트뿐 — `LoginAutoImport` 가드·`TimeManager` 분기·가드 테스트 동반). 빈 partial 훅(`SoundManager`·`CameraDirector`)과 조각 하나뿐인 partial 키워드 6 도 걷었다. 테스트는 보존했다 — 데미지 숫자 배치 테스트는 바이트 동일한 코어 사본을 겨누고, 맵 연결성 첫 케이스는 손 픽스처를 쓴다. `PaletteSanityProbe` 는 씬에 떨궈 쓰는 수동 진단 도구라 남겼다. 현재형 거짓 주석 58 파일을 현재 주인 또는 「옛 … — 이력」 으로 고쳤다(코어 무접촉).

## 완료 기준

- [x] 비주석 grep 0: `Unity\.Entities|EntityManager|SystemAPI|\bISystem\b` · `BattleBridge` · `Wassup\.Battle(\.|;)`(`Assets/_Project --include=*.cs`, `//`·`///` 줄 제외 — 코어·Skills 주석의 「옛 `Wassup.Battle.Effects.*` 의 미러」 설명은 정당한 이력이다, 예: `HazardDef.cs:16·27`·`SkillCcKind.cs:5`). — **○** `Wassup\.Battle(\.|;)` 0 · `BattleBridge` 0 · Entities 계열 1 = `CoreArchitectureTests.cs:89` 의 부재 단언 문자열(`StringAssert.DoesNotContain("Unity.Entities", …)`) — 그물 자체다.
- [x] `manifest.json` 에 `entities` 0 · lock 재생성 뒤 `com.unity.serialization`·`scriptablebuildpipeline` 이 전이로 사라진다(남으면 누가 끄는지 적는다). — **○** manifest 0 · lock 에서 entities·entities.graphics·serialization·scriptablebuildpipeline·profiling.core 소멸. burst·collections 는 남는다(구현 4).
- [ ] Unity 콘솔 컴파일 에러 0 · asmdef 전부 초록 · EditMode(`.EditMode`·`.Assets`·`.Core`) 선행 2 외 빨강 0 · PlayMode.Core·PlayMode(아웃게임) 초록 · 테스트 **총계가 줄어든 만큼이 삭제 목록 합과 같다**(안 돈 테스트 판별). — **△(아웃게임 PlayMode 의 `[Explicit]` 라이브 서버 2 만 환경 빨강)** 콘솔 에러 0 · EditMode 2706→**1320**(−1376 삭제 파일 + −10 그물) · .Assets 360→**287**(−73 = 삭제 6 파일 · `ValueSource` 2×3 포함) · 선행 2 외 빨강 0 · .Core **891** 무변(건너뜀 4 = 규칙 차이) · PlayMode.Core **95/95** · PlayMode(아웃게임) 2/4 — `PresetBarPopupLayerTest` 2 초록, `[Explicit]` 라이브 서버 2 빨강(`AuthE2ETest` 닉네임 중복 · `DeckInfoPresetApplyLiveE2ETest` 로그인 필요 — 환경 · 삭제와 무관).
- [x] 골든 11종 무변(Unity) · 헤드리스 3종(build 0 · test · Check 0 — Entities dll 참조 없이). — **○** `Golden/Verify` 11건 일치(삭제 뒤 · 네임스페이스 뒤 두 번) · 헤드리스 export build 0 · test **876/880**(680 + 이식 200 · 건너뜀 4) · Check 0 · `Retire.Check` 0(Entities dll 제외).
- [x] 로비 콜드 스타트 `targetFrameRate == 60`(옛 훅이 사라진 뒤라 이번엔 판별력이 있다 — 8b 완료 기준의 두 번째 측정). — **○** OutgameScene Play 5.7초 시점 `targetFrameRate=60` · `vSyncCount=0`(옛 `GameManager` 훅은 삭제됨).
- [ ] **Android QA 빌드 2회째**(Entities 없이) 성공 · APK 크기 전후 기록 · 실기기 1판. — **보류** 미시도(사용자 몫).
- [ ] `.githooks/commit-msg` 없음 · `git config --get core.hooksPath` 빈 값(main 머지 뒤) · 리뷰 감지기는 `Scripts/BattleCore/` 에서만 울린다. — **△** 훅 파일 삭제 · 감지기 = 코어 경로만(임시 저장소로 확인 — 옛 `Scripts/Battle/` 변경에는 안 울린다) · hooksPath unset 은 머지 뒤(리드).
- [x] 문서 18편 + 스킬 5 + CLAUDE.md 편집 목록 전 행 처리 · `grep -rn "\[옛 전투" CLAUDE.md` = 0. — **○** `66e122ec6`·`45d43c8a0`·`10ea8cffe`(스킬 5번째 = 생성기 삭제 커밋) · 루트 README 도 · grep 0.
- [x] `core-reviewer` **APPROVE**(2026-09-25 — CRITICAL·HIGH·MEDIUM 0 · LOW 2 = 장부의 `BoardSpaceTests` 표기(이식 대체 주석으로 정정) · `[Explicit]` 어셈블리 실행 제한 미기록(`test-procedure.md` 에 기록)). 삭제 = 퇴역 목록 1:1 · 코어 diff 는 네임스페이스 치환만 · [Ignore] 4건은 옛 규칙과 코어의 차이가 맞다(독립 판정).

구현 2026-09-25 — `66e122ec6`~`9a8756016` + 훅 삭제(마지막 커밋). 옛 규칙 변경 0(규칙이 다른 4건은 `[Ignore]` 로 기록만).

리드 재검증 2026-09-25 — HEAD `2c298b3b1`(= `d2f70a7a8` + Check csproj 의 Entities·Transforms dll 참조 제거 — `60c09db21` 에서 빠진 spec 변경 대상 :56~57): 클린 export build 0 · test 876/880(Ignore 4) · Check 0(Entities 참조 없이) · manifest entities 0 · 장부 3종 exit 0 · 옛 폴더 4 부재 · asmdef 에 Entities/Burst/Transforms 0 · 골든 파일 diff 0. Unity: 도메인 Entities 어셈블리 0 · 로비 콜드 스타트 fps 60 · EditMode 3 어셈블리 2496/2498(선행 2) · PlayMode 코어 95/95 · 아웃게임 `PresetBarPopupLayerTest` 2/2 · 골든 Verify 11 일치. 남은 것 = 옛 규칙 차이 4건 사용자 결정 · 플레이 4차 · main 머지(→ `core.hooksPath` unset) · Android QA 빌드.

9d 리드 재검증 2026-09-26(HEAD `2f13fa90a`): core-reviewer **APPROVE**(finding 0 · LOW 1 = 무참조 6 파일 후속). 클린 export build 0 · test 894/894 · Check 0 · Retire.Check(전체 트리) 0 · 코어·골든 diff 0. Unity: 콘솔 에러 0 · Missing Script 0 · EditMode 3 어셈블리 2495/2497(선행 2 — 총계 −15 = 삭제 테스트) · PlayMode 코어 97/97(단, 첫 실행에서 `CoreShapeGuideTests` 1건 빨강 — 사용자 플레이가 `dev_forceMapIndex` 를 0→3 으로 바꿔 픽스처 지형이 사라진 **머신 상태** 문제, 0 으로 단독 3/3 초록 확인 뒤 3 복원) · 골든 Verify 11 일치.
