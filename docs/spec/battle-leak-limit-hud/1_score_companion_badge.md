# 1 — 점수 하단 스트레스 배지

## 목적

점수 HUD의 시각 언어를 유지하면서 패배 조건을 읽기 쉬운 보조 배지로 추가한다. 점수의 보상
연출과 스트레스의 위험 피드백이 경쟁하지 않도록 크기와 모션 위계를 분리한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/ScoreHudView.cs`
- `Assets/_Project/Scenes/BattleScene.unity` — 기본값과 다른 튜닝이 필요할 때만

## 구현

- 기존 360px 점수 플레이트 바로 아래에 같은 폭의 64px 네이비/골드 플레이트를 붙인다.
- 좌측 골드 pill은 `스트레스`, 우측 큰 숫자는 `{current} / {limit}`를 표시한다.
- 폰트와 material은 점수의 Kanit SDF 설정을 그대로 사용한다.
- 잔여 허용치가 경고 임계 이하이면 주황, 치명 임계 이하이면 적색으로 숫자를 바꾼다.
- 현재값 증가 또는 최대값 감소 시 숫자에 짧은 white flash와 punch를 1회 재생한다.
- 임계값·색·크기·간격·모션 수치는 모두 ScoreHudView 직렬화 필드다.
- 점수의 roll/burst/shine/milestone 로직과 누수 배지의 피드백은 서로 호출하지 않는다.

## 완료 기준

- 점수 아래에서 `스트레스 0 / 10`이 점수보다 작은 위계로 선명하게 읽힌다.
- 정상/경고/치명 상태 색상이 실제 잔여 허용치와 일치한다.
- 누수 증가와 limit 감소에만 1회 반응하며 상시 pulse하지 않는다.
- Battle 이탈과 OnDisable에서 트윈과 transform이 정상 복원된다.
- Unity 컴파일/Console 오류 0.
