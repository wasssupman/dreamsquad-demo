# knockup-fighter-defender — 말파이트 (광역 넉업 파이터)

> 상태: 초안 (사용자 승인 대기, 2026-07-29)

## 목표

공격 시 히트 대상 전원에게 20 피해 + **공중 띄우기(넉업)** 를 거는 근접 파이터
**말파이트**(id `malphite`)를 추가한다. 4종 동시 개발분 중 신규 메커니즘 절반(넉업)을 소유한다.

- **심 모델 = 기존 Stun 재사용**: 넉업의 시뮬 실체는 짧은 Stun(행동+이동 정지, action-lock 계약
  그대로). 신규 CC kind 를 만들지 않는다 — 공중에 뜬 것과 스턴은 시뮬 관점에서 동일하다.
- **"공중" 은 뷰 전용**: 평면 tilemap 보드라 sim-Y ≠ 화면 높이(BoardSpace 계약). 띄우기 호핑은
  유닛 view 의 수직 오프셋 애니메이션으로만 표현한다.
- 기존 `sleepOnHitSec`(주 타겟 1체 전용)과 달리 넉업은 **전 히트 대상** 적용 — 히트 CC 스코프의
  첫 확장이며 이 spec 의 유일한 시뮬 코드다.
- 배치 스킬 = **착지 충격**: 배치 순간 1타일 내 적 전원 넉업.

검증 질문: **"전 히트 대상 CC 확장이 기존 히트 CC 계약(주 타겟 1체)을 깨지 않고 공존하며, 넉업이 '띄운다'로 읽히는가?"**

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | `0_knockup_on_hit_all_targets.md` | `knockupOnHitSec` 필드 → 전 히트 대상 Stun enqueue |
| 1 | code | `1_onplace_stun_nearby.md` | 배치 스킬: `OnPlaceEffectType.StunNearby` 변종 (심만) |
| 2 | asset | `2_unit_asset_and_catalog.md` | 유닛 SO + 카탈로그 — 이 시점부터 Play 루프 성립(스턴만) |
| 3 | code | `3_knockup_launch_visual.md` | 뷰 수직 호핑 연출 — 에셋 위에서 iterate (공격+배치 양쪽 연결) |
| 4 | docs | `4_handoff_summary.md` | 인계 요약 (종료 시) |

## Feature-wide 계약

1. **히트 CC 경로 = `DefenderCcData`** (sleep-fighter 계약 1 승계). `AttackOutputKind` 에 CC 를
   신설하지 않는다.
2. **기존 `sleepOnHitSec` 의 주 타겟 1체 계약은 불변.** `knockupOnHitSec` 은 별도 필드로 전 히트
   대상 적용 — 두 필드의 스코프 차이를 필드 주석에 명시한다(다음 사람이 통합하려다 투머치토커를
   깨는 것 방지).
3. **Stun 계약 불변**: kind 별 병합(remainingTime=max)·action-lock 은 combat-action-lock 계약
   그대로. wake-on-hit 은 Sleep 전용이라 넉업엔 해당 없음(맞아도 안 깨어남 — 사양).
4. 넉업 연출은 **메커닉(유닛 데이터) 소유** — 히트 이벤트에서 attacker 의 knockup 보유 여부로
   구동하고, CcEffect(Stun) 쪽에 kind 분기를 넣지 않는다(frost_arrow 등 일반 스턴과 연출 비간섭).
5. 연출 수직 오프셋은 view 로컬 — 정렬/그림자/오버헤드 UI 와의 간섭은 unit 1 완료 기준에서 확인.
6. 전 수치는 SO — 하드코딩 금지.

## 초기값 (전부 튜닝 대상, SO 소유)

Fighter · Epic · 코스트 5 · HP 600 · 사거리 1 · 쿨다운 2.0s · attackTargetCount 3 ·
outputs `[Damage 20]` · knockupOnHitSec 0.8 (업타임 40% 광역 CC) · 착지 충격: 반경 1 · 0.8s.

## 파이프라인 커버리지 (Defender 아키타입 대조)

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Defender_Malphite.asset` 신규 + `DefenderUnitData.knockupOnHitSec` 필드 신설 + **DefenderCatalog 등록**(unit 3) |
| 스폰 진입점 | 변경 없음 — `DefenderCcData` 베이크에 1필드 추가(unit 0) |
| ECS 컴포넌트 (Units) | 표준 세트 그대로. 능력 컴포넌트 전부 N/A(비활성) |
| 시뮬 시스템 | `AttackSystem` RESOLVE 히트 루프에 knockup enqueue 분기(unit 0). CcApply/action-lock 기존 그대로 |
| 이벤트 큐 | 신규 채널 0 — `EnemyCcEventsSingleton`(Stun)·기존 히트 이벤트 재사용 |
| View/Pool | 기존 SpineUnitPool + 넉업 호핑(unit 1 — 적 view 수직 오프셋 one-shot) |
| 체력 표시 | 변경 없음 — 오버헤드 UI 와 호핑 오프셋 간섭만 확인(unit 1) |
| 씬 wiring | **N/A 예상 — 신규 SerializeField 없음**(기존 drain/view 경로 확장). unit 1 에서 확정 |

## 후속 후보

- **넉업 중 추가 피해 취약(에어본 콤보)** [M] · 떠 있는 대상 피격 배율 — 시너지 축이라 별도 결정.
- **보스 넉업 면역** [S] · BossTag 게이트 1줄 — 수면 면역 백로그와 같은 결.
- **낙하 착지 데미지/슬로우** [S] · 착지 순간 미니 효과.
- **전용 아트 패스** [S] · portrait/파츠/착지 충격 VFX (placeholder 교체, guid 유지).
