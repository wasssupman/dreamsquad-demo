# 4 — 검증 + 인계 요약

상태: **units 0~4 완료 2026-08-09 · 행동 변화 0 · Play 검증 불필요(판이 안 바뀜)**

## Commit

| 커밋 | 내용 |
|---|---|
| `70d49b9c` | unit 0 — 셀 층 비트를 sim 으로 (`FlowFieldSingleton.cellLayers`) |
| `97ae481a` | unit 1a — 라우팅을 슬롯 stride 로, 소비처 전부 슬롯 뷰 |
| `fd25d53b` | unit 1b — 마스크 집합 → 슬롯 N개 |
| **`43b714d1`** | **1b 회귀 수정 — 통행 층을 `tiles` 에서만 파생** (적이 안 움직이던 것) |
| `ffe6f55d` | rev 3 재작성 — 목표 재정립, 로스터 수집 삭제, 1a·1b 동결 |
| `08188de7` | unit 2 — 유닛 통행 층 축 (SO 2 + 컴포넌트 1) |
| `5df1b930` | unit 3 — 순찰 필드가 유닛 층을 쓴다 + 벽 술어 조립 1곳으로 회수 |
| `8a98e038` | `.meta` 6개 누락 보정 |
| (이 커밋) | unit 4 — `PatrolFieldSystem` 시스템 경로 테스트 + handoff |

## Implemented

- `FlowFieldSingleton.cellLayers` — 셀이 여는 층 비트. **`tiles` 에서만 파생**(저작된 `placeMask` 를 읽지 않는다)
- `flow`/`dist` 를 `[slot * CellCount + cell]` **flat stride** 로. 슬롯 뷰(`FlowSlot`/`DistSlot`)가 stride 를 감춰 순수 함수 시그니처가 그대로다
- `TraversalSlots.FillWalkMask` — 이 spec 의 정의식 `(셀 층 & 슬롯 마스크) != 0` 의 **단일 소유자**
- `AttackUnitData`/`DefenderUnitData.traversalLayers` + `PathFollowState.traversalLayers` — 스폰 2곳에서 주입
- `MovementCellTrim.FillWalkMask(field, layers, …)` — **층 인지 조립**. 벽 술어 조립 지점이 다시 1곳
- `PatrolFieldSystem` 이 유닛 층으로 마스크를 만든다 (한 칸 메모로 «프레임당 1회» 특성 보존)

## Key Files

| 파일 | 역할 |
|---|---|
| `Battle/Effects/FlowFieldSingleton.cs` | `cellLayers`·`maskValues`·슬롯 접근자 |
| `Battle/Effects/TraversalSlots.cs` | 교집합 정의식 (순수) |
| `Battle/Movement/MovementCellTrim.cs` | **`NavGrid` 조립의 유일 지점** — 층 인지 오버로드 포함 |
| `Bridge/SimFieldInstaller.cs` | `cellLayers` 생성 + 슬롯별 빌드 |
| `Battle/Effects/PatrolFieldSystem.cs` | 유일한 소비자 |

## Verified

- EditMode **2032 중 2029 통과 · 실패 0** (나머지 3은 기존 `[Ignore]`)
- **다섯 unit 연속 «기존 테스트 기대값 갱신 0건»** — 행동 변화 0 의 증거
- 라이브 경로 회귀 2건: 스폰·골 층 폐쇄 후에도 필드가 산다 · `PatrolFieldSystem` 시스템 경로
- **음성 대조군** — 층이 안 맞으면 유닛이 실제로 멈춘다(테스트에 이빨이 있음을 증명)

## Notes — 되돌리면 안 되는 의도

1. **통행 층은 `tiles` 파생이다.** 저작된 `placeMask` 를 읽으면 **적이 안 움직인다** — `BattleBridge` 가 필드 굽기 직전 스폰·골 칸의 `placeMask` 를 0 으로 닫기 때문이다(배치 불변식). 두 축은 다르다: 배치 = «설 수 있나», 통행 = «지날 수 있나». 상세 README §0.
2. **폴백은 전원 `Path`.** «방어유닛 = 자기 `placementLayers`» 로 바꾸면 순찰 소환물이 `Ground` 로 떨어지는데, 그 값은 이니셜라이저 잔여값이고 실제로는 Walk 층에서 돈다 → **굳는다**.
3. **골 슬롯은 1개로 동결.** 소비자가 «골로 가는 적» 뿐이라, 적 통행 층이 갈리기 전(=물 적)까지 늘릴 이유가 없다.
4. **`new NavGrid(...)` 를 새로 만들지 말 것.** 조립은 `MovementCellTrim` 하나가 소유한다 — 이미 한 번 두 벌이 됐다가 회수했다.
5. **`SlotFor` 는 완전일치인데 모델은 교집합이다.** 슬롯 1개라 무해하지만, 슬롯을 늘리는 spec 이 **먼저 고쳐야** 한다(README §8 D1).

## Follow-up

- **방어유닛 이동 켜기** — README §5. 앵커 스냅(`TryGetNearestWalkCell`)이 `Walk` 하드코딩이라 **지금 방어유닛에 `Ground` 를 저작하면 앵커가 자기 층 밖이 되어 굳는다**(`WrongLayerForAnchor_UnitCannotMove_NegativeControl` 이 그 상태를 고정해 둔다)
- **물 적** — README §6. 셀 쪽 변경은 3줄
- **리팩토링 잔여** (리뷰 P1) — 인라인 체비셰프 12곳 → 기존 `GridMath.ChebyshevDistance` · `AttackSystem` 2곳 → 기존 `TargetPersistence.KeepsLock`
- **`PlacementLayer` → `CellLayer` 리네임** — 통행에도 쓰이므로 이름이 거짓말한다(참조 40+)

---

## 추가분 — unit 5 (2026-08-10, 이 핸드오프 이후에 붙었다)

**Commit**: `4cbfe751` fix(traversal-layers): unit 5 — 충돌 그리드도 통행 층을 본다

**무슨 일이 있었나**: units 1b·3 은 **라우팅(BFS) 마스크**만 층 인지로 바꿨다. `MovementSystem` 이 충돌·셀 트림에 쓰는 `NavGrid` 는 프레임당 하나였고 그 입력이 `field.walkMask`(= `Path` 전용)로 남아 있었다. 그래서 이 spec 의 **첫 소비자**(순찰병 `Ground|Path`)가 배치지에 서면 자기 칸이 벽으로 읽혀 `PatrolStep.dir` 을 받고도 영원히 clamp 됐다.

**되돌리면 안 되는 것**: `MovementSystem` 의 nav 는 **유닛 통행 층별**이다. 프레임당 하나로 되돌리면 `Path` 아닌 층을 가진 모든 유닛이 즉시 굳는다. 조립은 여전히 `MovementCellTrim` 한 곳이고, 장애물은 마스크에 구워져 나오므로 `NavGrid` 에 다시 넘기지 않는다.

**다음 사람이 읽을 것**: [`5_collision_grid_layers.md`](5_collision_grid_layers.md) 의 «이 결함을 놓친 이유» 섹션. 이동 계약을 바꿀 때 검증 축은 «순수 함수가 옳은 값을 내는가»가 아니라 **«라이브에서 유닛의 셀이 실제로 바뀌는가»** 다. 이 spec 은 그걸 세 라운드 놓쳤다.

**남은 위험**: 위 Follow-up 의 «방어유닛 이동 켜기» 항목이 경고한 앵커 스냅 문제는 `summon-patrol-defender` unit 9 에서 층 인지 스냅(`TryGetPatrolHomeCell`)으로 해소됐다. 다만 **`TryGetNearestWalkCell` 자체는 여전히 `Walk` 하드코딩**이고 다른 호출처(해저드 디버그 스폰 등)가 남아 있다.
