# synergy-toggle — 인접 동족 시너지 제어

상태: **은퇴 2026-09-03** — 시너지 기능 전량 제거(커밋 6e9acfbb, 사용자 결정). 이 spec 이 만든
토글로 라이브에서 내내 꺼져 있던(휴면) 기능이라, 토글 유지 대신 기능 자체를 철거했다.
`ModifierOrigin.Synergy`(서수 3)와 stackId 1 은 **번호만 보존** — `SkillModifierOrigin` →
`ModifierOrigin` 숫자 캐스트(EcsSkillContext)가 서수에 매여 있어 지우면 하위 출처가 조용히
밀린다. 재사용 금지. 이하 본문은 은퇴 전 이력이다.

## 목표

인접한 같은 방어 유닛의 공격력 시너지를 기본 비활성으로 전환한다. 효과 타일의 개별 유닛 modifier와 시너지 슬롯을 분리해, 배치 타일 효과의 적용 대상을 명확히 한다.

## 작업 단위

| # | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 토글 + 중립화 | `0_disable_adjacency_synergy.md` | 시너지 생성 차단 및 기존 슬롯 중립화 |

## 계약

- `BattleBridge.enableAdjacencySynergy`의 기본값은 `false`다.
- 비활성일 때 같은 유닛 인접 여부는 `DamageMul`에 영향을 주지 않는다.
- 기존 시너지 슬롯은 `stackId=1`과 동일 merge-key로 `+0`을 enqueue해 중립화한다. Effects 소유 `ModifierStats` 직접 쓰기는 금지한다.
- 효과 타일은 전용 `stackId=2`를 계속 사용하므로 비활성화 대상이 아니다.

## 후속 후보

- 인접 시너지 규칙을 별도 SO로 이관하고 수치·범위를 데이터화.
