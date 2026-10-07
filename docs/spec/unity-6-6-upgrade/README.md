# unity-6-6-upgrade — Unity 6000.6.3f1 전환

상태: **완료 2026-10-07** — 커밋 `08dbd96f9`(단위 0) · `dc607b059`(단위 1) · `d73c0d86e`(포워더 제거) · 문서 커밋은 「결과」 참조. 배치 검증은 사용자 결정으로 생략.

## 목표

프로젝트 핀을 `6000.4.3f1` → **`6000.6.3f1`** 로 올리고, 6.6 에서 Demo 가 **컴파일 에러 0** 으로 열리게 한다. 사용자 결정(2026-10-02): *「6000.6.3f1 이 정식이 될 것」*. somnia-client 도 6000.6.3f1 이라 이식 전에 같은 버전이어야 한다.

순서 변경(2026-10-07): 원래 `demo-diet` 뒤로 미뤘으나 **이 spec 을 먼저** 한다 — 사용자 에디터(6.6)가 컴파일이 안 돼 Play·콘솔·씬 확인이 전부 막혀 있고, 다이어트 단위 2(에셋·씬 정리)는 에디터가 살아 있어야 눈으로 검증된다. demo-diet 단위 0+1 이 아웃게임을 지우면서 6.6 에러 자리도 줄어 남은 수정이 작다.

## 원인과 수정 (프로브 워크트리 `wt66` 에서 2회 검증 — 적용 후 컴파일·셰이더·Burst·임포트 에러 0)

| # | 원인 | 지금 남은 자리 | 수정 | 단위 |
|---|---|---|---|---|
| 1 | `Unity.Mathematics` 가 6.6 부터 **엔진 모듈**(`UnityEngine.MathematicsModule`)이다. 패키지 1.4.0 은 `TypeForwardedTo` 78줄짜리 껍데기. `noEngineReferences: true` 인 코어 asmdef 3개가 못 봐 CS1069 | 34건 · asmdef 3 | 플래그 `false` + 포워더 참조(asmdef 8)·패키지 줄 제거(코드 변경 0) | 0 |
| 2 | ugui 2.6 TMP `enableWordWrapping` 이 obsolete-as-error | 3파일 4줄 | `textWrappingMode = TextWrappingModes.NoWrap` | 0 |
| 3 | `Object.GetInstanceID()` error | **0건**(`CoreMatchEntryTests` 가 demo-diet 단위 0 에서 삭제됨) | 없음 | — |
| 4 | `com.coplaydev.unity-mcp` 9.6.6 이 6.6 API(`GetInstanceID`/`EntityId`)에서 깨짐 | manifest 1줄 | 제거. MCP 는 이식 뒤 somnia 의 `com.unity.ai.assistant` 를 쓴다 | 1 |
| 5 | 6.6 업그레이드 중 PrimeTween tgz 항목이 manifest 에서 사라짐(Package Manager 조작 추정) | manifest 1줄 | `"com.kyrylokuzyk.primetween": "file:../Assets/Plugins/PrimeTween/internal/com.kyrylokuzyk.primetween.tgz"` 복구 | 1 |
| 6 | 헤드리스 lane 의 `Library/ScriptAssemblies/Unity.Mathematics.dll` 이 6 KB 포워더가 됨 → `BattleCore.csproj` CS1069 | csproj 3 + 스크립트 1 | `UnityEngine.MathematicsModule.dll` 참조 추가 | 0 |

## 사용자 결정

| 날짜 | 결정 |
|---|---|
| 2026-10-02 | 정식 버전 = **6000.6.3f1** |
| 2026-10-07 | **이 spec 을 `demo-diet` 단위 2 보다 먼저** 한다 |
| 2026-10-07 | **Mathematics 는 엔진 API 그대로 쓴다**(*「그냥 엔진 API 쓰면 되는 것 아니냐」*). 자체 포팅 없음. 코어 asmdef 3개의 `noEngineReferences` 를 끈다 |

### 「코어 경계」 보장의 이동

지금까지 「코어는 엔진을 모른다」를 **컴파일러**(`noEngineReferences`)가 지켰다. 6.6 에서는 Mathematics 가 엔진 모듈이라 그 플래그와 양립하지 않는다. 보장은 두 곳으로 옮긴다:

1. **소스 스캔 테스트** — `CoreArchitectureTests.코어에는_엔진_참조가_없다` 를 `Skills/`·`UnitAi/` 까지 넓히고 `UnityEditor` 도 금지한다. 지금은 `BattleCore/` 만 본다.
2. **헤드리스 lane** — `BattleCore.csproj` 는 `UnityEngine.MathematicsModule.dll` **하나만** 참조한다(`CoreModule` 없음). `GameObject`·`Debug`·`Time` 을 쓰면 여기서 빌드가 깨진다. 이 모듈은 순수 managed 수학 라이브러리라 .NET 9 에서 그대로 로드된다 — 서버 이식 때도 이 DLL 하나만 따라간다.

CLAUDE.md 「코어 경계」·`battle-core-architecture.md` §8-3·`core-reviewer.md` 체크리스트의 문장을 이에 맞춰 고친다(단위 2).

## 작업 단위

| # | 문서 | 목적 | 어느 에디터에서 초록인가 |
|---|---|---|---|
| 0 | `0_code.md` | asmdef 3 · TMP 4줄 · 헤드리스 csproj · 소스 스캔 테스트 확장 | 4.7 과 6.6 둘 다(`textWrappingMode` 는 ugui 2.0 에도 있다) |
| 1 | `1_packages_and_settings.md` | manifest(unity-mcp 제거 · PrimeTween 복구 · 6.6 버전 16개) · lock · `ProjectVersion` · ProjectSettings 4 + 신규 2 | 6.6 |
| 2 | `2_verify_and_docs.md` | 6.6 배치 EditMode 전체 · PlayMode.Core · **골든 드리프트 확인** · 헤드리스 · 문서 현행화 · 워크트리 정리 · 종료 | 6.6 |

단위 0 과 1 은 작업 트리에 **같이** 적용하고(0 만 적용하면 6.6 에서 PrimeTween 이 없어 안 뜬다 — 지금 작업 트리의 manifest 는 이미 에디터가 6.6 으로 다시 쓴 상태), 커밋은 **둘로** 나눈다(코드 diff 와 lock 파일 churn 을 섞지 않는다).

## 공통 원칙

- **코드 변경 최소, 그러나 6.6 에서 처음 만든 모양으로.** 버전 전환이지 리팩터가 아니다 — 단 호환 껍데기(포워더 패키지·그 asmdef 참조)를 남기는 땜빵은 하지 않는다(사용자 2026-10-07). 우리 코드(`Assets/_Project`)의 6.6 obsolete 경고는 0 — 남은 경고는 벤더(GabrielAguiar `PrefabStage.prefabAssetPath` 등)뿐이라 손대지 않는다.
- **사용자 에디터가 곧 1차 검증기다.** 메인 리포에 적용하면 열린 6.6 에디터가 바로 재컴파일한다. 콘솔(또는 `Logs/Editor.log` 의 `error CS`)이 0 이 되는 것을 본다. 적용 전 `Temp/UnityLockfile` 로 에디터 상태를 보고, Play 중이면 기다린다.
- **배치 검증은 에디터를 닫고.** 6.6 에디터를 연 채 다른 Unity 인스턴스가 Library 를 임포트하면 메모리(32 GB)가 모자라 죽는다. 배치는 사용자 에디터를 닫은 뒤 메인 리포에서, 또는 `wt66`(6.6 Library 보유)에서 돌린다.
- **골든은 정본이다.** 6.6 의 Mono 가 float 평가를 바꿨다면 `CoreGoldenTests` 가 빨개진다. 그러면 **멈추고 묻는다** — 재굽기는 결정론 제약(CLAUDE.md)의 사용자 결정이다.
- 커밋은 경로 지정. Layer Lab/Spine 아틀라스·png meta 재임포트 churn 은 **이 spec 에서도 스테이징하지 않는다**(demo-diet 단위 2 가 Layer Lab 을 지운다).

## 이 spec 밖 (후속)

- 경고 22건(`FindObjectOfType` → `FindFirstObjectByType` 등, 벤더 포함).
- `Retire.Check.csproj` 의 6000.4.3f1 mac 경로 — 옛 ECS 은퇴 lane 이라 6.6 에서 돌리지 않으면 그대로.
- MCP: Demo 에 `com.unity.ai.assistant` 를 넣지 않는다. 이식 뒤 somnia 설정을 쓴다. `lessons/01-unity-mcp-operation.md` 는 옛 unity-mcp 기준이라 머리에 이력 표시만.
- 이 클론의 CRLF 빨강 9(`.gitattributes eol=lf`)는 6.6 과 무관 — `demo-diet` 단위 4 또는 별도.

## 결과 (2026-10-07)

- **에디터(6.6) 컴파일 에러 0.** 경고는 벤더 2종(GabrielAguiar `PrefabStage.prefabAssetPath` · 벤더 데모 CS0414)뿐. 포워더 `Unity.Mathematics.dll` 은 `ScriptAssemblies` 에서 사라졌다.
- **추가 정리(사용자 「땜빵 금지」)**: asmdef 8개의 `Unity.Mathematics` 참조와 manifest 의 `com.unity.mathematics` 줄을 지웠다(`d73c0d86e`). 6.6 에서 처음 만든 프로젝트의 모양이다.
- **헤드리스**: Core 빌드 · Check 빌드 통과(6.6 어셈블리). Core 테스트 1,038/0/4 는 포워더 제거 **전**(14:19) 통과 — 그 뒤 이 머신의 Windows Smart App Control 이 새로 링크된 테스트 DLL 로드를 차단해(`0x800711C7`) 테스트 lane 은 못 쓴다(`test-procedure.md`).
- **생략(사용자 결정 「필요없음 — 에러 없이 6.6 전환과 다이어트에만 집중」)**: 배치 EditMode 전체 · PlayMode.Core · 골든. **골든 드리프트는 미확인** → `docs/spec/README.md` 백로그 「6.6 골든 드리프트 확인」.
- **워크트리**: `wt47`(4.7) 제거. `wt66`(6.6) 은 demo-diet 가 끝나면 제거.
- 다음: `demo-diet` 단위 2(에셋) — 6.6 에디터 위에서.
