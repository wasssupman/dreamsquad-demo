# aggro-tile-chase — 어그로 추격을 타일 목적지 경로 이동으로 재작성

> 상태: 구현 완료 2026-07-20 (units 0~4 커밋, EditMode 1016·PlayMode 스모크 green — 사용자 Play 체감 확인 대기). 인계는 `5_handoff_summary.md`.
> 연결 문서: `docs/reference/arknights-defense-mechanics.md` (리서치 + 2026-07-20 결정 기록), `docs/spec/aggro-standoff/` (정지⟺발사 metric 통일 — 유지됨), `docs/spec/aggro-targeting/` (히트 구동 어그로 — 유지됨)

## 배경 / 문제

어그로 추격(Chasing)이 **가디언 유닛 위치를 향한 직선 이동 + cell-trim** 이라, 직선이 벽에 걸리면 수선(垂線) 지점·코너에서 영구 고착된다. 고착 중엔 발사도 이중 금지(Chasing 상태 게이트 + sticky 타겟 Null) → "걸어가다 멈춰서 아무것도 안 하는" 좀비. 15×10 스위치백 맵(평행 통로 + 얇은 Place 띠)에서 대량 발화.

근본 원인은 어그로도, 판정 괴리도 아니다 — 정지/발사 판정은 이미 tile-Chebyshev 로 통일돼 있다. **괴리는 "도달 조건" vs "이동 실행" 사이**: 직선 실행기는 도달 조건을 만족하는 지점까지 갈 능력을 보장하지 않는다.

## 결정 (2026-07-20 사용자)

- **명일방주식(접촉 저지 + 가중치-only 도발) 미채택.** 마그넷 어그로(이동 목표 변경) 유지.
- 이동을 "유닛 위치 추적"에서 **"어그로 대상의 타일 기준 목적지 타일로 경로 이동"** 으로 교체.
- 채택 불변식 (reference doc): ① 멈춘 적은 반드시 때린다 ② 갈 수 없으면 멈춰 있지 않는다.

## 검증 질문

임의 맵/배치에서 어그로된 적이 **(a) 사거리 타일에 도달해 공격하거나 (b) 도달 불가 판정과 함께 어그로를 잃고 행군을 계속하거나** — 둘 중 하나임이 보장되는가? (제3의 상태 = 고착 좀비가 존재하지 않는가)

## Feature-wide 계약

1. **목적지 = 타일**: 어그로 획득 시 "가디언 **셀** 기준 tile-Chebyshev ≤ RangeToTiles(적 AttackState.range) 인 walk 셀" 후보 중 결정론적 선택(최근접, 동률 시 인덱스 순). 도달(목적지 셀 진입) ⟺ 발사 가능이 **정의상** 일치.
2. **이동 = per-guardian BFS 필드 하강** (cardinal step). 가디언은 정적 배치이므로 필드는 획득 시 1회 계산·캐시. 소유는 Effects(어그로 lifecycle 소유자), Movement 는 RO 소비 — `DefenderFieldSingleton`(boss hunting) 선례.
3. **도달 불가 = 획득 시점 판정**: BFS 상 적의 셀이 unreachable 이거나 후보 셀이 0개면 Aggroed 를 **부착하지 않는다**(또는 즉시 해제). 좀비 금지.
4. **walk 셀 술어 공유** (cell-trim 리뷰 C5): 목적지 후보/BFS 통과 판정은 `MovementCellTrim.IsWallCell` 을 재사용 — 고립 zero-flow 셀을 목적지로 찍지 않는다.
5. **Standoff 전이는 기존 유지**: 목적지 도달 전이라도 사거리에 들면 조기 Standoff 허용 (기존 계약 무변경). AttackSystem sticky/발사 게이트도 무변경.
6. **변위 안전망** (cell-trim 리뷰 C2): 프레임당 총 변위(이동+impulse+pull)를 1타일 미만으로 클램프 — dt 스파이크/강한 넉백의 벽 관통(터널링) 차단.
7. **tornado pull 재작성** (cell-trim 리뷰 C4, 사용자 결정): 중심으로 끌리되 **cell-trim 에 걸리면 벽에 막힌 것처럼 제한**. 경로(이동 목표/flow 따라가기)를 바꾸지 않고, **이동 스텝 후처리 단계에서 가산 변위**로 적용한다. ※ 해석 명시 — "LateUpdate 변위 조정"을 "sim 스텝 말미의 후처리 가산 변위 + trim"으로 번역(pull 이 flow step 을 대체(continue)하던 현행 구조 폐지). 프레젠테이션(view) 전용 변위로는 만들지 않는다 — pull 의 지연 효과는 게임플레이(sim)다.
8. cell-trim 자체(축별 clamp·epsilon)는 안전망으로 유지. wall-slide(C1)·대각 슬립(C3)은 이 spec 범위 밖 — 후속 후보.

## 작업 단위 (예정)

| # | 작업 | 목적 |
|---|---|---|
| 0 | 순수함수 | 목적지 후보 선정 + BFS dist/flow 계산 (plain 입출력, EditMode 테스트) |
| 1 | Effects | 어그로 획득 시 chase field 빌드/캐시/해제 + 도달불가 시 미부착 (AggroStateSystem 확장) |
| 2 | Movement | Chasing 분기 교체: 직선 → chase field 하강. 프레임 변위 클램프(계약 6) 동봉 |
| 3 | Movement | tornado pull 후처리 가산 변위화 (계약 7) |
| 4 | Tests | EditMode(선정/필드) + PlayMode 스모크(고착 재현 지형 → 도달 or 해제 보장) |
| 5 | Handoff | 인계 요약 |

## 후속 후보

- cell-trim wall-slide (C1) — 넉백 impulse 가 코너에서 흡수되는 품질 이슈 (redesign 후 어그로 경로에서는 자연 소멸)
- 대각 코너 슬립 차단 (C3)
- 동적 blocking hazard 가 chase 경로를 막는 경우의 재판정 (해저드는 일시적이라 현 scope 밖)
- 어그로 대-방어유닛 사거리 데이터 정리 (`aggroAttackRange` vs native range 이원화 — aggro-standoff 후속 후보 승계)
