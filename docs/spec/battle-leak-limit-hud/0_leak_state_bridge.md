# 0 — 누수 상태 전달 계약

## 목적

HUD가 별도 계산이나 하드코딩 없이 BattleBridge의 실제 패배 판정값을 받도록 단일 전달 경로를
만든다. 몽마의 계약으로 런타임 허용치가 감소하는 경우도 같은 프레임에 반영한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/UI/ScoreHudView.cs`

## 구현

- `EffectiveLeakLimit()`가 `deck.defeatGoalReachedCount - _leakAllowancePenalty`를 반환한다.
- `RemainingLeakAllowance()`, 로그, 패배 비교도 같은 helper를 사용해 산술 중복을 제거한다.
- BattleBridge는 기존 직렬화 참조 `scoreHud`에 `SetLeakStatus(current, limit)`를 호출한다.
- 전달 시점은 매치 상태 초기화, 몽마의 계약 허용치 지불 성공, GoalReached 누적 직후다.
- ScoreHudView는 값을 저장하고 UI가 아직 생성되지 않았거나 숨겨진 상태에서도 안전하게 받는다.
- AttackDeck SO, GoalReached 이벤트 구조, 패배 조건의 의미는 변경하지 않는다.

## 완료 기준

- 기본 WaveA 시작 상태가 `0 / 10` 스냅샷으로 전달된다.
- 누수 증가와 허용치 지불 성공 직후 스냅샷이 갱신된다.
- 패배 비교와 HUD 분모가 항상 같은 helper를 사용한다.
- Unity 컴파일 오류 0.
