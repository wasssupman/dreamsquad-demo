# 0 — 탄성 펀치 + 화이트핫→골드 플래시

## 목적

현재 수동 `Lerp` 펀치(1.45x 선형 감쇠) + 흰→골드 색을, **PrimeTween 탄성 오버슈트 슬램 + 화이트핫→리치 골드** 로 교체한다. 카운트업 롤도 초기 상승이 빠른 곡선으로 강화해 "쾅 올라간다" 느낌을 만든다. 이 단위가 골드 아이덴티티의 토대.

## 변경 대상

- `Assets/_Project/Scripts/UI/ScoreHudView.cs`

## 구현

- **애니 엔진 전환**: `Update()` 안 수동 펀치/플래시 Lerp 제거(매-프레임 `_value.color`/`localScale` 덮어쓰기 제거 — 안 그러면 트윈을 매 프레임 뭉갬. 데미지 스펙의 "단색 덮어쓰기가 그라데이션 뭉갬" BLOCKER와 동형) → PrimeTween 구동.
  - **펀치**: 값 `RectTransform` 에 `Tween.PunchScale` 또는 `Tween.Scale(→punchScale→1, Ease.OutBack)` — 오버슈트로 "슬램". 진행 중 재트리거 시 이전 트윈 stop 후 재시작(연속 킬 스택 안전).
  - **색 플래시**: `_value.color` 를 화이트핫(`flashColor`)에서 baseGold 로 Tween(`Tween.Color`, 짧게). 기존 흰→골드가 아니라 **화이트핫→골드**(순간 과노출 후 리치 골드로 안착).
- **같은-프레임 병합**: `OnEnemyKilled()` 는 누적만(카운터++/`_targetScore += pointsPerKill`), 실제 펀치/플래시 트리거는 프레임당 1회 flush(Update 초 또는 LateUpdate). AoE 다처치가 펀치 폭주 대신 1회 강화 슬램이 되게(README 계약).
- **골드 아이덴티티**: `baseColor` 를 **리치 골드**로. **단, `baseColor`/`flashColor` 는 BattleScene 컴포넌트에 이미 직렬화(흰/골드) → C# 기본값이 shadow 됨. 반드시 씬 값 재저작**(MCP, README "씬 값 재저작" 위생 절차). 데미지(Bangers 다색)와 단색 프리미엄 골드로 대비.
- **카운트업 롤 강화**: 목표 추종 곡선을 초기 급상승형으로(빠른 rollLerp 또는 tween 기반). 라벨은 `CeilToInt` 유지.
- **시간축**: unscaled 유지 — PrimeTween 1.4.0 shortcut 오버로드에 `useUnscaledTime` 파라미터 존재(`Tween.PunchScale(...,  useUnscaledTime: true)`, `Tween.Color(..., useUnscaledTime: true)`). named arg 로 명시 전달(드캐 모달 timeScale=0 중에도 동작).
- **트윈 수명**: 펀치/색 `Tween` 핸들을 필드로 보유 → `OnDisable`·`OnPhaseChanged`(Battle 이탈) 시 `Stop()` + `localScale`=1·색=base 리셋. 비활성 패널 위 유령 트윈 금지(`DraftCardVfxDriver.OnDisable` 선례).
- **폰트 교체 + 스테일 주석 수정**: 점수 폰트를 Anton → **Kanit Bold Italic**(다이내믹 스포티 이탤릭)로 교체(사용자 피드백 "폰트가 밋밋"). `Kanit SDF.asset`(Dynamic·512·SDFAA, 프로젝트 컨벤션) + `Kanit Outline Mat.mat`(Score Outline Mat 설정 복제: FaceColor 흰색→코드 골드 틴트·OutlineWidth 0.22 검정·FaceDilate 0.1). `ScoreHudView.cs` 스테일 주석도 Kanit 기준으로 정정. TTF+OFL 라이선스 커밋.
- **직렬화**: `punchScale`, `punchDuration`, `flashColor`(화이트핫), `baseColor`(골드), 롤 강도, ease 선택 등 전부 `[SerializeField]`(기존 필드 재사용/확장).

## 완료 기준

- compile: CS 에러/경고 0.
- Play: 처치 시 점수 숫자가 **탄성 오버슈트로 "쾅"** 커졌다 안착 + **화이트핫→골드** 플래시. 연속 킬 시 스택/깜빡임 없이 자연스러운 재트리거.
- 값이 인스펙터에서 Play 중 실시간 튜닝 가능.

✅ 2026-07-07: compile 0 err + Play 검증 통과. 커밋 `b559d136`.
