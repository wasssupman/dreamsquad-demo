# 8 — 대상 지향 추격 (규칙을 문장 그대로 구현한다)

## 목적

감지의 규칙은 **적의 타입과 무관하다**:

> **내 감지 반경 안에 적이 있고, 그 적을 향해 갈 수 있는 이동 경로가 있으면 그쪽으로 간다.
> 없으면 원래 가던 길로 간다.**

units 1~6 은 이 문장의 **1·3단계만** 구현했다. 2단계(「그 적을 향해 갈 수 있나」)를 **공용
사냥판**에 위임했는데, 그 필드는 다른 질문에 답한다. 이 unit 이 2단계를 문장대로 만든다.

## 확인된 것 (코드에서, 문서 아닌)

| 단계 | 규칙 | 오늘 답하는 주체 | 판정 |
|---|---|---|---|
| 1 | 감지 반경 안에 «때릴 수 있는» 적이 있나 | `DetectionSystem` 직선 스캔 — **층 무관** | ✅ |
| 2 | **그 적을 향해 갈 수 있는 경로**가 있나 | `huntField.dist[idx] != MaxValue` | ❌ |
| 3 | 없으면 원래 길 | `hunting=false` → `SlotFor(Goal, entityLayers)` | ✅ |

2단계가 규칙과 갈리는 지점 둘:

- **「그 적까지」가 아니다.** 공용 필드는 「**아무** 방어유닛의 사격 칸까지 경로 최단」이다.
  실측 **5.0%** 에서 감지 대상과 이동 도착지가 다르다.
- **「내가」가 아니다.** `DefenderFieldSystem:41` 이 `goalField.walkMask`(= `tiles == Walk`,
  **지상 전용**)로 굽는다. 비행 적은 자기 통행 집합으로 질문되지 않는다.

그래서 3단계가 **잘못된 이유로** 발동한다 — 비행 적이 벽 위에 있으면 「그 적에게 갈 경로가
없어서」가 아니라 「**지상** 경로가 없어서」 원래 길로 간다.

⚠ **비행 제외는 독립된 판단이 아니었다** — 위 근사의 부산물이다. 이 unit 뒤에는 비행이
특별 취급 없이 편입된다(층은 `PathFollowState.traversalLayers` 에서 온다).

**규칙의 정본 구현이 이미 리포에 있다** — 어그로 추격판(`AggroStateSystem:214~270`):

```
FillWalkMask(field, enemyLayers, …)          → 「내가」 갈 수 있나
BuildChaseField(walkMask, …, guardianCell)   → 「그 대상」까지
tmpDist[enemyCell] == MaxValue → continue    → 경로 없으면 안 붙인다(좀비 금지)
```

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/DetectionChaseCell.cs` (신규) — 대상 지향 dist 필드
- `Assets/_Project/Scripts/Battle/Combat/DetectionSystem.cs` — 대상 선정에 **경로 질의** 편입
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — 유한 감지는 이 버퍼를 하강
- `Assets/_Project/Scripts/Battle/Effects/FlowFieldRebuildSystem.cs` — 장애물 변경 시 무효화
- `Assets/_Project/Data/Enemies/Enemy_Skimmer.asset` 등 — 비행 저작(unit 6 표 갱신)

## 구현

**유한 반경 = 대상 지향 추격판 / 무제한 = 공용 사냥판.** 의미가 정확히 갈린다 — 무제한은
「아무 방어유닛이나」가 **진짜 질문**이라 공용 필드가 옳고, 유한은 대상이 특정된다.

`DetectionSystem` 의 후보 선정에 2단계를 넣는다:

1. legal + 반경 통과 후보를 **최근접 순**으로 본다(기존 랭킹 그대로).
2. 그 후보까지 **내 통행 층으로** 추격판을 굽는다
   (`FillWalkMask(field, myLayers, …)` → `BuildChaseField(…, targetCell, myAttackRange, …)`).
3. `srcCount == 0` 또는 `dist[myCell] == MaxValue` → **경로 없음** → 다음 후보로.
4. 다 없으면 `hunting = 0` — 원래 가던 길(규칙 3단계).

⚠ **탐색 상한 `MaxPathProbes = 3`.** 후보 전부에 BFS 를 돌리면 최악이 방어유닛 수만큼이다.
감지 획득은 실측 **8판에 632회**(≈0.44회/초)라 3회까지는 어그로와 같은 케이던스다.

⚠ **버퍼 수명은 `AggroChaseCell` 규약 그대로다** — 대상이 정해질 때 ECB 로 부착, 감지가
풀릴 때 제거, **장애물 변경 시 `FlowFieldRebuildSystem` 이 무효화**한다. 그 시스템이 어그로에
대해 이미 하는 일이고, 안 하면 「낡은 경로를 따라 얼어붙는다」가 재발한다.

`MovementSystem` 의 사냥 분기는 갈린다:
- **무제한** → 오늘 그대로(`huntField.flow`/`dist`, 평활화 포함)
- **유한** → `DetectionChaseCell` 을 `FlowRecovery.RecoveryDir` 로 하강(어그로 `Chasing` 과 같은 형태).
  평활화는 안 쓴다 — flow 배열이 없고, 어그로 추격도 같은 이유로 안 쓴다.

## 구현 결과 (2026-09-06)

규칙 2단계가 이제 코드 한 곳에서 답해진다 — `DetectionSystem` 의 후보 탐침이
`FillWalkMask(…, myLayers, …)` → `BuildChaseField(…, targetCell, …)` → 「내 칸이 도달 가능한가」
를 묻고, 통과한 후보만 대상이 된다. 실패하면 다음 후보(최대 3), 다 실패하면 `hunting = 0`.

**비행은 저작 한 줄로 켜졌다** — `skimmer`·`dragon` = 3칸. **코드에 비행 분기는 없다.**
`waypoint_air` 는 계속 0이다(경로 저작이 정체성 — 규칙에 의한 배제).

⚠ **새로 생긴 성질**: 비행은 배치지 위를 지날 수 있어 감지가 걸리면 **배치 구역 위로 파고든다.**
지상 감지 적에는 없던 일이다. Play 육안에서 이것부터 본다.

## 완료 기준

- ✅ compile · **EditMode 2757건 중 실패 2건**(`boomerang`·`bomb_man` 문안 — 시트 소유 선행 실패).
- ✅ EditMode 신규 `DetectionChaseFieldTests`(8) + `DetectionLeakProofTests` 개정(10):
  - **비행 적이 벽 너머를 감지한다** / **지상은 못 간다** — 차이가 `traversalLayers` **한 바이트뿐**.
    **이 짝이 이 unit 의 본체다.**
  - 반경 판정 자체는 층을 안 본다(반경 밖이면 비행도 안 걸린다).
  - 최근접 후보가 도달 불가면 **다음 후보**를 잡는다 / 전부 불가면 `hunting == 0`.
  - 무제한 감지는 버퍼를 **안 붙인다**(보스 무회귀) · 이동도 공용 사냥판을 탄다.
  - 대상 변경 · 장애물 시그니처 변경에 다시 굽는다.
  - 이동 계층: 유한은 추격판(−X)을, **추격판이 없으면 골 흐름장(+X)** 을 탄다(규칙 3단계).
- ⬜ **계측 재실행** — 아직 안 했다. 대상 지향이 되었으므로 「감지 대상 ≠ 이동 도착지」는
  **정의상 0%** 여야 한다. 0이 아니면 하강이 버퍼를 안 보고 있다는 뜻이다.
- ⬜ **Play 육안** — 비행이 배치 구역으로 파고드는 그림이 납득되는지가 첫 항목.
- ⬜ 그 뒤 **unit 5 의 표식이 대상을 가리켜도 참이 된다** — 계약 6 을 그때 다시 쓴다.
