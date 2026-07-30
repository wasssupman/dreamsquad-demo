# projectile-shot-sequence — 개별 탄환 시퀀스

> 상태: 진행 중 — unit 0~2 완료, unit 3 대기 (2026-07-30)
> 선행: `projectile-emission-pattern` · `defender-directional-volley`

## 목표

공용 emitter에서 `1회 트리거 → N발`을 실행한다. 각 step은 방향과 직전 탄 이후 interval을
갖는다. 샷건너는 10발 spread·4타일 수명으로 바꾸고 머신거너도 이관한다. 투사체 표시 높이는
카메라 평면으로 투영하되 시뮬 원점은 유지한다.

검증 질문: **10발의 순서·방향·거리·출발점이 트리거와 맵 위치에 무관하게 일관적인가?**

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_shot_sequence_contract.md` | step·가변 interval·각도 순수 로직, 기존 패턴 이관 |
| 1 | `1_direction_bound_emitter.md` | 무타겟 Direction binding + trigger-frame 소비 |
| 2 | `2_defender_attack_pattern_cutover.md` | defender trigger seam·2유닛 이관·legacy 제거 |
| 3 | `3_projectile_launch_projection.md` | 공통 표시 높이 투영 + 맵 상·하단 검증 |
| 4 | `4_handoff_summary.md` | Play 결과·인계 |

## Feature-wide 계약

1. shot 목록이 N의 source of truth다. step = `directionT(0..1)` + `intervalAfterPreviousSec`;
   각도 = `lerp(min,max,directionT)`.
2. runtime은 unmanaged `FixedList128Bytes`(최대 15발). 초과 거절.
3. `PatternSelectionRule.None`은 정상 방향 발사다. base=`DeployedFacing`, 후보 0이어도 발사한다.
4. START가 성사된 facing 방향 공격은 첫 RESOLVE 전 witness가 사망·이탈해도 고정 facing으로
   emitter trigger를 만들고, 이후 spec·damage·origin·direction·`maxDistance`를 스냅샷한다.
   레인/CC와 무관하게 완주하고 host 사망 시 중단한다. boss damage는 유지한다.
5. 수명은 `attackRange * tileSize`; 마지막 sweep 뒤 개별 소멸한다.
6. `ProjectileData`는 탄 성질, 패턴은 순서·방향·간격을 소유한다.
7. emitter는 두 producer 뒤에 실행한다. 애니·cast·SFX는 trigger당 1회다.
8. 신규 시스템/채널 없이 `PatternSlot`·`EmitterInstance`와 기존 drain을 재사용한다.
9. 표시 높이·arc·drop은 기존 값을 보존해 카메라 평면에 투영한다. 시뮬·히트 원점은 불변이다.

## 초기값

- 샷건너: 10 step, `-30°..+30°`, 불규칙 중심 밀집 `5-3-2` 클러스터, 총 전개 0.05초
- 사거리 4. 탄당 12→6으로 풀히트 60 보존. 다음 쿨다운은 마지막 탄 뒤부터
- 머신거너: 기존 10발×0.1초·각도 0과 현재 SO의 기본 쿨다운 보존
- 기존 나이트메어 패턴 2종은 1-step으로 선이관한 뒤 legacy schedule 필드를 제거

## 파이프라인 커버리지

| 정거장 | 이번 spec |
|---|---|
| 데이터 | 패턴 shot/각도 + 두 defender SO |
| 스폰 | instance push → emitter carrier N개 → Bridge drain |
| ECS/시뮬 | 기존 버퍼·Move/Hit 재사용, Direction 개통 |
| 이벤트 | N/A — 신규 채널 없음 |
| View | `ProjectileViewPool` 공통 높이 투영 |
| 씬 | N/A — 신규 배선 없음 |

## 후속 후보

- 15발 초과 시 fixed-list 폭 재검토
