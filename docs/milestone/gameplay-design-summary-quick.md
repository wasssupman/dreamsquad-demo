# Defense Tournament — 빠른 기획 현황 요약

> **역사 보존 배너 — 2026-05-08 snapshot.** 이 빠른 요약의 draft·flow·수치는 현재 구현과 다를 수 있다. 2026-07-29 기준 실제 흐름은 [`production-transition/demo-baseline.md`](../production-transition/demo-baseline.md)를 우선한다.
>
> 기준일: 2026-05-08  
> 상세본: `docs/milestone/gameplay-design-summary.md`  
> 용도: 상세 기획서 작성 전 현재 구현/미결정 빠른 확인

## 한 줄 정체성

같은 공격 패턴을 두고 플레이어가 10장 중 3장을 버려 7종 방어 유닛을 구성하고, 2종 스킬과 실시간 코스트로 3분 방어 결과를 겨루는 비동기 토너먼트 디펜스.

## 한 판 흐름

```text
Draft
  - 맵 프리빌드 표시
  - 웨이브 패턴 공개
  - 10장 중 3장 폐기 = 7종 픽
  - 스킬 6종 중 2종 랜덤 픽 표시
Placement
  - 30초 또는 START BATTLE
  - startingCost 10으로 유닛 배치
Battle
  - 180초 타이머
  - 웨이브 자동/Next Wave 강제 호출
  - 코스트 자동 충전, 추가 배치, 스킬 사용
Result
  - VICTORY / DEFEAT
  - 더미 리더보드
  - RESTART / REDRAFT
```

## 현재 구현된 핵심 시스템

| 영역 | 현재 상태 |
|---|---|
| 맵 | seed 기반 절차 생성, 4타일(Walk/Place/Env/Deco), Flow Field, forest theme props |
| 웨이브 | seed 기반 10~15 웨이브, 웨이브당 2종/10~15마리, Wave Strip + Next Wave |
| 드래프트 | Basic 3 + Meta 2 + Ego 1 + Collection 4 = 10장, 3장 폐기 |
| 등급 | Common/Rare/Epic/Ego, 카드 테두리 + 슬롯 배너 + 등급별 VFX |
| 스킬 | 6종 풀에서 2종 랜덤, SkillBar, Portal 2-tap, cooldown+cost |
| 배치 | D&D, Place 타일 제한, ghost/hover, 코스트 차감, On-Place 발동 |
| 코스트 | starting 10, max 15, regen 1/sec, placement 30sec |
| 전투 | ECS 전투, 통합 AttackSystem, projectile, heal, modifier, CC |
| Hazard | Poison/Ice/Fire zone, Rock blocking hazard, hazard caster defender |
| 결과 | ResultScreen, 더미 Bot 리더보드, Restart/Redraft |
| 로그 | JSON battle log, map/wave/draft/skill/placement/result 기록 |

## 콘텐츠 현황

| 콘텐츠 | 수량/목록 |
|---|---|
| 방어 유닛 | 15종: Scout, Guardian, Cannon, Ranger, Piercer, Marksman, Archer, Bastion, Healer, Sniper, FireCaster, IceCaster, PoisonCaster, BlockingCaster, Bruiser |
| 공격 유닛 | 6종: Basic, Swift, Tanker, Rootcaster, Needler, Runner |
| 스킬 | 6종: SlowField, PowerSurge, RapidFire, Tornado, Meteor, Portal |
| Hazard | Zone 6종(Poison/Ice/Fire 1x1+3x3), Blocking 2종(Rock 1x1+3x3) |
| 투사체 | 6종: Arrow, Bolt, CannonBall, Sniper Crimson, Enemy RitualBolt, Enemy Needle |
| Spine | BellKnight(Tanker), player-main 일부 defender |

## 승패와 점수

| 항목 | 현재 구현 |
|---|---|
| Defeat | 적 5마리 Goal 도달 시 패배 (`AttackDeck.defeatGoalReachedCount`) |
| Victory | 모든 웨이브 처리 + 남은 적 0 |
| Timeout | 180초 생존 시 VICTORY_TIMEOUT, UI는 VICTORY |
| Score | 임시 공식: `max(0, elapsedBattleSeconds * 10 - enemiesReachedGoal * 50)` |

## 상세 기획 전 결정 필요

1. 스코어 공식: 처치 점수, 시간 보너스, 남은 적 체력 반영 여부.
2. 패배 조건 튜닝: Goal 도달 5회가 적절한지.
3. 코스트/스탯/스킬/Hazard 수치 밸런스.
4. 시너지 규칙 상세: 인접 조건과 표시 방식.
5. 패배 귀인 UX: 결정적 순간, 배치 요약, 실패 원인 표시.
6. 인벤토리 UX: 드래프트 7종 카드와 배치 현황 표시.
7. 특수 유닛 처치 코스트 보너스: PRD 원안 유지 여부.

## 다음 우선순위 후보

| 우선 | 후보 | 이유 |
|---|---|---|
| 1 | 스코어 공식 확정 | 반복 플레이/리더보드 가설의 핵심 |
| 2 | 결과 화면 요약 강화 | 패배 귀인 가능성 검증에 필요 |
| 3 | 배치/스킬 로그 기반 리캡 | 상세 기획서의 피드백 루프 정의에 필요 |
| 4 | 밸런스 패스 | 코스트 긴장감과 유닛 역할 체감 확보 |
