# Spec — 데미지 숫자 팝업 (Damage Number Popup)

> 상태: **완료 2026-06-05**
> 검증 질문: *"방어유닛이 적을 히트할 때, 적 머리 위에 스타일리쉬하고 강력한 데미지 텍스트가 뜨는가?"*

## 상위 목표

방어 유닛이 적(`AttackUnitTag`) 유닛에게 데미지를 입힐 때, **적 머리 위에 데미지 수치 텍스트**를 띄운다. 텍스트는 스타일리쉬한 전용 폰트를 쓰고, **데미지 크기에 비례한 강조 연출**(크기·색·펀치 스케일)을 갖는다.

기존 `HealAppliedEvent` 파이프라인(ECS enqueue → NativeQueue 싱글턴 → `BattleBridge` 드레인 → MonoBehaviour 스포너)을 그대로 미러링한다. 새로운 ECS 맥락은 만들지 않는다 — enqueue 는 **Units 맥락**(`DamageApplicationSystem`, 실제 HP 차감 지점)에서만 일어난다.

## 결정 사항 (사용자 확정 2026-06-05)

- **표시 범위**: 적 유닛만. 디펜더 피격은 숫자 없음. (enqueue 시 `AttackUnitTag` 보유 + `totalDamage > 0` 으로 필터)
- **연출 강도**: 크기 비례 강조. 데미지가 클수록 폰트 크기↑ + 색 변화(흰→노랑→주황→빨강) + 큰 히트는 펀치 스케일/흔들림 강화. 크리티컬 개념은 없음(값만으로 임팩트 차등).
- **폰트**: 무료 스타일리쉬 디스플레이 폰트(Bangers 계열 굵은 폰트)를 TMP SDF 로 임포트, 아웃라인+그라데이션 머티리얼 구성.

## 작업 단위 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | ECS 토대 | `0_event-channel.md` | `DamageNumberEvent` 구조체 + `DamageNumberEventsSingleton` (NativeQueue 채널 #15) + BattleBridge 생성/해제 wiring |
| 1 | ECS enqueue | `1_enqueue-in-damage-system.md` | `DamageApplicationSystem` 에서 적(`AttackUnitTag`) 피격 시 데미지 enqueue |
| 2 | 폰트·머티리얼 | `2_font-and-material.md` | 무료 디스플레이 폰트 → TMP SDF 에셋 + 아웃라인/그라데이션 머티리얼 |
| 3 | 뷰·풀 | `3_view-and-pool.md` | `DamageNumberView` (월드 TMP, 펀치 스케일·드리프트·페이드·빌보드, 값→크기/색) + 간단 풀 + 팝업 프리팹 |
| 4 | 스포너·브리지 | `4_spawner-and-bridge.md` | `DamageNumberSpawner` MonoBehaviour + `BattleBridge.DrainDamageNumberEvents()` + 씬 wiring + Play 검증 |
| 5 | 인계 | `5_handoff_summary.md` | 구현 종료 요약 (커밋 후) |

## Feature-wide 계약

- **이벤트 구조체**: `DamageNumberEvent { float3 position; float amount; }`. `position` 은 enqueue 시점 적 `LocalTransform.Position`(발치). 머리 오프셋은 스포너의 직렬화 필드로 적용(하드코딩 금지). 적만 enqueue 하므로 타입 필드 불필요.
- **채널 소유**: NativeQueue 싱글턴은 `BattleBridge` 가 생성·소유·해제한다(다른 13→14개와 동일 패턴, `HealAppliedEventsSingleton` 미러). 채널 수 **14 → 15**. `CLAUDE.md` 의 채널 목록도 unit 0 에서 갱신한다.
- **enqueue 위치**: `DamageApplicationSystem`(Units 맥락) 한 곳만. `AttackUnitTag` 보유 엔티티 + `totalDamage > 0` 일 때만. 맥락 경계 위반 없음(쓰기 = NativeQueue enqueue, Units 가 소유 싱글턴에 쓰는 것).
- **드레인**: `BattleBridge.Update()` 드레인 시퀀스에 `DrainDamageNumberEvents()` 추가(`DrainHealAppliedEvents` 인근). 스포너 null 이면 큐 Clear 후 return(힐 패턴과 동일).
- **연출 파라미터는 전부 에셋/직렬화**: 수명, 드리프트 거리/속도, 펀치 스케일, 머리 Y오프셋, 값→크기 곡선, 값→색 그라데이션 임계값은 `DamageNumberSpawner`/`DamageNumberView` 의 `[SerializeField]` 또는 TMP 머티리얼에서 나온다. 코드 상수 하드코딩 금지(TRD §5).
- **빌보드**: 팝업은 매 LateUpdate 카메라를 정면으로 바라본다(전투 카메라 yaw=0, pitch 고정). 유닛 빌보드 틸트와 별개.
- **풀링**: 팝업은 GC 스파이크 방지를 위해 풀에서 재사용. 적 다수 동시 피격 시 spam 가능 → 풀 + 짧은 수명(~0.8s).
- **HitFlashTag 와 공존**: 피격 스케일 플래시(0.15s)는 ECS 측 별개 연출. 데미지 숫자는 이를 대체하지 않고 함께 뜬다.

## 비범위 (후속 후보)

- 디펜더 피격 데미지 숫자(색 구분). — 사용자가 "적만" 으로 확정.
- 힐 숫자 텍스트 표시(현재 힐은 파티클만). — 같은 `DamageNumberView` 재사용 후보지만 별도 spec.
- 크리티컬/약점 시스템 연동(현재 전투에 크리티컬 개념 없음).
- 누적 데미지 합산 표시(DoT 틱 묶기) — 우선 틱마다 단발 표시로 시작.
- 데미지 타입별 아이콘/색(물리/마법 등) — 현재 데미지 타입 미구분.
