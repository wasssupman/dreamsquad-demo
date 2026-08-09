# unit 3 — 순찰 필드가 유닛 층을 쓴다 (행동 변화 0)

## 목적

**«유닛이 지날 수 있는 층» 이 실제로 경로에 반영되는 지점을 만든다.** unit 2 가 값을 흘렸고, 여기서 처음으로 그 값을 **읽는다**.

폴백이 `Path` 라 저작 전에는 판이 안 바뀐다. 이 spec 의 마지막 기계 조각이다.

## 착수 전 조사

| 사실 | 그래서 |
|---|---|
| `fullMask` 는 `:49-50` 에서 **엔티티 루프 밖 · 프레임당 1회** 만들어진다 | 유닛별로 달라지면 **그 hoist 가 깨진다** — unit 2 조사에서 미리 잡았다 |
| `fullMask` 소비처는 2곳 — `FillAreaMask:66`(박스 ∩ 마스크) · `StepDir:73`(박스 밖 복귀) | 둘 다 **전체 격자** 마스크를 요구한다. 박스만으로 안 된다 |
| `MaterializeWalkMask` 는 셀마다 **자기 인덱스만** 읽고 쓴다 | `outMask` 를 층 마스크 버퍼로 재사용해도 안전하다(임시 배열 불필요) |
| `new NavGrid(...)` 조립이 이 변경으로 **세 곳**이 될 참이었다 | 추출 시점이 왔다 (아래) |

## 설계 판정 (렌즈 4개)

**① ECS** — 순찰 필드는 이미 ISystem 이고 엔티티별 데이터를 읽는다. 새 시스템·컴포넌트 없음.

**② 로직-아키텍처 분리** — 판정식은 이미 순수(`TraversalSlots.FillWalkMask`). 이 unit 이 추가하는 건 **조립 오버로드 하나**이고 그건 ECS 싱글턴 → `NavGrid` 어댑터라 순수화 대상이 아니다.

**③ 불필요한 설계 — 캐시 자료구조를 만들지 않았다.**
층별 마스크 캐시(`NativeHashMap<byte,int>` + stride 배열)를 만들 뻔했으나, **오늘 층 값이 1종**이라 항목이 하나뿐인 자료구조가 된다. 대신 **한 칸 메모**(`builtLayers` byte 하나)를 쓴다:
- 오늘: 전원 `Path` → **프레임당 1회 빌드가 그대로 유지**된다(현행 특성 보존)
- 층이 섞이면: 최악 «엔티티당 1회» = 200셀 × 순찰 수 로 **완만히** 나빠진다
- 순찰 엔티티가 수십을 넘으면 그때 키 캐시로 바꾼다

**④ 출처·소비처** — 출처는 `PathFollowState.traversalLayers`(unit 2), 소비처는 `fullMask` 2곳. 미주입(0)이면 `Path` 로 떨어져 현행 재현.

## 변경 대상

- `Battle/Movement/MovementCellTrim.cs` — **층 인지 `FillWalkMask` 오버로드** 신설
- `Battle/Effects/FlowFieldRebuildSystem.cs` — 두 번째 `new NavGrid(...)` 를 그 오버로드로 교체
- `Battle/Effects/PatrolFieldSystem.cs` — 유닛 층으로 마스크를 만든다 (한 칸 메모)
- `Tests/EditMode/CellLayersInstallTests.cs` — 3건 추가

## 벽 술어 조립을 한 곳으로 되돌렸다

`MovementCellTrim.cs` 헤더가 못박은 계약: *"여기 남은 것은 ECS 싱글턴 → NavGrid 조립 하나뿐이다. 벽 판정이 바뀔 때 고칠 곳은 NavGrid 하나여야 한다."*

unit 1b 가 `FlowFieldRebuildSystem` 에 **두 번째** `new NavGrid(...)` 를 만들었다(리팩토링 리뷰 지적). 술어 자체는 복제하지 않았지만 **조립 인자 6개**(`tileSize`/`origin`/`gridSize`/…)가 두 벌이 됐다. unit 3 이 세 번째를 만들 참이었으므로 여기서 회수했다 — 층 인지 오버로드 하나로 **셋이 다시 하나**가 된다.

## 완료 기준

- [x] compile 에러 0 · EditMode **2025 중 2022 통과 · 실패 0**
- [x] **기존 테스트 기대값 갱신 0건** — 늘어난 3건은 이 unit 이 추가한 것
- [x] `new NavGrid(` 프로덕션 조립 지점이 **다시 1곳**(`MovementCellTrim`)
- [x] 신규 3건: **`Ground` 유닛은 `Place` 칸만 지난다**(계약이 실제로 작동) / `Path` 유닛은 `walkMask` 와 셀 단위 동일(무변경 축) / **in-place 쓰기가 오염되지 않는다**(임시 배열 경유 결과와 동일)

## 남은 것

이 spec 의 기계는 여기서 끝난다. **방어유닛을 실제로 움직이려면** README §5 가 필요하다 — 특히 앵커 스냅(`TryGetNearestWalkCell`)이 `Walk` 하드코딩이라, 지금 방어유닛에 `Ground` 를 저작하면 **앵커가 자기 층 밖**이 된다.

---

**완료 기준 확인**: 2026-08-09 · EditMode 2025 중 2022 통과 · 실패 0 · 행동 변화 0
