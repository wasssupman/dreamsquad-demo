---
name: unity-vfx-integration
description: Use when wiring `_SKELETON` VFX prefabs into MonoBehaviour presenters, assigning renderer slots, adding SerializeFields, or bridging timed ECS VFX through BattleBridge and NativeQueue.
---
# Unity VFX Integration
## Overview
This skill consumes authored `_SKELETON.prefab` assets and connects them to the existing Unity 6 gameplay stack. It does not design the visual itself; it wires ownership, timing, prefab slots, and fallbacks.

## The Iron Law
"MonoBehaviour에서 EntityManager/World.DefaultGameObjectInjectionWorld/SystemAPI 직접 호출 금지. BattleBridge만 ECS 통신 창구."

## When to Use
- `_SKELETON.prefab` 을 `VfxSpawner` 나 presenter 계층에 연결할 때
- 인스펙터 `SerializeField` 슬롯을 추가하거나 정리할 때
- 즉시 발동 효과와 ECS 완료 시점 효과를 분기할 때
- 코드 프리셋 폴백 로그를 표준화할 때
- `BattleBridge -> NativeQueue -> VfxSpawner` 경로를 새 효과에 재사용할 때

## Workflow
1. authoring handoff 를 읽는다. effect 이름, loop 여부, 권장 particle 수, 필요한 material 을 확인한다.
2. 경로를 결정한다. 즉시 발동인지, ECS 시뮬레이션 후 발동인지 먼저 고른다.
3. prefab slot 을 `SerializeField` 로 노출한다. 빈 슬롯일 때의 폴백 경로를 같이 구현한다.
4. ECS 타이밍이 필요하면 새 이벤트 payload 와 singleton, enqueue 지점, `BattleBridge` drain 을 같은 패턴으로 만든다.
5. MonoBehaviour 가 ECS API 를 직접 만지지 않는지 확인한다.
6. Play Mode 에서 빈 슬롯 로그, 폴백 로그, 발동 타이밍을 검증한다.

## Decision Tree
```text
+-------------------------------+------------------------------+
| 질문                          | 경로                         |
+-------------------------------+------------------------------+
| 클릭/배치 즉시 보여야 하나?   | direct call                  |
| ECS 판정 완료 프레임이 기준인가?| NativeQueue via BattleBridge |
| 한 효과가 둘 다 필요한가?     | 두 경로 병행 허용            |
+-------------------------------+------------------------------+
```

혼합 소유권 주석:
"한 효과가 즉시 발동 + ECS 지속 둘 다 필요하면 두 경로 병행 사용 가능. 예: Meteor 경고링 직접 호출 + 폭발 NativeQueue"

## Red Flags
- `BattleBridge` 밖에서 `EntityManager` 나 `SystemAPI` 를 직접 쓰는 경우
- prefab slot 이 비었는데도 경고 없이 코드 폴백으로 넘어가는 경우
- OnValidate 요약 로그 없이 빈 슬롯이 누락되는 경우
- authoring 단계 책임인 shader/material 설계를 여기서 다시 하는 경우
- ECS 이벤트 payload 에 managed reference 나 scene object 를 넣는 경우

## Rationalization Table
| Topic | Default | Why |
| --- | --- | --- |
| ECS gateway | `BattleBridge` only | ownership 을 한 곳에 고정 |
| Immediate VFX | direct call | 입력 피드백 지연을 피함 |
| Sim-timed VFX | `NativeQueue` | 판정 프레임과 동기화 |
| Missing prefab | code fallback + warning | 개발 중 단절 방지 |
| Validation | Play Mode visual check | 타이밍과 연결을 가장 빨리 확인 가능 |

## Common Mistakes
- `VfxSpawner` 가 직접 ECS singleton 을 읽게 만든다.
- enqueue 는 했지만 `BattleBridge` drain 을 추가하지 않는다.
- prefab slot 이 비었을 때 조용히 리턴해서 효과가 사라진다.
- 즉시 경고링과 판정 시점 폭발을 한 경로로 억지 통합한다.
- `OnValidate` 에서 빈 슬롯 개수 요약을 남기지 않는다.

## Quick Reference Checklist
- `_SKELETON.prefab` handoff 를 받았는가
- `SerializeField` 슬롯이 있는가
- prefab slot 비어 있으면 아래 로그를 남기는가
- `Debug.LogWarning($"[VfxSpawner] {effectName} prefab slot empty, using code fallback")`
- Play Mode 진입 시 `OnValidate` 또는 동등한 검증에서 빈 슬롯 개수 요약을 남기는가
- ECS 경로면 payload struct + singleton + enqueue + drain 이 모두 있는가
- MonoBehaviour 가 ECS API 를 직접 호출하지 않는가

## Handoff
authoring/integration 경계:
"종료 산출물 = _SKELETON.prefab + .mat 저장까지. Renderer 슬롯 연결 및 SerializeField 추가는 integration 스킬 호출"

폴백 경고 로그 규칙:
- `VfxSpawner` 가 prefab slot 비어 코드 프리셋 폴백 진입 시: `Debug.LogWarning($"[VfxSpawner] {effectName} prefab slot empty, using code fallback")`
- Play Mode 진입 시 `OnValidate` 에서 빈 슬롯 개수 요약 로그 남기기

NativeQueue 적용 기준:
- 즉시 보여야 하는 배치/경고/시전 예고는 direct call
- ECS 결과가 확정된 피해/처치/폭발은 `NativeQueue`
- 이벤트 payload 는 위치, 반경, 색 인덱스 같은 값 타입 위주로 유지
