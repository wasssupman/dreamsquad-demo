# Prefab Skeleton Template

`_SKELETON.prefab` 생성 시 최소 가이드:

## 저장 규칙
- 파일명은 반드시 `<EffectName>_SKELETON.prefab`
- 같은 폴더에 `<EffectName>.mat` 또는 목적별 material 파일을 같이 저장
- 정식 prefab 승격은 사용자 승인 이후에만 수행

## 필수 오버라이드 4개
### 1. Duration
- one-shot: 대체로 `0.1s ~ 1.5s`
- loop: 반복 주기와 종료 조건을 함께 적기
- destroy 타이머가 필요한 경우 handoff 에 명시

### 2. StartColor
- 대표 색 1개 또는 gradient 시작색
- palette 이유를 한 줄로 남기기
- 너무 넓은 HDR 값으로 밝기를 해결하지 않기

### 3. MaxParticles
- 일반 효과 상한: `50`
- 임팩트 효과 상한: `100`
- 배경 또는 aura 상한: `200`
- 상한 초과 제안 시 이유와 대체안부터 적기

### 4. Loop
- `true` 또는 `false`
- `true` 면 stop condition 과 cleanup 주체 명시
- `false` 면 예상 종료 시점 명시

## 모바일 예산 가이드
- soft particle 중첩이 크면 overdraw 경고를 남긴다
- Sub Emitter 는 최소화한다
- Texture Sheet Animation 은 가능하면 피한다
- mesh particle 은 강조용 소수만 사용한다
- trail 은 짧게 유지하고 width/alpha fade 를 빨리 준다

## handoff 메모 템플릿
- Effect name:
- Role: oneshot / looping / warning / impact
- Trigger path: direct call / NativeQueue
- Renderer slots needed:
- Required material:
- Notes on fallback:
