# unit 6 — `dist`/`flow` 소비처 재검증

## 목적

unit 4 가 `dist` 의 **단위**(×10 스케일)와 `flow` 의 **정의역**(4방향 → 8방향)을 바꿨고, unit 5 가 `dist` 를 **전투 중에 변하게** 만들었다. 이동 밖에서 이 둘을 읽는 4곳이 여전히 옳은지 확인하고, 그 계약을 테스트로 못박는다.

**프로덕션 코드 변경이 없는 것이 정상적인 결과다.** 이 unit 의 산출물은 판정과 회귀 테스트다.

## 검증 결과

| 소비처 | 무엇을 읽나 | 판정 |
|---|---|---|
| `FrontmostTargeting` | `dist` 상대 비교 + `int.MaxValue` 센티넬 | **안전**. 절대값을 쓰지 않고 `<` 비교와 센티넬만 본다 |
| `BlinkMath.TryFindLandingCell` (`HealthThresholdSystem`) | `dist == int.MaxValue` 만 | **안전**. 스케일 무관 |
| `BattleBridge:1807` 경로 추적 | `dist[idx] == 0` (골 도달) + `flow` 추종 | **안전**. 골은 두 체계 모두 0 |
| `AttackSystem:1460` 넉백 방향 | `flow` → `normalizesafe` | **안전**. 대각 flow 도 이미 단위 벡터이고 정규화는 멱등 |

### 의도된 거동 변화 2건 (결함 아님)

1. **`FrontmostTargeting` 의 "앞선 적" 순서가 달라질 수 있다.** 이제 대각 비용(14)이 반영된 **실제 경로 비용** 기준이다. 대각으로 질러갈 수 있는 적이 이전보다 앞선 것으로 평가된다 — 더 정확해진 것이지 회귀가 아니다.
2. **전투 중 순서가 바뀔 수 있다** (unit 5). 장애물 생성/파괴로 필드가 다시 구워지면 그 시점에 우선순위가 재평가된다. 이전엔 판 내내 고정이었다.

### 스폰 예고 라인

`BattleBridge:1807` 의 추적은 `flow` 를 따라가므로 이제 **대각 구간이 그려진다**. 실제 이동과 일치하므로 개선이다. 다만 unit 7(평활화) 이후에는 **필드 경로 ≠ 실제 이동선**이 되므로 육안 재확인 대상으로 남긴다.

## 변경 대상

- 신규: `Assets/_Project/Tests/EditMode/DistContractTests.cs`
- 프로덕션 코드 변경 없음

## 완료 기준

- [ ] `DistContractTests` — 센티넬 보존 / 골 dist 0 / `FrontmostTargeting` 이 스케일 무관하게 순서 유지 / 도달 불가 후보 제외
- [ ] EditMode 실패 0
- [ ] 위 표의 4곳 외에 `dist`/`flow` 를 읽는 곳이 없음을 grep 으로 재확인

---

**완료 기준 확인**: (미확인)
