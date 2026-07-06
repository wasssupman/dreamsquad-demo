# 프로젝트 교훈 (Lessons)

이 폴더는 작업 중 반복해서 부딪힌 **프로젝트·환경 고유의 함정과 검증 기법**을 모은다. CLAUDE.md/TRD 가 "규칙"이라면 여기는 "겪어보고 알게 된 것" — fresh clone 한 사람(또는 다른 경로로 재클론한 나)이 같은 지뢰를 다시 밟지 않게 한다.

> 출처: 개인 auto-memory 에 쌓였던 지식의 승격본 (spec `docs/spec/workflow-reproducibility/` unit 1). 개인 auto-memory 는 경로별로 분절돼 팀·재클론 간 공유되지 않으므로 레포로 승격했다.

| 파일 | 주제 |
|---|---|
| `01-unity-mcp-operation.md` | Unity Editor 를 MCP 로 구동할 때의 함정 (포커스·reimport·execute_code·run_tests·Play 검증) |
| `02-dev-workflow-git-scene.md` | 테스트 배치·격리 리그·git 샌드박스·병행 세션 커밋·씬 저장/checkout 위생 |
| `03-rendering-assets.md` | Spine 3.8 고정·타일맵 렌더·프랍 authoring·투사체 VFX·카메라 페이즈 |
| `04-sim-design.md` | 전투 시뮬 설계 원칙 (구조적 결정론·시간 제어) |

각 항목은 **증상 → 원인 → 처방** 구조. 커밋 해시·파일:라인은 당시 근거이며 코드가 진실원이다(이동/변경됐을 수 있으니 확인).
