# 테스트 실행·작성 절차

> 무엇을 언제 돌리고, 새 테스트를 어디에 둘지.

## 다섯 개의 어셈블리

`run_tests` 의 `test_names`/`group_names` 필터는 이 셋업에서 0-match 다.
**동작하는 유일한 입도는 `assembly_names`** — 그래서 어셈블리가 곧 실행 단위다.

| 어셈블리 | 무엇 |
|---|---|
| `Wassup.Tests.EditMode` | 전투 밖 순수 계산(맵 빌드·카메라 수학·프로필·UI 레이아웃 등) + 합성 픽스처 UI. **실제 프로젝트 에셋을 로드하지 않는다.** 옛 ECS 전투 테스트가 `battle-core-rebuild` unit 9 에서 대량 삭제돼(목록 = `docs/spec/battle-core-rebuild/ledgers/retire-set.md` 5번 묶음) 전보다 훨씬 작다 |
| `Wassup.Tests.EditMode.Assets` | 실에셋(SO·맵·덱·카탈로그·프리팹) 저작 검증 |
| `Wassup.Tests.EditMode.Core` | **전투 코어**(`Wassup.BattleCore`)의 규칙. 엔진을 안 쓰고 씬도 안 연다 |
| `Wassup.Tests.PlayMode.Core` | 전투 씬(`BattleCoreScene`) 부팅 스모크 · 뷰 방출 순서 · 틱 발행률 · 배치 사슬 · 씬 배선 · 뷰가 자를 새로 만들지 않았나 |
| `Wassup.Tests.PlayMode` | **아웃게임 PlayMode · 씬 부팅 없음.** 남은 것은 `AuthE2ETest`·`DeckInfoPresetApplyLiveE2ETest`(둘 다 `[Explicit]` — 라이브 서버가 필요하다. ⚠ Unity Test Runner 는 **어셈블리 단위 실행에서 `[Explicit]` 을 걸러 주지 않는다**(NUnit 어댑터의 알려진 제한) — 어셈블리째 돌리면 둘이 딸려 돌아 환경 빨강이 나고 `AuthE2ETest` 는 실서버에 가입을 시도한다. 이름 지정 실행은 위 0-match 라 우회로가 아니다 — 이 어셈블리는 `[Explicit]` 격리(백로그) 전까지 돌리지 않는다)·`PresetBarPopupLayerTest`. 옛 전투 씬을 부팅하던 테스트는 unit 9 에서 은퇴했다(`retire-set.md` 7번 묶음) |


**헤드리스 lane**(`tools/battle-core-rebuild/headless/`)은 위 어셈블리와 **별개**다 — 코어를 .NET 으로
컴파일해 Unity 없이 돌리는 빠른 확인이다. `dotnet build …/BattleCore.csproj` · `dotnet test …/BattleCore.Tests.csproj` ·
`dotnet build …/BattleCoreUnity.Check.csproj`(Unity 층 컴파일 확인). 골든은 제외한다(계약 5: 골든의 정본 런타임은 Unity
— Mono 와 .NET 의 float 결과가 갈린다).
한 번에 돌리려면 `tools/battle-core-rebuild/headless/verify-fresh-skills.sh [ref=HEAD] [워크트리 파일 …]` — 커밋을 클린 export 해
`Wassup.Skills.dll` 을 **그 소스로 새로 구운 뒤** 위 셋 + `Retire.Check` 를 돈다(csproj 는 Skills dll 을 워크트리 `Library/ScriptAssemblies` 에서
받아, 에디터 재컴파일 전이면 옛 dll 로 거짓 빨강/초록이 난다). ⚠ `BattleCoreUnity.Check` 는 여전히 워크트리의 `Wassup.Runtime.dll` 을
참조한다 — `Data/` 저작 타입이 바뀐 커밋은 그 lane 이 거짓 빨강이고, 증거는 전 소스 컴파일인 `Retire.Check` 다.

**lane 판별 한 줄**: 바꾼 파일이 `Scripts/BattleCore/` 면 `EditMode.Core`(+ 헤드리스),
`Scripts/BattleCoreUnity/` 면 거기에 `PlayMode.Core` 를 더한다. 아웃게임(로비·프로필·토너먼트 UI)이면 `EditMode`,
에셋·시트면 `EditMode.Assets`.

`PlayMode.Core` 는 씬을 `EditorSceneManager.LoadSceneAsyncInPlayMode` 로 연다(`Tests/PlayModeCore/CoreSceneFixture.cs`).
빌드 설정 목록을 바꾸지 않는다.

## 언제 무엇을 돌리나

| 상황 | 실행 | 시간 |
|---|---|---|
| 아웃게임 코드 변경 루프 중 | `assembly_names=["Wassup.Tests.EditMode"]` | 초 단위 |
| **전투 코어 변경 후** | `assembly_names=["Wassup.Tests.EditMode.Core"]` (Unity 없이 먼저 보려면 헤드리스) | 초 단위 |
| **전투 Unity 층(드라이버·뷰 풀·입력) 변경 후** | 위 + `mode="PlayMode" assembly_names=["Wassup.Tests.PlayMode.Core"]` | 분 단위 |
| **시트 임포트·에셋·맵·콘텐츠 편집 후** | 위 + `["Wassup.Tests.EditMode.Assets"]` | 초 단위 |
| 작업 단위 완료·커밋 전 | `assembly_names` 생략 = EditMode 전체. 전투 Unity 층을 건드렸으면 `PlayMode.Core` 를 사용자에게 묻고 | 분 단위 |
| spec 종료·머지 전 | `mode="PlayMode" assembly_names=["Wassup.Tests.PlayMode.Core"]` — 사용자에게 묻고 돌린다. 아웃게임 `PlayMode` 는 `[Explicit]` 격리(백로그) 전까지 돌리지 않는다 | 분 단위 |

- **카드(시트·SO) 편집 후** Assets lane 의 `CardEffectWitnessTests`(카드 한 장 = 케이스 하나 · 붙이고/시전하고 강제 발동해 효과 종류가 걸리나)와 `CardBakeSnapshotTests`(굳힌 굽기 텍스트와 같나)를 본다. 스냅샷이 빨갛고 **의도한 변경이면** 메뉴 `Wassup/BattleCore/Debug/카드 스냅샷 갱신` → `Tests/EditModeAssets/Fixtures/card_bake_snapshot.txt` diff 를 같은 커밋에 싣는다(테스트는 파일을 쓰지 않는다).
- **유닛 · 적 규칙(소유 줄 · 효과 SO) 편집 후** 같은 lane 의 `BindingBakeSnapshotTests`(방어유닛 · 적 굽기 규칙 줄 + 카드 굽기 로그 = `Tests/EditModeAssets/Fixtures/binding_bake_snapshot.txt`)를 본다. 카드 스냅샷과 **갱신 방법이 다르다** — 메뉴가 없고 **테스트가 파일을 쓴다**: 파일이 없으면 구워 쓰고 「기준선 생성됨 — 커밋 필요」로 빨갛게 끝난다. 의도한 변경이면 파일을 지우고 다시 돌린 뒤 diff 를 같은 커밋에 싣는다. SO 를 읽으므로 Unity 에서만 구워진다(헤드리스 불가).
- `include_failed_tests=true` 로 돌리고 `failures_so_far` 를 읽는다. `failures_capped=false` 면
  거기 없는 테스트는 전부 통과다.
- **PlayMode 판정은 에디터 실행으로 한다.** (이력 — 옛 ECS 전투, unit 9 에서 제거: 배치 `-batchmode -nographics`
  에서는 Entities 의 `EntitiesAssetGC` NRE 가 그때 돌던 테스트에 임의 귀속돼 실패가 부풀어 보였다. Entities 패키지가
  빠진 뒤 배치 PlayMode 가 믿을 만한지는 다시 확인하지 않았다 — 확인 전까지는 에디터 실행이 기준이다.)
- 신규 `.cs` 를 만들었으면 실행 전 `refresh_unity(scope=all)` — `scope=scripts` 로는 .meta 가
  안 생겨 어셈블리에서 통째로 빠진다.

## 빨강을 만났을 때

**EditMode lane 의 빨강은 회귀로 취급한다.** 알려진 선행 실패는 `docs/spec/README.md` 백로그 「(마) 사용자 몫」에 있다 — 거기 없는 빨강은 회귀다. 카드 전체를 한 번에 단언하는 집계형 테스트는 개수가 같아도 새 카드가 섞일 수 있으니 실패 메시지의 id 목록까지 대조한다.

PlayMode 도 같다 — 알려진 선행 실패는 가장 최근 spec 인계의 검증 줄에 개수와 함께 적힌다
(2026-09-30 `skill-data-table/10_handoff_summary.md` = EditMode 선행 3 · PlayMode Core 97/97).
여기에 복제하지 않는다(두 곳에 적으면 갈라진다). 거기 없는 빨강은 내 변경이 만든 회귀로 취급한다.

## 새 테스트를 어느 lane 에 두나

**판별은 한 줄이다 — `AssetDatabase.LoadAssetAtPath`/`FindAssets` 로 실제 프로젝트
에셋을 읽는가?** 읽으면 `Tests/EditModeAssets/`, 아니면 `Tests/EditMode/`.

한 파일에 둘이 섞이면 파일을 나눈다(코어 lane 의 "에셋 편집에 면역"이 깨지므로).
선례: `EnemyCatalogAuthoringTests`(카탈로그 검증) — 짝이던 bake 로직 쪽 `EnemyTierBakeTests` 는
(이력 — 옛 ECS 전투, unit 9 에서 제거). 전투 규칙 테스트는 이 판별과 무관하게 `Tests/EditModeCore/` 에 둔다.

## 수치를 단언할 때

밸런스 시트(`UnitStatImportDto`·`DcSheetImportDto` 가 덮는 필드 — health · attackRange ·
atk→`outputs[].magnitude` · attackCooldown · cost · DC 의 percent·magnitude·duration 등)는
**로그인 자동 임포트가 매번 에셋에 덮어쓴다.** 그 값을 리터럴로 못박으면 아무 회귀도
막지 못하면서 밸런스 패스마다 테스트가 빨개진다.

- 쓰지 말 것: `Assert.AreEqual(12f, unit.outputs[0].magnitude)`
- 쓸 것: 부호(`Greater(…, 0f)`) · 배율(`Greater(…, 1f)`) · 상대 비교(보스 killScore > 잡몹) ·
  구조(배열 길이, enum, 배선 non-null)
- 콘텐츠 개수 pin(`AreEqual(44, cards.Count)`)도 같은 이유로 피한다 — "모든 카드가 X 다"를
  직접 단언하면 콘텐츠 추가에 면역이다.
- 예외: 시트가 **안** 덮는 저작 계약(패턴 각도·발수, 애니 이름, 프리팹 배선, 아트 임포트
  설정, 등급 공식 유도값)의 리터럴은 유지한다. 그건 밸런스가 아니라 계약이다.

모범 사례였던 `EditModeAssets/WaveKillBudgetPinTests.cs`(리터럴 pin 이 밸런싱 머지에서 깨진 사고와
상대 단언으로의 전환 근거가 헤더 주석에 있었다)는 옛 웨이브 생성기를 굴리는 테스트라
(이력 — 옛 ECS 전투, unit 9 에서 제거). 교훈은 위 목록 그대로 유효하다.

## 새 테스트를 넣었으면 **총계를 확인한다**

`run_tests` 결과의 `total` 이 **안 움직이면 그 테스트는 안 돈 것**이다. 초록은 「통과」가 아니라
「실행된 것 중 실패가 없다」는 뜻이라, 파일이 컴파일에서 빠지면 **더 초록해 보인다.**

실제 사고(2026-09-05, enemy-detection-range): `EditModeAssets` 에 새 테스트 4건을 넣었는데
`using` 하나가 빠져 어셈블리가 통째로 컴파일에 실패했다. 콘솔의 `error CS0103` 은 이후 로그에
밀려 있었고, **총계가 2740 그대로**인 것이 유일한 신호였다. 고친 뒤 2747.

- 넣은 테스트 수만큼 `total` 이 늘었는지 매번 본다(가장 싼 검산이다).
- 안 늘었으면 **먼저 `read_console` 로 `error CS` 를 찾는다** — 테스트 실패가 아니라 컴파일 실패다.

## 관련 문서

- [`lessons/01-unity-mcp-operation.md`](lessons/01-unity-mcp-operation.md) — `run_tests` MCP 운용 함정
