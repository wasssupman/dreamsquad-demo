# 2 — 발광 펄스 + 대각 샤인 스윕

## 목적

처치 순간 점수 숫자가 **발광 펄스**(순간 밝아졌다 안착) + **대각 라이트 스트릭 스윕**(좌→우로 훑는 광택)으로 "빛나며 오른다". 배경(바쁜 전투 화면)과 분리되어 강렬하게 읽히게 한다. 진짜 URP Bloom이 아닌 모바일 안전 가짜 글로우.

## 변경 대상 (실제)

- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — 글로우/샤인 `Image` 생성(BuildCanvas) + 수동 Update 타이머(`UpdateGlowShine`) + TriggerHit 트리거
- 신규 `Assets/_Project/Shaders/UI_Additive.shader`(`Wassup/UI/Additive`, `Blend SrcAlpha One`) + `Assets/_Project/VFX/Score Additive.mat`
- 신규 스프라이트 `Assets/_Project/VFX/Textures/ScoreGlow.png`(소프트 라디얼) · `ScoreShine.png`(소프트 세로 바) — 절차 생성 후 Sprite 임포트

## 구현 (실제)

TMP 머티리얼 글로우(GLOW_ON) 대신 **별도 additive Image 2개**로 구현 — 숫자 머티리얼(모바일 안전 `fe393ace`) 불변, 예측 가능·정밀 제어.

- **발광**: 숫자 뒤 라디얼 글로우 `Image`(additive, 골드). 평상시 `glowRestAlpha`(0.05, 은은한 백라이트), 처치 시 `glowFlashAlpha`(0.22) flare 후 `glowFlashDuration` 감쇠 + `glowPulseScale` 스케일 펄스. **가독성 우선** — additive 글로우가 강하면 골드 숫자를 덮으므로 flash 알파 절제(0.55→0.22, 오프스크린 렌더로 확인).
- **샤인 스윕**: 숫자 위 얇은 대각(`shineTiltDeg` 18°) additive `Image`(폭 `shineWidth` 24), 처치 시 좌→우 `shineTravel` 스윕 + `sin` 페이드. `DraftCardFoil_UI` 는 TMP 불가·무한 shimmer 라 미사용. 마스킹 없이 얇은 글린트가 숫자를 지나감.
- **시간축**: 수동 타이머 `unscaledDeltaTime`(PrimeTween Image API 불확실성 회피). 모달 중에도 동작.
- **직렬화/에셋**: 글로우/샤인 색·크기·알파·시간·스윕 전부 `[SerializeField]`. 셰이더는 빌드 안전(빌트인 legacy additive 스트립 위험 → 자체 셰이더).

## 완료 기준

- compile: CS/셰이더 에러 0.
- Play: 처치 시 발광 펄스 + 샤인 스윕이 보이고, 바쁜 배경 위에서도 숫자가 강렬히 분리 + **가독 유지**.
- 진짜 Bloom/post-FX 미사용(모바일 안전).

✅ 2026-07-07: compile 0 err + 오프스크린 합성 렌더로 글로우(0.22 flash·숫자 가독)·대각 샤인 글린트 확인(`score_glowshine_preview3.png`) + **Play 검증 통과**. 커밋 `0274a04d`. Play 피드백 "샤인 직선 이동이 튄다" → 샤인 알파 0.5→0.22·스윕 0.4→0.25s 약화(`2be826de`), 재-Play "훨씬 자연스럽다" 확인.
