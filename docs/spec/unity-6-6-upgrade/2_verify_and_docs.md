# 2 — 검증 · 문서 현행화 · 종료

## 목적

6.6 에서 테스트 lane 전부가 4.7 때와 같은 결과인지 확인하고(특히 골든), 버전·경계·MCP 를 말하는 문서를 고친다.

## 검증 — **생략(사용자 결정 2026-10-07 「필요없음」)**

아래 표는 계획이었다. 실제 완료 기준은 「사용자 에디터 컴파일 에러 0」(단위 1 완료 기준)이고, 골든 드리프트는 백로그로 넘겼다. 절차는 다음 EditMode 실행 때를 위해 남긴다.

```
U66="/c/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe"
"$U66" -batchmode -projectPath <repo> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>
"$U66" -batchmode -projectPath <repo> -runTests -testPlatform PlayMode -assemblyNames Wassup.Tests.PlayMode.Core -testResults <xml> -logFile <log>
```

| lane | 기대 | 비교 기준(4.7, demo-diet 단위 0+1 커밋 `df85f0151`) |
|---|---|---|
| EditMode 전체 | 2,240(기존 테스트의 스캔 범위만 넓어짐, 건수 동일) · 실패 = 선행 3 + CRLF 9 | 새 빨강 0 |
| **`CoreGoldenTests`**(EditMode.Core 안) | **전부 초록** — 빨강이면 6.6 Mono 의 float 평가가 달라진 것. **멈추고 묻는다**(재굽기 = 결정론 결정) | 골든 파일 변경 0 |
| PlayMode.Core | 94 · 실패 7(배치 환경 드래그 미리보기 — HEAD 와 같은 집합) | 실패 id 집합 동일 |
| 헤드리스 | Core 1,038/0/4 · Check 빌드 통과 — 6.6 `ScriptAssemblies` + `UnityEngineDir` | 동일 |
| 사용자 에디터 | 콘솔 에러 0 · `BattleCoreScene` Play 한 판(입력 없이 드라이버 저작으로 시작 — demo-diet 단위 0 의 「에디터 Play 육안 확인은 아직」을 여기서 닫는다) | — |

## 문서

| 파일 | 고칠 것 |
|---|---|
| `CLAUDE.md` 「스택」 | `6000.4.3f1 · URP 17.4` → `6000.6.3f1 · URP 17.6` |
| `CLAUDE.md` 「코어 경계」 | 「참조는 `Unity.Mathematics`·`Wassup.Skills`·`Wassup.UnitAi` 뿐(`noEngineReferences`)」 → 「엔진 모듈 중 **`UnityEngine.MathematicsModule` 만**(6.6 부터 Mathematics 가 엔진 모듈이라 `noEngineReferences` 는 끈다) — 경계는 `CoreArchitectureTests` 소스 스캔 + 헤드리스 lane(CoreModule 불참조)이 지킨다」 |
| `CLAUDE.md` 「Unity 함정」 MCP 줄 · 「더 읽을 곳」 | unity-mcp 패키지 제거 사실. lessons/01 은 이력 |
| `docs/reference/battle-core-architecture.md` `:41,251,429` | 같은 문장 셋(다이어그램 라벨 · 층 표 · §8-3 「경계는 컴파일러가 지킨다」 → 「테스트 + 헤드리스가 지킨다」) |
| `.claude/agents/core-reviewer.md:36` | 「asmdef must be `noEngineReferences: true`」 → 소스 스캔 기준 |
| `docs/reference/lessons/04-sim-design.md:59` | 「`noEngineReferences: true` 는 유지해도 된다」 → 6.6 이후 주석 |
| `docs/reference/lessons/01-unity-mcp-operation.md` 머리 | 「unity-mcp 패키지는 6.6 전환(2026-10)에서 제거 — 에디터 공유·포커스·Reload 모달 교훈은 여전히 유효」 |
| `docs/reference/test-procedure.md` 헤드리스 절 | `-p:UnityEngineDir` 관례 · `Retire.Check` 는 옛 lane |
| `Assets/_Project/Tests/EditModeCore/README.md:4` | 참조 목록에 MathematicsModule |
| `docs/map-editor-reference-for-somnia.md:21` | 버전 |
| `docs/spec/demo-diet/README.md` | 「순서」 행 정정(6.6 먼저, 2026-10-07) · 공통 원칙 「검증 환경」(wt47 → 6.6 배치) |
| `docs/spec/README.md` | 「진행 중 spec」 → 완료 목록으로 · 후속 백로그에 경고 22건 |
| 메모리 `unity-6-6-upgrade-findings` · `clone-remote-and-unity-setup` | 적용 완료 · 4.7 관례 폐기 · 검증 워크트리 교체 |

## 워크트리

- `wt47`(4.7): `git worktree remove` — 4.7 lane 은 끝.
- `wt66`(6.6): 프로브 패치를 버리고(`reset --hard`) 이 spec 의 커밋을 checkout 해 **검증 워크트리**로 쓴다(사용자 에디터가 메인을 열고 있을 때 배치용 — 단 동시 실행은 메모리 때문에 피한다).

## 완료 기준

- [~] lane 표 — 생략(사용자 결정). 에디터 컴파일 0 · 헤드리스 빌드 2종만. 골든 → 백로그
- [x] 문서 표 전부 반영(+ `test-procedure.md` 「배치」·Smart App Control 절) · 잔존 언급은 이력 표기뿐
- [x] 워크트리: `wt47` 제거 · `wt66` 는 `d73c0d86e`(demo-diet 끝나면 제거)
- [x] 커밋(경로 지정) · README 상태 줄에 해시
- [ ] push 는 사용자 승인 후(demo-diet `df85f0151` 과 함께)

확인 2026-10-07.
