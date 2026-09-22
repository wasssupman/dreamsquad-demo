# `Wassup.Tests.EditMode.Core` — 전투 코어 lane

`Wassup.Runtime` 도 `Unity.Entities` 도 **참조하지 않는다.** 참조는 코어 셋
(`Wassup.BattleCore` · `Wassup.Skills` · `Wassup.UnitAi`) + `Unity.Mathematics` +
테스트 러너뿐이다. 그래서 이 lane 의 초록은 「코어가 혼자 선다」를 증언한다 —
옛 lane(`Wassup.Tests.EditMode`)은 Runtime 을 끌고 있어 그 질문에 답할 수 없다.

## 두 러너에서 같은 소스가 돈다

같은 `.cs` 가 두 군데서 컴파일된다:

- **Unity EditMode** — 정본 lane. 에디터가 열려 있을 때.
- **헤드리스 `dotnet test`** — `Tools/battle-core-rebuild/headless/BattleCore.Tests.csproj`.
  에디터 없이 도는 반복 lane.

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
| `CoreGoldenTests` | 골든 2종 베이크·검증 |

⑥(`configHash`)은 SO 를 읽어야 해서 `Tests/EditModeAssets/` 에 있다.
