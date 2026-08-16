# 테스트 실행·작성 절차

> 무엇을 언제 돌리고, 새 테스트를 어디에 둘지. 왜 이 구조인지의 진단과 이력은
> [`docs/spec/test-suite-fast-lane/`](../spec/test-suite-fast-lane/README.md).

## 세 개의 어셈블리

`run_tests` 의 `test_names`/`group_names` 필터는 이 셋업에서 0-match 다.
**동작하는 유일한 입도는 `assembly_names`** — 그래서 어셈블리가 곧 실행 단위다.

| 어셈블리 | 무엇 | 규모 |
|---|---|---|
| `Wassup.Tests.EditMode` | 고속 코어. 순수 계산 + 합성 픽스처 ECS/UI. **실제 프로젝트 에셋을 로드하지 않는다** | ~2,230개 · **~26초** |
| `Wassup.Tests.EditMode.Assets` | 실에셋(SO·맵·덱·카탈로그·프리팹) 저작 검증 | ~160개 · **~5초** |
| `Wassup.Tests.PlayMode` | 씬 부팅 E2E·스모크 (67파일 중 59개가 씬 로드) | ~144개 · **~8분** |

`Wassup.DepthParallax.Tests`(6개)는 모듈 로컬이라 전체 실행 때만 따라온다.

## 언제 무엇을 돌리나

| 상황 | 실행 | 시간 |
|---|---|---|
| 코드 변경 루프 중 | `assembly_names=["Wassup.Tests.EditMode"]` | ~26초 |
| **시트 임포트·에셋·맵·콘텐츠 편집 후** | 위 + `["Wassup.Tests.EditMode.Assets"]` | +~5초 |
| 작업 단위 완료·커밋 전 | `assembly_names` 생략 = EditMode 전체 + 관련 PlayMode 파일 | 분 단위 |
| spec 종료·머지 전 | `mode="PlayMode"` 전체 | ~8분 |

- `include_failed_tests=true` 로 돌리고 `failures_so_far` 를 읽는다. `failures_capped=false` 면
  거기 없는 테스트는 전부 통과다.
- **PlayMode 판정은 에디터 실행으로 한다.** 배치(`-batchmode -nographics`)는 `EntitiesAssetGC`
  NRE 가 그때 돌던 테스트에 임의 귀속돼 실패가 부풀어 보인다. 배치는 EditMode 전용.
- 신규 `.cs` 를 만들었으면 실행 전 `refresh_unity(scope=all)` — `scope=scripts` 로는 .meta 가
  안 생겨 어셈블리에서 통째로 빠진다.

## 빨강을 만났을 때

**EditMode 두 lane 은 기지 실패가 없다(2026-08-16 기준). 빨강 = 회귀다.**

PlayMode 에는 분류된 사전 실패가 남아 있다 — 목록과 각각의 원인·다음 행동은
[`docs/spec/README.md`](../spec/README.md) 의 «PlayMode 사전 실패» 절이 정본이다.
여기에 복제하지 않는다(두 곳에 적으면 갈라진다). 실패를 만나면 먼저 그 절에
있는지 확인하고, 없으면 내 변경이 만든 회귀로 취급한다.

## 새 테스트를 어느 lane 에 두나

**판별은 한 줄이다 — `AssetDatabase.LoadAssetAtPath`/`FindAssets` 로 실제 프로젝트
에셋을 읽는가?** 읽으면 `Tests/EditModeAssets/`, 아니면 `Tests/EditMode/`.

한 파일에 둘이 섞이면 파일을 나눈다(코어 lane 의 "에셋 편집에 면역"이 깨지므로).
선례: `EnemyTierBakeTests`(bake 로직) ↔ `EnemyCatalogAuthoringTests`(카탈로그 검증).

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

모범 사례: `EditModeAssets/WaveKillBudgetPinTests.cs` — 리터럴 pin 이 밸런싱 머지에서
깨진 사고와 상대 단언으로의 전환 근거가 헤더 주석에 남아 있다.

## 관련 문서

- [`lessons/01-unity-mcp-operation.md`](lessons/01-unity-mcp-operation.md) — `run_tests` MCP 운용 함정
- [`docs/spec/test-suite-fast-lane/`](../spec/test-suite-fast-lane/README.md) — 이 구조를 만든 진단과 작업 이력
