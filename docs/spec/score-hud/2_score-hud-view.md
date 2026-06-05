# 2 — 점수 HUD 뷰 (ScoreHudView)

## 목적

전투 화면 상단 중앙 타이머 아래에 라이브 점수를 표시한다. 적 처치마다 강하게 증가: **카운트업 롤 + 펀치 스케일 + 색 플래시**. Bangers SDF + 아웃라인 머티리얼(데미지 spec 재사용). 표시 전용.

## 변경 대상 (신규)

- `Assets/_Project/Scripts/UI/ScoreHudView.cs`

## 구현

`TimerDisplay` 패턴을 따른다(런타임 빌드 UGUI, sortingOrder 6, ScreenSpaceOverlay, 1920×1080).

- **레이아웃(개정)**: 패널 anchor 상단 중앙(0.5,1), `anchoredPosition=(0, topOffset=-8)` → 화면 최상단 여백(게임영역 바깥). 캡션 "SCORE"(작게) + 값(크게). **폰트=Anton SDF**(데미지=Bangers 와 구분), 값 83 / 캡션 29(1.3배).
- **직렬화 튜닝**: `pointsPerKill(10)`, `rollLerp(12)`, `punchScale(1.45)`, `punchDuration(0.2)`, `flashColor(gold)`, `baseColor(white)`, `topOffset`, `valueFontSize(64)`, `captionFontSize(22)`, `scoreFont`, `scoreMaterial`.
- **OnEnemyKilled()** (BattleBridge 가 킬당 호출): `_targetScore += pointsPerKill; _punchTimer = punchDuration;`
- **Update**(`unscaledDeltaTime` — 드캐 일시정지 중에도 동작):
  - 롤: `_shownScore = Lerp(_shownScore, _targetScore, clamp01(dt*rollLerp))`, 라벨 = `CeilToInt(_shownScore)`.
  - 펀치/플래시: `_punchTimer` 감소, `f=_punchTimer/punchDuration` → 값 스케일 `Lerp(1, punchScale, f)`, 색 `Lerp(base, flash, f)`.
- **표시/리셋**: `GameManager.Instance.PhaseChanged` 지연 구독(Instance 준비 시). `Battle` → 점수 0 리셋 + 표시, 그 외 phase → 숨김.

## 계약/주의

- 점수 로직(누적 + pointsPerKill)은 뷰 소유. 외부 노출 없음(표시 전용).
- 폰트/머티리얼 미할당 시 TMP 기본 폰트 폴백(컴파일/동작 안전).
- `unscaledDeltaTime` 사용 — 드캐 모달(timeScale=0) 중에도 롤/펀치 진행.
- caption/value `raycastTarget=false` — 입력 가로채지 않음.

## 완료 기준

- ✅ compile: CS 에러/경고 0 (`textWrappingMode` 신 API 사용).
- 시각/동작 검증은 unit 3 Play 에서.

✅ 2026-06-05 구현 완료(Anton 폰트·상단 배치·1.3배 반영). 커밋: 1434911
