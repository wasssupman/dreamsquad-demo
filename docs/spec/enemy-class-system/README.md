# Spec — Enemy Class System

> 상태: 진행 중 (Unit 0 구현)

## 목표

적(enemy) 유닛에 **클래스(archetype)** 개념을 도입한다. 현재 적은 순수 스탯(`AttackUnitData`)만 있고 역할 구분이 없다. defender 가 `DefenderClass(role)` 로 역할을 갖는 것과 대칭으로, 적도 클래스를 부여해 이후 행동 분기(도발/공격-이동 정지/슈터 2타입)의 토대를 만든다.

부수적으로, defender 의 `Bruiser` 명칭을 `Fighter` 로 바꿔 적 클래스 `Bruiser` 와 이름 충돌을 피한다.

## 검증 질문

> "기존 적 6종이 각자의 클래스로 분류되어 인스펙터/런타임 데이터에서 식별 가능한가? defender 의 Bruiser 가 Fighter 로 일관되게 바뀌었는가?"

## 적 클래스 정의

| 클래스 | 행동 (목표 동작 — 후속 단위에서 구현) |
|---|---|
| **Tanker** | 묵묵히 걸어감. 높은 체력. |
| **Runner** | 공격하지 않고 빠른 속도로 목표 지점까지 이동. **도발(taunt)에 걸렸을 때만** 공격. |
| **Bruiser** | 근거리 딜러. 공격 모션 중 대기 → 모션 종료 후 재이동. |
| **Shooter** | 원거리 딜러. 두 타입 보유: ① 공격하면서 이동, ② 공격 모션 중 대기 후 이동. |

## 기존 6종 매핑 (Unit 0)

| 에셋 | 클래스 | 근거 |
|---|---|---|
| Enemy_Runner | Runner | atk0, spd7.2 |
| Enemy_Swift | Runner | atk0, spd4.5 |
| Enemy_Basic | Bruiser | 근접 R2, atk10 |
| Enemy_Tanker | Tanker | HP100, 저속 |
| Enemy_Needler | Shooter | 원거리 R4 투사체 |
| Enemy_Rootcaster | Shooter | 원거리 R6 투사체 |

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | enum + 필드 + 분류 + Fighter 개명 | `0_enemy-class-enum-and-assignment.md` | 클래스 데이터 토대 |

## Feature-wide 계약

- `EnemyClass` enum 은 적 전용. defender 는 기존 `DefenderClass(role)` 를 계속 사용한다.
- enemy 클래스 필드는 `AttackUnitData`(적 전용 SO)에 둔다. defender 의 `DefenderUnitData` 와 분리 유지.
- 이번 단위는 **데이터(authoring) 추가만** 한다. 행동 분기 로직은 후속 단위에서 ECS Movement/Combat 맥락에 구현한다. 클래스 필드는 아직 런타임에서 소비되지 않는다.
- defender `Bruiser`→`Fighter` 는 enum 멤버명 + displayName 만 변경. id(`bruiser`)·에셋 파일명은 세이브 키이므로 유지.

## 후속 후보 (현 스펙 범위 밖, 별도 단위)

1. **Runner 도발 행동** — taunt 상태일 때만 공격하도록 Combat 타겟팅 분기.
2. **Bruiser/Shooter 공격-이동 정지** — 공격 모션 중 이동 멈춤(이미 `movePauseOnAttackSec` 존재 — 클래스 기반으로 정규화 검토).
3. **Shooter 2타입** — kiting(이동사격) vs stop-and-shoot 구분 플래그/서브타입.
4. **WavePlan/Generator 의 클래스 인지** — 웨이브 구성 시 클래스 비율 기반 패턴.
