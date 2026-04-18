---
name: unity-vfx-authoring
description: Use when authoring a new Shuriken VFX prefab skeleton, preparing VFX materials, drafting mobile-safe particle specs, or handing off a `_SKELETON` prefab to integration.
---
# Unity VFX Authoring
## Overview
This skill authors mobile-safe VFX assets for the Unity 6 prototype without using VFX Graph. Output stays in Shuriken, Material, and `_SKELETON.prefab` territory only.

## The Iron Law
"_SKELETON` 접미사 없는 prefab 생성 금지. 사용자 폴리시 완료 전까지 정식 prefab화 불가."

## When to Use
- 새 VFX 를 만들어야 하지만 현재 프로젝트에 prefab 자산이 없을 때
- 기존 코드 폴백 VFX 를 `_SKELETON.prefab` 으로 승격할 준비를 할 때
- 모바일 예산에 맞춰 Shuriken 파라미터를 정리할 때
- Material 또는 Shader Graph 템플릿 복사 전략이 필요할 때
- integration 스킬로 넘길 handoff 패키지를 정리할 때

## Workflow
1. 효과 목적을 정의한다. 즉시 피드백인지, 지속 루프인지, ECS 완료 시점에만 보여야 하는지 먼저 적는다.
2. `common-skill-vfx-reference.md` 에서 가장 가까운 카탈로그 초안을 찾는다. 없으면 임의 추가하지 말고 draft 제안으로 멈춘다.
3. Shuriken 으로 1차 구현한다. 기본은 sprite billboards, 최소 모듈, 최소 sub emitter 다.
4. Material 은 먼저 `Universal Render Pipeline/Lit`, `Universal Render Pipeline/Unlit`, `Universal Render Pipeline/Particles/Unlit` 중 하나로 해결한다.
5. 복잡한 dissolve 또는 glow 가 꼭 필요할 때만 `templates/` 의 placeholder 경로를 사용한다. JSON 직접 생성은 피한다.
6. `_SKELETON.prefab` 과 `.mat` 을 저장한다.
7. 아래 4개 필수 오버라이드를 문서화한다.
   - `Duration`
   - `StartColor`
   - `MaxParticles`
   - `Loop`
8. integration handoff 를 작성한다. 필요한 renderer slot 수, loop 여부, 예상 trigger 경로를 적는다.

## Decision Tree
If visual can be expressed with particles, color, size, alpha, and simple UV animation:
Use Shuriken + URP material only.

If visual needs dissolve or glow accent but still remains surface/simple:
Copy a template Shader Graph asset in Unity Editor and only swap exposed parameters.

If visual requires complex graph logic, many sub emitters, or heavy mesh VFX:
Stop and re-scope. This skill does not approve VFX Graph or JSON-authored Shader Graph.

## Red Flags
- `_SKELETON` 접미사 없이 prefab 을 저장하려는 경우
- `MaxParticles` 를 모바일 예산 상한 없이 올리려는 경우
- overdraw 경고를 무시한 채 큰 soft sprite 를 다층으로 겹치는 경우
- Texture Sheet Animation 을 기본값처럼 사용하는 경우
- Shader Graph JSON 을 텍스트로 직접 생성하거나 수정하려는 경우
- 사용자 승인 없이 카탈로그 항목을 추가하려는 경우

## Rationalization Table
| Topic | Default | Why |
| --- | --- | --- |
| Runtime path | Shuriken | Mobile OpenGL ES 3.0+ 대응 범위가 가장 안전함 |
| Prefab status | `_SKELETON.prefab` | 승인 전 정식 에셋과 분리해야 함 |
| Material policy | Built-in URP shaders first | 80%를 파라미터만으로 처리 가능 |
| Complex shader need | Template copy only | GUID 손상과 JSON 오염을 피함 |
| Budget | General 50 / Impact 100 / Background 200 | 모바일 입자 상한을 빠르게 판단 가능 |
| Catalog edits | Approval-gated | 레퍼런스 드리프트 방지 |

## Common Mistakes
- 4개 필수 오버라이드를 적지 않고 prefab 만 저장한다.
- 임팩트 효과인데 `Loop=true` 를 유지한다.
- `MaxParticles` 를 높여서 밀도 문제를 감춘다.
- Sub Emitter 로 해결하려다 관리 복잡도와 overdraw 를 키운다.
- integration 단계 책임인 renderer slot 연결을 여기서 하려 든다.

## Quick Reference Checklist
- `_SKELETON.prefab` 인가
- `.mat` 이 같이 저장되었는가
- 필수 오버라이드 4개가 적혔는가
- 모바일 예산 상한을 지켰는가
- Overdraw 경고를 남겼는가
- Sub Emitter 를 최소화했는가
- Texture Sheet Animation 을 피했는가
- 카탈로그 항목이 승인 대기 상태인가
- "사용자 승인 없이 카탈로그에 항목 추가 금지" 규칙을 지켰는가

## Handoff
종료 산출물 = `_SKELETON.prefab + .mat` 저장까지. Renderer 슬롯 연결 및 `SerializeField` 추가는 integration 스킬 호출.

Skeleton 최소 오버라이드:
- `Duration`: one-shot 이면 0.1~1.5s 범위, loop 면 반복 주기 명시
- `StartColor`: 대표 색 1개 또는 gradient 시작색
- `MaxParticles`: 일반 50 / 임팩트 100 / 배경 200 상한
- `Loop`: true/false 와 정지 조건

모바일 파티클 예산:
- 일반 효과 상한 `MaxParticles=50`
- 임팩트 효과 상한 `MaxParticles=100`
- 배경 또는 aura 상한 `MaxParticles=200`
- 큰 투명 쿼드 다중 중첩이면 overdraw 경고를 남긴다
- Sub Emitter 는 꼭 필요한 1단계만 허용한다
- Texture Sheet Animation 은 기본 선택지로 쓰지 않는다

Shader Graph 정책:
- 기본은 `URP/Lit`, `URP/Unlit`, `URP/Particles/Unlit`
- dissolve/glow 같은 복잡 표현만 `templates/` 파일을 Unity 에디터에서 복사해 사용
- 복사 후에는 노출 프로퍼티만 교체한다
- JSON 직접 생성 또는 수동 편집은 기피한다
