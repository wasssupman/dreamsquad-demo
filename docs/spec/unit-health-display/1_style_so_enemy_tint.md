# 1 — HealthDisplayStyle SO + 적 저체력 틴트

## 목적

체력 표기 시각 파라미터의 단일 소스 SO 를 만들고, 첫 소비자로 적 저체력 컬러 틴트(정상→창백→검붉음)를 적용한다. 화면 요소 추가 없이 "이 적이 얼마나 상했나"가 몸에서 읽히게.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Data/HealthDisplayStyle.cs` (ScriptableObject)
- 신규 asset: `Assets/_Project/Data/Config/HealthDisplayStyle.asset`
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — `SetHealthTint(Color)`
- `Assets/_Project/Scripts/Presentation/QuadUnitView.cs` — 동일 (fallback 뷰)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `[SerializeField] HealthDisplayStyle`, `SyncMonoUnitViews` enemy 루프에서 평가·전달

## 구현

- SO 필드(이번 unit 분): `Gradient enemyTintGradient` (time=hpRatio, 1.0=정상 白 → 저체력 창백/검붉음). unit 2/3 필드는 해당 unit 에서 추가.
- 평가 위치는 **BattleBridge**: enemy 루프(:1742~)에서 `Health` read-only 조회(`HasComponent` 가드) → `ratio = clamp(value/max,0,1)` → `Evaluate(ratio)` → 뷰에 Color 전달. 뷰는 SO 를 모른다.
- `SpineUnitView.SetHealthTint`: `Skeleton.R/G/B` 세팅 (`SetColor`). `_dying` 중엔 마지막 틴트 유지(덮지 않음). `Spawn` 에서 白 리셋 (풀 재사용 대비).
- `QuadUnitView`: owned material `_BaseColor` 에 base color × tint 곱. 스폰 리셋 동일.
- ratio→색 계산에 순수 헬퍼(예: `HealthDisplayStyle.EvaluateTint(float ratio)` — 내부에서 clamp + max 가드)를 둬 EditMode 테스트 대상으로.
- style 미할당 시 틴트 스킵(경고 1회) — 기존 spawner null 가드 패턴.

## 완료 기준

- compile 0 에러 + EditMode 무회귀.
- 신규 EditMode 테스트: `EvaluateTint` — ratio 0/0.5/1 경계, 음수/1 초과 clamp, gradient 양끝 색 일치.
- Play 스크린샷: 만피 적은 원색, 저체력 적은 창백/검붉음 단계가 육안 구분. 사망 애니메이션 중 틴트 유지.
