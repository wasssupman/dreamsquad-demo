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

## 남은 정리 (Play 확인 후)

되돌릴 가능성이 있어 지금은 남겼다. 체감이 확정되면 지운다.

- `DragSwaySettings` 의 `tapTravelDuration`/`tapTravelScaleMin`/`Max`/`tapSettleDistance`/
  `tapSettleSpeed`/`tapSettleMaxDuration`/`tapFollowSmoothTime` — 소비처 0.
- `tapArcHeightFactor`/`tapArcLateralFactor`/`tapThrow*` 는 **살아 있다** — 소유자가 탭 배치에서
  재배치 비행(`ComputeThrowArc`)으로 넘어갔을 뿐이다. 이름을 옮길지는 별도 판단.
- `DefenderDragPlacementController._simFocusCell` 과 `Update` 의 `_simulatedDrag` 분기
  (포커스 락 · SmoothDamp 추종) — 세션이 한 프레임이라 도달 불가.

## 완료 기준

- [x] compile 클린 (2026-08-19 — Runtime/Tests.PlayMode 각 오류 0)
- [ ] `DropDismountTest` 초록 — §4 가 **뒤집힌 단정**(시뮬 경로도 오버라이드 등록 → 착지에 해제)
- [ ] Play: 세 방식의 배치가 «집은 곳에서 날아가 스틱 착지» 하나로 읽힌다(반동 · 아치 · 착지 스쿼시
      · 링 펄스 · 고리·줄 페이드 전부 동일)
- [ ] Play: 탭 배치의 고리·줄 잔류가 **트레이 셀 위**에서 페이드한다(원점이나 화면 구석 아님)
- [ ] Play: 타일 팝이 착지 프레임에 한 번만 난다
- [ ] Play: **아치 높이 판단** — 탭 거리는 D&D 보다 훨씬 길어 `dropArcHeightFactor`(현 1.399)가
      비례로 먹으면 화면 위로 크게 솟는다. 너무 높으면 노브를 낮추되 **D&D 와 공유하는 값**이므로
      한쪽만 낮추려면 별도 노브가 필요하다(그 판단은 이 unit 밖)
- [ ] Play: 방향 지정 유닛(`RequiresFacing`)을 탭 배치해도 하마와 조준 페이즈가 함께 돈다(계약 8)
