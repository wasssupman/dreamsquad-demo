# 4 — 전투 표현 스케일 반영 (Spine / VFX)

## 목적

전투 표현(유닛 Spine, 투사체/데미지넘버 VFX)의 재생 속도를 `ScaleOf(Battle)` 에 맞춘다. 안 하면 슬로우모에서 애니는 1x, 실제 공격은 0.2x → desync.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`, `SpineUnitPool.cs`
- `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` (+ 관련 VFX 뷰)
- (선택) 단명 전투 VFX: `BattleBridge.cs:2884/2910` deploy/ring pulse 등

## 구현

- **스폰 pull (레이스 방지)**: 뷰가 풀에서 활성화될 때 `float s = TimeManager.Instance.ScaleOf(TimeDomain.Battle);` 로 즉시 초기화:
  - Spine: `skeletonAnimation.timeScale = s;`
  - 파티클: `particleSystem.main.simulationSpeed = s;` (root + 자식 순회)
- **변화 신호**: `TimeManager.ScaleChanged` 구독 → `domain==Battle` 이면 활성 뷰 전체에 위 값 반영.
  - `SpineUnitPool._byEntity`, `ProjectileViewPool` 의 활성 목록 순회로 fan-out.
  - 구독/해제는 풀 lifecycle(OnEnable/OnDisable 또는 Init/Cleanup)에 건다.
- **단명 코루틴 VFX**: `elapsed += Time.deltaTime` 로 도는 것(BattleBridge deploy/ring pulse)은 스케일 대상이면 `TimeManager.Instance.DeltaTime(Battle)` 로 교체. 스코프 판단: 전투 연출이면 포함, 순수 UI 피드백이면 제외.

## 완료 기준

- [ ] 컴파일 통과.
- [ ] Play 슬로우모(0.2): 유닛 공격/이동 애니가 시뮬과 **동기**로 0.2x. 스크린샷 육안 확인.
- [ ] **슬로우모 중 스폰된 적**도 0.2x 로 애니(스폰 pull 동작 확인).
- [ ] 정지(0): 유닛 애니·전투 VFX 정지. 재개 시 이어감.
- [ ] 드래그 프리뷰/HUD 는 영향 없음(Interaction 도메인).

## 주의

- 어떤 VFX 가 "전투 표현"이고 어떤 게 "UI 피드백"인지 구현 시 명시 분류. 애매하면 전투 시뮬과 붙어 보이는 것만 포함(최소 스코프).
