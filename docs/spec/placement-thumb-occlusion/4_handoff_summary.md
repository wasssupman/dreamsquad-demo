# 4 — Handoff Summary

작성 2026-07-29. units 0~3 구현·자동검증 완료, **육안 Play 확인 대기**.

## Commit

| 해시 | 범위 |
|---|---|
| `0fb13921` | spec 초안 (README + 0~3) |
| `664c4f69` | critic 리뷰 반영 — 코드 오진술 4건 교정 |
| `039b8be2` | unit 0 — 가상 포인터 오프셋(트레이 D&D) |
| `6f117212` | unit 1+3 — 보드/재배치 오프셋 + 사거리 적색화 |
| `31a79042` | unit 2 — 하단 조작성 계측 + 상한 실측 |

## Implemented

- **가상 포인터**: 배치 판정 포인터 = 실제 포인터 + 화면 up × (offset × ramp). 변환은
  `UpdateDrag` 진입부 **한 곳**. `EndDrag` 무변경(위임하므로 여기서 또 변환하면 이중 가산).
- **드래그로 승격된 뒤에만 적용**: 트레이 D&D 는 첫 프레임부터, armed 보드는 `_boardDragging` 후,
  재배치는 신설 승격 게이트 후. **무이동 탭은 raw**(누른 칸에 배치).
- **램프는 이동량 비례**(`PlacementPointerOffset.Ramp`, 60px). 시간 기준 폐기 — 손가락이 멈춰도
  하이라이트가 올라가고 16px 승격이 65px 점프를 만드는 증폭이 남기 때문.
- **raw 분리**: 카메라 포커스(`SetDragFocus`)만 실제 좌표. NDC 절대 변환이라 가상을 주면 상수
  바이어스가 실려 카메라가 프레임을 당기고 오프셋이 벌린 간격을 되돌린다.
- **거부 라벨** 화면 상단 클램프(오프셋 때문에 손가락 기준 96+offset px 로 올라간다).
- **사거리 적색화 + 전이 1회 플래시**: 유효성 상태는 `SetPlacementRangeValidity` 단독 소유, 전이에만
  스탬프. 틴트 재적용 경로 신설 없음(`Update` 가 이미 매 프레임 `ApplyRangeTint`).
- **조준 채널 경계 = 소유권**: `ClearRange` 반납 + 스킬 조준/텔레그래프 획득 시 리셋.
- **`TileSet_Desert.rangeTile` 배선** — 선행 버그(사막 시즌 사거리 미렌더) 해소.
- **하단 조작성 가드**: 최하단 배치가능 행을 노릴 수 있는 가장 높은 손가락 Y > 화면 12%.

## Key Files

- `Assets/_Project/Scripts/UI/PlacementPointerOffset.cs` — 순수 정책(Ramp/Apply). 소비처 2곳.
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 변환 seam · 램프 소유 · 라벨 클램프
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — 목적지 승격 게이트 · `AimScreen`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `SetPlacementRangeValidity` · `RangeTintColor` 플래시
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 포워딩 + owner 전환 리셋 3곳
- `Assets/_Project/Data/Config/DragSwaySettings.asset` — ratio **0.10**(2026-07-31 사용자 튜닝
  `e45ee478`, 최초 확정값은 0.06) / rampDistance 60
- 테스트: `PlacementPointerOffsetTests`(EditMode) · `DragPlacementReachTest` ·
  `DropDismountTest` · `RelocationPlacementSessionTest`

## Verified

컴파일 에러·경고 0. EditMode `PlacementPointerOffsetTests` 8/8. PlayMode
`DragPlacementReachTest` 1/1 · `DropDismountTest` 1/1 · `RelocationPlacementSessionTest` 2/2.

민감도 실측(1080 세로): 0.06 통과 · 0.18 통과 · **0.20 실패**(row 0 조준 최고점 119px = 11% < 12%).
첫 실패 ≈ 0.19 → 확정값 0.06 은 약 3배 여유. **가드가 실제로 무는 것이 증명됐다.**

2026-07-31 라이브 값은 **0.10**(≈108px @1080). 가드 여유 약 1.9배. 이 값으로 테스트를 다시 돌리지는
않았고, 하단 도달성은 **사용자 Play 확인**으로 통과했다(실측 표의 0.18 통과가 상한 근거).

## Notes (되돌리면 안 되는 의도)

- **변환 지점은 하나다.** `EndDrag`/`BeginDrag` 에 변환을 추가하면 오프셋 이중 가산 → 릴리즈 칸이
  하이라이트보다 한 칸 더 튄다.
- **카메라 포커스에 가상 좌표를 주지 마라.** 무해해 보이지만 상수 바이어스다.
- **무이동 탭에 오프셋을 주지 마라.** 피드백 루프를 볼 시간이 없어 오배치로 읽힌다.
- **`Set/ClearPlacementRange` 에서 `_rangeInvalid` 를 리셋하지 마라.** `SetPlacementRange` 가 내부에서
  `ClearPlacementRange` 를 부르므로 무효 영역을 훑는 동안 플래시가 연발한다.
- **`_rangeAimStyle` 로 조준 채널을 지킬 수 없다.** 스킬 조준·텔레그래프는 `aimStyle=false` 다.
- **하단 단언 A 는 약한 단언이다.** clamp 때문에 거의 항상 성립한다 — 안전망은 단언 B 다.
- 재배치 테스트는 **하이라이트 추적** 방식이다. 오프셋 역산으로 되돌리면 ramp 순환 +
  `FindObjectOfType` 의 비활성 누락으로 다시 깨진다.

## Follow-up

- **육안 Play 확인(사용자)**: ① 하이라이트가 커서 위 ② 릴리즈 칸 == 하이라이트 칸 ③ 카메라 안 밀림
  ④ 라벨 안 잘림 ⑤ 적색 전환+플래시 ⑥ 코스트 부족 구간 ⑦ 무효 영역 훑을 때 연발 없음
  ⑧ facing/폭탄 레인 가독성 ⑨ 사막 시즌 격자 ⑩ 스킬 조준 적색 미유출.
- **실기기 하단 확인**: 최하단 배치가능 행을 실제로 놓아 보고 하이라이트가 트레이에 안 가리는지.
  `BottomSafeRatio = 0.12` 는 트레이 높이의 근사라 실측이 최종 판정이다.
- **테스트 격리 문제(기존, 오프셋 무관)**: `DragPlacementReachTest` · `DropDismountTest` ·
  `RelocationPlacementSessionTest` 를 **한 묶음으로** 돌리면 `DropDismountTest` 의 탭-게이트 단언
  (`overrideSightings == 0`)이 깨진다. 각각 단독 실행은 전부 통과. 별도 추적 대상.
- **facing/폭탄 레인 `alphaMul` 바이패스** — 육안에서 적색이 안 읽히면 결정.
- README 후속 후보: 드림캐쳐 카드 드롭의 같은 문제 · 색약 대응 · 오프셋 dp 전환 · solid 승급 ·
  재배치 무효 표현 통일 · 램프 스칼라 통합.
