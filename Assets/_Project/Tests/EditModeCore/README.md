# `Wassup.Tests.EditMode.Core` — 전투 코어 lane

`Wassup.Runtime` 도 `Unity.Entities` 도 **참조하지 않는다.** 참조는 코어 셋
(`Wassup.BattleCore` · `Wassup.Skills` · `Wassup.UnitAi`) + `Unity.Mathematics` +
테스트 러너뿐이다. 그래서 이 lane 의 초록은 「코어가 혼자 선다」를 증언한다 —
옛 lane(`Wassup.Tests.EditMode`)은 Runtime 을 끌고 있어 그 질문에 답할 수 없다.

## 두 러너에서 같은 소스가 돈다

같은 `.cs` 가 두 군데서 컴파일된다:

- **Unity EditMode** — 정본 lane. 에디터가 열려 있을 때.
- **헤드리스 `dotnet test`** — `Tools/battle-core-rebuild/headless/BattleCore.Tests.csproj`.
  에디터 없이 도는 반복 lane. **골든 대조(`[Category("Golden")]`)는 뺀다** — Unity 의 Mono 는
  float 식을 확장 정밀도로 평가해 .NET 9 와 1 ulp 부터 갈리고(약 300틱), 긴 판은 이벤트
  순서까지 갈린다(`kill_race_3min` 9,887틱). 골든은 Unity 에서 굽고 Unity 에서 대조한다.

그래서 테스트 소스는 **`NUnit.Framework` 와 코어 타입만** 쓴다.
`UnityEngine.Debug` · `Application.dataPath` · `[UnityTest]` 를 쓰면 헤드리스에서 깨진다.
파일 경로가 필요하면 `CoreGoldenStore` 를 쓴다(두 러너의 작업 디렉터리를 흡수한다).

## 완료 기준과의 대응

| 테스트 | 완료 기준 |
|---|---|
| `MatchClockTests` | ① 10,800틱 뒤 `MatchEnded(complete)` 1회 · 이후 틱 no-op |
| `DeterminismTests` | ② 같은 정의표+스케줄 2회 → 트레이스 동일 |
| `SubmitCommandTests` | ③ `Submit` 은 60초 전 거절 · 후 수락 |
| `SimEntityIdTests` | ④ 스폰 순번 · `None` 정렬 배제 |
| `DestroyEventTests` | ⑤ `UnitDestroyed` 없이 사라진 유닛 0 |
| `CoreGoldenTests` | 골든 5종 베이크·검증(unit 1 의 2 + unit 2 의 3) |

## unit 2 (맵·이동) 대응

| 테스트 | 무엇을 증언하나 |
|---|---|
| `MapGridMathTests` · `FlowFieldBuilderTests` · `NavGridAndTrimTests` | 격자 수학 · 흐름장(옥타일·코너컷) · 층별 벽 조립 |
| `AgentCollisionTests` · `PathSmoothingTests` | 충돌 슬라이드·접선 보존 · 평활화와 코너 조준(같은 여유 값) |
| `SeparationTests` · `MovePureMathTests` · `PatrolAreaMathTests` | 밀어내기 · 스폰 흩뿌림 · 경로 진행 · 거점 고르기 · 순찰 |
| `MapRuntimeTests` | 슬롯 없음 = loud · 장애물 시그니처 · 다칸 점유 · 효과 타일 자리 |
| `AttackReachTests` | 도달 산식(제약 13) — 이동의 정지 조건이 공격과 **같은 자**를 쓴다 |
| `MovementRulesTests` · `DetectionRulesTests` | 판을 돌려 묻는 규칙: 골 도달 1회 · 우회 · 정지 관찰 · 감지 네 박자 · 어그로/도발 |
| `MovementRulesTests.군집_통과_검산_1칸_복도_20기_100초` | 단독 통과 ≠ 군집 통과 |
| `DeterminismTests.적_20기_군집도_두_실행이_같다` | 분리 누적이 `SimEntityId` 오름차순으로 닫혔나 |

⑥(`configHash`)은 SO 를 읽어야 해서 `Tests/EditModeAssets/` 에 있다.
