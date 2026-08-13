# 11 — Handoff (units 8~10 · 대가 모델 개정)

units 0~6 의 인계는 `7_handoff_summary.md`. 이 문서는 rev 묶음만 다룬다.

## Commit

- `62ce01e6` docs — 스펙 개정 (계약 1·3·4·8·10·11 rev + 12·13 신설, units 8~10)
- `568d2f9f` unit 8 — 코스트 차감 + 배치 스킬 재발동 + 체력 50% 회복
- `1e100459` unit 9 — 제자리 재정비(같은 칸 확정) + 취소 경로 Strict 전환
- `5de9d07f` fix — 이동모드 하이라이트가 안 뜨던 **선행 버그**(재배치 스펙 밖)
- `1e7a3b9e` unit 10 — 코스트 잠금 + 드래그 지름길(첫 판본)
- `088030d7` fix — 지름길 **표면 정정**: 보드 타일 → 트레이 초상화

## Implemented

- 재배치가 확정 프레임에 `unitData.cost` 전액을 낸다. 순서 = 판정 → 차감 → 스왑.
- 착지 후 활성화 시점에 밀치기 + on-place 재발동 + `max*refitHealRatio`(기본 0.5) 회복이
  한 꼬리(`ActivateRelocatedDefender`)에서 일어난다.
- 효과 타일은 `_effectTileAppliedEntities` 자기 가드로 엔티티당 1회에 남는다(재무장에 딸려오지 않음).
- 같은 칸이 확정이다(제자리 재정비). 취소는 보드 밖 릴리즈 + 타임아웃.
- 이동 버튼이 코스트/쿨다운/페이즈를 매 프레임 읽어 잠기고 풀린다. 라벨에 코스트 표기.
- **소진된 트레이 슬롯: 탭 = 데려가기, 드래그 = 집어들기(이동모드).**

## Key Files

- `Bridge/BattleBridge.Relocation.cs` — 코스트 게이트/차감 · 재무장 · `ActivateRelocatedDefender` · `ApplyRefitHeal`
- `Bridge/BattleBridge.cs` — `ApplyEffectTileOnce`(가드 분리) · `ShowPlacementHighlight(unit, extraCell)`
- `UI/DefenderRelocationController.cs` — `CanBeginMoveModeFor` · carried-press · Strict 릴리즈/hover
- `UI/DefenderDragSlot.cs` — `TryBeginRelocationFromSlot`(드래그 분기)
- `UI/DefenderDragPlacementController.cs` — 하이라이트 자기치유를 **켜는 방향만**으로 축소
- `UI/Dreamcatcher/DcInspectPanelView.cs` — `SetMoveState(enabled, cost)`

## Verified

- EditMode **2344 중 4 실패** — units 8·9·10 내내 **동일한 4건**(다른 세션 `map-rework` 의 통로 폭
  계약, `MultiGoalPoolSeparationTests`). 이 묶음이 만든 신규 회귀 **0**.
- PlayMode 재배치 스위트 **9/9**, `BoardLimit*` 포함 **9/9**.
- 로그 실측 `Refit heal 68 ... ratio 0.50` = 레인저 최대 체력 136 의 절반.
- 라이브 `RelocationSettings.asset` 은 YAML 에 키가 없어 이니셜라이저로 0.5 를 읽는다(에셋 수정 불요).
- **사용자 Play 확인 완료 2026-08-13** — 초상화 드래그로 집어들기 · 소스 칸 하이라이트 ·
  이동 버튼 코스트 잠금까지 눈으로 확인.

## Notes (되돌리면 안 되는 의도)

- **코스트 차감 순서**(판정 → 차감 → 스왑). 차감 뒤에 실패 경로가 생기면 코스트가 증발한다.
- **효과 타일 가드를 on-place 와 다시 합치지 말 것.** 이유는 "같은 효과가 겹쳐서" 가 **아니다** —
  같은 stat 은 병합키가 refresh 라 겹치지 않는다. 진짜 구멍은 `duration=∞` 인데 회수가 없다는
  것: 공속 타일 → 공격력 타일로 옮기면 공격력이 붙고 **공속이 영원히 남는다**.
- **`RelocationCheck` 의 `from == to` 검사를 `SpatialPlacementCheck` 뒤로 옮기지 말 것.** from 이
  아직 점유 집합에 있어 자기 자리가 `Occupied` 로 오판된다.
- **릴리즈/hover 는 `TryScreenToCellStrict`.** 관대한 변형은 보드 밖을 가장자리 칸으로 clamp 해
  true 를 주므로 "보드 밖 = 취소" 가 성립하지 않는다. 자기 칸 탭이 확정으로 넘어간 지금 즉시
  취소는 이 경로뿐이라, 헐거우면 8초 타임아웃 말곤 빠져나갈 길이 없다.
- **하이라이트 자기치유는 켜는 방향만.** 양방향으로 되돌리면 매 프레임 도는 배치 컨트롤러가
  재배치가 켠 하이라이트를 도로 끈다(그게 `5de9d07f` 로 고친 그 버그다).
- **`_placeableHlExtraCell` 은 인자가 아니라 상태.** 리페인트가 매번 처음부터 다시 계산한다.
- **소진 슬롯의 탭·드래그를 다시 합치지 말 것** — board-limit 계약 5 를 unit 10 이 의도적으로
  갈랐다. 드래그에 "저기로 옮긴다"는 자기 의미가 생겼기 때문이다.
- 기존 PlayMode 에서 "옮겨 갈 칸" 을 스캔할 땐 **소스 칸을 명시로 제외**한다(제자리도 유효 목적지).

## Follow-up

- **사용자 Play 육안 확인** — 초상화 드래그 / 소스 칸 하이라이트 / 이동 버튼 코스트 잠금.
- **보드 유닛 직접 드래그**는 상한 2+ 결정과 묶는다(README 후속 후보). **홀드로 만들지 말 것** —
  원안이 홀드였고 실패해서 걷어냈다.
- 회복량·코스트 배율 튜닝은 `RelocationSettings` 에서. 나머지는 README "후속 후보" 참조.
