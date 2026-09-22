# battle-core-rebuild — 계획 레벨 적대 리뷰 (완전판)

> 대상: `docs/spec/battle-core-rebuild/README.md` + `class-diagram.md` + `match-mode-design.md`
> 보조 자료: `docs/plans/2026-09-22-battle-core-rebuild-census/` (00~05 + census 6편)
> 성격: 코드 리뷰가 아니라 **이주 계획(migration plan)의 완전성** 공격. 읽기 전용.
> 작성 2026-09-23. 모든 수치는 이 저장소에서 grep 으로 실측한 값이다.

---

## 판정 (한 줄)

계획은 **전투 규칙의 이식 설계로는 촘촘하지만, 이식을 수행할 「작업 환경」 설계가 통째로 비어 있다.**
씬 · 브랜치 · 동결 장치 · 리뷰 도구 · 테스트 lane 후계 · 에디터 도구 어느 것도 unit 을 갖지 않으며,
계획 3문서 전체에서 `씬` · `scene` · `BattleScene` · `branch` · `브랜치` · `SerializeField` ·
`PlayMode` · `hook` · `훅` 의 등장 횟수는 **전부 0** 이다.

---

## 빠진 것 (severity 순)

### 1. 브리지의 「메서드」만 귀속표에 있고 「직렬화 필드 91개」는 없다

**무엇** — unit 0 의 귀속표는 「브리지 메서드 348 → 어느 담당자로 / 삭제」다. 그런데 브리지가 들고 있는 것은
메서드만이 아니다. 씬에 구워진 저작 값 91개가 같이 죽는다.

**근거**
- `Assets/_Project/Scenes/BattleScene.unity` 의 BattleBridge MonoBehaviour 블록: 표준 헤더 10개를 뺀
  **저작 필드 91개**.
- 코드 쪽 `[SerializeField]` 선언 합계 **93개** (`BattleBridge.cs` 76 · `.BossLeap.cs` 11 ·
  `.UltimateLeap.cs` 5 · `.BonusWave.cs` 1).
- 내용 분류: 뷰 풀·프리젠터 참조 약 20 (`spineUnitPool`, `enemyViewPool`, `vfxSpawner`,
  `statusFxSpawner`, `unitOverheadUiLayer`, `beamPresenter`, `_projectileViewPool`, `scoreHud` …) ·
  보스/궁극기 도약 연출 16 · 블롭 그림자 및 lift 11 (`blobShadowLift`, `liftScalePerHeight`,
  `liftScaleMax`, `liftShadowFullHeight`, `useRealShadows` …) · 심장 HUD 7 (`heartRestBpm`,
  `heartBarPunchDecayPerSec` …) · 맵·타일 8 (`tileSet`, `tilemapBillboardTilt`,
  `propDistanceTiltFactor` …) · 스폰 분산 4 · 픽업/사직서 뷰 7 · 데이터 자산 참조
  (`deck`, `mapPool`, `seasonRegistry`, `scoreRules`, `bonusWaveData`, `fixedMapSeed`).

**계획의 어느 unit 에** — unit 0 의 귀속표가 「메서드 348」이 아니라 「메서드 348 + 필드 91」이어야 한다.
실제 이사와 씬 YAML 마이그레이션은 unit 7.

**안 하면** — unit 7 에서 브리지를 지우는 순간 이 값들의 유일한 보관소가 사라진다. Unity 는 저장된 씬에
필드를 소급 이전하지 않는다. 이 함정은 저장소 안에 이미 문서화돼 있다:
`.claude/skills/unity-feature-wiring/SKILL.md` 112행 — *「Scene YAML diff missing the new field …
Unity won't retroactively add fields to saved prefab/scene instances」*.
수개월간 손으로 튜닝된 값(`liftScaleMax`, `heartBarPunchDecayPerSec`, `spawnSpreadFraction`)이 기본값으로
조용히 돌아가도 그것을 잡는 테스트는 한 개도 없다. 육안으로만 드러나고, 육안은 11개 중 1개만 잡는다.

---

### 2. 새 코어를 「어디서」 돌리는지 정해져 있지 않다

**무엇** — 계약 10 은 옛 전투가 계속 플레이 가능하고 동결이라고 못박는다. 조각 B 의 완료 기준은
「카드 없이 판이 돈다」다. 그 판이 도는 장소가 계획 어디에도 없다.

**근거**
- 계획 3문서에서 `씬` · `scene` · `BattleScene` 등장 횟수 **0**.
- `BattleScene.unity` 에 `BattleBridge` 인스턴스는 **하나**뿐이고, 뷰 풀·프리젠터 전부가 그 91개 필드로
  배선돼 있다. 새 뷰 풀들도 같은 프리팹 참조를 필요로 한다.
- 대안 씬 후보도 없다. `Scenes/` 에는 `BattleScene` · `OutgameScene` · `MapTest` · `FluidScratch` ·
  `PaletteSanity` 뿐이고 뒤 셋은 스크래치다.

**계획의 어느 unit 에** — unit 0. 조각 B(unit 4) 가 여기에 전적으로 의존한다.

**안 하면** — 구현자는 둘 중 하나를 한다. (a) 같은 씬에 새 드라이버를 옛 브리지 옆에 얹는다 — 전투 두 개가
한 씬에서 뷰 풀과 입력을 다투고, 그 순간 계약 10 의 동결이 실질적으로 깨진다. (b) 조각 B 한가운데서 씬을
새로 만든다 — 91개 필드를 다시 배선하는 작업이 계획에 없던 채로 조각 B 안에 끼어든다.
어느 쪽이든 조각 B 의 공수 추정이 무의미해진다.

---

### 3. 브랜치 전략이 없고 main 은 자동 출하된다

**무엇** — 수개월짜리 반쯤 동작하는 코어가 어디에 사는지 계획이 말하지 않는다.

**근거**
- 계획에서 `branch` · `브랜치` 등장 **0**.
- `git worktree list` = 3개 (메인 · `wassup-testrig` · `.claude/worktrees/dreamcatcher-orb-dock`).
  로컬 브랜치 17개. CLAUDE.md 가 *「여러 세션이 같은 워크트리를 공유한다」*고 명시.
- 원격은 `origin`(GitHub, 정본) + `gitlab`(미러). push 는 사용자 승인제지만, **승인된 push 하나가
  워킹트리의 모든 커밋을 싣는다.**
- 프로젝트 메모리 규칙 `feedback_unpushable_work_needs_branch`: *「main 에 두면 다른 스펙 push 에
  자동으로 실려 간다. 롤백 가능 ≠ 격리」*.

**계획의 어느 unit 에** — unit 0. 새 asmdef 뒤에 숨기는 방식이면 그 근거를, 브랜치면 그 브랜치 이름과
동기화 주기를 적어야 한다.

**안 하면** — 조각 A 의 뼈대가 옆 세션의 UI 수정 push 에 실려 정본에 나간다. 그 커밋들이 GitLab 미러로도
넘어가면 fast-forward 계보가 그 위에 쌓여 되돌리기가 더 비싸진다.

---

### 4. 콘텐츠 동결(계약 10)에 강제 장치가 0이고, 이미 위반 예정인 spec 이 4건 커밋돼 있다

**무엇** — 계약 10 「옛 전투 코드에 규칙 변경 없음」은 문장일 뿐 장치가 없다.

**근거 (장치 쪽)**
- `.git/hooks/` 에 sample 을 제외한 훅 **0개**.
- 저장소 전체에 `CODEOWNERS` **없음**.
- `.claude/settings.json` 의 훅은 `ecs-review-detector.mjs` 하나이고, 그것은 리뷰 감지기이지 동결 게이트가
  아니다.
- CLAUDE.md 에 옛 전투 스코프 규칙 **없음**. 제약 9(스펙 범위 엄수)는 「현재 작업 중인 spec」 기준이라,
  다른 spec 을 착수하는 세션에는 아무 제동이 없다.

**근거 (위반 예정 쪽)** — `docs/spec/README.md` 250~268행, 커밋 `9461d6ae` 에 실려 있다.
- `wide-board-camera` — L, units 0~8, **「먼저 착수하는 것이 이것 하나다」**. 브랜치
  `wip/wide-board-camera` 가 이미 존재한다.
- `wide-board-content` — 초안 L, 36×14 저작 + 마음 N개 공유 체력 + 밸런스.
- `squad-slots-ten` — 초안 M, 편성 7→10. **「언제든 병렬 가능」**이라고 적혀 있다.
- `heart-stress-axis/12_shared_heart_pool.md` — 사용자 결정 2026-08-25, 위 둘의 선행 계약.

**계획의 어느 unit 에** — unit 0. 그리고 이 4건과의 관계(보류 / 새 코어에서 / 옛 코어에서 예외 허용)를
명시 판정해야 한다. 계획은 `heart-stress-axis/12` 만 「후속 후보」로 언급하고 나머지 3건은 이름도 없다.

**안 하면** — 동결은 병행 세션이 읽지 않는 README 한 줄이다. census 가 「현재 구현에서 전수 추출」한 규칙
정본이 조각 A 도중에 낡고, 그 낡음은 조각 D 쯤에서 「왜 이 규칙이 census 와 다르지」로 발견된다.
특히 `squad-slots-ten` 은 `SquadPreset.SlotCount` / `SquadDraw.FieldCount` 두 상수를 건드리는데,
매치 모드의 `squadSlots`(현행 7) 필드와 정면으로 겹친다.

---

### 5. 에디터 전용 전투 도구 10개의 후계가 없고, 그중 5개는 unit 8 이 「전투 코드」로 오인해 지운다

**무엇** — 계획은 unit 8 에서 「`Battle/` 삭제」와 「골든 러너 교체」를 말한다. 그런데 `Battle/` 안에는
시뮬레이션이 아니라 **도구**가 섞여 있고, 그 도구들은 교체 대상 목록에 없다.

**근거** — 10개 전부 브리지 또는 Entities 결합이다.

| 파일 | 위치 | `BattleBridge` 참조 | Entities/World 참조 |
|---|---|---|---|
| `SimGoldenMenu.cs` | `Editor/Battle/` | 4 | 0 |
| `SimHarnessRunMenu.cs` | `Editor/Battle/` | 3 | 0 |
| `SimHarnessRunner.cs` | `Editor/Battle/` | 다수 | 0 |
| `SimOrderDumpMenu.cs` | `Editor/Battle/` | 0 | 2 |
| `DetectionProbeMenu.cs` | `Editor/Battle/` | 7 | 12 |
| `BlockingHazardDebugMenu.cs` | **`Scripts/Battle/Effects/`** | 3 | 1 |
| `HazardDebugMenu.cs` | **`Scripts/Battle/Effects/`** | 3 | 0 |
| `FatigueDebugMenu.cs` | **`Scripts/Battle/Effects/`** | 4 | 0 |
| `ObstacleDebugMenu.cs` | **`Scripts/Battle/Effects/`** | 2 | 0 |
| `PatrolDebugMenu.cs` | **`Scripts/Battle/Movement/`** | 3 | 1 |
| `RelocationDebugMenu.cs` | **`Scripts/Bridge/`** | 2 | 0 |

굵은 6개는 `Scripts/Battle/` 또는 `Scripts/Bridge/` 안에 있어 **unit 8 의 「`Battle/` 삭제」에 조용히
쓸려 나간다.** 이들은 해저드·장애물·피로·순찰·재배치를 에디터에서 손으로 발생시키는 유일한 수단이다.
`SimOrderDumpMenu` 는 시스템 갱신 순서를 덤프하는 도구라 ECS 제거 후 **의미 자체가 사라진다**(시스템이
없다). `DetectionProbeMenu` 는 CLAUDE.md 가 인용하는 「감지와 이동이 실측 5.0% 에서 갈린다」 수치를
만든 계측기다.

**계획의 어느 unit 에** — unit 0 의 귀속표에 「도구 10개: 이식 / 재작성 / 폐기」 행이 필요하다.
골든·하네스 도구는 계약 3 이 새 골든을 요구하므로 unit 0 에 재작성이 들어가야 한다(아래 순서 오류 3 참조).

**안 하면** — 조각 A~D 동안 새 코어에는 해저드·순찰·재배치를 손으로 찔러 볼 도구가 하나도 없다.
재현이 안 되면 CLAUDE.md 의 버그 수정 절차(「재현이 먼저다」)가 통째로 집행 불가능하다.

---

### 6. ECS 를 전제한 리뷰 도구 6종의 은퇴·교체 계획이 없고, 감지기는 죽는 경로만 감시한다

**무엇** — 리뷰 자동화 전체가 `Scripts/Battle/` 과 `BattleBridge.cs` 두 경로에 매여 있다.

**근거**
- `.claude/agents/ecs-reviewer.md` — 에이전트 정의 1개.
- `.codex/skills/ecs-reviewer/` — `SKILL.md` + `references/hybrid-ecs-review-checklist.md` +
  `agents/openai.yaml`.
- `.claude/skills/two-track-review/SKILL.md` — 투트랙 수렴 규칙.
- `.claude/hooks/ecs-review-detector.mjs` — `.claude/settings.json` 의 `UserPromptSubmit` 에 배선됨.
- `.codex/hooks/ecs-review-detector.mjs` + `two-track-review-stop-gate.mjs` — 커밋된
  `.codex/hooks.json` 의 `UserPromptSubmit` / `Stop` 에 배선됨.
- **감지기의 감시 경로는 정확히 두 개다**:
  ```js
  const ECS_PATH_RES = [
    /^Assets\/_Project\/Scripts\/Battle\//,
    /^Assets\/_Project\/Scripts\/Bridge\/BattleBridge\.cs$/,
  ];
  ```
  둘 다 unit 8 에서 사라지는 경로다. 새 코어 경로는 애초에 매치되지 않는다.
- 레시피 문서도 죽는다: `.claude/skills/unity-vfx-integration/ecs-bridge-pattern.md` 가 참조하는
  `MeteorBurstEvent.cs` 계열 4파일은 CLAUDE.md 기준 **이미 은퇴**했고(투사체 수렴), 이 문서는 아직
  그것을 템플릿으로 가르친다. `.claude/skills/unity-feature-wiring/SKILL.md` 는
  `typeof(Wassup.Bridge.BattleBridge).GetField("vfxSpawner", …)` 를 리플렉션으로 하드코딩한다.
- 참고: 현재 워킹트리에서 `.codex/hooks.json` 과 훅 2개가 **미커밋 상태로 비워져 있다**
  (`{"hooks":{}}`, 총 -323줄). 세션 상태이지 계획 결함은 아니나, 「지금은 안 울린다」를 「앞으로도
  괜찮다」로 읽으면 안 된다.

**계획의 어느 unit 에** — **감지기 경로 재배선은 unit 0**(첫 커밋 전). 에이전트·스킬·체크리스트 은퇴는
unit 8.

**안 하면** — unit 0 첫 커밋부터 새 코어 파일은 어떤 자동 리뷰 레인에도 걸리지 않는다.
`two-track-review-stop-gate` 는 「매치된 것이 없으므로」 통과한다. **침묵이 초록으로 보인다.**
이것은 이 저장소가 이미 겪은 계열의 사고다 — `test-procedure.md` 의 「총계가 안 움직이면 안 돈 것」,
`SimGoldenMenu` 의 「조용히 return 하지 않는다」가 같은 교훈을 적어 두고 있다.

---

### 7. PlayMode lane 의 후계가 없다

**무엇** — unit 8 의 「테스트 정리(Entities 146파일)」 한 줄이 통합 테스트 레인 전체의 처분이다.
「정리」는 삭제고, 대체물이 명시돼 있지 않다.

**근거**
| lane | 파일 | `Unity.Entities` 참조 | `BattleBridge` 코드 참조 | 규모 |
|---|---|---|---|---|
| `Wassup.Tests.EditMode` | 270 | 69 | 22 | ~2,230건 · 26초 |
| `Wassup.Tests.EditMode.Assets` | 37 | 2 | 1 | ~160건 · 5초 |
| `Wassup.Tests.PlayMode` | 92 | 77 | **82 (89%)** | ~144건 · 8분 |

- Entities 참조 테스트 파일 실측 합계 **148** (계획 표기 146).
- `BattleBridge` 코드 레벨 참조 라인 총 **990줄**, 파일 **151개**.
- PlayMode 기준선(`docs/spec/README.md` 646행~, 2026-09-21 `2d28a6d4`, 별도 worktree
  `-runTests PlayMode` 219건): **59 실패** = Gear 본 로그 어설션 33 + 분류된 26.
- 새 코어의 검증은 계약 3 에 따라 「의도 규칙의 EditMode 테스트 + 사용자 플레이」뿐이다.

**계획의 어느 unit 에** — 조각 B 와 unit 8 사이에 자기 unit 이 필요하다. 최소한 「Unity 층 통합을
무엇이 지키나」에 답해야 한다.

**안 하면** — 씬을 부팅해 뷰 풀·드래그 배치·HUD 를 함께 돌려 보는 **유일한** 레인이 사라진다.
하필 그 계층 100%를 다시 쓰는 시점이다. 헤드리스 EditMode 는 `DespawnMissing` 이 유령 뷰를 남기는지,
드래그가 커맨드를 제대로 내는지, 배치 링이 제자리에 뜨는지 하나도 보지 못한다.

---

### 8. 패키지 제거의 파급이 측정되지 않았다

**무엇** — 부수 검증 질문이 「Entities 패키지가 manifest 에서 사라졌는가」인데, 사라질 때 같이 끊어지는
참조가 세어져 있지 않다.

**근거**
- `Wassup.Runtime.asmdef` 참조 17개 중 3개가 entities 계열: `Unity.Entities` ·
  `Unity.Entities.Graphics` · `Unity.Transforms`.
- **`Unity.Transforms` 어셈블리는 `com.unity.entities` 패키지 안에 있다.** 이것을 참조하는 asmdef 는
  4개다: `Wassup.Runtime` · `Wassup.Tests.EditMode` · `Wassup.Tests.EditMode.Assets` ·
  `Wassup.Tests.PlayMode`. 패키지 제거 시 4개 모두 미해결 참조가 된다.
- `com.unity.serialization`(3.1.5) · `com.unity.scriptablebuildpipeline`(2.6.1) 은 lock 파일에서
  **depth 1**, 즉 entities 의 전이 의존이라 함께 사라진다. 사용처 미확인.
- 계획에서 `manifest` 1회 · `Burst` 1회 · `IL2CPP` 1회 등장하는데, `IL2CPP` 는 **후속 후보 섹션의
  성능 항목**(「틱 30Hz 실측」) 안이다. 즉 패키지 제거의 빌드 영향으로는 한 번도 언급되지 않았다.

**계획의 어느 unit 에** — unit 8, asmdef diff 를 명시한 형태로.

**확인된 좋은 소식** — 계약 4 는 실현 가능하다.
`com.unity.burst`(1.8.29) · `com.unity.collections`(6.4.0) · `com.unity.mathematics`(1.3.3) 는 전부
manifest 의 **depth 0 명시 항목**이고, `com.unity.render-pipelines.core` 도 셋에 의존한다. 따라서
entities 를 지워도 이들은 남고, `Wassup.Skills` · `Wassup.UnitAi`(둘 다 `noEngineReferences: true` +
`Unity.Burst` + `Unity.Mathematics`)는 그대로 컴파일된다. 새 `Wassup.BattleCore` 도 같은 형태가 된다.

**안 하면** — unit 8 이 패키지를 지운 뒤 4개 asmdef 가 동시에 빨개지고, 그것이 「테스트 정리」와 같은
커밋 안에서 터져 무엇이 원인인지 분간되지 않는다.

---

### 9. 서버 계약 변경에 주인이 없다

**무엇** — 매치 모드 설계가 제출 payload 와 리더보드 분리를 바꾸는데, 서버는 이 저장소에 없다.

**근거**
- `match-mode-design.md`: 제출 `{modeId, seed, score, sortDirection}`, 리더보드는 `leaderboardId` 로
  분리, *「한 토너먼트 = 한 모드. 서버 계약」*.
- 현행 `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs`:
  `ReportResult(int score, string deckInfoJson, Action<TournamentApi.ResultData> onRanking, Action<string> onError)`.
  **modeId 없음, 정렬 방향 없음.** attempt/entry 기반이고 `TournamentSeed` 는 서버가 준다.
- `PlayerProfile.schemaVersion = 1`, 주석은 *「다음 저장에서 사라진다(마이그레이션 없음 ·
  schemaVersion 불변)」*. 모드 선택을 프로필에 저장하려면 이 계약과 충돌한다.
- 사용자 판정 3 이 이미 「서버 계약」이라고 성격을 규정했는데, 계획의 어떤 unit 도 그것을 작업으로 갖지
  않는다. unit 3 은 코어 담당자 구현이다.

**계획의 어느 unit 에** — unit 3 **앞**에 판정이 필요하다. 「v1 은 단일 모드라 서버 무변, `WaveClear`·
`TimeAttack` 은 `submitsReport=false` 로만 존재」가 답일 수도 있다. 그렇다면 그것을 적어야 한다.

**안 하면** — `IMatchGoal` concrete 3 을 v1 에 만들어 놓고 그중 둘은 제출할 길이 없다. 또는 누가
payload 를 먼저 바꿔 라이브 리더보드를 깬다.

---

### 10. 브리지 밖의 전투 규칙 보유자 9개가 귀속표에 없다

**무엇** — 계약 12 는 「전투 안에 매니저·브리지·컨트롤러라는 이름의 클래스를 두지 않는다」인데,
귀속표의 대상은 브리지 하나뿐이다.

**근거** — 계획 3문서에서 아래 9개 이름의 등장 횟수는 **전부 0** 이다.

| 파일 | 행 | 무엇을 들고 있나 |
|---|---|---|
| `Core/TilemapMapView.cs` | 1,608 | 보드 렌더 + 브리지 결합. 제2의 거대 클래스 |
| `Core/GameManager.cs` | 656 | `[SerializeField] BattleBridge` 보유, 판 흐름 |
| `Core/Dreamcatcher/DreamcatcherHandController.cs` | 551 | 손패 규칙. UML 의 `HandDeck` 과 정면 중복 |
| `Core/DraftController.cs` | 252 | 뽑기 |
| `Core/SkillLoadoutController.cs` | 143 | 덱 편성 |
| `Core/TimeControl/TimeManager.cs` | 140 | 도메인 시간 제어 (제약상 의도된 예외) |
| `Core/CostRuntime.cs` | 110 | 코스트. UML 의 `CostLedger` 와 중복 |
| `Core/PlacementInput.cs` | 103 | 배치 입력 |
| `Core/SkillRuntime.cs` | 99 | 스킬 런타임 |
| `Core/PlacementCooldownRuntime.cs` | 97 | 배치 쿨다운. `PlacementService` 와 중복 |
| `Core/MatchTally.cs` | 69 | 결과 집계 |

참고: 브리지 메서드 선언 실측은 7개 partial 합계 **369개**(계획 표기 348. 오차는 grep 방식 차이로
보이며 문제는 아니다).

**계획의 어느 unit 에** — unit 0 귀속표의 범위를 「브리지 + 이 9개」로 넓혀야 한다.

**안 하면** — 계약 12 가 브리지에 대해서만 지켜지고 나머지에서 깨진다. `DreamcatcherHandController` 가
`HandDeck` 옆에서 손패 규칙을 계속 들고 있어 **같은 규칙이 두 곳에 산다** — 이것이 애초에 브리지를
없애는 이유였다. `CostRuntime` / `CostLedger`, `PlacementCooldownRuntime` / `PlacementService` 도 같다.

---

### 11. 문서 포인터 1,229건이 언제 거짓이 되는지 표가 없다

**무엇** — 계획은 문서 3종의 갱신만 말한다. 나머지가 언제 거짓이 되는지, 누가 판정하는지 없다.

**근거**
- `Scripts/Battle/` 또는 `BattleBridge` 를 인용하는 마크다운 **1,229개**:
  `docs/spec` 1,157 · `docs/plans` 28 · `docs/prototype` 22 · `docs/reference` 14 ·
  `docs/production-transition` 6 · 기타 2. spec 폴더는 총 313개다.
- 계획이 다루는 것: `object-pipeline-map.md`(unit 7 전면 재작성) ·
  `battle-core-architecture.md` §2~§10 + CLAUDE.md(unit 8).
- **CLAUDE.md 참조표에 실려 있는데 계획이 안 다루는 것**: `test-procedure.md`(PlayMode lane 행과
  `EntitiesAssetGC` 배치 함정) · `enemy-movement-algorithm.md` · `map-wave-balancing.md` ·
  `score-formula.md` · `lessons/04-sim-design.md`(ECS 언급 8) · `lessons/01-unity-mcp-operation.md`
  (`run_tests` 어셈블리 운용).
- **특히 `.claude/skills/enemy-wave-integration/SKILL.md`**. CLAUDE.md 참조표가 이 스킬을
  *「그 코드를 바꾸면 **같은 커밋에서** 스킬을 갱신한다」*로 의무화했고, 스킬 안의 「갱신 트리거」표는
  `WavePatternGenerator.ResolveWaveEligibleIndex` · `ClampGroupCounts` · `PickConcept` ·
  `AssignLanes` · `InheritLanes` · `AttackDeck` 필드 추가/삭제 · `WavePlanAsset`·`FromPlanAsset` 을
  트리거로 열거한다. unit 3 의 `WaveScheduler` 와 매치 모드의 `waveSourceKind`·`timerDurationSec`
  이관이 이 트리거를 전부 건드린다.
- 반대로 `battle-core-architecture.md` **§1(전투 설계 아웃라인, 1.1~1.15)은 아키텍처 중립**이라
  살아남는다. 계획이 §2~§10 만 지목한 것은 정확하다.

**계획의 어느 unit 에** — unit 8 에 명시 목록. 웨이브 스킬 갱신은 **unit 3 의 완료 기준**에.

**안 하면** — 이식이 끝난 뒤 반 년간 모든 세션이 참조표를 타고 들어가 없는 파일을 찾는다.
스펙 1,157건은 역사서라 그대로 둘 수 있지만(메모리 규칙 `feedback_spec_as_history_not_package`),
`docs/reference/` 14건은 「현재 구현」을 자처하는 문서라 거짓말이 된다.

---

### 12. 「키 타입 치환으로 끝난다」가 참이 아닌 파일이 3개 있다

**무엇** — unit 7 의 *「Entities 누수 24파일 → `SimEntityId` … 키 타입 치환으로 끝난다」* 는 주장이
파일 수도 성격도 과소평가다.

**근거 (수)** — 런타임 누수는 24가 아니라 **28개**다(브리지 7파일 제외):
Presentation 15 · UI 8 · Data 3 · Core 1 · Skills 1 · Bridge/`SimFieldInstaller.cs` 1.
에디터 3개는 별도.

**근거 (성격)** — 셋은 키 치환이 아니다. 매 프레임 `EntityManager` 에 **생존 질의**를 한다.
- `SpineUnitPool.DespawnMissing(EntityManager)` — `_byEntity` 를 돌며 `!entityManager.Exists(kv.Key)` 면
  뷰를 회수.
- `QuadUnitViewPool.DespawnMissing(EntityManager)` — 동일.
- `DcAuraVisualPool.Sync(EntityManager)` — `!em.Exists(host)` 면 인스턴스 Destroy + 등록 제거.
  추가로 `Func<Entity, Transform> _resolveAnchor` 로 앵커를 되묻는다.

여기에 `Entity.Null` 센티널 사용(`DcAuraVisualPool`, `UnitOverheadUiLayer`, `BeamPresenter`)과
`Dictionary<Entity, …>` **19곳**이 겹친다. 새 설계는 이벤트 구독뿐이라 이 폴링 그물이 사라진다.

**계획의 어느 unit 에** — unit 7 이 둘 중 하나를 계약으로 적어야 한다.
(a) 모든 소멸 경로가 반드시 소멸 이벤트를 낸다, (b) 코어가 `IsAlive(SimEntityId)` 를 노출한다.
그리고 `SimEntityId` 에 `Entity.Null` 대응 센티널이 있는지도 unit 0 에서 확정해야 한다.

**안 하면** — 이벤트를 빠뜨린 소멸 경로 하나가 판 위에 유령 뷰를 영구히 남긴다. 그것을 덮어 주던
폴링이 없으므로 증상은 「가끔 죽은 유닛이 안 사라진다」로 나타나고, 원인 경로는 수십 개 중 하나다.

---

### 13. (정정) `AGENTS.md` 중복 — 대부분 이미 해결돼 있다

**앞선 보고 정정.** 1차 리뷰에서 「`AGENTS.md` 가 CLAUDE.md 의 바이트 동일 사본」이라고 severity 5 로
올렸는데 **틀렸다.**

**실측**
- `AGENTS.md` 는 **git symlink** 다: 파일 모드 `120000`, 타깃 `CLAUDE.md`.
  `.claude/worktrees/dreamcatcher-orb-dock/AGENTS.md` 도 같은 symlink.
  따라서 CLAUDE.md 를 재편하면 **자동으로 따라온다. 동기화 부담 0.**
- 실제 파일은 `docs/production-transition/AGENTS.md`(mode `100644`, 1,537바이트) 하나뿐이고,
  이것은 CLAUDE.md 제약 11 의 **owner-gated dormant subtree** 다. 사용자가 명시 지시하지 않는 한
  건드리지 않는 것이 규칙대로다. 계획이 언급하지 않은 것이 **맞다.**

**남는 것** — 부작용 하나는 실재한다. symlink 라서 CLAUDE.md 의 **ECS 절반이 AGENTS.md 경로로도 그대로
주입된다.** 이것은 독립 결함이 아니라 아래 「순서 오류 1」의 영향 범위가 한 경로 더 넓다는 뜻이다.

---

## 계획이 말은 했지만 주인·시점·장치가 없는 것

| 항목 | 계획의 문구 | 없는 것 |
|---|---|---|
| 콘텐츠 동결 | 계약 10 「옛 전투 코드에 규칙 변경 없음」 | **장치.** 훅 0 · CODEOWNERS 0 · 스코프 규칙 0. 그리고 착수 대기 spec 4건과의 관계 판정 |
| ECS 제약 교체 시점 | 계약 11 「unit 8 에서 교체된다. 그 전까지 옛 코드에는 그대로 적용」 | **과도기 문안.** CLAUDE.md 는 매 세션 자동 주입이라, unit 0~7 동안 모든 세션이 제약 1(브리지가 유일 창구)·2(맥락 경계)·3(ISystem 우선)·12(브리지 진입 최후수단)를 브리지도 맥락도 없는 새 코어에 적용한다 |
| 조각별 테스트 | 「조각 안의 unit 은 전부 구현한 뒤 한 번에 테스트한다」 | **어느 lane 인가.** EditMode 26초 / Assets 5초 / PlayMode 8분·59실패 기준선. 「조각의 초록」 정의가 없다 |
| 새 코어 골든 | 계약 3 「새 코어는 자기 골든을 `LegacyTraceV0` 포맷으로 갖는다」 | **굽는 도구.** 현행 `SimGoldenMenu` 는 `SimHarnessGuards.TryGetBridge` 로 브리지 없으면 하드 실패하고, `SimHarnessRunner.Run(BattleBridge bridge, …)` 이며 `configHash` 는 `bridge.MatchConfigHash` 에서 나온다. 교체가 unit 8 에 있다 |
| `configHash` 에 modeId | 매치 모드 위험 「명시로 섞는다」 | **주인.** configHash 의 현재 소유자가 브리지다. 새 소유자가 지정돼 있지 않다 |
| 「남는 메서드 0」 | 계약 12 · UML 주석 | **검증 장치.** 귀속표를 파일에 대조하는 기계 검사가 없으면 자기 신고다. 실측 369 선언 |
| 덱 타이머 이관 | 「모드가 이기고 덱 값은 폴백으로 한 릴리스 병행 후 제거」 | **「릴리스」의 정의.** 이 프로젝트에 릴리스 케이던스가 없다. 시점이 영원히 오지 않거나 임의로 온다 |
| 모드 유효성 | 「모드 × 덱/플랜 유효성 … EditMode 테스트 필수」 | **어느 lane.** `targetWaves` 대 `maxWaveCount` 는 실제 SO 를 읽으므로 `EditModeAssets` lane 이다(`test-procedure.md` 의 한 줄 판별). unit 3 에 명시 필요 |
| 뷰 구독 순서 | 「뷰 간 순서가 필요한 곳은 구독 순서로 고정」 | **그 순서를 지키는 테스트.** 옛 `LateUpdate` 순서 계약은 PlayMode 가 지켰는데 그 lane 이 사라진다(빠진 것 7) |

---

## 순서 오류

### 1. CLAUDE.md 재편이 unit 8 인데 CLAUDE.md 자신이 「먼저」를 요구한다

CLAUDE.md 79행 원문:

> Transition과 무관한 Demo 아키텍처 변경은 Demo 목표만으로 별도 승인받고 이 파일과
> `docs/reference/battle-core-architecture.md` 를 **먼저 갱신해야 한다.**

계획은 둘 다 unit 8, 즉 **마지막**에 둔다. 취향 문제가 아니라 문면 위반이다.
그리고 symlink 때문에 AGENTS.md 경로로도 같은 옛 제약이 주입된다(빠진 것 13).

**옳은 순서** — unit 0 앞에 「제약 재편」 작업 단위. 최소한 제약 1·2·3·12 에 적용 범위를 명시하는
과도기 문안(예: 「`Scripts/Battle/` 과 `Scripts/Bridge/` 에만 적용. `Wassup.BattleCore` 는 계약 4·12 를
따른다」)이 먼저 들어가야 한다.

### 2. ecs-review-detector 재배선이 없어서(사실상 unit 8 이라서) 조각 A~D 가 무리뷰로 간다

감지기의 경로 정규식 두 개는 죽는 경로만 가리킨다. 새 코어 경로를 unit 0 **첫 커밋 전**에 학습시켜야
한다. unit 8 에 두면 전체 구현 기간이 리뷰 공백이다.

### 3. 골든 러너 교체가 unit 8 이라 조각 B·D 에 비교 기준선이 없다

계약 3 은 옛 골든을 「무엇이 달라졌나 볼 때의 참고」로 쓰겠다고 한다. 그 비교는 **양쪽이 동시에 존재할
때만** 가능하다. unit 8 은 옛 전투를 먼저 지운다. 러너 재작성은 unit 0(하네스 골격) 에 붙어야 하고,
그때 `configHash` 의 새 소유자도 같이 정해진다.

### 4. 씬·브랜치 결정이 unit 4 에 필요한데 unit 0 에 없다

조각 B 의 완료 기준이 「판이 돈다」인데 돌 장소가 조각 B 안에서 정해지면, 91개 필드 재배선이 계획에
없던 채로 그 조각에 끼어든다.

### 5. 서버 payload 확인이 unit 3 과 같은 시점에 걸려 있다

`IMatchGoal` concrete 3 을 v1 에 두기로 **이미 확정**(사용자 판정 1)했으므로, 그 셋 중 둘이 제출
가능한지는 unit 3 **앞**에 답이 나와야 한다. 아니면 만들어 놓고 못 쓰는 목표 둘이 남는다.

### 6. 도구 10개의 처분이 unit 8 인데 조각 A~D 가 그 도구를 쓴다

해저드·순찰·재배치 디버그 메뉴 6개는 `Scripts/Battle/`·`Scripts/Bridge/` 안에 있어 unit 8 의 삭제에
쓸려 간다. 그런데 그 기능들을 새 코어에서 손으로 찔러 봐야 하는 시점은 조각 C·D 다.

---

## 계획이 옳게 잡은 것 (짧게)

- **세로 조각 + 옛 전투 병존.** big-bang 이식을 피한 것. 계약 10 의 장치가 없을 뿐 방향은 맞다.
- **계약 4 의 `noEngineReferences` 어셈블리로 컴파일러가 경계를 지키게 한 것.** 실현 가능함을 검증했다:
  `Wassup.Skills`·`Wassup.UnitAi` 가 이미 정확히 그 형태이고, 필요한 `com.unity.burst`·
  `com.unity.mathematics` 는 entities 와 독립된 depth 0 manifest 항목이다.
- **계약 12 가 「어느 담당자로도 못 가는 브리지 메서드 = 설계 결함 신호」를 판정 장치로 세운 것.**
  검사 대상이 브리지로 한정된 것이 결함이지(빠진 것 10), 장치의 형태는 옳다.
- **계약 2 의 구분** — 「없으면 조용히 망가지는 세부는 땜빵이 아니라 필수」 + unit 별 「이식 제외」 표.
  플레이 중 이상하면 그 표부터 본다는 사용법까지 정한 것.
- **계약 6 (값의 정본은 판 밖).** 시트 파이프라인이 코어에 영향을 주지 않음을 확인했다:
  `Data/StatImport/` · `Scripts/SheetSync/` · `LoginAutoImport.cs` · `DcSheetRuntimeRefresher.cs`
  어디에도 `BattleBridge` 나 `Unity.Entities` 참조가 **0건**이다. SO 전용 파이프라인이 맞다.
- **매치 모드 원칙 3** — 모드는 값을 덮어쓰지 않고 「어느 저작 자산을 쓸지」만 고른다. 웨이브 램프·
  당김 상한·케이던스를 덱 소유로 남긴 것.
- **`battle-core-architecture.md` 를 §1 / §2~§10 로 가른 판단.** §1.1~§1.15 는 실제로 아키텍처
  중립이라 살아남고, §2 부터가 구현 서술이다. 정확한 절단선이다.
- **계약 5 의 드라이버 틱 발행률로 슬로모·정지를 만든 것.** `TimeManager` 도메인 시간 제어(제약상
  의도된 예외 2건 중 하나)와 충돌하지 않는다.
- **`Endless` 를 enum 에 넣지 않은 판단.** `KillScoreTimed + CountUp + submitsReport=false` 로 표현
  가능하다는 근거와, 은퇴한 `endless-mode` 판례를 든 것.

---

## 부록 — 이 리뷰의 실측 수치 한눈에

| 항목 | 값 |
|---|---|
| BattleScene 의 브리지 저작 필드 | 91 |
| 코드의 `[SerializeField]` 선언 | 93 |
| 브리지 메서드 선언 (7 partial) | 369 |
| `BattleBridge.cs` 본체 | 11,135행 |
| 브리지 코드 참조 파일 / 라인 | 151 / 990 |
| `Unity.Entities` 참조 파일 (전체) | 386 |
| 그중 `Scripts/Battle/` 밖 | 187 (테스트 148 + 런타임 28 + 에디터 3 + 브리지 8) |
| 테스트 lane 파일 (EditMode/Assets/PlayMode) | 270 / 37 / 92 |
| PlayMode 중 브리지 결합 | 82 (89%) |
| PlayMode 기준선 실패 (2026-09-21) | 59 |
| 브리지/Entities 결합 에디터·디버그 도구 | 10 (그중 6개가 삭제 예정 폴더 안) |
| `Scripts/Battle/` · `BattleBridge` 를 인용하는 마크다운 | 1,229 |
| spec 폴더 총수 | 313 |
| entities 제거 시 끊기는 asmdef 참조 (`Unity.Transforms`) | asmdef 4개 |
| 리뷰 자동화 감지기의 감시 경로 | 2 (둘 다 소멸 예정) |
