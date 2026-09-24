# 세션 인계 — battle-core-rebuild (2026-09-22 ~ 09-24)

> 이 문서는 **다른 세션이 이어받기 위한 지도**다. 정본은 `README.md`(계약 13 · 작업 표 · 상태 라인) → 각 unit 파일 → 코드·커밋이고, 여기엔 「어디까지 왔나 · 무엇이 결정됐나 · 어디서 넘어졌나 · 어떻게 일하나」만 담는다. 이 세션의 리드는 Claude(Fable) 였고 구현·리뷰는 격리 에이전트가 했다.

## 1. 지금 상태 (2026-09-24)

| 항목 | 값 |
|---|---|
| 브랜치 | `rebuild/battle-core` = 워크트리 `/Users/sy/dev/wassup-core`. main 워크트리 `/Users/sy/dev/wassup` 는 문서 정본. 조각 B 까지 **main 에 머지됨**(`d9fbe90c`), 이후 둘은 같은 지점을 따라간다(main 문서 커밋 → 브랜치 `--ff-only`) |
| 푸시 | GitHub `f119c1b1`(main + 브랜치)까지 푸시됨(2026-09-24). 그 뒤 커밋은 미푸시. GitLab 미러는 SSH 불통으로 사용자가 직접. 푸시는 매번 명시 승인 |
| 완료 | unit 0(환경) · 조각 A(1~4) · 조각 B(5a·5b·5c + 플레이 1~3차 결함 9건 수정) · **조각 C 전부(6a·6a2·6b·6b2·6c+후속 5)** · 드리프트 감사 수정 13건 · unit 7 spec 4편(7a~7d) |
| 진행 중 | **사용자 플레이(조각 C) 대기** — 리뷰 전건 APPROVE. 플레이 통과 시 unit 7(7a→7b→7c→7d). 다른 세션이 이어받으면 브랜치 HEAD 와 `git status` 로 산출물 유무를 먼저 확인 |
| spec 있음·구현 전 | 7a~7d · 8·9·10(README 행만) |
| 검증 기준선(HEAD `f7fbbdd29`) | 헤드리스 export: build 0 · test 529 · Unity 층 Check 0 · Unity EditMode 코어+Assets 766/768(선행 2 = `bomb_man`·`boomerang`, 골든 11종 무변) · PlayMode 코어 49/49 · `check_ledgers.py` exit 0, `bridge-methods` 미정 **44** · rules 보류 38 |
| 새 씬 진입 | 워크트리 에디터 메뉴 `Wassup/BattleCore/씬 열고 플레이 (기본 모드)` (빌드 설정 무변, 로비 진입 없음) |

## 2. 사용자 결정 기록 (날짜순 — 되돌리지 말 것)

- 09-22/23 초기: 순수 C# 코어 + Mono 드라이버/뷰 · 옛 트레이스 parity 폐기 · 서버권위 규율 제거 · **「기획 그대로」— 규칙은 옮기고 기계는 안 옮김, 땜빵은 의도만, 세세한 건 플레이로 개선** · 캐스터 4기 + 캐스트 기계 제거(소환사 유지) · 죽은 기능 4종 제거 · 슬로모 = 틱 발행률, 종료 후 틱 0 · `IMatchGoal` concrete 3, WaveClear/TimeAttack 마음 붕괴 = **패배**, 한 토너먼트 = 한 모드, v1 제출 KillScoreTimed 만 · 제약 8 개정(닫힌 변형 축) · CLAUDE.md 범위별 재편 선행 · 용어 「sim/시뮬」 금지 → 「전투 코어」 · 브리지는 흩지 않고 **파괴**(담당자 8 + 조립 지점) · 「초안 쓸까요」 금지, 플레이어 규칙만 묻는다.
- 09-23: 판 안 **재시작 없음** · 액티브 쿨다운 = **판의 시계**(감속에 같이 느려짐) · 결정론은 **같은 런타임 안**의 계약, 골든 정본 = Unity EditMode.
- 09-23 플레이 결정: 배치 하이라이트 = **불가 칸**(프랍 막힘 빨강 / 유닛 점유 앰버, 가능 칸 안 칠함 — 옛 spec 결정 대체) · 배치 **자석 보정 제거** · 퇴근 = 선택 패널 버튼(길게 누르기 은퇴).
- 09-24: F30 투사체 디버프 곱누적 = **(a) 고친다**(출처 = 발사자) · 시전자 착탄 효과는 **그 시전자가 쏘는 모든 탄**에 적용 · 겹침 = **합, 상한 있음** · 부여 어휘 **신설 없음**(화염 = `ApplyStack(Fire)`) → `6a2`.
- 09-24 플레이 1차: 범위 그림 vs 판정 차이(그림 = 범위 + 자기 몸, 판정 = + 대상 몸)는 옛 게임과 동일 → **(a) 그대로**. 사용자 추가 지시 「옛 로직 재탐색·구현 정확성 확인」 → 옛/새 `AttackReach` 수치 패리티 테스트(`AttackReachParityTests`, Assets lane) 신설.
- 미결(기본값으로 진행 중): 없음. 「사용자 결정 필요」 항목은 README 상태 영역에만 둔다.

## 3. 쟁점·함정 (이 세션에서 실제로 넘어진 것)

1. **리뷰는 top-down 도 돌려라.** 내용 리뷰만 돌려 씬·브랜치·훅·테스트 lane 이 통째로 빠졌었다(06 완전성 리뷰 13건). 지금은 spec 초안마다 격리 critic 을 붙인다.
2. **에이전트의 「N/N 통과」는 믿지 않는다.** 커밋 SHA 를 `git archive` 로 클린 export 해 리드가 build/test/Check 3종을 재실행한다(§5).
3. **Unity Mono 는 float 을 확장 정밀도로 평가한다.** .NET 9 헤드리스와 301틱부터 1 ulp 갈려 3분 판은 이벤트까지 갈렸다. 골든은 Unity 에서만 굽고(`Wassup/BattleCore/Golden/Bake Missing`), 헤드리스 lane 은 `[Category("Golden")]` 제외. 정본: `docs/reference/lessons/04-sim-design.md` 마지막 절.
4. **spec 은 양쪽에서 갱신된다.** 구현 에이전트가 브랜치 커밋에 spec 갱신을 넣어 main 이 stale 했고, 감사가 stale 본을 읽어 오판했다. unit 종료마다 `git checkout rebuild/battle-core -- docs/spec/battle-core-rebuild` 로 main 에 docs 커밋.
5. **5b 가 옛 기능을 「은퇴」로 오판하고 새 조작을 지었다**(탭 선택 배치 · 패널의 퇴근 버튼 → 길게 누르기). 「이식 제외」의 제거는 **옛 spec 인용**이 있어야 한다.
6. **손으로 쓴 씬 YAML 함정**: Grid 클래스 ID 는 156049354(156 은 TerrainData). 열린 씬의 YAML 을 외부에서 고치면 Reload 모달이 떠 MCP 가 멈춘다(사용자가 눌러야 함).
7. **워크트리 에디터 MCP 세션은 도메인 리로드마다 바뀐다** → `set_active_instance` 재호출. 끊기면(instance 0) 에디터에서 Start Session. 비포커스 에디터는 ~9fps 라 0.45초 연출은 캡처 불가.
8. **빌더 매핑 누락은 조용히 죽는다.** 유닛 배치 저작 7칸(코스트 등)이 정의표로 안 옮겨져 배치가 공짜였고, 발사 명세 선정 규칙 enum 번호가 어긋나 12 중 11 이 오독됐다(수정 중). 새 enum/정의표 필드마다 **핀 테스트 + 빌더 매핑 테스트**.
9. **리뷰어도 틀린다.** 5c 리뷰의 MEDIUM(`using Wassup.Battle.Units` 잔류)은 오판이었다 — `Faction` 열거형의 집. 리뷰 지적은 코드로 대조하고 나서 반영한다.
10. 스폰 퍼짐·몸 반경이 코어 리터럴이었다(unit 2) → `MovementTuningConfig` 정의표로. **코어에 숫자 리터럴 금지**(제약 6).
11. **공유 인덱스에서 `git add <p> && git commit` 은 남의 스테이징을 삼킨다**(`acc3c572a`). 커밋은 `git commit -- <경로>` 로만, 한 파일에 두 에이전트 헝크가 섞이면 `git add -p` 로 자기 헝크만. 파일 소유권은 «같은 파일을 둘이 만지지 않게» 나눈다.
12. **구현 에이전트가 옛 규칙을 바꿔 놓는다**(unit 4 의 「개체당 1회」를 「칸 1회 소비」로 오독 → 6b 에서 효과 타일이 첫 배치에 사라졌다). 「주의할 점」에 규칙 차이가 나오면 옛 코드 줄로 확정하고 **옛 규칙으로 되돌리는 것이 기본**(사용자 결정이 있을 때만 예외). 반대로 죽은 기제(도발 공격 프로필)를 살리라고 잘못 지시한 사례도 있었다 — 데이터가 그 분기를 «실제로 타는지»까지 봐야 한다.

## 4. 조각별 남은 작업

- **조각 C(효과)**: 6a(진행 중) → 6a2(탄 관문) → 6b(존·길막·장·효과 타일) → 6b2(픽업·사직서·열기/피로, 골든 재굽기 1회) → 6c(뷰 풀 4 + 착탄 예고 표식 + 디버그 도구 3). 장부 59 → 51 → 46 → 46 → 44.
- **조각 D(트리거)**: 7x spec 초안 대기(spec-7) → critic → 구현. 정본 rev 3. 미정 44 → 0. 사용자 플레이 2차 → main 머지.
- **조각 E**: 8(뷰·규칙 보유자 10 이전 · 브리지 소멸 · 파이프라인 맵), 9(ECS·패키지·옛 폴더·옛 lane·리뷰 도구 은퇴 · 문서 목록 · **CLAUDE.md 옛 절 삭제 + 코어 절 승격** · 로비 진입을 새 씬으로), 10(인계).
- 잡다: `CoreCameraFeed` 를 `BattleCoreUnity/` 로 이동(lane 전환으로 가능) · 결과 화면 뒤 HUD 게이팅 · 키링 잔류물(고리·줄) · `Faction` 네임스페이스 이사 여부(8/9) · 후속 후보(README).

## 5. 운용 규칙 (이 세션이 실제로 쓴 절차)

- **unit 사이클**: spec(초안 에이전트 → 격리 critic → 리드가 코드로 대조 후 반영) → 구현 에이전트(재현 테스트 먼저 · 결함당 커밋 1 · 경로 명시 스테이징 · `.meta` 동반 · 커밋 트레일러) → 리드 export 3종 재검증 → Unity EditMode·PlayMode 코어 lane → core-reviewer(`.claude/agents/core-reviewer.md`) → spec 완료 기준 체크 → main docs 동기화 → 필요 시 사용자 플레이.
- **export 검증 명령**: `git -C /Users/sy/dev/wassup-core archive <sha> Assets/_Project/Scripts Assets/_Project/Tests Assets/_Project/Data Assets/_Project/Editor Assets/_Project/Shaders Assets/Resources tools/battle-core-rebuild | tar -x -C <scratch>` → `dotnet build tools/battle-core-rebuild/headless/BattleCore.csproj` · `dotnet test …/BattleCore.Tests.csproj` · `dotnet build …/BattleCoreUnity.Check.csproj`. 헤드리스 csproj 는 워크트리 `Library/ScriptAssemblies` 의 dll 을 참조한다(브랜치가 새로 연 Runtime 메서드가 보이도록).
- **Unity MCP**: 인스턴스 `wassup-core@d01bc0f2458f1ba9`(워크트리) vs `wassup@…`(main) — 반드시 워크트리로 `set_active_instance`. `execute_code` 는 CodeDom C# 6 본문. `refresh_unity mode=force` 금지, 테스트 중 refresh 금지, 손으로 쓴 `.meta` 뒤엔 `refresh_unity(scope=all, compile=request)`. UI 포함 캡처는 `ScreenCapture.CaptureScreenshot`(카메라 캡처는 UI 를 못 담는다).
- **동결**: `.githooks/commit-msg` 가 `Scripts/Battle/`·`Bridge/` 스테이징을 `[old-battle]` 없이 거부. 새 코드에 그 태그를 쓰면 무언가 잘못된 것.
- **버그 절차**: 사용자 문장을 재현하는 빨간 테스트 → 원인 → 수정 → 초록. 3회 실패면 정지.
- **문서 크기**: 옛 코드 복사·적응이 ~2.5k 줄을 넘으면 unit 을 나눈다(5→5a/5b/5c, 6→6a/6a2/6b/6b2/6c).

## 6. 이 세션의 커밋 지도 (브랜치, 최근순)

`f7fbbdd29` 거점 최근접 증언 · `eadd0d2b0`·`dc31350d0` 부가 타격 D2·D1 · `dd9d19193` 도달 패리티 · `bbfa490f4` 도형 가이드 테스트 정정 · `b3aa03dea` 마크 자격 옛 규칙 · `1b2a9483c` **드래그 도형 가이드 이식** · `72785fa35` dev 편성(캐스터 제외) · `ef3606e4c`·`1a798bb8e` 6c 문서(브레스·예고 7a 이월) · `8fb234be7` 방패 마음 부수 피해 백스톱 · `785cf4234` AttackResolved 도형 스냅샷 · `e3337272f` LastRunEnded 55 · `405bf29f3` AggroReleased 54 · `eca3e47e7` **6c** · `ec041a8af` 도발 공격 이식 제외 기록 · `53e8fec4a` H6 revert(cc8fe232 철회) · `bed80d790` **6b2** · `a6098eab` 라이브 정의표 스모크 lane · `c70ea3f1`~`881550a3` 감사 수정 H1~H5·M6·M7·빌더 의미·enum 핀 · `acc3c572a` 6b 리뷰 docs(**+ fix-audit 모드 배선 4파일 편승** — 공유 인덱스 사고, 내용은 검증본) · `a3798db83` 효과 타일 옛 규칙 복구 · `cd390e511` **6b** · `732b5a00`·`56b8a4d8` 6a2 · `0087e9d2`·`ec10619d`·`94846d3c`·`a5180e9e` 6a · `80f90953` 6a2 spec · `8eb1daaa` 6c 추가 2건 · `4388d104` unit 6 spec 4편 · `d9fbe90c` **조각 A·B main 머지** · `99395bc0` 선택 패널+퇴근 버튼 · `bc43587b`~`e92477fa` 플레이 2차 결함 5 · `bf6aa7ae`~`04cfc393` 플레이 1차 결함 3(+반전) · `725df315`~`ebf055f4` 5c · `c4c98c92`·`f979550d`~`e3731485` 5b · `1b7e033b`~`aa16ee9d` 5a(+골든 Unity 재굽기 `68c28363` · 이동 튜닝 정의표 `fae42944`) · `e734f33e` 골든 정본 Unity · `7abfec27`·`55688ef5` unit 4 fix · 그 앞은 README 상태 라인 참조.
- **도달 판정 패리티(2026-09-24, 사용자 지시 「옛 로직 재탐색」)**: 방어유닛→적 「닿나」 사슬(산식·입력·도형 rev 3·그림)은 옛 로직과 정확히 같다 — `AttackReachParityTests` 20,000건 불일치 0(`dd9d19193`). 다른 곳은 부가 타격 선정 둘(힐러 회복 순위 HP 비율 순 · 부가 타격 클래스 필터 없음) → 옛 규칙으로 수정. 동거리 동률의 SimId 우선은 결정론 의도(옛은 엔티티 순서 = 비결정)로 유지.
