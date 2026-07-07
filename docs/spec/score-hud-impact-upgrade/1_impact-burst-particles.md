# 1 — 임팩트 버스트 (UiEmber 절차 파티클)

## 목적

처치마다 점수 숫자 위치에서 **방사형 골드 스파크/코인 버스트**가 터지게 한다. 데미지 숫자의 라운드 도트와 다른 형상(별/코인/플러스 등)으로 점수만의 정체성. ScreenSpaceOverlay HUD 위라 실제 파티클이 안 되므로 절차적 UGUI 쿼드로 구현.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/ScoreBurstPool.cs` (plain 클래스, ScoreHudView 가 소유·구동) + `ScoreHudView.cs` (직렬화 `ScoreBurstStyle burst` + Tick/Emit/ClearAll 배선)
- 스파크 텍스처: **생성한 `Assets/_Project/VFX/Textures/ScoreSpark.png`** (소프트 4점 스파클, 절차 생성 후 Sprite 임포트). 빌트인 `Resources.GetBuiltinResource` UI 스프라이트는 null·GA 텍스처는 Sprite 아님 → 자체 스파클로 결정. 데미지의 둥근 `Circle18` 도트와 **형상 구분**(4점 반짝임).

## 구현

- **UiEmber 프리미티브만 차용** (`DraftCardVfxDriver.SpawnUiEmbers`): "`Image`+`CanvasGroup` 쿼드를 lerp 로 이동" 형태만 가져온다. **그 참조의 수명 로직(무한 `Mathf.Repeat` 루프 + `Random.Range` + 풀링 없음)은 복사 금지** — 우리는 one-shot·풀링·균등분산이 목표.
- **방사**: 버스트당 N개(직렬화, 기본 8~12)를 **균등 각도 분산**(index 기반 N-레인 round-robin — 재현성이 아니라 코스메틱 고른 방사가 목적). start=중심, end=바깥 방사 + 약한 중력.
- **같은-프레임 병합**: 처치당 개별 버스트가 아니라 프레임당 1버스트(README 계약). 같은 프레임 처치 수 `k` 에 비례해 버스트 강도(쿼드 수/크기/속도)를 스케일 — AoE 는 더 큰 한 방.
- **모션**: `unscaledTime` 기반 lerp(중심→방사) + 페이드인/아웃 + 스케일 감쇠. PrimeTween 또는 수동 update 중 택1(풀링 반납 콜백 필수).
- **색/형상**: 골드 계열(`baseColor` 연동 또는 별도 직렬화). 형상은 데미지 도트와 구분되는 텍스처.
- **풀링**: 반납 시 비활성 + 재사용. 오브젝트 누수 0. `OnDisable` 정리(활성 쿼드 전량 반납).
- **모바일**: 버스트당 개수 상한 + **전역 동시 쿼드 상한**(둘 다 직렬화). 상한 초과 시 신규 스폰 skip(폭증 방지). 처치당 1버스트가 아니라 프레임당 1버스트.

## 완료 기준

- compile: CS 에러/경고 0.
- Play: 처치 시 골드 입자가 숫자에서 방사·페이드. 데미지 도트와 시각 구분됨.
- **AoE/전멸 프레임**: 한 프레임 다처치가 폭증 대신 강화된 1버스트로 병합, 전역 쿼드 상한 초과 시 skip 확인.
- 연속 다처치에도 풀 재사용(오브젝트 무한 증가 없음), 반납 누수 0.
- 오프스크린 렌더 또는 Play 스크린샷으로 버스트 형상/색 육안 확인.

✅ 2026-07-07: compile 0 err + 오프스크린 렌더로 골드 4점 스파클 방사 확인(`score_burst_preview2.png`) + **Play 검증 통과**. 커밋 `d2e3b833`.
