# 7 — 배치 3종의 착지 통일 (계약 1 뒤집기)

> 추가 2026-08-19 (사용자 발의). unit 0~5 종료 후의 정책 조정.

## 목적

배치 방식 셋이 **같은 배치 연출**을 갖게 한다. 연출의 정의는 «집은 곳에서 착지점까지 실유닛이
날아가 정착한다» 이고, 방식마다 다른 것은 **집은 곳이 어디냐** 뿐이다.

| 방식 | 입구 | 집은 곳 |
|---|---|---|
| 트레이 D&D | `EndDrag` | 키링에 매달린 위치(손가락) |
| 탭투플레이스 (셀 arm → 타일 탭) | `HandleBoardTap` → `SimulateDragTo` | **트레이 유닛 셀** |
| 탭투프레스 (셀 arm → 보드 프레스-드래그-릴리스) | `CommitBoardDrag` → `SimulateDragTo` | **트레이 유닛 셀** |

뒤 둘은 `SimulateDragTo` 한 입구로 모이므로 여기 하나를 고치면 둘이 함께 바뀐다.

## 뒤집는 계약

README 계약 1 「적용 범위 = 실드래그 릴리스만 (`!_simulatedDrag` 게이트)」 → **배치 3종 전부**.
재배치는 여전히 제외(자기 비행 소유).

## 왜 게이트만 여는 걸로는 안 됐나

첫 시도는 `!_simulatedDrag` 만 뗐다. 그런데 시뮬 경로는 고스트가 **타일 바로 위
(`endFeet + boardN·previewHeight`, 0.35)** 까지 날아가 정착한 **뒤에** 커밋했다. 하마의 출발점은
커밋 프레임의 `_unitPosWorld` 이므로 낙차가 0.35 밖에 없어 **제자리 홉**이 됐다. 착지 어휘는
같아졌지만 «집은 곳 → 착지» 라는 구조가 아니었다.

## 구현

### A. 고스트 던지기 폐기 — 커밋을 앞으로

`SimulateDragTo` 를 코루틴(`RunSimulatedDrag`)에서 **동기 메서드**로 바꾼다. 세션은 한 프레임만
살아서 고리·줄 하드웨어와 공용 커밋 꼬리를 빌려주고 정리된다.

1. `BeginDrag(unit, fromScreen, simulated: true)` — 세션 생성.
2. `_unitPosWorld`/`_ringWorld` 를 **트레이 셀 자세**로 세운다(`ScreenToBoardFeet(fromScreen)`).
3. 고리·줄 **트랜스폼을 직접 배치**한다. 평소 이 일을 하는 `Update` 추종 블록을 이 경로는 한 번도
   지나지 않으므로, 빠뜨리면 잔류 고리가 원점에 남아 화면 구석에서 페이드한다.
4. `CommitPlacementAt(targetCell)` — 여기서 `StartDropDismount` 가 트레이 셀 → 타일 비행을 가져간다.

`_unitVelWorld = 0` 이라 반동은 순수 dip 이다(D&D 는 릴리스 스윙 속도가 접선으로 실린다).

### B. 컷신은 시뮬 경로 제외

`BeginDrag` 의 컷신 분기에 `!simulated` 를 더한다. 세션이 한 프레임이라 `CommitPlacementAt` 의
`ForceStopAndReset` 이 같은 프레임에 죽인다 — 켜 두면 좌하단이 1프레임 번쩍일 뿐이다.
컷신은 «들고 있는 동안» 의 연출이고 탭에는 그 구간이 없다.

### C. 탭 범위 flourish 를 하마 비행에 매단다

`RunTapPlaceRangePeek` 의 `while (_session.active && _simulatedDrag)` → `while (_session.active ||
_activeDismounts.ContainsValue(cell))`. 세션이 한 프레임이 됐으므로 종전 조건이면 flourish 가 즉시
꺼진다. 탭에는 스카우트 구간이 없어(D&D 의 드래그 · 탭투프레스의 프레스-드래그와 다른 점) 이
flourish 가 유일한 범위 피드백이다.

## 수용한 대가 (사용자 결정 2026-08-19)

- **비행이 0.45초 상한을 따른다** (`dropTotalSeconds`, `deploymentDuration` 으로 클램프). 종전
  던지기는 최대 1.5초였다. 늘리려면 활성화 지연을 늘려야 하는데 그러면 공중 유닛이 공격·피격
  가능해진다(계약 3 파손) — **밸런스 결정 없이는 못 늘린다.**
- **배치 컷신이 탭 경로에서 사라진다** (7종 보유). — ※ 같은 날 별도 결정으로 컷신 자체가 꺼졌다
  (`enableDeployCutscene: 0`, `defender-deploy-cutscene` README 상단). 그래도 위 `!simulated`
  가드는 남긴다: 스위치를 되켤 때 탭 경로만 1프레임 번쩍이는 것을 막는 **범위 규칙**이지
  두 번째 on/off 스위치가 아니다.
- **커밋(코스트 차감·엔티티 스폰)이 탭 순간으로 앞당겨진다.** D&D 와 같은 구조가 된 결과다.

## 정리 (Play 확인 후 수행 — 2026-08-19)

고스트 던지기가 사라지면서 도달 불가가 된 것들을 지웠다. **살아 있는 스위치는 손대지 않았다** —
`selection-entry-narrowing` 의 세 스위치처럼 «정책으로 꺼둔» 코드는 되돌릴 수 있어야 하므로 남기고,
여기서 지운 것은 «경로째 삭제돼 되돌릴 대상이 없는» 것뿐이다.

- **`_simulatedDrag` 필드 전면 삭제**(+ `_simFocusCell`, `_tapFlightSeq`). 시뮬 세션이 한 프레임도
  살지 않으므로 이 플래그의 **모든 읽기 지점이 도달 불가**였다 — `Update` 의 포커스 락·SmoothDamp
  추종, 하이라이트 파생, 취소 존 판정, `SetHover` 의 사거리 억제, 취소 라벨 게이트. 전부 «참» 쪽으로
  접었다. `BeginDrag(simulated:)` **매개변수는 남는다**(Disarm 알림·flourish 취소·UserDragStarted·
  컷신 범위 — 넷 다 커밋 전에 판단된다).
- `ResolveFocusAndTarget(lockCell:)` 매개변수 제거 → 구 `else` 본문이 본체가 됐다. 액체 하이라이트의
  «탭이면 스트레치 0» 분기도 함께 사라진다.
- `DragSwaySettings` 죽은 노브 7개 삭제(`tapTravelDuration`/`tapTravelScale{Min,Max}`/
  `tapSettle{Distance,Speed,MaxDuration}`/`tapFollowSmoothTime`) + `.asset` 키 정리.
- `tapArc*`/`tapThrow*` 는 **남긴다** — 소유자가 탭 배치에서 재배치 비행(`ComputeThrowArc`)으로
  넘어갔을 뿐 소비처가 있다. 개명하면 저작값이 든 `.asset` 키가 갈리므로, 이름은 두고 **유래와 현재
  소유자를 SO 주석에 적었다**.

## 완료 기준

- [x] compile 클린 (2026-08-19 — Runtime/Tests.PlayMode 각 오류 0)
- [~] `DropDismountTest` — §4 를 뒤집힌 단정으로 갱신했으나 **도달하지 않는다.** 그 앞 D&D
      구간(`commit frame: cell occupied`)이 사전 실패라 거기서 멈춘다(`docs/spec/README.md`
      «PlayMode 사전 실패», 2026-08-16 분류, 같은 메시지). 이 unit 의 판정은 아래 Play 확인이
      맡았다. **사전 실패가 풀리기 전까지 이 통일에는 자동 가드가 없다** — 별도 조사 대상.
- [x] Play: 세 방식의 배치가 «집은 곳에서 날아가 스틱 착지» 하나로 읽힌다(반동 · 아치 · 착지 스쿼시
      · 링 펄스 · 고리·줄 페이드 전부 동일)
- [x] Play: 탭 배치의 고리·줄 잔류가 **트레이 셀 위**에서 페이드한다(원점이나 화면 구석 아님)
- [x] Play: 타일 팝이 착지 프레임에 한 번만 난다
- [x] Play: **아치 높이 판단** — 탭 거리는 D&D 보다 훨씬 길어 `dropArcHeightFactor`(현 1.399)가
      비례로 먹으면 화면 위로 크게 솟는다. 너무 높으면 노브를 낮추되 **D&D 와 공유하는 값**이므로
      한쪽만 낮추려면 별도 노브가 필요하다(그 판단은 이 unit 밖)
- [x] Play: 방향 지정 유닛(`RequiresFacing`)을 탭 배치해도 하마와 조준 페이즈가 함께 돈다(계약 8)

> 사용자 Play 확인 2026-08-19 · 커밋 `7608ef0a`
