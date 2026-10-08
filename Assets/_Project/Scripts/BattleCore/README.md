# `Somnia.Battle.BattleCore` — 전투 코어

엔진을 모르는 순수 C# 전투 코어. `noEngineReferences: true` 라서 `UnityEngine` ·
`Unity.Entities` · `Unity.Collections` 는 **컴파일러가 막는다**(`Somnia.Battle.Skills` ·
`Somnia.Battle.UnitAi` 와 같은 결). 참조는 `Unity.Mathematics` · `Somnia.Battle.Skills` ·
`Somnia.Battle.UnitAi` 셋뿐이다.

정본 계약은 `docs/spec/battle-core-rebuild/README.md`(Feature-wide 계약 13) 와
`CLAUDE.md` 의 「새 전투 코어 — 절대 제약」 6항이다. 이 README 는 폴더 지도만 든다.

| 폴더 | 무엇이 사는가 |
|---|---|
| `Match/` | 판의 조립 지점(`BattleMatch`) · 정의표(`MatchDefinition`) · 커맨드/receipt · 이벤트/버스 · 틱 파이프라인 · RNG 계열 · 시드 파생 |
| `World/` | 개체(`Unit`) 와 그 목록(`BattleWorld`) · `SimEntityId` |
| `Owners/` | 담당자. 각자 **상태 + 규칙 + 자기 틱 단계 + 자기 이벤트**를 소유한다 |
| `Trace/` | 관측 기록 포맷 `LegacyTraceV0`(옛 전투와 공용 — 옛 골든을 읽을 수 있어야 한다) |
| `Harness/` | 엔진 없이 판을 돌리는 러너(`CoreHarness`)와 새 코어 골든(`CoreTrace`) |

## 시간

`dt` 는 상수 `1/60` 이다. 코어에는 `Time` 도 `DateTime` 도 `System.Random` 도 없다 —
슬로모·정지는 Unity 층(`BattleDriver`)이 **틱 발행률**로 만든다. 난수는
`Unity.Mathematics.Random` 6계열이고 전부 `MatchSeed.Derive*` 에서 나온다.

## 할당

틱 루프 안에서 힙 할당이 없다. 목록·풀은 `Begin` 에서 잡고, 이벤트는 struct 이고,
LINQ·클로저·문자열 포매팅을 쓰지 않는다. 문자열이 나오는 유일한 자리는 `CoreTrace`
직렬화이고 그것은 틱 밖에서 돈다.
