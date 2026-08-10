# unit 5 — 충돌 그리드도 층을 본다 (미완 부분 교정)

## 목적

**«걸을 수 있다»의 정의를 경로 탐색과 충돌 판정이 같이 쓰게 한다.**

unit 1b·3 은 **라우팅(BFS) 마스크**를 층 인지로 바꿨다. 그런데 `MovementSystem` 이 충돌·셀 트림에 쓰는 `NavGrid` 는 프레임당 **하나만** 조립되고 그 입력이 `field.walkMask` = **`Path` 전용**으로 남아 있었다. 결과:

- `Ground|Path` 로 저작된 순찰병이 배치지 칸에 서면, 그 칸이 충돌상 **벽**이라 `AgentCollision` 이 자기 셀 안에 영원히 clamp 한다 → `PatrolStep.dir` 이 `(-1,0)` 을 내도 **위치가 안 변한다**(라이브 로그로 확인)
- 배치지 칸이 전부 벽이므로 **이동타일 ↔ 배치타일 왕래가 구조적으로 불가**

즉 spec 의 정의식이 반쪽만 배선돼 있었다. 층을 연 유닛이 그 층 위에서 **경로는 찾는데 발을 못 뗀다.**

## ⚠ 이 결함을 놓친 이유 (재발 방지)

unit 3·4 의 검증은 `StepDir`·`FillAreaMask` 같은 **순수 함수 시뮬**이었다. 그 아래에서 `dir` 을 실제 변위로 바꾸는 `MovementSystem` 구간을 한 번도 태우지 않았다. 순수 함수는 전부 초록이었고 판은 멈춰 있었다.

**규칙**: 이동 계약을 바꾸면 검증 축은 «순수 함수가 옳은 값을 내는가»가 아니라 **«라이브에서 유닛의 셀이 실제로 바뀌는가»** 다. 계약 7(시스템 레벨 검증)의 구체적 적용이다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs` — 층 인지 `BuildNavGrid` 오버로드
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — 엔티티 통행 층별 nav (한-칸 메모)
- `Assets/_Project/Tests/EditMode/` — 회귀 2건

## 구현

### 조립 지점은 여전히 하나

`new NavGrid(...)` 를 `MovementSystem` 에 직접 쓰지 않는다 — `MovementCellTrim` 헤더가 못박은 "조립은 한 곳" 계약이다. 층 인지 오버로드를 그 파일에 추가하고 시스템은 그것만 부른다.

장애물은 마스크에 **이미 구워져** 나오므로(`MaterializeWalkMask`) `NavGrid` 에 다시 넘기지 않는다 — 같은 결과에 해시 조회가 사라진다.

### 프레임당 1회 → 층이 바뀔 때만 재조립

`PatrolFieldSystem` 이 BFS 마스크에 쓰는 **한-칸 메모**와 같은 방식이다. 층 값이 직전과 같으면 재사용한다.

쿼리는 아키타입(청크) 순회라 적(층 `Path`)과 순찰병(층 `Ground|Path`)이 서로 다른 청크에 모여 있다 → 실제 재조립은 **프레임당 층 종류 수**(오늘 2회)다. 유닛마다 층이 흩어지면 최악 «엔티티당 1회」로 완만히 나빠지고, 그때 층 값 키 캐시로 바꾼다.

### 적 거동 변화 0

적은 층 `Path` 이고, `(cellLayers & Path) != 0` 은 `tiles == Walk` 와 **같은 집합**이다 — unit 1b 가 `SingleDefaultSlot_MatchesWalkMaskRouting` 으로 셀 단위 고정해 둔 등식이다. 그래서 적이 보는 nav 는 기존 `walkMask` 와 동일하다.

## 완료 기준

- [ ] compile 에러 0 · EditMode 실패 0
- [ ] 신규 2건: ① 층 `Ground|Path` nav 는 배치지 칸을 통행 가능으로 본다 ② 층 `Path` nav 는 기존 `walkMask` nav 와 셀 단위 동일(무변경 축)
- [ ] **라이브 배틀 로그**: 순찰병의 셀이 실제로 바뀌고, 배치지 칸과 경로 칸을 **둘 다** 밟는다 — 순수 함수 시뮬은 이 축의 증거로 쓰지 않는다
- [ ] 적 이동 회귀 없음(유출/교착 관찰)
