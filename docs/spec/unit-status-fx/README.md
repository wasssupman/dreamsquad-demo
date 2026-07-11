# Spec — Unit Status FX (상태별 프리팹 연출 시스템)

> 상태: **완료 2026-07-09** (인프라 + 어그로 이관) — 커밋 `02a9db24`. handoff `4_handoff.md`.
> 출처: aggro-targeting 후속. 어그로 전용 `AggroIcon*` 를 **상태 종류별 프리팹 연출** 인프라로 일반화.
> 실제 추가 상태(스턴/빙결/독)는 각 ECS 소스 준비 시 registry 항목 + reconcile 훅으로 붙이는 후속.

## 목표

유닛에 붙는 "상태 연출"(느끼게 하는 시각물)을 **상태 종류마다 다른 프리팹을 끼워** 표현하는 일반 인프라. 어그로가 첫 등록 상태(현 "!" 유지). 스턴·빙결·독 등은 나중에 **registry 항목 + ECS 소스 훅 몇 줄**로 추가.

버프/디버프 **아이콘 스트립**(정보 배지)은 다른 축 — `unit-modifier-indicators` 별도 spec. 본 spec 은 "연출"만.

## 검증 질문

> "어그로된 적에 등록된 연출(현 '!')이 뜨고 해제 시 사라지는가? 새 상태(예: 스턴)를 registry 에 프리팹 하나 + reconcile 쿼리 몇 줄로 추가할 수 있는 구조인가? 한 유닛에 여러 상태 연출이 동시에 붙는가?"

## feature-wide 계약

1. **상태 = 프리팹.** `StatusFxRegistry` SO 가 `StatusFxKind → prefab(+오프셋/스케일/빌보드/폴백틴트)`. prefab 비면 절차적 "!" 폴백(현 어그로 유지).
2. **kind 는 append-only enum.** `StatusFxKind { Aggro, … }`. 직렬화 안전.
3. **상태 구동 reconcile.** BattleBridge 가 매 프레임 상태별 ECS 소스 조회 → `(entity, kind)` 마다 Ensure, 종료 시 회수. `AggroIconSpawner` 3단계 reconcile 골격 재사용.
4. **멀티 상태 동시.** 한 유닛에 여러 kind 프리팹 동시 부착(연출은 겹침 허용, 스트립 레이아웃 없음). 활성 키 = `(entity, kind)`.
5. **kind별 풀링.** 프리팹이 kind마다 달라 풀은 kind별. 회수는 같은 kind 로만 재사용.
6. **소스 훅은 상태별 국소.** 새 상태 추가 = registry 항목 + reconcile 에 쿼리+Ensure 몇 줄. 프리마추어 provider 추상화 금지(현재 소스 1개=Aggro).
7. **어그로 이관 무손실.** capacity·씬 배선·"!" 외형·Play 동작 보존. `AggroIcon*`/`AggroIconStyle.asset` → `StatusFx*`/`StatusFxRegistry.asset`.

## 작업 단위

| 파일 | 작업 | 문서 |
|---|---|---|
| 0 | enum + registry SO | `0_kind-and-registry.md` |
| 1 | Spawner/View 일반화 (AggroIcon* 재편) | `1_spawner-view.md` |
| 2 | BattleBridge reconcile 일반화 + 씬 재배선 | `2_bridge-and-scene.md` |
| 3 | registry 에셋 + Play 스모크 + handoff | `3_asset-tests-handoff.md` |
| 5 | Sleep 상태 Zz 아이콘 (첫 추가 상태) | `5_sleep_status_icon.md` |

## 파이프라인 커버리지 (상태 연출 = 오버헤드/온-바디 View)

`AggroIcon` 오버헤드 View 파이프라인의 일반화. 신규 아키타입 아님.

| 정거장 | Status FX |
|---|---|
| 데이터(SO) | `StatusFxRegistry`(kind→prefab/offset/scale/billboard/tint) |
| ECS 상태 | 상태별(Aggro=`Aggroed`) — FX용 신규 컴포넌트 없음 |
| 생성 트리거 | BattleBridge `ReconcileStatusFx` 매 프레임 |
| 뷰/풀 | `StatusFxSpawner`(kind별 풀) / `StatusFxView`(프리팹 or 절차 폴백) |
| teardown | `Spawner.Clear()` |

## 후속 후보

- 실제 추가 상태(스턴/빙결/독) registry 등록 + ECS 소스 훅. 각 상태 ECS 표현 준비 시.
  (Sleep 은 unit 5 로 완료 2026-07-11 — CcEffect 버퍼 스캔 소스. Stun 도 같은 스캔에 kind 분기만 추가하면 됨.)
- Sleep reconcile 스캔 O(전체 유닛) 최적화 — 프로파일 등재 시 Effects 토글 enableable `AsleepTag` (ecs-review M2).
- 폴백 글리프 캐시 배열(2) ↔ FallbackGlyph enum 개수 수동 커플링 — 3번째 글리프 추가 시 주의 (code-review LOW).
- follow 모드 확장(ground 링=회전 평면, on-body=오프셋0). 현재 overhead 빌보드 + 오프셋만.
- 어그로 "!" → 전용 프리팹 연출(가디언 tether 등). 재사용 맵: BlobShadow(발밑)·DragPlacement cord(tether 템플릿)·SetHealthTint(틴트 충돌 주의).
