# 1 — 브리지가 비행 창을 여닫는다

## 목적

일반 도약의 비행 창(뷰 시계 0.83s)에 맞춰 `LeapFlight` 를 붙였다 뗀다. 창의 시작·끝을 아는 것은
비행을 구동하는 브리지뿐이다 — `PendingDeployment` 를 `TryBeginDefenderDeployment` /
`ActivateDeployedDefender` 가 여닫는 선례 그대로.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.BossLeap.cs` — 부착·해제 3지점

## 구현

- **부착**: `DrainBossLeapVisualEvents` 가 코루틴을 시작하는 지점(`:108` 오버라이드 등록 직후) —
  `_em.AddComponent<LeapFlight>(evt.entity)`. 드레인은 LateUpdate 라 구조 변경 안전.
- **해제 2경로**:
  - 정상 착지: `RunBossLeap` 종료(`ResolveLanding` 직전) — `RemoveComponent`.
  - abandon(사망·teardown·오버라이드 clear): 코루틴의 `abandoned` 탈출 분기 — 엔티티가 아직
    존재하면 `RemoveComponent`. 사망은 어차피 `DeadTag` 로 행동이 끝나 있지만, **시체에 태그를
    남기지 않는 위생**이 목적(엔티티 재사용은 없으나 디버깅 시 오독 방지).
- 부착·해제 모두 `_em.Exists` 가드. 브리지는 ECS 창구라 `EntityManager` 직접 사용이 허용 경로다.

## 완료 기준

- compile 클린 · EditMode 무회귀
- **Play**: 보스 도약(체력 50%·10%) 비행 중 — 보스가 공격을 멈추고, 착지 후 정상 재개.
  비행 중에도 방어유닛이 보스를 계속 때리고 데미지가 들어간다(피격 가능 유지)
- **드리프트 해소 확인**: 착지 프레임에 뷰→sim 전환 팝이 이전보다 줄었는가(비행 중 이동 잠금의
  부수 효과 — flight-lift-feel 후속 후보 소화)
- 비행 중 보스 사망 시 콘솔 에러 0 (abandon 경로)
