# 1 · 팔레트 재설계 + 정점 그라데이션 + 모션 강화

## 목적

색·모션의 단조로움을 걷어낸다. 마그니튜드 기반 색을 유지하되 선명한 램프로 재설계하고, 원 스펙에 있었으나 미구현된 정점 그라데이션을 부활시키며, 펀치/셰이크/미세 회전으로 타격감을 준다. 코덱스 에셋 의존 없음 — 즉시 구현 가능.

## 타이밍 전제 (critic 반영 — 반드시 준수)

- 프로젝트는 글로벌 `UnityEngine.Time.timeScale` 을 **항상 1 로 고정**한다(`TimeManager`). 따라서 `Time.deltaTime` 은 TimeManager 정지/슬로우에 **반응하지 않는다** — 정지 메커니즘은 `TimeManager.DeltaTime(domain) = unscaledDeltaTime * ScaleOf(domain)` 이다.
- 현재 `DamageNumberView` 는 raw `Time.deltaTime` 을 써서 **정지 중에도 애니메이션이 계속되는 기존 버그**가 있다. 이번 unit 에서 **`TimeManager.Instance.DeltaTime(TimeDomain.Battle)`(또는 `Time.deltaTime * ScaleOf(Battle)`) 경유로 교정**한다. `SpineUnitView`/`EnemyHitBarView`/`ProjectileViewPool` 이 이미 이 패턴을 쓴다.
- "unscaled 금지" 는 오해였다 — 도메인 스케일 곱 경유가 정답. Time.timeScale 은 건드리지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/DamageNumberStyle.cs` — 팔레트 Gradient 기본값 + 모션 강도 직렬화 필드
- `Assets/_Project/Scripts/Presentation/DamageNumberView.cs` — 정점 그라데이션 적용·`_tmp.alpha` 페이드·TimeManager 델타·index 기반 셰이크/회전

## 구현

### 팔레트 재설계

- `DamageNumberStyle.damageColor` Gradient 기본값을 선명한 램프로: 소형=청록(cyan `#35E0D0`) → 중형=라임/스프링그린(`#6BF06B`) → 대형=골드(`#FFD23A`) → 초대형=핫오렌지(`#FF6A2A`). 데미지 크기 정규화 t 로 평가(현 `EvaluateColor(t)` 구조 유지). 인스펙터 오버라이드 가능.

### 정점 그라데이션 vs 알파 페이드 (충돌 해소)

- 현 코드는 매 프레임 `_tmp.color = c`(RGB+알파)를 덮어써 4-corner 그라데이션을 뭉갠다. 대신:
  - **RGB(면색+상단 부스트)는 그라데이션으로 1회 설정**: `_tmp.enableVertexGradient = true`, `_tmp.colorGradient = new VertexGradient(top, top, bot, bot)`, `bot = faceColor`, `top = faceColor * topBoost`(clamp01, 기본 `1.35`). 스폰 시 1회.
  - **페이드는 `_tmp.alpha` 로 분리**: 매 프레임 `_tmp.alpha = alphaCurve.Evaluate(n)`. `_tmp.color` RGBA 를 매 프레임 덮어쓰지 않는다(그라데이션 유지). TMP 의 `.alpha` 는 정점색 알파에 곱해진다.
- 프리팹 `m_enableVertexGradient` 는 코드가 런타임에 켜므로 프리팹 플래그 의존 불필요(unit 2 프리팹 갱신 시 함께 켜도 무방).

### 모션 강화 (index 결정론)

- **펀치**: 기존 `scaleCurve` + `bigHitPunchMul` 유지, 오버슈트 상향(대형일수록 큰 첫 스케일).
- **셰이크**(신규): 대형 히트에 수명 초반 감쇠 진동을 위치에 더한다 — `shakeAmp * decay(n) * dir(index)`. **방향은 오직 spawn index 로 결정**(frame-count 등 시간 소스 금지). 소형 히트엔 0. 진폭·감쇠는 직렬화 필드. 카메라축(camRight/camUp) 평면에서 흔든다.
- **미세 회전**(신규): 스폰 시 index 기반 소량 roll(±`maxTiltDeg`, 기본 ~6°)로 숫자마다 각도를 달리해 격자의 딱딱함을 푼다. 빌보드 회전에 곱해 적용(빌보드 유지).
- 모든 모션 델타는 **`TimeManager.DeltaTime(TimeDomain.Battle)`** 경유. TimeManager 정지 시 함께 정지.
- 튜닝값(`topBoost`, `shakeAmp`, 감쇠, `maxTiltDeg`, 펀치 배수)은 전부 `DamageNumberStyle` 직렬화 필드. 코드 상수 금지.

## 완료 기준

- compile 성공, 콘솔 에러 0.
- Play: 소형/대형 데미지가 뚜렷이 다른 색(청록↔골드↔오렌지)으로 뜨고 숫자 상단이 밝게 그라데이션 진다(스크린샷). 페이드가 그라데이션을 뭉개지 않는다(사라질 때도 상단 밝기 유지).
- Play: **TimeManager 정지 시 데미지 숫자 애니메이션도 정지**한다(교정 확인). 대형 히트에 짧은 셰이크 + 숫자마다 미세하게 다른 각도.
- 셰이크/회전이 동일 index 재현 시 동일(결정론). 기존 EditMode 회귀 무손상(사전 실패 `ObstaclePlacerTests` 무관).
