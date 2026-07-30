# 3 — 격자 밖 관용: "보드 밖에 놓으면 취소" 를 성립시킨다

## 목적

**이미 있던 기능의 구멍을 막는다.** `EndDrag` 는 원래부터 세 갈래였다:

```
칸 있고 배치가능 → 배치
칸 있고 불가     → 거부 플래시 + 취소     ← "배치불가 타일에 놓으면 취소" (동작함)
칸 없음          → 취소                   ← 이 분기도 원래 있었다
```

세 번째가 **사실상 도달 불가**였다. `PlacementCellSnap.Resolve` 가 결과를 무조건 격자로
`Mathf.Clamp` 해서, 화면 어디를 눌러도 칸이 하나 잡혔기 때문이다. 그래서 보드 밖으로 빼서 놓으면
가장자리 칸으로 접히고, 그 칸이 배치 가능하면(Serpent 최하단 행 15/15 Place) **취소가 아니라
배치**가 됐다.

즉 "배치불가 타일 = 취소" 는 있는 기능이었고, **맵과 방향에 따라 되거나 안 됐다.** 이 unit 은
그 조건부를 없앤다.

## 변경 대상

- `Assets/_Project/Scripts/UI/PlacementCellSnap.cs` — `Resolve` 가 `Vector2Int?` 반환
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 칸 없음 분기 + 취소 예고
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — ⑥ 그룹 관용 노브
- `Assets/_Project/Tests/EditMode/PlacementCellSnapTests.cs` — 관용 계약 4건

## 구현

### A. Resolve 가 nullable 을 돌려준다

```csharp
public static Vector2Int? Resolve(Vector2Int? current, Vector2 frac, float stickMargin,
    Vector2Int gridSize, int outsideToleranceCells)
```

관용은 **셀 인덱스 초과분**으로 센다(정수 판정). `tol=1` 이면 `cx=-1` 은 0 으로 붙고 `cx=-2` 는
null 이다. frac 임계를 따로 두지 않는 이유: 히스테리시스 밴드와 **두 개의 경계**가 생겨 "액체는
안 끊긴 척인데 이미 칸이 없다" 같은 불일치가 난다.

**히스테리시스가 이미 "테두리 칸에 올라선 뒤의 관용" 을 담당한다는 점이 load-bearing.**
`current=0` 이면 `frac ∈ [-(0.5+margin), …)` 동안 0 이 유지되므로, `tol` 은 그 밴드 **밖**의 추가
여유만 정한다. 그래서 `tol=0` 이어도 가장자리 배치가 갑자기 빡빡해지지 않는다(테스트가 잰다).

### B. 노브 — `placementOutsideToleranceCells` 기본 1

셀 폭이 화면 75px 남짓이라 `tol=1` 은 보드 밖 약 100px 까지 관용이다. `0` 이면 밴드 직후부터
취소(약 22px) — 가장자리 행을 반복 배치하는 플레이에 빡빡해서 기본값은 1 로 잡았다.

큰 맵은 보드가 화면을 거의 채워 **좌우 여백이 얇다** — 그 방향은 관용 안에 들어가 취소가 안 될 수
있다. 맵 무관하게 도달 가능한 취소는 unit 0(트레이 존)이 담당한다. **둘은 대안이 아니라 보완이다.**

### C. 칸 없음 = 취소 예고

`ResolveFocusAndTarget` 이 null 을 받으면 `_noCell = true` → `ClearHover()`(하이라이트·사거리·
액체 소거, 진입 프레임 1회) → 포인터 추종 라벨을 `✕  놓으면 취소 · 코스트 유지` 로. 프리뷰 고스트
알파는 트레이 존과 **공용**이다(같은 "이대로 놓으면 안 꽂힌다" 신호).

**소거는 판정, 예고는 게이트.** hover/사거리는 칸이 없으면 즉시 걷어야 하지만(판정 사실), 라벨과
고스트는 `CancelArmed` 의 dwell 게이트를 지난다 — 가장자리 열을 노리며 좌우로 흔들면 관용 링을
순간 넘었다 돌아오는데, 게이트가 없으면 그때마다 Spine 알파가 1↔0.4 로 튀고 라벨이 껌뻑인다
(리뷰 M1). 릴리즈 취소는 게이트를 보지 않는다.

표면은 **하나**다(rev3 에서 배너를 지우며 통합) — 두 사유가 같은 포인터 추종 라벨을 시분할한다.
문구도 같으므로 사유를 구분해 보여주지 않는다. `CancelArmed` 구간에서 `_noCell` 을 내리는 것은
"살아 있는 사유는 하나" 라는 위생 목적이고, 라벨은 감추지 않고 취소 문구로 유지한다.

릴리즈는 기존 "칸 없음" 분기가 그대로 받는다. 다만 소리를 추가했다 — 취소는 거부가 아니므로
`FlashPlacementReject` 없이 트레이 존과 **같은** 복귀음.

## 완료 기준

- [x] 컴파일 통과, CS 에러 0
- [x] EditMode 전량 — 실패 1(dirty `MapDocument_Zig` 사전 실패)뿐. 신규 `OutsideTolerance_*` 4건 포함
- [x] 라이브 함수 프로브 — `PlacementCellSnap.Resolve` 10케이스 직접 호출로 관용 경계 확인
      (관용 안 스냅 / 밖 null / tol 0 / 히스테리시스 유지 / 레거시 clamp 등가)
- [x] PlayMode — `DragPlacementReachTest` 통과 (**상단/하단 도달성이 관용에 깎이지 않았음**)
- [x] Play — 보드 밖으로 한 칸 이상 빼서 놓으면 취소되고 코스트가 줄지 않는다
- [x] Play — 그때 하이라이트가 사라지고 프리뷰가 고스트, 포인터에 취소 문구가 뜬다
- [x] Play — **가장자리 열을 좌우로 흔들 때** 고스트/라벨이 껌뻑이지 않는가 (리뷰 M1 dwell 게이트)
- [x] Play — 맵 **가장자리 행/열 배치가 여전히 편한가** (tol 1 의 감각 확인 — 빡빡하면 2)
- [x] Play — 배치불가 타일 릴리즈는 종전대로 **거부 플래시**(취소와 구분되는가)

확인: 2026-07-30 사용자 Play 확인 통과. 구현 커밋 `c377b60f`(units 0~1) · `ec5e9c05`(unit 2 철회) ·
`c61aa51c`(unit 0 rev2 + unit 3) · `ffd6ae28`(rev3 배너 삭제) · 리뷰 반영분은 `fbcac2db` 안
(병행 세션 인덱스에 쓸림 — `be073d33` 참조).
