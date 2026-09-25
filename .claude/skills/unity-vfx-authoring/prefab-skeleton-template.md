# VFX Handoff 템플릿 (구 Prefab Skeleton Template)

저작 종료 시 integration 스킬로 넘기는 메모 양식. `_SKELETON` 접미사 규칙은 `SKILL.md` 참조(승인 전 표식 · 승인 후 다음 편집 때 제거).

## 필수 4값
- **Duration**: one-shot 0.1~1.5s / loop 면 주기 + 종료 조건 + 정리 주체
- **StartColor**: 대표 색 1개(또는 gradient 시작색) + 팔레트 이유 한 줄. HDR 로 밝기를 해결하지 않는다
- **MaxParticles**: 일반 50 / 임팩트 100 / 배경·오라 200 상한. 초과 제안은 이유·대체안부터
- **Loop**: true/false. true 면 stop condition, false 면 예상 종료 시점

## handoff 메모
- Effect name / 프리팹 경로:
- Role: oneshot / looping / warning / impact / ground-shape
- 레시피: A 스프라이트 / B 절차 텍스처+쿼드 / C 벤더 사본
- 원점: 공격자 발밑(`attackVfxAtAttacker`) / 대상 / 자리(칸) — 방향: 없음 / `attackVfxFacesTarget`(사건의 공격 축 · 공격자→대상) / 데이터 knob(`attackVfxEulerOffset`)
- Trigger path: 입력 층 직접 호출(프리뷰·조준) / 코어 사건(판정 시점 — 어느 `CoreEventKind`, 받는 `Core*` 뷰 풀)
- Renderer slots needed(SO 필드명):
- Required material(`.mat` 경로) / 절차 텍스처·메시 에셋:
- 오프스크린 시트 PNG 경로(방향 효과는 3방향):
- 라이브 캡처 PNG 경로:
- `ParticleCurveModeConsistencyTests` 결과:
- Notes on fallback(슬롯 비었을 때):

## 모바일 예산 가이드
- 큰 반투명 쿼드 다층 중첩 → overdraw 경고를 남긴다
- Sub Emitter 는 1단계까지 · Texture Sheet Animation 은 벤더 사본에서만
- mesh 파티클은 지면 도형(레시피 B)과 강조용 소수
- trail 은 짧게, width/alpha fade 빨리 · `autodestruct=false`
