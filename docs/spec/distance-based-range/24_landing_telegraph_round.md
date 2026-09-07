# 24 — 착지 예고를 피해와 같은 자로 (사각 → 원 + 원점 몸)

> 제약 13 미이행분 중 **[높음]**. unit 23 전수 감사가 「**오늘 이미 화면이 거짓말 중**」이라고
> 적어 둔 유일한 항목이다 — 다른 미이행분은 판정끼리의 불일치인데 이것은 **플레이어에게 보이는** 거짓이다.

## 무엇이 틀렸나

궁극기 강습의 **착지 예고**가 `[-N,+N]²` 사각을 통째로 칠했다. 그런데 슬램 피해는
unit 4b 이후 **원**이고, unit 23b 이후로는 **내리찍는 몸**까지 더한다.

| | 예고(종전) | 실제 피해 |
|---|---|---|
| 모양 | 사각 `(2N+1)²` | 원 |
| 원점 항 | 없음 | **그 몸**(`ProjectileSpawnRequest.originBodyRadius`) |
| 반경 2 의 정대각 | **칠해짐** | 2.83 > 2.5 → **안 맞음** ← 거짓 예고 |
| 몸 1.0 인 보스, 3칸 | 안 칠해짐 ← **좁게 가르침** | 3 ≤ 3.5 → 맞음 |

즉 **두 방향으로 동시에 틀렸다**: 모서리는 겁주고(안 맞는데 칠함), 몸이 큰 보스는 안심시킨다
(맞는데 안 칠함). 주석의 「예고 셀 = 피해 셀 계약」은 unit 4b 시점부터 거짓이었다.

## 변경 대상

- `Scripts/Skills/SkillMath.cs` — 진입점 `ReachFromUnitToCell` 신설(5번째).
- `Scripts/Bridge/BattleBridge.cs` — `BuildZoneCells` 가 원 술어를 지나고 **원점 몸을 받는다**.
- `Scripts/Bridge/BattleBridge.UltimateLeap.cs` — 예고가 도약 주체의 `HitRadius` 를 넘긴다.
- 테스트 — `TileAoeTests`(3) · `ReachEntryPointGuardTests`(1).

## 구현

**왜 진입점이 하나 더 필요했나.** 예고는 「이 **칸**이 걸리나」를 묻는데 원점은 **유닛의 몸**이다.
기존 넷 중 그 조합이 없었다 — `ReachFromCell` 은 칸 반폭을 **원점**에 박아 몸을 못 싣고
(그게 unit 23 이 고친 결함), `ReachFromUnit` 은 대상 항을 요구하는데 칸은 자기 몸을 모른다.

```csharp
public static bool ReachFromUnitToCell(float dx, float dz, float rangeTiles, float originBodyRadiusTiles)
    => Reach(dx, dz, rangeTiles, originBodyRadiusTiles, CellHalfWidthTiles);
```

- 원점 항은 **인자(데이터)**, 칸 반폭은 **함수의 성질** — 제약 13 의 「상수를 손으로 넘길 수 없다」 유지.
- 이름이 `Reach` 로 시작하는 것이 **의도**다: 진입점 목록 가드가 그 접두사로 스캔하므로,
  다른 이름을 쓰면 **가드 밖에서 조용히 늘어난다**(그 사유를 코드 주석에 남겼다).
- 스캔 상자를 `ceil(N + 몸) + 1` 로 넓혔다 — 좁으면 실제로 맞는 칸이 안 칠해진다.

⚠ **남는 한계(의식적)**: 예고는 「칸 위의 **1×1** 유닛」에 정확하다. 몸이 더 큰 방어유닛
(2×2 = 1.0 · 배스티온 1.5)은 칠해진 칸 **바깥에서도 맞는다**(차이 = 그 몸 − 0.5). 칸은 거기 설
유닛의 몸을 모르므로, 더 넓히려면 「누가 서 있나」를 봐야 한다 — 표기의 한계로 남긴다.

## 완료 기준

- [x] 예고가 판정과 **같은 본체**를 지난다(`ReachFromUnitToCell`).
- [x] **모서리가 빠진다** — 반경 2 의 정대각(2.83)이 예고에서 제외(`LandingTelegraph_DropsTheSquareCorners`).
- [x] **원점 몸에 반응한다** — 3칸이 몸 0 이면 밖, 몸 1 이면 안(`LandingTelegraph_WidensWithTheSlammingBody`).
- [x] **예고 = 피해**(칸 위 1×1 기준) — 6×6 격자 전수 대조(`LandingTelegraph_MatchesTheDamagePredicate_ForAUnitOnThatCell`).
- [x] **되돌아가지 못하게** — 소스 가드가 사각 열거 부활·원점 몸 누락을 잡는다.
- [x] EditMode 2616 / 실패 0 (2612 + 4 — 새 단언이 실제로 돈 것을 총계로 확인).
- [ ] Play 육안: 보스 강습 예고가 **모서리 없는 원**으로 뜨고, 그 안에 선 유닛만 맞는지.
