# Spec — Defender Placement Cooldown (방어 유닛 배치 쿨타임)

상태: 완료 2026-07-22 (commit `4b9caeeb`) — unit 0·1·2 구현·커밋, 사용자 시각 확인. 동작 엣지 전체 Play 패스는 handoff Follow-up.

## 검증 질문

> 방어 유닛을 배치하면 그 유닛 타입이 일정 시간 재배치 불가가 되고, 남은 시간이 인게임 유닛 선택 트레이의 해당 셀에 **슬로모션에 정직하게** 표시되는가?

## 상위 목표

배치에 시간 자원(코스트)뿐 아니라 **재사용 대기**라는 축을 추가한다. 유닛 SO 에 배치 쿨타임(초) 필드를 두고, 배치 성공 시 그 유닛 타입을 쿨타임에 넣는다. 쿨타임 중에는 트레이 셀에서 배치를 시작할 수 없고, 셀 위에 남은 시간이 **레이디얼 스윕(시계 와이프)** 으로 표시된다. 쿨타임 시계는 전투 슬로모션을 따라 느려진다.

## feature-wide 계약

1. **필드**: `DefenderUnitData.placementCooldown`(float 초, 기본 `0`). `0` = 쿨타임 없음 → 기존 에셋 무영향. 쿨타임은 **유닛 타입 단위**(에셋 참조를 키로).
2. **시계 = Battle 도메인**: 쿨타임 tick 은 `TimeManager.Instance.DeltaTime(TimeDomain.Battle)` 로 진행한다(`CostRuntime` 재생과 동일 시계). 드래그 배치 슬로모(0.2×) 중 1/5 감속, 메뉴 정지 시 동결. **표시 숫자 = 남은 배틀시간**이라 항상 정직 — 이것이 "슬로모션 고려"의 구현.
3. **시작 시점**: 배치 **성공** 시점(`DefenderDragPlacementController.PlacementCommitted` 이벤트, unit 을 실어 발화). 배치 거부/취소 시 시작하지 않는다. `RequiresFacing` 유닛은 조준 페이즈 진입 시점(=엔티티 스폰·코스트 소모가 이미 확정된 지점)에 시작한다.
4. **상태 소유**: `PlacementCooldownRuntime`(MonoBehaviour, **비싱글턴**, `GameManager.CooldownRuntime` 로 도달) — `CostRuntime` 를 미러. 유닛→남은초 맵을 들고, 자체 `Update()` 가 Battle 델타로 tick.
5. **차단 위치 = 슬롯 레벨**: `DefenderDragSlot.OnBeginDrag`/`OnPointerClick`, 기존 코스트 게이트와 같은 자리(`_suppressedDrag` 패턴). 쿨타임 중이면 드래그/arm 세션을 시작하지 않고 피드백만. **ECS/BattleBridge 에는 쿨타임 개념을 넣지 않는다** — 순수 Mono/UI 관심사이므로 맥락 경계를 지킨다.
6. **표시 = 빠지는 액체 오버레이**(사용자 결정 2026-07-22, 액체 디벨롭): 코스트 물통 셰이더(`Wassup/UI/CostWell`)를 **재사용**하되, 포트레이트 위에서 `_Fill = 남은비율`로 액체가 **아래로 빠지며**(코스트는 차오름 — 반대) 유닛이 떠오른다. 색은 **탁한 슬레이트**(코스트 하늘색/골드와 구분), 위치는 유닛 포트레이트(코스트는 전용 셀), 중앙 **카운트다운 숫자**(코스트는 재화 수치), 종료 시 팝. `DefenderSelector` 가 프레임별로 `RemainingFor`/`Fraction` 을 읽어 리페인트(활성 쿨타임 있을 때만). 반투명은 셰이더가 액체색 alpha 를 안 보므로 `Image.color.a` 로 준다. **코스트와 방향·색·위치·숫자 4축으로 구분**(액체 구조 재사용 + 역할 구분 = 사용자의 "재사용 구조" 요구 충족).
7. **리셋**: 배치 페이즈 진입(`PlacementPhaseView` 의 `CostRuntime.ResetToStart()` 자리)에서 `CooldownRuntime.ResetAll()` — 매치 시작·재시작·리드로우를 전부 커버. 추가로 `BattleBridge` teardown(`TimeManager.ResetAll()` 자리)에도 방어적 `ResetAll()`(critic m5).
8. **순수 계산은 EditMode 테스트**: 시작/tick/만료, fill 비율(0..1), 표시 숫자(올림) 산출을 `CostRuntimeTests` 미러로 검증.
9. **재사용 구조 · 비추상화**: 오버레이 위젯과 유닛-키드 런타임은 자체 완결·재사용 가능하게 짓되, 실제 2번째 소비처(스킬/드림캐쳐/기믹 쿨타임 등)가 생기기 전에는 인터페이스 추출/일반화하지 않는다(프로젝트 규칙 8). 이름은 현 스코프에 정직하게(`PlacementCooldownRuntime`).

## 검토 메모 (critic 확인 사항)

- **조준 유닛 시작 타이밍**: `RequiresFacing` 유닛은 aim-begin 시점에 쿨타임 시작(계약 3). 검증 결과 그 시점엔 이미 코스트 소모·엔티티 스폰이 확정되고 `DirectionAimController` 에 사용자향 refund/cancel 경로가 없음 → "phantom 쿨타임" 아님. 타이밍 선택 유효.
- **Battle 페이즈 중 배치**: 트레이는 Battle 에서도 슬림 노출되고 배치가 허용된다 → 쿨타임도 Battle 중 tick/표시됨. Battle 도메인 시계라 의도대로 일관(정상).
- **풀 내 중복 에셋**: 쿨타임 키 = `DefenderUnitData` 참조. 같은 에셋이 풀에 두 번 있으면 두 셀이 한 쿨타임을 공유(타입 단위 설계와 일관). 풀에 중복이 없다는 전제이며, 있어도 공유가 의도.
- **우회 경로 없음**: 표준 배치는 전부 슬롯 입력(`OnBeginDrag`/`OnPointerClick`)에서 출발하고 튜토리얼은 soft 추천만 → 게이트 우회 없음(상세 unit 1).

## 작업 단위 목록

| 파일번호 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 토대 | `0_cooldown_runtime_and_field.md` | `DefenderUnitData.placementCooldown` + `PlacementCooldownRuntime` + `GameManager.CooldownRuntime` 노출 + 리셋 훅 + EditMode 테스트. 씬에 컴포넌트 배선. |
| 1 | 로직 | `1_start_and_gate.md` | 배치 성공 시 쿨타임 시작(`PlacementCommitted` 구독) + 슬롯 배치 차단(드래그/탭) + 피드백. |
| 2 | 표현 | `2_slot_cooldown_overlay.md` | 레이디얼 스윕 오버레이 + 카운트다운 + 종료 팝. `DefenderSelector` 프레임 리페인트. Play 시각 검증. |
| 3 | 인계 | `3_handoff_summary.md` | (구현 종료 시) 커밋/검증/주의점 요약. |

## 파이프라인 커버리지

**N/A** — 이 스펙은 새 플레이 오브젝트(유닛/적/투사체/해저드/VFX)를 신설하지 않고, 오브젝트 생성→렌더 경로를 바꾸지 않는다. 추가되는 것은 (a) SO 필드 1개, (b) Mono 런타임 상태 1개, (c) 배치 진입 게이트, (d) 트레이 셀 UI 오버레이뿐이다. `docs/reference/object-pipeline-map.md` 대조 불요.

## 후속 후보 (현 스코프 밖)

- **오버레이·런타임 재사용**: 스킬/드림캐쳐/기믹 쿨타임으로 같은 위젯·런타임을 재사용. 실제 2번째 소비처가 확정될 때 공통 추출(지금은 하지 않음).
- **쿨타임 단축 버프**: 드림스톤/시너지로 배치 쿨타임 감소(CostRate 스톤이 재생 배율을 바꾸는 것과 유사한 축).
- **오디오 큐**: 쿨타임 종료 시 재사용 가능 짧은 사운드(현 스코프는 시각만).
- **쿨타임 중 셀 상호작용**: 쿨타임 중 셀 탭 시 상세/남은시간 툴팁 강조 등.
