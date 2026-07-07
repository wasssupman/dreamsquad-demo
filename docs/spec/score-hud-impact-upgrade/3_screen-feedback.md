# 3 — 화면 피드백 (패널 킥 + 마일스톤 플래시)

## 목적

처치 시 점수 패널이 **UI-space로 짧게 킥/셰이크**해 임팩트를 몸으로 느끼게 한다. 큰 마일스톤(예 매 N점)엔 **화면 가장자리 플래시**로 한 번 더 강조. 배틀 카메라는 절대 건드리지 않는다(전투 뷰 안정).

## 변경 대상

- `Assets/_Project/Scripts/UI/ScoreHudView.cs`
- (선택) 풀스크린 플래시 오버레이 `Image`(스코어 캔버스 하위 또는 전용 오버레이)

## 구현

- **패널 킥**: 처치 시(프레임당 flush 1회) `_panel`(또는 값) RectTransform 에 PrimeTween `ShakeLocalPosition` / `PunchLocalRotation`(정확한 API명 — `PunchRotation` 아님) 짧게(오버슈트 후 안착). 강도/시간 직렬화. **배틀 카메라 transform 무변경** — UI-space 한정.
- **마일스톤 플래시(선택)**: 누적 점수가 임계(직렬화, 예 100점 단위) 통과 시 풀스크린 vignette/edge-flash `Image` 를 저알파·단발 펄스(PrimeTween 알파 in/out). 모바일 풀스크린 오버레이 비용 주의 — 단발·짧게·저알파, raycastTarget=false.
- **시간축**: unscaled — PrimeTween shortcut 오버로드의 `useUnscaledTime: true` named arg 전달(shake/punch/color 모두 지원, 모달 중에도 동작).
- **직렬화**: 킥 강도/시간, 마일스톤 간격, 플래시 색/알파/시간 전부 `[SerializeField]`.

## 계약/주의

- **배틀 카메라 불건드림** (데미지 스펙과 동일 원칙). 화면 피드백은 UI-space 패널 + 풀스크린 UGUI 오버레이만.
- 마일스톤은 **표시 전용 트리거** — 점수값/스코어링 로직 불변(킬당 +10 유지).

## 완료 기준

- compile: CS 에러/경고 0.
- Play: 처치 시 패널이 짧게 킥. 마일스톤 통과 시 화면 가장자리 플래시 단발. 배틀뷰 흔들림/오염 없음.
- 값 Play 중 실시간 튜닝 가능.
