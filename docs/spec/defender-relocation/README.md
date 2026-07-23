# Spec — Defender Relocation (배치 유닛 재배치)

상태: 스펙 작성 2026-07-23 · 구현 대기 (unit 0 부터)

## 검증 질문

> Battle 중 배치된 유닛을 1초 홀드로 집어 들고(슬로모), 탭 또는 드래그로 다른 타일에 내려놓으면,
> 유닛이 비행 연출로 이동한 뒤 재전개 시간을 거쳐 전투에 복귀하는가 — 그리고 이 과정이
> 코스트가 아닌 **재전개 시간(DPS 공백)** 만을 대가로 요구하는가?

## 상위 목표

배치 확정을 "되돌릴 수 없는 결정"에서 "시간을 지불하면 조정 가능한 결정"으로 완화한다.
비용은 코스트가 아니라 **그 유닛의 전투 이탈 시간**(비행 + 착지 후 재전개)이다.
UX 는 기존 배치와 대칭: 홀드로 이동모드 진입(슬로모) → 탭투플레이스 / 프레스 드래그 양방 지원 → 비행 연출.

## feature-wide 계약

1. **비용 = 재전개 시간 단독**: 코스트 0 · 배치 쿨다운(`PlacementCommitted`) 미발화. 대가는
   확정~전투복귀까지의 이탈 시간뿐. 모든 노브는 신규 `RelocationSettings` SO (홀드 시간 1s ·
   이동모드 진입 쿨다운 · 이동모드 타임아웃 · 착지 후 재전개 시간 · 하이라이트 색).
2. **이탈 구간 = `PendingDeployment` 재사용**: 확정 프레임에 재부착 → 비행·재전개 내내
   비타겟(피격 불가)·비무장(공격 불가)·시너지 제외 — 신규 배치 대기 상태와 완전 대칭.
   기존 메커니즘: `AttackSystem.cs` 후보 쿼리 `WithNone<PendingDeployment>`.
3. **상태 보존**: 엔티티 유지 — HP/실드/모디파이어/드림캐쳐 부착/`DeployedFacing` 전부 그대로.
   방향 유닛은 **기존 방향 유지**(재조준 없음 — `DeployedFacing` 1회 기록 불변 계약을 건드리지 않는 유일한 선택.
   재조준은 후속).
4. **on-place 스킬 미재발동**: relocate 활성화는 `ActivateDeployedDefender` 의
   `TriggerDeploymentOnPlaceSkill` 을 지나지 않는다(무한 재시전 차단). 컷신도 스킵.
5. **원자 스왑 시점**: `_occupiedTiles`/`_defenderByTile`/`DefenderTile` 은 **확정 프레임**에
   from→to 스왑(비행 중 양쪽 타일 이중 배치 차단, 사망 이벤트 셀 = to 로 일관).
   `LocalTransform` 은 **착지 프레임**에 갱신(뷰 순간이동 방지 — 뷰는 프리뷰가 비행).
6. **시너지 재계산 양쪽**: 확정 시 `RecomputeSynergyFor(from)`(이탈 반영), 활성화 시
   `RecomputeSynergyFor(to)`(기존 활성화 경로가 이미 수행).
7. **슬로모**: 이동모드 진입 시 기존 드래그 배치와 동일(`TimeDomain.Battle`, `dragSlowmoScale` 0.2×,
   priority 0) → **확정 또는 취소 시 해제**. 비행은 실시간 — 유닛의 DPS 공백이 보이는 것 자체가 대가의 시각화.
   남용 방지 = 진입 쿨다운(취소해도 적용) + 이동모드 타임아웃(자동 취소).
8. **연출·판정 재사용, 커밋만 분기**: 키링 프리뷰·비행 궤적·hover/팝·reject 피드백·`TryScreenToCell`·
   `SpatialPlacementCheck` 는 기존 파이프라인 재사용. 커밋 꼬리만 신설 `TryBeginDefenderRelocation` —
   코스트 차감·엔티티 스폰·on-place·컷신·`PlacementCommitted` 를 지나지 않는다.
9. **ECS 쓰기는 Bridge 창구 직접**: `DefenderTile`/`LocalTransform` 갱신은 스폰(`CreateDefenderEntity`)과
   동일하게 BattleBridge 가 `_em` 으로 직접 쓴다. 신규 NativeQueue 채널 없음. 코드는
   `BattleBridge.Relocation.cs` partial 로 분리(`BattleBridge.Dreamcatcher.cs` 관례).
10. **입력 소유권**: 홀드 진입 조건 = Battle 페이즈 && `!IsAiming` && UI 밖 && 트레이 armed 없음 &&
    활성 세션 없음. 짧은 탭(홀드 임계 전 릴리즈)은 소비하지 않고 기존 소비자(`DcInspectController`)에 양보.
11. **엣지 자동 취소**: 이동모드 중 대상 유닛 사망 → 즉시 취소(슬로모 해제 포함). to == from 은 취소 취급.

## 작업 단위 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 토대 | `0_bridge_relocation_api.md` | Bridge relocate API(Begin/Finish/Activate 변형) + 점유·타일·시너지 원자 처리 + EditMode 테스트. 즉시형으로 완결 동작 |
| 1 | 입력 | `1_hold_gesture_move_mode.md` | 보드 유닛 1초 홀드 → 이동모드(하이라이트·슬로모·카메라) + 취소·쿨다운·타임아웃 |
| 2 | 배치 | `2_relocate_placement_session.md` | 이동모드에서 탭/드래그 배치 → relocate 커밋 분기(스킵 셋 적용) |
| 3 | 연출 | `3_flight_redeploy_activation.md` | 실뷰 숨김+프리뷰 비행 → 착지 → 재전개 → 활성화. Play 전체 플로우 검증 |
| 4 | 정리 | `4_tap_inspect_reconcile.md` | 짧은 탭과 DcInspect 경합 정리(유닛 상태 화면은 기존 인스펙트에 위임) |
| 5 | 인계 | `5_handoff_summary.md` | (구현 종료 시) 커밋/검증/주의점 |

의존: `0 → 2 → 3`, `1 → 2`. `4` 는 `1` 이후 아무 때나.

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트 없음, 기존 유닛의 생성→렌더 경로 불변(스폰·활성화 레일을 그대로 재사용,
위치 컴포넌트 값만 갱신). 비행 연출은 기존 키링 프리뷰 재사용. `docs/reference/object-pipeline-map.md` 대조 불요.

## 후속 후보 (현 스코프 밖)

- **착지 후 재조준 페이즈** (방향 유닛) — `DeployedFacing` 쓰기 소유권 재정의 선행. directional-volley 백로그
  "배치 후 방향 재지정"과 합류.
- **Placement 페이즈 중 재배치** — 초기 배치 수정 QoL. 슬로모·재전개 무의미 구간이라 규칙 별도 설계.
- **풀 유닛 상태 화면** — 스탯/모디파이어/스킬 표시. DcInspect 확장 방향으로 별도 spec.
- **어그로 chase 재계산** — 가디언 이동 시 쫓던 적은 옛 목적지로 감(aggro-tile-chase 백로그와 합류).
- **이동 가능 타일 프리하이라이트** — 이동모드 진입 시 유효 타일 표시(`placement-attack-range-preview` 재사용).
- **재전개 시각 연출 고도화** — 현 스코프는 기존 PendingDeployment 표현 재사용.
