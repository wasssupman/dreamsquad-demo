# projectile-shot-sequence — 개별 탄환 시퀀스

> 상태: units 0~4 완료 · unit 5 구현 중 (2026-07-30)
> 선행: `projectile-emission-pattern` · `defender-directional-volley`

## 목표

공용 emitter에서 `1회 트리거 → N발`을 실행한다. 각 step은 방향과 직전 탄 이후 interval을
갖는다. 샷건너는 10발 spread·4타일 수명으로 바꾸고 머신거너도 이관한다. 투사체 표시 높이는
카메라 평면으로 투영하되 시뮬 원점은 유지한다. 후속 unit 5에서 샷건너를 일반 배치·자동
타겟 방향 발사로 전환하고, 모든 유닛 발사체의 첫 표시점을 실제 무기/몸체 앵커에 맞춘다.

검증 질문: **10발의 순서·방향·거리·출발점이 트리거와 맵 위치에 무관하게 일관적인가?**

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_shot_sequence_contract.md` | step·가변 interval·각도 순수 로직, 기존 패턴 이관 |
| 1 | `1_direction_bound_emitter.md` | 무타겟 Direction binding + trigger-frame 소비 |
| 2 | `2_defender_attack_pattern_cutover.md` | defender trigger seam·2유닛 이관·legacy 제거 |
| 3 | `3_projectile_launch_projection.md` | 공통 표시 높이 투영 + 맵 상·하단 검증 |
| 4 | `4_handoff_summary.md` | Play 결과·인계 |
| 5 | `5_targeted_shotgun_body_launch.md` | 샷건 일반 배치·START 방향 스냅샷 + 공통 몸체 발사점 |

## Feature-wide 계약

1. shot 목록이 N의 source of truth다. 기본 step = `directionT(0..1)` +
   `intervalAfterPreviousSec`; 각도 = `lerp(min,max,directionT)`. randomize 패턴은
   trigger마다 동일 개수의 runtime step을 min/max 각도·interval 범위 안에서 다시 스냅샷한다.
2. runtime은 unmanaged `FixedList128Bytes`(최대 15발). 초과 거절.
3. `PatternSelectionRule.None`은 정상 방향 발사다. base는 배치 방향 유닛은
   `DeployedFacing`, 자동 조준 유닛은 START 타겟 방향 스냅샷이다.
4. START가 성사된 방향 공격은 첫 RESOLVE 전 witness가 사망·이탈해도 고정 방향으로
   emitter trigger를 만들고, 이후 spec·damage·origin·direction·`maxDistance`를 스냅샷한다.
   진행 중 sequence는 타겟/레인/CC와 무관하게 완주하고 host 사망 시 중단한다.
5. 수명은 `attackRange * tileSize`; 마지막 sweep 뒤 개별 소멸한다.
6. `ProjectileData`는 탄 성질, 패턴은 순서·방향·간격을 소유한다.
7. emitter는 두 producer 뒤에 실행한다. 애니·cast·SFX는 trigger당 1회다.
8. 신규 시스템/채널 없이 `PatternSlot`·`EmitterInstance`와 기존 drain을 재사용한다.
9. 표시 높이·arc·drop은 기존 값을 보존해 카메라 평면에 투영한다. 시뮬·히트 원점은 불변이다.
10. 유닛 발사체의 첫 표시 프레임과 trail 원점은 실제 Spine 무기 bone 또는 몸체 앵커다.
    다음 프레임부터 기존 투영 궤적을 따르며, ECS 원점·충돌·수명은 바꾸지 않는다.

## 초기값

- 샷건너: 10 step, `-30°..+30°`, trigger별 결정론적 랜덤 방향,
  탄간 `0.006~0.018초`(첫 탄 즉시)
- 샷건 pellet 표시: 정리된 GA `Shard01`, scale `0.7`; 기존 hit/cast VFX 유지
- 사거리 4. 탄당 12→6으로 풀히트 60 보존. 다음 쿨다운은 마지막 탄 뒤부터
- 샷건너 배치: 일반 D&D 확정. 별도 방향 지정 페이즈 없이 가장 가까운 사거리 내 적을
  START witness로 삼아 그 방향을 spread 기준축으로 고정
- 머신거너: 기존 10발×0.1초·각도 0과 현재 SO의 기본 쿨다운 보존
- 기존 나이트메어 패턴 2종은 1-step으로 선이관한 뒤 legacy schedule 필드를 제거

## 파이프라인 커버리지

| 정거장 | 이번 spec |
|---|---|
| 데이터 | 패턴 shot/각도 + 두 defender SO |
| 스폰 | instance push → emitter carrier N개 → Bridge drain |
| ECS/시뮬 | 기존 버퍼·Move/Hit 재사용, Direction 개통 |
| 이벤트 | N/A — 신규 채널 없음 |
| View | `ProjectileViewPool` 공통 높이 투영 + Spine 무기/몸체 launch anchor |
| 씬 | N/A — 신규 배선 없음 |

## 후속 후보

- 15발 초과 시 fixed-list 폭 재검토
- 일반 target-bound 투사체의 wind-up 중 타깃 소실 정책. 현재 호밍 투사체는 RESOLVE 재판정으로
  발사가 취소될 수 있다. START 타깃 커밋·재타겟·빗나감 중 규칙은 별도 spec에서 결정한다.
