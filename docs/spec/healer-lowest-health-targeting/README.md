# healer-lowest-health-targeting

> 상태: 완료 2026-07-25 (투트랙 리뷰 APPROVE + 사용자 Play 확인)

## 목표

아군을 타겟하는 방어 유닛(힐러·버프러, `DefenderUnitData.targetAllies`)이 **가장 가까운 아군** 대신 **체력 비율(%)이 가장 낮은(= 가장 다친) 아군**을 우선 타겟한다.

## 검증 질문

사거리 내에 체력이 서로 다른 아군 여러 기가 있을 때, 힐러가 HP 비율이 가장 낮은 아군을 먼저 힐하는가? (단일·AoE 모두)

## 공통 원칙 (계약)

- 탐색은 `AttackSystem` 후보 루프의 **랭킹 기준만** 거리 → 체력비율로 바꾼다. 사거리·자기 제외·아군 마스크 필터는 불변.
- 랭킹 기준(결정론): ① `hpRatio` 오름차순 → ② `sqDist` 오름차순(근접 tie-break) → ③ Entity Index → Version. `FrontmostTargeting` 선례를 그대로 미러링한 순수 비교자.
- 체력비율 = `Health.ComputeRatio(value, max)` (기존 공유 함수, `[0,1]` 클램프).
- **풀피 스킵 없음**: 사거리 내 아군이 전부 풀피여도 대상을 고른다(오버힐은 기존 `min(maxHp)` 클램프로 낭비). 홀드파이어 아님. (사용자 결정 2026-07-25 — "그냥 재정렬")
- **적용 게이트**: `mask == Faction.Defender && DefenderUnitTag`. taunt된 적(`TauntAttackGrantSystem` 도 `targetMask = Defender`)은 `DefenderUnitTag` 가 없어 제외 → 최근접 타겟 유지.
- 랭킹은 **유닛 단위**(타겟 1기 선정 후 전 output 적용). 힐/버프 output 을 분리하지 않는다 → 힐+버프 겸용 유닛은 둘 다 최저 HP 아군에게.

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_lowest-health-ranking.md` | 순수 비교자 + AttackSystem 단일/AoE 랭킹 교체 + EditMode 테스트 |

## 후속 후보

- **다친 아군만 힐** (풀피 스킵 + 홀드파이어) 옵션 — 이번 스코프 밖(사용자: 그냥 재정렬). 필요 시 `rankByHealth` 게이트에 "healHasBest && bestRatio < 1" 조건 추가.
- **게이트가 "힐"이 아니라 "아군 타겟(`mask == Defender && DefenderUnitTag`)"에 걸린다** (투트랙 리뷰 M1): 현재 `targetAllies` 유닛은 힐러뿐이라 무해하나, 향후 **아군 대상 버퍼/실드 서포터**를 추가하면 그 유닛도 자동으로 최저 HP 아군 재정렬을 받는다(버퍼는 최근접·최고가치 아군을 원할 수도). 비-힐 ally-targeter 신설 시 게이트를 **Heal output 존재**로 좁히거나 output-kind 별 타겟 분리 검토(현 구조는 유닛당 타겟 1기).
- **힐러 + facing/frontmost 카드 겸비 시 힐 픽이 덮인다** (투트랙 리뷰 L1, 양측 확인): `DeployedFacing`·`FrontmostAttackLock` 은 DefenderTag 게이트라 heal override 를 재차 덮는다. 현재 콘텐츠상 상호배타 아키타입이라 dormant(그 후보들도 `mask == Defender` 필터라 여전히 아군을 가리켜 시뮬 손상은 없음). 이 조합이 생기면 facing/frontmost 를 `!rankByHealth` 로 게이트.
