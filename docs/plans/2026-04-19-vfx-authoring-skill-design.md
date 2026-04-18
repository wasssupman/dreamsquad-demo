# 2026-04-19 VFX Authoring Skill Design

## 배경
- Unity 6 모바일 프로토타입 기준으로 VFX Graph는 배제한다. 타깃은 OpenGL ES 3.0+이고, 초기 범위는 Shuriken ParticleSystem 기반 authoring이다.
- 현재 저장된 VFX 프리팹은 0건이다. 따라서 첫 산출물은 정식 프리팹이 아니라 `_SKELETON` 접미사의 임시 프리팹을 우선 생성한다.
- ECS 연동은 새 추상화를 늘리지 않고 `NativeQueue` 1패턴으로 통일한다. 기준 예시는 `MeteorBurstEvent` 와 `MeteorBurstEventsSingleton` 이다.

## 확정 Q&A
- Q1. 프리팹 authoring 방식은? 하이브리드다. 기본은 `_SKELETON.prefab` 저장까지 하고, 실제 슬롯 연결과 코드 노출은 integration 스킬에서 이어받는다.
- Q2. 셰이더 정책은? 기본 URP 셰이더와 Material 파라미터를 우선한다. 복잡한 dissolve/glow 만 템플릿 복사 후 파라미터 교체를 허용한다.
- Q3. 직접 호출 vs `NativeQueue` 시간 규칙은? 즉시 피드백은 직접 호출, ECS 시뮬레이션 완료 시점과 동기화된 효과는 `NativeQueue` 를 사용한다.
- Q4. 폴백 규칙은? 폴백의 폴백까지 허용하되 반드시 `Debug.LogWarning` 으로 드러낸다.
- Q5. 스킬 트리거 수는? 5개 이내로 제한한다.
- Q6. 카탈로그 위치는? 별도 reference 파일로 분리한다.

## 8개 수정 반영 상세
1. VFX Graph 완전 배제 이유를 모바일 렌더링 제약과 함께 문서 전면에 명시했다.
2. authoring 산출물을 정식 프리팹이 아닌 `_SKELETON.prefab` 으로 강제했다.
3. `_SKELETON` 최소 오버라이드 4개를 `Duration`, `StartColor`, `MaxParticles`, `Loop` 로 고정했다.
4. 모바일 파티클 예산 상한을 일반 50, 임팩트 100, 배경 200으로 수치화했다.
5. Shader Graph JSON 직접 생성을 금지하고 placeholder 파일만 두는 정책으로 바꿨다.
6. 카탈로그를 SKILL 본문에서 분리해 `common-skill-vfx-reference.md` 초안으로 이동했다.
7. authoring 과 integration 의 경계를 명확히 나눠 handoff 기준을 문장으로 못 박았다.
8. 테스트 전략을 Play Mode 시각 검증 중심으로 재정의하고, 자동 테스트는 prefab spawn 가능성만 확인하도록 축소했다.

## 2 스킬 구조 요약
### `unity-vfx-authoring`
- 책임: Shuriken 기반 VFX 오브젝트 제작, Material 생성 또는 복사, `_SKELETON.prefab` 및 `.mat` 저장.
- 경계: 여기서 종료 산출물은 `_SKELETON.prefab + .mat` 저장까지다.
- 비책임: `Renderer` 슬롯 연결, `SerializeField` 추가, `BattleBridge` 배선, ECS drain 코드 수정.

### `unity-vfx-integration`
- 책임: 저장된 skeleton prefab 을 `VfxSpawner`, presenter, bridge 계층에 연결하고 `SerializeField` 노출 및 폴백 로그를 정리한다.
- 경계: authoring 결과를 소비한다. 새 비주얼 설계나 Shader Graph 작성은 하지 않는다.

## 데이터 흐름
1. authoring 스킬이 카탈로그 초안 또는 사용자 지정 요구를 바탕으로 Shuriken 조합을 만든다.
2. authoring 스킬이 Material 을 기본 URP 셰이더 또는 템플릿 복사본으로 준비한다.
3. authoring 스킬이 `_SKELETON.prefab` 과 `.mat` 을 저장한다.
4. handoff 시 effect 이름, 의도, 권장 `MaxParticles`, 루프 여부, 필요한 슬롯 수를 integration 스킬에 전달한다.
5. integration 스킬이 prefab slot 과 `SerializeField` 를 연결한다.
6. 즉시 효과는 직접 호출, ECS 완료 타이밍 효과는 `BattleBridge -> NativeQueue drain -> VfxSpawner` 경로로 배선한다.

## authoring / integration 경계
- authoring 종료 정의: `_SKELETON.prefab + .mat` 가 저장되고 최소 오버라이드 4개가 명시된 상태.
- integration 시작 정의: 저장된 skeleton prefab 을 코드나 인스펙터 슬롯에서 소비해야 하는 순간.
- 금지: authoring 스킬이 `BattleBridge`, `World.DefaultGameObjectInjectionWorld`, `EntityManager`, `SerializeField` 배선을 직접 수정하는 것.

## 에러 처리
- 폴백: 지정한 셰이더가 없으면 `URP/Particles/Unlit -> URP/Unlit -> Sprites/Default` 순으로 내린다. 폴백 진입은 경고 로그를 남긴다.
- Shader Graph 템플릿 실패: JSON 직접 생성으로 복구하지 않는다. placeholder 유지 후 "Unity 에디터에서 수동 생성 필요" 를 명시한다.
- 카탈로그 미스: reference 파일에 없는 효과명은 임의 추가하지 않는다. 사용자 승인 전에는 draft 로만 제안한다.
- prefab slot 미스: integration 단계에서 코드 프리셋 폴백을 허용하되 로그를 남긴다.

## 테스트 전략
- 1순위는 Play Mode 시각 검증이다. 실제 카메라 거리, 모바일 화면 크기, 오버드로우, 루프 정지 시점을 눈으로 확인한다.
- 자동 테스트는 최소화한다. 현재 범위에서는 "prefab 이 스폰 가능한가" 정도만 검증하고, 미감이나 타이밍은 자동화 대상으로 보지 않는다.
- ECS 연동 효과는 `NativeQueue` drain 타이밍이 맞는지만 확인한다.

## TDD RED 단계 생략 사과
- 이번 skill 문서는 TDD RED 단계를 별도 재현하지 않는다.
- 대신 `Phase 8 §12` 에 이미 존재하는 실제 실패 사례와 그 수정 흔적이 RED 증거 역할을 한다고 명시한다.
- 즉, Meteor burst 가 ECS 시점과 어긋났던 문제, ParticleSystem 재생 타이밍 문제, 코드 폴백 필요성 자체가 실패 사례 기록이며 본 설계는 그 위에서 정리된 것이다.

## 구현 메모
- `VfxSpawner` 는 이미 Shuriken-only, prefab 0건, 코드 기반 fallback 구조를 갖고 있다.
- `BattleBridge` 는 MonoBehaviour 와 ECS 사이 유일한 창구라는 규칙을 이미 선언하고 있다.
- integration skill 은 이 규칙을 반복 강화해야 하며, 새 VFX 연결도 동일한 창구를 통과해야 한다.
