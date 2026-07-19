# 5. Handoff Summary

## Commit

- `e14e8701` unit 0 — 사거리 해석 + chase field 순수함수 (FlowFieldBuilder 재사용)
- `45a5c52a` unit 1 — 획득 게이트 + per-enemy chase field 부착/해제 (Effects)
- `7016a1c9` unit 2 — Chasing 필드 하강 교체 + 프레임 변위 클램프 (Movement)
- `fc547c4f` unit 3 — tornado pull 후처리 가산 변위화
- `2d57bddc` unit 4 — PlayMode 스모크 수리(무장 더미 부채) + 실전 체인 검증

## Implemented

- 어그로 추격 좀비 버그(직선 greedy 가 수선/코너에서 영구 고착 + 발사 이중 금지) 제거 — 획득 시 목적지 타일(가디언 사거리 내 walk 셀) BFS 필드를 굽고 하강. 도달⟺발사 정의상 일치
- 도달 불가/전투수단 없음은 **획득 자체 거부** (README 계약 3 — 좀비 클래스 생성 불가)
- 프레임 XZ 변위 < 0.9타일 상한 (cell-trim 단일 셀 검사 전제의 불변식화, 터널링 차단)
- tornado pull: 이동 대체 → 가산 변위 + trim (벽에 막힘, 계약 7 사용자 결정)

## Key Files

- `Battle/Combat/AggroChaseMath.cs` — 정의 계층 (ResolveTileRange/BuildChaseField)
- `Battle/Effects/AggroStateSystem.cs` Pass 3 — 게이트·부착 / Pass 1 — 해제 시 버퍼 제거
- `Battle/Effects/AggroChaseCell.cs` — per-enemy dist 버퍼 (Aggroed 수명 동기)
- `Battle/Movement/MovementSystem.cs` — Chasing 하강·pull 가산 / `MovementCellTrim.ClampDisplacement`

## Verified

- EditMode 1016 전부 green 매 unit (신규 11 포함) · PlayMode `MovementIntegritySmokeTest` green — 히트→어그로→추격→도달→응전+타일 불변식 실전 체인
- PlayMode 잔여 실패 3(Gift 페이즈 2·덱 캐리인 1)은 본 spec 무관(병행 세션 영역) — 이전부터 실패, 보고만
- 사용자 Play 체감 확인(원버그 재현 배치) 남음

## Notes (되돌리면 안 되는 것)

- Chasing 은 chase 버퍼 없으면 **정지가 정답** (직선 추격 부활 금지 — 고착의 근원)
- 획득 거부는 버그가 아니라 계약: 가디언이 통로에서 (적 사거리+도달) 밖이면 어그로가 안 걸린다 — 적은 그냥 행군 (명일방주 통과 원칙의 우리식 번역)
- dt=1s 합성 이동 테스트는 변위 상한 때문에 서브틱/소틱으로 작성 (단일 1s 틱 = 상한 발동)
- 스모크의 더미 가디언은 반드시 무장 상태 유지 (무기 없으면 히트 구동 어그로가 성립 불가)

## Follow-up

`docs/spec/README.md` Follow-up Backlog 이관 예정 항목: cell-trim wall-slide(C1)·대각 슬립(C3)·동적 해저드 chase 재판정·aggroAttackRange 데이터 결정(1 유지 시 "통로 2칸 밖 가디언" 배치는 어그로 미획득이 사양) — README 후속 후보 참조.
