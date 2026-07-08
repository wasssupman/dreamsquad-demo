# 드림캐쳐 개별유닛 트리거 메커닉 — 설계 메모 (2026-07-08)

> 얇은 브레인스토밍 결과물. 구현 계약과 작업 단위는 `docs/spec/dreamcatcher-unit-trigger/` 가 source of truth.

## 목표

기존 드림캐쳐(축 매칭 스탯% 패시브)와 다른 새 카드 부류를 연다:

- **개별 유닛 바인딩** — 스쿼드/축이 아니라 유닛 1명에게 부착. 유닛 사망 시 카드는 회수되어 재사용 가능(회수 상세 로직/UX 는 별도 spec).
- **이벤트 트리거형 효과** — 첫 사례: "공격 5회마다 공격 대상에게 20 데미지 투사체" (가칭 콕콕 바늘).

## 핵심 결정

1. **2계층 구조 (아키텍처 비의존 정의 / 아키텍처별 해석).**
   - 정의 계층(`Scripts/Data/Dreamcatcher/`): `DcTriggerSpec {kind, period}` × `DcPayloadSpec {kind, magnitude, 에셋 참조}` 순수 데이터. ECS 타입 참조 금지. 기획 설명 템플릿도 이 데이터에서 수치를 끼운다.
   - 해석 계층: BattleBridge 가 부착 시점에 unmanaged `DcTriggerSlot` 으로 베이크(기존 `DefenderUnitData`→컴포넌트 베이크 패턴과 동형), Combat 이 실행. 아키텍처가 바뀌면 번역자만 다시 쓴다.
2. **소유권 = Combat (A안).** 트리거 소스(공격 resolve, 향후 킬/피격)와 페이로드 출구(투사체 요청, StatModifier 큐)가 대부분 Combat 이므로, 카운터를 `AttackSystem` RESOLVE 옆에 두면 첫 카드 기준 신규 채널 0개 + 타겟 엔티티를 공짜로 알며 + 카운트→소모 순서가 결정론적. Effects 소유안(B)은 신규 채널 2개 + 프레임 지연 비용으로 배제. MonoBehaviour 쪽 카운팅(C)은 프레젠테이션 채널로 시뮬을 구동해 결정론이 깨져 배제.
3. **페이로드 = 기존 프리미티브 4종의 조합 어휘.** 투사체 요청 / StatModifier / StackModifier / Hazard 요청의 파라미터화로 표현하고 새 데미지 경로를 만들지 않는다. 예: 콕콕 바늘=Homing+SingleSplash 투사체(damage=20, 스킬 투사체의 state.damage 선례), 물결=TileAoe, "다음 공격 2배"=charge 소모형 StatModifier(신규 범용 프리미티브, 후속).
4. **카운팅 계약.** RESOLVE(타격 판정) 시점 1회 = 1카운트(멀티 output 무관). 효과 인스턴스마다 독립 카운터(instanceId 발급, 기존 stackId 카운터 패턴).

## 스코프

이번 spec 은 `AttackN` 트리거 × `ProjectileToTarget` 페이로드 1조합을 end-to-end 로 실증한다(카드 1종 + Play 검증). SelfTileAoe / NextAttackModifier / 추가 트리거 / 바인딩·회수 UX 는 후속 후보.

## 포인터

- 구현 spec: `docs/spec/dreamcatcher-unit-trigger/`
- 기존 드림캐쳐 파이프라인: `docs/spec/ingame-dreamcatcher/`
- 투사체 라이프사이클: `docs/spec/projectile-trajectory-payload/`
