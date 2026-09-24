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
| 옛 씬 경로가 남은 곳 | `ResultScreen` 의 `MatchTally` 입력(8a 가 남긴 이중 입력) · `CameraDirector.cs:141~149`·`SoundManager.cs:133~151` 의 `GameManager` 구독(8a 가 옛 씬용으로 남겼다) |
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
| 8c 도달성이 「남는다」로 판정한 옛 폴더 밖 공유 부품(`KeyringSim`·`BlobShadow`·`DefenderRetireFlight`·`DcInspectPanelView`·`DreamcatcherCardText` 등) | 새 층이 부른다 |
| `TimeManager`·`SoundManager` | 추가 제약의 의도된 예외 |
| 덱·플랜의 `timerDurationSec` 필드 | 구현 6 |
| `docs/spec/**` 의 옛 포인터 | 역사서 |

## 파이프라인 커버리지

N/A — 새 정거장이 없다. 지우는 정거장은 8c 가 맵에서 이미 이력으로 내렸다. 확인할 것은 하나다: 맵의 모든 심볼이 삭제 뒤에도 존재한다(grep).

## 완료 기준

- [ ] 비주석 grep 0: `Unity\.Entities|EntityManager|SystemAPI|\bISystem\b` · `BattleBridge` · `Wassup\.Battle(\.|;)`(`Assets/_Project --include=*.cs`, `//`·`///` 줄 제외 — 코어·Skills 주석의 「옛 `Wassup.Battle.Effects.*` 의 미러」 설명은 정당한 이력이다, 예: `HazardDef.cs:16·27`·`SkillCcKind.cs:5`).
- [ ] `manifest.json` 에 `entities` 0 · lock 재생성 뒤 `com.unity.serialization`·`scriptablebuildpipeline` 이 전이로 사라진다(남으면 누가 끄는지 적는다).
- [ ] Unity 콘솔 컴파일 에러 0 · asmdef 전부 초록 · EditMode(`.EditMode`·`.Assets`·`.Core`) 선행 2 외 빨강 0 · PlayMode.Core·PlayMode(아웃게임) 초록 · 테스트 **총계가 줄어든 만큼이 삭제 목록 합과 같다**(안 돈 테스트 판별).
- [ ] 골든 11종 무변(Unity) · 헤드리스 3종(build 0 · test · Check 0 — Entities dll 참조 없이).
- [ ] 로비 콜드 스타트 `targetFrameRate == 60`(옛 훅이 사라진 뒤라 이번엔 판별력이 있다 — 8b 완료 기준의 두 번째 측정).
- [ ] **Android QA 빌드 2회째**(Entities 없이) 성공 · APK 크기 전후 기록 · 실기기 1판.
- [ ] `.githooks/commit-msg` 없음 · `git config --get core.hooksPath` 빈 값(main 머지 뒤) · 리뷰 감지기는 `Scripts/BattleCore/` 에서만 울린다.
- [ ] 문서 18편 + 스킬 5 + CLAUDE.md 편집 목록 전 행 처리 · `grep -rn "\[옛 전투" CLAUDE.md` = 0.
- [ ] `core-reviewer` APPROVE(삭제 diff 는 `retire-set.md` 대조로).
