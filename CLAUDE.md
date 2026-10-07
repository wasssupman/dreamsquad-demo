<!--
  사람용 메모 (컨텍스트에 주입되지 않는다)
  - 이 파일은 매 세션 통째로 들어간다. 코드·폴더를 보면 아는 것은 넣지 않는다.
  - 넣는 기준: 에이전트가 같은 실수를 두 번 했다 · 도구/학습 데이터 기본값과 이 프로젝트가 다르다 · 코드로는 안 보이는 의도.
  - 「제약」은 프로덕션에서 실제로 막는 실패가 있고 되돌리기 어려운 것만. 나머지 섹션은 지식이다.
  - 상세는 docs/ 로 보내고 여기서는 가리킨다. 200줄 이하 유지. 규칙에는 이유를 한 줄 붙인다.
  - 2026-10-01 전면 재작성. 옛 본(절대 제약 1~13 · 전투 코어 절대 제약 1~6)은 git 이력에 있다.
    코드 주석의 「CLAUDE.md 제약 13」「전투 코어 — 절대 제약 N」은 docs/reference/battle-core-architecture.md §8 로 옮겨졌다.
    그 옛 번호와 겹치지 않게 「제약」 단락은 번호 대신 이름으로 부른다.
-->

# Defense Tournament (wassup)

비동기 토너먼트 모바일 디펜스 게임. **프로덕션 초기 단계**다 — 지금 목표는 코드를 정식 설계에 맞추는 것이고,
아웃게임(로그인·로비·프로필·토너먼트·결과 화면)은 이 리포에 **없다** — somnia-client 가 담당하고, 이 리포는 전투만 든다(`demo-diet` 2026-10-07).
인게임 UI·에셋은 데모용이라 정본이 아니다. 방향은 서버 권위 실시간 게임 서버다: 매치 설정을 서버에서 받고
핵심 로직은 서버에서 돌며, 클라는 표시와 커맨드 전송을 맡는다. 그래서 전투 코어의 커맨드·사건·정의표가 곧 서버 계약 후보다.

현재 설계의 요약은 `docs/blueprint/README.md` 에서 시작한다.

## 제약 — 어기게 되면 멈추고 묻는다

- **결정론** — 고정 틱 1/60 · 단일 스레드 · 순회는 `SimEntityId` 오름차순 · 난수는 시드 스트림(`RngStreams`)으로만. 비동기 토너먼트는 같은 입력이 같은 판이어야 한다(리플레이 · 공정성). 깨지면 골든을 전부 다시 굽는다.
- **코어 경계** — 전투 코어는 엔진도 I/O 도 모른다. 참조는 `Wassup.Skills` · `Wassup.UnitAi` 와 엔진 모듈 중 **`UnityEngine.MathematicsModule` 하나**(6.6 부터 `Unity.Mathematics` 가 엔진 모듈이라 asmdef 의 `noEngineReferences` 는 꺼져 있다). 경계는 컴파일러가 아니라 `CoreArchitectureTests` 의 소스 스캔(세 어셈블리에서 `UnityEngine`·`UnityEditor` 금지)과 헤드리스 lane(`CoreModule` 없이 빌드)이 지킨다. 코어는 나중에 서버로 옮겨 간다.
- **전투 코어에 매니저 없음** — 코어 안의 판정·상태·저장은 그 일의 담당자만 한다(책임이 한 클래스로 다시 모이면 서버로 옮길 계약이 흐려진다). `BattleMatch` 는 담당자를 만들고 틱 순서를 나열하는 조립 지점일 뿐이다. 담당자는 모드를 모른다(모드는 SO `MatchModeData` 의 닫힌 집합, 재현은 modeId + seed). 판 밖 전역 매니저(`TimeManager` · `SoundManager`)는 이 제약 밖이다.
- **커맨드 ≠ 사건** — 플레이어 입력은 커맨드(틱 시작에 적용 + receipt), 사건은 값 스냅샷(`SimEntityId` 키). 사건으로 상태를 되묻지 않는다. UI·뷰는 읽기 모델과 사건만 읽고 바꿀 것은 커맨드로 보낸다 — MonoBehaviour 에 전투 판정을 쓰지 않는다. 이 경계가 곧 서버 계약이 된다.
- **도달 판정은 산식 하나** — `|좌표 차| ≤ 범위 + 원점 항 + 대상의 몸`. 정본 진입점(`Wassup.Skills.SkillMath` 공개 진입점 · 코어 어댑터 `AttackReach` — 방향 도형은 `InReachShaped` 하나)을 호출만 하고 인라인으로 쓰지 않는다. 원점 항은 효과의 형이 정한다(몸에서 나오는 것 = 그 몸 / 자리에 떨어지는 것 = 칸 반폭). 함수 뒤에 숨은 상수 가정 때문에 같은 결함이 두 번 났다 — 상세 `docs/reference/battle-core-architecture.md` §8-7.
- **에이전트는 시트에 쓰지 않는다** — 시트 push 는 8탭 전량 업서트라 동료가 시트에서 조정한 값까지 되돌린다. 그래서 push 도구 자체를 뺐다(2026-10-07 `battle-content-finish`). 에셋을 고치고 시트 반영은 사용자에게 요청한다.
- **신규 기능의 성질은 먼저 묻는다** — 무엇을 만들지, 그것이 플레이어에게 어떤 규칙인지는 설계를 제안하기 전에 묻는다. 물었을 때 에이전트가 상정한 두 답 밖의 축이 두 번 돌아왔다. 전투 코어의 아키텍처 결정(담당자 소속 · 커맨드냐 사건이냐 · 틱 단계 · 결정론 영향)도 묻는다 — 작업 전에 묶어서 한 번.
- **git** — push 는 매번 사용자 승인 후. 커밋은 경로를 지정해서(`git commit -m … -- <경로>`) 하고 `--amend` 는 쓰지 않는다(여러 세션이 한 인덱스를 쓴다 — plain commit 이 남의 스테이징을 삼켰다). 단 한 파일에 다른 세션의 변경이 섞여 있으면 경로 지정 커밋이 그 파일의 작업 트리 전체를 싣는다 — 그때는 `git add -p` 로 내 헝크만 올리고 `git diff --cached` 로 남의 것이 없음을 확인한 뒤 경로 없이 `git commit`. GitLab 에서 직접 커밋하지 않는다(보호 브랜치라 미러의 fast-forward 가 막히면 force 도 못 한다). 강제 push · `--amend` 는 훅(`.claude/hooks/guardrails.mjs`)이 거절하고 push 는 확인창이 뜬다.

## 스택 — 기본값과 다른 것

- Unity `6000.6.3f1` · URP 17.6 · **Input System 전용**(레거시 `Input` 아님) · spine-unity **4.3** 런타임(export 는 같은 major.minor — `Assets/Spine/version.txt`) · 트윈은 PrimeTween.
- 전투 = 순수 C# 코어 `Assets/_Project/Scripts/BattleCore/`(asmdef `Wassup.BattleCore`) + Unity 층 `Scripts/BattleCoreUnity/`(시간 `BattleDriver` · 정의표 물질화 `MatchDefinitionBuilder` · 뷰 · 입력). 전투 입구는 값 `MatchEntryInput`, 출구는 사건(`BattleDriver.MatchStarted/DeckLocked/MatchFinished/MatchAbandoned`) — 바깥(somnia App)은 그 둘로만 통한다. 전투 UI·연출은 MonoBehaviour.
- 판 저작은 SO 두 장이다 — `Data/BattleContent.asset`(카탈로그 · 장판 · 스택 · 튜닝 · 시즌 · 보너스 · 공용 액티브 · 런타임 머티리얼 묶음)과 `Data/DefaultLoadout.asset`(바깥 입력이 없을 때의 유닛 · 돌 · 덱). 씬의 `BattleDriver` 는 그 둘과 모드 SO 를 참조만 든다.
- Entities/ECS 전투는 제거됐다. Burst·Collections 패키지는 남아 있다(URP 의존 + 맵 빌드가 `NativeArray`·`FixedList` 를 직접 쓴다) — 전투 코어에서는 쓰지 않는다.
- 코드 주석의 「옛 `BattleBridge.X` 의 후계」 · 옛 spec 이름 꼬리표는 이력이다. 현재 동작은 코드 본문으로 확인한다. 「CLAUDE.md 제약 13」 같은 옛 번호는 `docs/reference/battle-core-architecture.md` §8 머리의 대조표로 찾는다.

## 전투 코어

- 구조 전체와 설계 불변식은 `docs/reference/battle-core-architecture.md`(§8) — 전투를 바꾸기 전에 대조한다.
- 위 제약의 상당수를 `Tests/EditModeCore/CoreArchitectureTests.cs` 가 기계로 지킨다. 빨개지면 테스트를 고치기 전에 그 테스트 주석의 이유를 읽는다.
- 슬로모·정지는 틱 발행률이다(`BattleDriver` 가 `TimeManager` 배율만큼만 틱을 낸다). 분산·지터는 시드 난수보다 index 기반이 낫다.
- 새 사건 종류(`CoreEventKind`)를 열면 트레이스 채널(`Harness/CoreTrace.cs` `TryChannel`)에 실을지 같이 정한다 — 안 실으면 골든이 그 사건을 조용히 모른다.

## 데이터 — 값의 정본

- 유닛 스탯·스킬·카드 값의 정본은 **구글 시트**다. 시트는 에디터 임포터(`Editor/UnitStatImport`)가 SO 파일에 쓴다(로그인/로비 진입의 런타임 덮어쓰기는 `demo-diet` 에서 제거). SO 파일만 고치면 다음 임포트에 되돌아간다. 시트와 에셋은 한 세트로 맞아야 한다(시트 쓰기는 제약 「에이전트는 시트에 쓰지 않는다」).
- 그래서 스탯·스킬·VFX 수치는 SO·시트·프리팹에 둔다. 코드 리터럴은 밸런싱에서 안 보인다. 테스트도 밸런스 수치를 리터럴로 박지 않는다(부호·배율·상대 비교·구조로 단언).
- 시트 임포터는 대부분 미리보기 없이 즉시 에셋에 쓴다(`Skills`/`SkillOwners` 만 diff 미리보기가 있다). 시트와의 대조는 `curl` 읽기 전용으로. 탭·헤더 정본은 `docs/spec/skill-data-table/5_sheet_io.md`.
- 저작 SO → 정의표 매핑이 빠지면 기본값 0 으로 조용히 산다(배치가 공짜였다). 정의표 필드를 늘리면 빌더 매핑 테스트를, 저작 enum 을 코어가 미러하면 번호 핀 테스트를 같은 커밋에 둔다.

## Unity 함정 — 실제로 당한 것

- `Time.timeScale` 을 쓰지 않는다. 시간은 `TimeManager.Request(TimeDomain, scale)` lease 다(전투만 멈추고 UI 는 실시간으로 두기 위해).
- `UnityEngine.Object` 에 `?.` / `??` 를 쓰지 않는다 — 파괴된 객체의 fake-null 을 몰라 `OnDestroy` 정리 루틴이 중간에 죽었다.
- `Shader.Find` 는 빌드에 포함된 셰이더만 찾는다(에디터에선 다 찾아서 모바일 빌드에서야 null 로 드러난다). 런타임 머티리얼은 `Wassup.Rendering.RuntimeMaterialFactory` 경유이고 원본은 SO `Data/Materials/Runtime/RuntimeMaterialSet.asset` 의 슬롯이다 — 새 셰이더는 머티리얼을 만들어 슬롯을 늘린다(`Resources` 폴더 없음 · `Shader.Find` 폴백 없음).
- 런타임 코드의 에디터 전용 API 는 `#if UNITY_EDITOR` 로 막는다 — CI 가 없어 모바일 빌드에서야 깨진다.
- 에디터는 사용자·여러 세션과 공유한다. 스크립트 저장·refresh·테스트·Play 전에 `isPlaying` 을 확인한다 — 사용자 플레이가 끊긴다.
- 열린 씬의 YAML 을 밖에서 고치면 Reload 모달이 에디터를 멈춘다. 열린 씬은 에디터 안에서(일회용 MenuItem 스크립트 · 인스펙터), YAML 직접 편집은 안 열린 씬에만.
- `EditorSceneManager.SaveScene` · `AssetDatabase.SaveAssets()` 는 남의 미저장 WIP 와 임포터가 메모리에 덮은 값까지 디스크로 민다. 저장은 `SaveAssetIfDirty(대상)` 로, 씬 검증은 가능하면 저장 없이 in-memory 로. 열린 씬에 배선을 영속해야 하는데 손대기 전부터 씬이 dirty 였으면 `lessons/02` 의 delta 격리(스냅샷 → HEAD 로 되돌림 → 내 변경만 재적용 → 커밋 → 복원), 아니면 SaveScene 해도 된다(남의 헝크는 커밋 때 「git」 제약대로 거른다).
- unity-mcp 패키지는 6.6 전환(2026-10-07, `docs/spec/unity-6-6-upgrade/`)에서 뺐다 — 이 리포엔 MCP 가 없다(이식 뒤 somnia 의 `com.unity.ai.assistant` 를 쓴다). 에디터가 닫혀 있을 때의 테스트는 배치 CLI(`docs/reference/test-procedure.md` 「배치」). 에디터 공유·포커스·Reload 모달·워크트리별 인스턴스 교훈은 `docs/reference/lessons/01-unity-mcp-operation.md`(옛 MCP 기준)에 남아 있다.
- `Assets/Screenshots/` 안은 비추적 스크래치다 — 폴더째 지우지 않는다(복구 불가).

## 검증

- lane: `BattleCore/` → `Wassup.Tests.EditMode.Core`(Unity 없이는 헤드리스 `tools/battle-core-rebuild/headless/`) · `BattleCoreUnity/` → 거기에 `PlayMode.Core` · 전투 밖 순수 계산(맵 빌드·카메라 수학·UI 레이아웃) → `EditMode` · 에셋·시트 → `EditMode.Assets`. 상세 `docs/reference/test-procedure.md`.
- 기본은 EditMode 까지다. PlayMode 는 `Wassup.Tests.PlayMode.Core` 만 돌리고, 에디터를 수 분 점유하니 lane 표에 있어도 사용자에게 묻고 돌린다.
- 새 테스트를 넣었으면 `total` 이 늘었는지 본다. 안 늘었으면 테스트가 아니라 컴파일 실패다(`read_console` 에서 `error CS`).
- 골든은 Unity 에서만 굽고 대조한다(Mono 의 float 이 .NET 과 갈린다). 헤드리스 lane 은 Golden 을 뺀다.
- 알려진 선행 빨강은 `docs/spec/README.md` 백로그 「(마) 사용자 몫」에 있다. 거기 없는 빨강은 회귀로 본다. 카드 전체를 한 번에 단언하는 집계형 테스트는 실패 개수가 같아도 새 카드가 섞일 수 있다 — 실패 메시지의 id 목록까지 대조한다(카드 효과 수치를 바꾸면 카드 설명 문안 테스트가 그 경우다).
- 씬 배선은 사용자 수작업으로 미루지 않는다 — 에디터 스크립트(일회용 MenuItem · 배치 `-executeMethod`)로 배선하고 Play 검증까지가 완료다.

## 일하는 방식

- **버그는 고치기 전에 재현한다.** 재현 단언은 사용자의 문장으로 쓴다(「안 움직인다」 → 「셀이 N프레임 안에 바뀐다」) — 내가 고친 함수의 반환값이 아니다. 빨간 것을 먼저 보고, 경계마다 계측한다. 세 번 고쳐도 남으면 멈추고 접근을 의심한다. 보고할 때 「구현됐다」와 「증상이 사라졌다」를 섞지 않는다. (`superpowers:systematic-debugging` 이 있으면 그 절차를 탄다.)
- 추상 타입의 근거는 닫힌 변형 축(enum·SO 로 열거된 종류)이다. 축이 있으면 구현체가 하나여도 타입을 열고, 축이 없으면 둘이어도 열지 않는다. 축이 보이는데 한 사례를 하드코딩해 두는 것도 같은 실수다.
- 코드 구조·이름·분할은 알아서 정하고 짧게 알린다.
- 스코프(이 작업에 무엇을 넣고 뺄지) 논의가 필요하면 사용자에게 묻는다.
- 기능 작업은 `docs/spec/{slug}/`(README + 번호 작업 단위)로 한다. 형식·진행 규칙·후속 백로그는 `docs/spec/README.md`. 작업 단위 하나가 끝나면 사용자 확인을 받고 다음으로 간다.
- 정본: **지금 어떻게 동작하나**는 코드·에셋이 답한다. **무엇이 맞나**는 규칙 문서가 정한다 — 위 「제약」 · 게임 규칙 `docs/reference/ingame-flow.md` · 설계 불변식 `battle-core-architecture.md` §8 · blueprint 「알고 뺀 것」 · 진행 중 spec 계약. 코드가 규칙 문서와 다르면 문서를 코드에 맞춰 고치지 말고 묻는다. 그 밖의 설명 문서(값 바꾸는 곳 · 절차 · 지도)가 코드와 다르면 그 문서가 낡은 것이다.
- 2026-10-01 spec 초기화로 옛 spec·plans·prototype 은 지웠다(남긴 spec 은 `docs/spec/README.md` 「시작점」). 지운 문서는 평소 읽지 않고, 꼭 필요하면 태그 `archive/pre-spec-reset` 에서 꺼낸다. reference 문서에 남은 옛 spec 이름도 그 태그 기준이다.
- 응답·문서는 한국어(기술 용어는 영어). 설명은 게임에서 무슨 일이 일어나는지 먼저, 코드 이름은 괄호로.

## git

- GitHub `origin` 의 `main` 이 정본이다. GitLab(`gitlab` remote)은 미러이고 기본 브랜치는 `master` 다(GitLab `main` 은 은퇴 — 쓰지 않는다).
- 작업은 `origin/main` 계보 위에 커밋한다(워크트리 브랜치 이름이 달라도 된다). push 는 `git push origin HEAD:main` → 미러 `git push gitlab HEAD:refs/heads/master`. 그 전에 `git fetch --all` 로 앞선 쪽이 없는지 본다 — GitLab 이 앞서 있으면 `git merge gitlab/master` 로 편입해 양쪽에 민다.
- GitLab 은 SSH(`git@gitlab.playlinks.co:cash-royale/dreamsquad-demo.git`)로만 — HTTPS 는 앞단 ALB 가 대용량 전송을 끊는다.
- 커밋은 자율로 해도 된다. 새 파일은 `git add -- <파일>` 뒤 같은 형태로. `.git/index.lock` 은 지우지 말고 기다린다. pull/push 전 `git status -sb`.
- Bash 샌드박스가 `.git/index` 쓰기를 조용히 롤백한다 — `git add`/`commit` 은 샌드박스를 끄고 실행한다.

## 더 읽을 곳

| 상황 | 문서 |
|---|---|
| 현재 설계 요약 · 어디가 정본인가 | `docs/blueprint/README.md` |
| 게임 규칙 · 설계 지향 7축 · 드림캐쳐 사용 규칙 | `docs/reference/ingame-flow.md` |
| 전투 구조 · 틱 단계 · 담당자 · 설계 불변식 | `docs/reference/battle-core-architecture.md` |
| 테스트 lane · 언제 무엇을 돌리나 | `docs/reference/test-procedure.md` |
| 이 프로젝트·환경의 함정(Unity MCP · git/씬 · 렌더/에셋 · 시뮬 · 에이전트 운용) | `docs/reference/lessons/` — Unity 조작·에셋 작업·커밋 전 해당 파일 |
| 점수 · 맵/웨이브 밸런스 · 적 이동 | `score-formula.md` · `map-wave-balancing.md` · `enemy-movement-algorithm.md` (모두 `docs/reference/`) |
| 맵 스테이지 · 무기 궤적 저작 | `docs/reference/map-stage-authoring.md` · `weapon-trail-authoring.md` |
| spec 형식 · 진행 규칙 · 다음 작업 후보 | `docs/spec/README.md` |
| 새 적 · 등장 조건 · 웨이브 생성 로직 변경 | `.claude/skills/enemy-wave-integration/` — 그 코드를 바꾸면 같은 커밋에서 스킬도 갱신 |
