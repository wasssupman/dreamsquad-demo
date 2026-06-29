# 3 — 통합 검증

## 목적

units 0~2 가 합쳐져 검증 질문 — *"적이 항상 walk 타일 위에서 target 으로 이동하고, target 변경(aggro)에도 경로가 타일 위에서 바뀌며, 같은 입력에 결정론적인가"* — 을 충족하는지 확인.

## 측정 기법

running Play 에서 `execute_code` 로 적 `LocalTransform` + `FlowFieldSingleton`(flow/grid/origin/goal) 을 읽어 각 적의 cell·walk 여부·perp(진행방향 수직 오프셋)를 스캔. `enemy-spawn-positioning/4` 진단과 동일 기법.

## 검증 결과 (2026-06-29, Play 측정)

| 항목 | 상태 | 근거 |
|---|---|---|
| **타일 이탈 0** | live ✅ | 한 시점 12마리 전원 `offWalk(sim)=0` (전 적이 walk 셀 위) |
| **aggro 시작 → 타일 유지** | live ✅ | 진짜 aggro 적(idx88)이 walk 셀 (10,1) 경계(perp 0.499)에 정착, guardian Place 타일 **미진입** — unit 2 정상 |
| **코너 비엣지** | live ✅ | 코너 4셀 맵, 적 perp **최대 0.250**(=deadband), 엣지-허깅(0.29~0.49) 0건 (unit 1) |
| **aggro 종료 → goal 복귀** | compositional ✅ | 별도 라이브 데모 미실행. 논리: aggro 가 적을 walk 타일에 유지(✅ live) + `Aggroed` 제거 시 동일 flow 분기가 인계(11 비-aggro 적으로 ✅ live) → 복귀 = 검증된 두 사실의 합성. 별도 return 코드 없음 |
| **결정론** | structural ✅ | unit 0 EditMode(`LaneFraction` 이산 N-레인 6종) + 이동/스폰 경로 RNG 잔존 0 |

세 결함의 핵심(타일 유지·코너·결정론)은 라이브/구조적으로 충족. aggro-종료 복귀만 합성 논거(논리 airtight).

### 라이브 재확인 (fresh 세션, 2026-06-29)

taunt defender 직접 배치 후 측정: aggro 적 **3마리 전원 sim walk 타일 위**(offWalk 0), walk 셀 가장자리(perp 0.27~0.50)에 정착. 사용자 관찰 "타일 벗어남"은 **엣지 + 스프라이트 피벗 겹침 착시**로 확정 — 발(sim)은 타일 안. aggro 타일 제약 라이브 확인. ※ 가장자리까지 미는 이유 = aggro 가 guardian 중심까지 밀어붙임(`stackThreshold` 0.05, standoff 미구현). standoff(사거리 정지)는 **다음 spec** 으로 분리(이동 무결성과 무관).

## 발견된 별개 이슈 (follow-up, 이 spec 범위 밖)

- **QuadUnit 뷰 누수** — Play 중 `QuadUnit_Needler_88` 이 그리드 밖 (1,−3) 에 떠 있었으나, 가장 가까운 live 적까지 4.56 유닛 = **뒤에 적 없는 orphaned 뷰**. 이름의 `88` 은 재활용된 생성-시점 entity.Index. `QuadUnitViewPool` 이 엔티티 사망 시 Quad 뷰를 해제 안 하는 presentation 누수로 추정(Spine 뷰 11 정상). sim 이동과 무관 → README 후속 후보.

## 완료 기준

- compile 0 · EditMode 25/25(units 0~2 누적).
- 위 검증표 5항목 충족.
