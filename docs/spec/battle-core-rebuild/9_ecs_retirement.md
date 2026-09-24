# 9 — ECS 은퇴: 옛 전투를 지우고 문서를 새 코어 기준으로 (조각 E)

## 목적

**README 부수 질문에 답한다 — 「전투 코드 어디에도 `Unity.Entities` 가 없고, `manifest` 에서 Entities 계열이 사라져도 asmdef 가 초록인가.」** 8c 가 「지워도 아무것도 안 부른다」를 증명했으므로 여기서는 지우고, 동결 장치·옛 리뷰 도구·옛 lane 을 걷고, 옛 구조를 가리키는 문서와 CLAUDE.md 를 새 코어 기준으로 바꾼다. 규칙 변경 0 — 골든 11종 무변이 그 증언이다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 코드 삭제 | `Scripts/Battle/**` · `Scripts/Bridge/**` · `Editor/Battle/**`(tools 1~5 의 옛 짝 — 1~3·5 는 코어판 완료, 4 `SimOrderDumpMenu` 은퇴) · 8c `retire-set.md` 의 나머지(옛 뷰·UI·규칙 보유자 10 중 `TimeManager` 제외) |
| 씬·자산 | `Scenes/BattleScene.unity`(8b 가 빌드 목록에서 뺐다) · `Tests/Golden/**`(옛 코퍼스 — 새 것은 `Tests/GoldenCore/`) · `ProjectSettings/EntitiesClientSettings.asset` |
| 패키지 | `Packages/manifest.json` 에서 `com.unity.entities`·`com.unity.entities.graphics` 제거 |
| asmdef | `Wassup.Runtime` −`Unity.Entities`·`Unity.Entities.Graphics`·`Unity.Transforms`·`Unity.Burst` · `Wassup.Tests.EditMode`·`.Assets`·`.PlayMode` −`Unity.Entities`·`Unity.Transforms`(·`Unity.Burst`) · `Wassup.Skills`·`Wassup.UnitAi` −`Unity.Burst` |
| 헤드리스 lane | `tools/battle-core-rebuild/headless/BattleCoreUnity.Check.csproj:56~57`(`Unity.Entities.dll`·`Unity.Transforms.dll`) |
| 동결 훅 | `.githooks/commit-msg` 삭제 · CLAUDE.md 「동결 장치」 줄 |
| 리뷰 도구 | `.claude/agents/ecs-reviewer.md` · `.claude/skills/ecs-reviewer/` · `.claude/skills/two-track-review/` · `.codex/skills/ecs-reviewer/` · `.codex/hooks/two-track-review-stop-gate.mjs` · `.claude/hooks/ecs-review-detector.mjs` → 옛 분기 제거 후 `core-review-detector.mjs` 로(`.claude/settings.json` 경로 갱신) · `.codex/hooks.json` |
| 네임스페이스 | `Wassup.Battle.Units`(Faction, 217 파일) → `Wassup.Skills` · 8c 에서 이사한 저작 타입의 `Wassup.Battle.*` → `Wassup.Data` |
| 잔여 이중화 | `Data/WavePatternGenerator.cs`·`Data/BonusWaveSchedule.cs`·`Data/BattleConfig.cs`(+ 자산) — 8a 뒤 소비처 0 확인 시 삭제 |
| 문서 | `docs/reference/` 16편 · 스킬 5 · `CLAUDE.md` — 아래 표 |

## 구현 (커밋 순서)

1. **삭제는 동결 훅이 살아 있는 동안 `[old-battle]` 태그로 한다.** 훅을 먼저 지우면 「옛 전투를 지운 커밋」이 태그 없이 남아 이력에서 안 보인다. 훅 삭제는 마지막 커밋.
2. **테스트 삭제에는 짝이 있어야 한다.** 옛 lane 테스트를 지울 때 파일마다 「같은 규칙을 증언하는 코어 테스트」 한 줄을 커밋 메시지나 `retire-set.md` 에 적는다 — `rules.md` 필수 행의 테스트 열이 그 지도다. 짝이 없으면 코어 테스트로 먼저 옮긴다. 비교가 본질인 테스트(`AttackReachParityTests` — 옛 `AttackReach` ↔ 새 것 2만 건)는 짝 없이 은퇴한다: 계약 3 「옛 러너와 병존하는 동안만 A/B」.
3. **옛 PlayMode lane 은 축소가 아니라 은퇴한다.** 남는 아웃게임 3건(`AuthE2ETest`·`DeckInfoPresetApplyLiveE2ETest`·`PresetBarPopupLayerTest`)은 8b 의 진입 4건과 함께 `Wassup.Tests.PlayMode.Core` 로 옮기고 `Wassup.Tests.PlayMode` asmdef 를 지운다(에이전트 결정 — lane 이 하나 줄어 `test-procedure.md` 가 단순해진다).
4. **남기는 패키지.** `com.unity.burst`·`com.unity.collections` 는 **지우지 않는다** — URP 의 `com.unity.render-pipelines.core` 가 둘 다 의존하고(`Packages/packages-lock.json`), `Data/GeneratedMap.cs`·`DioramaMapBuilder.cs`·`ProjectilePatternData.cs:33` 이 `NativeArray`·`FixedList128Bytes` 를 쓴다. `Unity.Burst` asmdef 참조는 `[BurstCompile]` 0 건을 확인한 뒤 뺀다(`Wassup.UnitAi` 가 Burst 를 참조하던 이유 = Burst 로 컴파일된 옛 시스템이 그 어셈블리를 불렀기 때문 — 그 호출자가 사라진다).
5. **네임스페이스 정리는 삭제 뒤 한 커밋.** 기계적 치환이라 규칙 diff 가 섞이지 않게 따로 둔다. 직렬화는 영향 없다(열거형 필드는 정수, 스크립트는 GUID) — `m_EditorClassIdentifier` 문자열만 다음 저장 때 갱신된다.
6. **덱 타이머 폴백(README 작업 표 unit 4 행 「unit 9 에서 제거」)은 재판정한다.** 덱의 `timerDurationSec` 는 시계만이 아니라 **웨이브 생성의 입력**이다(`BattleCore/Wave/WaveGenerator.cs:70`). 라이브 모드·덱 값이 같으면(180 = 180) 출처만 모드로 바꾸고 골든 무변으로 확인한다. 다르면 이식 제외 + 후속 후보 — 웨이브가 바뀌는 변경을 은퇴 unit 에 섞지 않는다.
7. **옛 웨이브 생성기 삭제는 스킬 갱신과 같은 커밋.** `enemy-wave-integration` 스킬이 `WavePatternGenerator.*` 를 규칙 근거로 인용한다(`SKILL.md:36~39·79·104`) — 스킬의 「갱신 트리거」 표 의무. 인용 대상을 코어 `WaveGenerator` 의 같은 함수로 바꾼다.

### 문서 목록 (2026-09-25 grep `BattleBridge|Unity.Entities|ISystem|EntityManager|NativeQueue|Scripts/Battle/` — README 의 「14건」은 16건이 됐다)

| 문서 | 적중 | 처분 |
|---|---|---|
| `battle-core-architecture.md` | 23 | §1 유지 · **§2~§10 을 코어 구조 지도로 교체**(담당자 8 · `TickPipeline` 단계 · `EventOrder`/`ViewOrder` · 드라이버/뷰 풀) — 원천 `class-diagram.md` |
| `object-pipeline-map.md` | — | 8c 에서 완료 |
| `test-procedure.md` | 1 | lane 표 5 → 4(`EditMode`·`.Assets`·`.Core`·`PlayMode.Core`) + 헤드리스 |
| `enemy-movement-algorithm.md` · `map-wave-balancing.md` · `score-formula.md` · `ingame-flow.md` · `map-stage-authoring.md` · `dreamcatcher-card-schema.md` · `dreamcatcher-portability.md` · `review-skill-comparison.md` | 3·3·4·2·2·3·5·3 | 심볼·경로를 코어로. `ingame-flow.md` 1축 「지지 않는다」 → **모드별**(`match-mode-design.md` 사용자 판정 2) |
| `lessons/01·02·03·04·README` | 1·1·2·4·1 | 옛 경로는 「이력」 표기, 규칙은 유지 |
| `docs/spec/unit-stats-and-modifiers/1·2` | — | 상단에 은퇴 표기(고정소수점 scale 1000 미채택 — README 후속 후보) |
| 스킬 `unity-vfx-integration`(SKILL 12 + `ecs-bridge-pattern.md` 12) · `unity-feature-wiring`(10) · `unity-vfx-authoring`(3) · `unity-prop-tile-authoring`(2) · `enemy-wave-integration` | | 브리지·NativeQueue 절 → 「코어 사건 → 뷰 풀 구독」. `ecs-bridge-pattern.md` 삭제 |
| `docs/spec/**` 옛 포인터 ~1,157 | | **두는 것이 규칙**(역사서 — README 후속 후보) |

### CLAUDE.md 편집 목록 (줄 = 2026-09-25 기준)

| 자리 | 편집 |
|---|---|
| `:7~13` 「⚠ 현재 상태 — 전투는 전환 중」 | 삭제 → 기술 스택 앞 한 줄 「전투 = 순수 C# 전투 코어(`battle-core-rebuild` 완료 YYYY-MM-DD)」 |
| `:36~38` 기술 스택 | 아키텍처 = 코어 + Mono 드라이버/뷰 · 필수 패키지에서 Entities·Entities Graphics·Jobs 제거(Burst·Collections 는 URP 의존으로 남는다고 적는다) · 「ECS 버전 기준」 줄 삭제 |
| `:41~57` 「옛 전투 — ECS 맥락 분리」 | 절 전체 삭제(채널 31 목록 포함) |
| 절대 제약 `:61·62·64·81`(1·2·4·12) | 한 줄 은퇴 표기로 — **번호는 보존**(제약 8 이 같은 관례) · 3 은 「네트워크 코드 완전 금지」만 |
| 제약 10 | 「ECS 시뮬」 → 「전투 코어」 · 모범 `ModifierMath.CombineMul` 경로를 코어(`BattleCore/Effects/ModifierMath.cs`)로 |
| 제약 13 | 인용 심볼(`SkillMath.CellHalfWidthTiles`·`SkillFiredEvent`·`ProjectileHitSystem`·`DeathSiteBlastSkill`)을 코어 정본 진입점으로 — 존재 grep 으로 확인. 규칙 문면 무변 |
| 추가 제약 `:141·145·148` | `[옛 전투]` 반절 삭제 · 「전투는 ECS 시스템에서만」 → 「전투 코어에서만」 · 「UI 가 ECS Component 를」 → 「UI 는 코어 읽기 모델·사건만」 · 매니저 예외 2건에 「판 밖 전역이라 코어 제약 1 과 충돌하지 않는다」 |
| `:150~159` 「새 전투 코어 — 절대 제약」 | 꼬리표 제거 · 제목 「전투 코어 — 절대 제약」 · 「절대 제약」 바로 뒤로 이동 · 적용 범위 인용문의 「전환 중」 삭제 |
| `:178` 참조 표 | 설계도 행 = 「§1 설계 아웃라인 · §2~ 코어 구조 지도」 · test-procedure 행 lane 수치 · 이동 알고리즘 행 「시스템 순서·MovementSystem」 → 코어 단계 이름 |
| `:302` 자가 점검 | `[옛 전투]` 반절 삭제 |
| `:305~320` 「ECS 설계의 불확실성 대응」 | 「전투 코어 설계의 불확실성」으로 교체 — 질문 가치: 담당자 소속 · 커맨드 vs 사건 · 틱 단계 위치 · 결정론 영향 |
| `:361` 맥락 폴더 금지 | 삭제 |

## 이식 제외 — 일부러 안 지우는 것

| 남기는 것 | 이유 |
|---|---|
| Burst·Collections 패키지 | 구현 4 |
| `KeyringSim`·`BlobShadow`·`DefenderRetireFlight`·`DcInspectPanelView`·`DreamcatcherCardText` 등 옛 폴더 밖 공유 부품 | 새 층이 부른다(8c 도달성) |
| `TimeManager`·`SoundManager` | 추가 제약의 의도된 예외 |
| `docs/spec/**` 의 옛 포인터 | 역사서 |

## 파이프라인 커버리지

N/A — 새 정거장 없음. 지우는 정거장은 8c 가 맵에서 이미 이력으로 내렸다. 확인만: 맵의 모든 심볼이 삭제 뒤에도 존재(grep).

## 완료 기준

- [ ] `grep -rn "Unity\.Entities\|EntityManager\|SystemAPI\|ISystem\b" Assets/_Project --include=*.cs` = 0 · `grep -rn "BattleBridge" Assets/_Project --include=*.cs` 비주석 = 0 · `grep -rnE "Wassup\.Battle(\.|;)" Assets/_Project --include=*.cs` = 0.
- [ ] `manifest.json` 에 `entities` 0 · lock 재생성 후 `com.unity.serialization`·`scriptablebuildpipeline` 가 전이로 사라짐 확인(남으면 누가 끄는지 적는다).
- [ ] Unity 콘솔 컴파일 에러 0 · asmdef 전부 초록 · EditMode(`.EditMode`·`.Assets`·`.Core`) 선행 2 외 빨강 0 · PlayMode.Core 초록 · 테스트 **총계가 줄어든 만큼이 삭제 목록 합과 같다**(안 돈 테스트 판별).
- [ ] 골든 11종 무변(Unity) · 헤드리스 3종(build 0 · test · Check 0 — Entities dll 참조 없이).
- [ ] **Android QA 빌드 2회째**(Entities 없이) 성공 · APK 크기 전후 기록 · 실기기 1판.
- [ ] `.githooks/commit-msg` 없음 · `git config core.hooksPath` 안내 문장 CLAUDE.md 0 · 리뷰 감지기가 `Scripts/BattleCore/` 에서만 울린다.
- [ ] 문서 16편 + 스킬 5 + CLAUDE.md 편집 목록 전 행 처리 · `grep -rn "\[옛 전투" CLAUDE.md` = 0.
- [ ] `core-reviewer` APPROVE(삭제 diff 는 도달성 결과 대조로).
