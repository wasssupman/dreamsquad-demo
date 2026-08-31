# 2 — 선재 결함 (전환과 무관하게 이미 열려 있는 것)

## 목적

전환이 **이 위를 밟는다.** unit 1 이 수렴시키면서 둘은 자동으로 닫히므로 여기서는 **회귀
단언으로 못박고**, 남은 하나(결정론)를 직접 고친다. 셋 다 오늘 이미 있는 결함이지 이 spec 이
만든 것이 아니다 — 그래서 자를 바꾸기 전에 닫는다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs:604` — 최근접 픽 tie-break
- `Assets/_Project/Tests/EditMode/` — 회귀 단언 2건

## 구현

**(a) 최근접 픽에 `simId` 폴백이 없다 — 직접 고친다.**

```csharp
// AttackSystem.cs:604 (현행)
if (d2 < bestSq) { bestSq = d2; bestTarget = targetEntities[i]; … }
```

형제 두 곳(`NearestTargeting.RanksBefore`, `HazardCastSystem.cs:103`)은 동거리에서 `simId` 가
작은 쪽을 고르는데 **여기만 없다.** ⚠ 이건 단순 선재 결함이 아니라 **`SimEntityId` 축 전환이 남긴
마지막 구멍**이다 — `battle-sim-extraction` M0 unit 1 이 형제(`HazardCastSystem`)의 tie-break 를
신설하면서 이 한 곳을 지나쳤고, 그 spec 의 **동률 예외 목록에도 없다**(= parity 상 exact 로
요구되는 지점인데 실제로는 청크 순서 의존이다). 스냅샷 부분 재시뮬에서 갈린다. 후보 배열이 `ToEntityArray` = 청크 순서라 「재현은 되지만
순서 비의존은 아니다」. unit 4 가 게이트를 바꾸면 후보 구성이 달라져 이 위를 밟는다.
→ 형제와 같은 규칙(`sqDist` → `simId`)으로 통일.

**(b) 다중타격 2번째 이후 대상만 자가 다르다 — unit 1 이 닫음, 여기선 단언.**

`AttackSystem.cs:1527` 이 셀 단독이라, 연속 이동 아군이 다중타격을 가지면 **첫 대상은 2단
게이트, 나머지는 셀 단독**으로 판정된다. 「내가 때릴 수 있는 적」의 정의가 발마다 다르다.
→ 「같은 발의 모든 대상이 같은 술어를 통과한다」를 EditMode 로 고정.

**(c) 어그로 `Standoff` 무기한 동결 — unit 1 이 닫음, 여기선 단언.**

`EnemyAiStateSystem.cs:93` 이 셀 단독이고 `Standoff` 는 **자기 이동 0 + 탈출 로직 없음**이다.
히트 구동 어그로는 `remainingTime = 0` = 무기한이라 타이머로도 안 풀린다. 지금 안 터지는
유일한 이유는 가디언이 전부 타일 고정이라 2차 게이트가 우연히 비활성이기 때문 —
**순찰병 SO 에 `aggroCapacity` 를 0 초과로 저작하면 오늘 당장 재현된다.**
→ 그 저작을 픽스처로 만든 PlayMode 단언 1개(unit 0 의 카나리아 재사용).

## 완료 기준

- [ ] (a) 후보 배열 순서를 셔플해도 같은 대상이 뽑힌다 — EditMode.
- [ ] (b)(c) 단언이 **unit 1 이전 코드에서는 red**, 이후 초록. red 를 먼저 본다(증상 재현 원칙).
- [ ] 골든 7건 무변화 — (a)는 동거리에서만 갈리므로 기존 트레이스에 동거리가 없으면 무변화가 정상.
