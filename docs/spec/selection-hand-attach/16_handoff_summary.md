# 16 — Handoff Summary (units 9~15 · 선택 모드 프레젠테이션 재설계)

> 2026-07-30. units 0~8 인계는 `6_handoff_summary.md`. 이 문서는 그 **이후**만 다룬다.

## 지금 상태 한 줄

units 9~15 구현·커밋 완료. **핵심 체감은 사용자 Play 확인을 받았고**(카메라·이동 버튼·
각성 버튼), 세부 항목 몇 개와 `unit 5` e2e 전량 훑기가 남았다. **푸시 전이다.**

## Commit

| 해시 | 내용 |
|---|---|
| `95d5ffdb` | unit 9 — 선택 중 각성 버튼 = 기본 전투 상태 복귀 |
| `b3c95f0d` | units 10~13 스펙 + 계약 7 개정·11~13 추가 |
| `6222425b` | unit 10 — 실효 스탯 read seam + 표시값 결정 순수 함수 |
| `f3944e9b` | unit 11 — 패널 좌측 고정 도킹 + 스탯 3종 + 델타 |
| `e422deb7` | unit 11 rev — 부착 셀에 효과 설명 |
| `40dda1ec` | unit 11 rev2 — 폰트 확대(40대 가독성) |
| `b20961bd` | unit 11 rev3 — `항상 →` 만 떼고 조건부 트리거 유지 |
| `91ba16d4` | unit 12 — 선택 모드 콜아웃 생략 |
| `ee265d25` · `7784c01a` · `5b0b09d5` · `33eb92d3` | unit 13 — FOV·전환 스무딩 → pitch 도입 → **철회** → 프레이밍 바이어스 → 확정 |
| `e040056e` | unit 14 — 부착 거절 시 카메라 킥 |
| `07ad0c53` | unit 15 — 이동 버튼을 패널로, 플립북 폐기 |

## Implemented

- 선택 중 **각성 버튼 = 그만하기**(계약 7 의 두 번째 예외). "선택 있음 + 손패 닫힘"은 도달 불가.
- `BattleBridge.TryGetUnitStatReadout` — 체력/공격력/공격속도 실효값 + 기본값(델타용).
- 상세 패널을 **좌측 고정 도킹**으로 재설계(앵커 추종·`Follow`·`LateUpdate` 전부 제거).
  스탯 3행 + 델타 칩(변화 없으면 숨김) + 부착 목록(효과 설명 포함).
- 선택 모드에서 **콜아웃 생략** — 이름·개수는 패널이 나른다. 조준 콜아웃은 불변.
- 카메라: `dolly 3` + `fovDelta -6` + 전환 NDC 추종 + `frameBiasY 0.35`.
- 부착 거절 시 **짧은 카메라 킥**(게이트 없는 `FeedbackKick`).
- **이동 버튼을 패널 하단으로** — 유닛 주변 플립북 폐기, 재배치 경로 복구.

## Key Files

- `Scripts/Data/UnitStatReadout.cs` — `UnitStatReadout` + `UnitStatMath`(순수, EditMode 9건)
- `Scripts/Bridge/BattleBridge.UnitStats.cs` — 읽기 창구(신설 partial)
- `Scripts/UI/Dreamcatcher/DcInspectPanelView.cs` — 패널 전면 재작성
- `Scripts/UI/Dreamcatcher/DcInspectController.cs` — 패널/스탯/이동 배선
- `Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` — `EffectOnly` + `AlwaysPrefix`
- `Scripts/Presentation/CameraDirector.cs` — NDC 추종 · `FeedbackKick`
- `Data/Camera/CameraDirectionConfig.asset` — 인스펙트 값

## Verified

- `dotnet build` 오류 0 · Unity 콘솔 error 0 (매 unit)
- EditMode `UnitStatReadoutTests` **9/9**. 전체 1589건 중 실패 1건은
  `MultiGoalPoolSeparationTests`(세션 시작부터 dirty 였던 **타 세션의 `MapDocument_Zig` 편집**) — 무관.
- **`Assets/_Project/Scripts/Battle/` diff 0** — 커밋 15건 전부 ECS 무변경으로 확인.
- 오프스크린 RT 렌더로 레이아웃 검증(결함 3건을 여기서 잡았다 — 아래 Notes).
- 사용자 Play: unit 9 기본 동작 · unit 13 카메라 체감 · unit 15 이동 버튼.

## Notes (되돌리면 안 되는 의도)

1. **카메라 pitch 음수 = 보드가 화면 아래로 내려간다**(1도당 약 25px). 선택 중에는 손패가
   항상 열려 헤드룸 -2 가 걸려 있어, `inspectPitchDeg` 를 켜면 하단 배치 유닛이 손패에 깔린다.
   **극적 틸트와 손패 상시 개방은 양립 불가** — 값 조정으로 안 끝난다. 부각은 각도가 아니라
   `inspectFrameBiasY`(프레이밍)로 한다. `inspectPitchDeg` 는 0 유지.
2. **`CameraDirector.Kick()` 은 `enableNonDragEffects` 게이트에 막혀 지금 no-op 이다.**
   사용자 행동 응답 피드백은 `FeedbackKick` 을 쓴다. 그 토글을 켜서 해결하지 말 것 —
   앰비언트 연출(브리딩·비행·펄스)이 통째로 살아난다.
3. **표시를 위해 sim 을 리팩터하지 않는다.** 최종 스탯은 저장되지 않고 `AttackSystem` 이
   소비 시점에 곱한다. 표시가 틀리면 숫자만 틀리지만 sim 회귀는 판정·밸런스가 틀린다.
   표시값은 **"조건 없는 타격당 피해"** — 조건부 배율은 넣을 수 없다(사양).
4. **패널의 `raycastTarget` 은 이동 버튼 하나뿐이다.** 다른 그래픽에 켜면 카드 드롭/조준이
   좌측에서 막힌다. `blocksRaycasts` 는 `_visible` 과 함께 움직여야 한다.
5. **값 라벨은 `NoWrap` + autosize.** 풀면 `340 / 420` 이 두 줄로 접혀 HP 게이지를 침범한다.
6. **`AlwaysPrefix` 는 생산자·소비자 공유 상수.** 리터럴로 흩으면 조용히 어긋난다.
7. **텍스트 상자 높이는 폰트 파생**(`fontSize × 1.3`). 상수로 두면 폰트를 키울 때 잘린다.

## 공간 실측 (추정 금지)

`BattleHudTrayConfig placementSize (980,136)` + `anchoredY 32` → **트레이 상단 y 912**,
가로 470~1450. 패널 x `24~454` 라 **가로로 안 겹친다**. 세로 여유 일반 **68.7px** /
최악 **33.1px**. 이전 세션들이 "~870" 으로 추측하며 세로를 과도하게 아꼈다 — 다시 재지 말 것.

## Follow-up

1. **`unit 5` Play e2e 전량** — 시나리오 1~12 + units 7·8·9 완료 기준. 미확인 잔여:
   델타 칩 실동작 · 거절 킥 세기 · 패널 폰트 충분성 · 버튼 밖 패널 영역의 카드 드롭 통과.
2. **units 11~15 반영 후 EditMode 전체 재실행** — Play 모드/테스트 실행 중이라 못 돌렸다.
3. **`DcActionFlipbookView` + 씬 `DcActionFlipbook` 제거** — 지금 inert. 씬을 안전하게
   저장할 수 있을 때(타 세션 WIP 해소 후) 오브젝트+클래스를 함께 지운다.
4. **효과 3개 카드의 설명 잘림** — `descMaxLines 2` 라 시한폭탄은 첫 효과만 보인다.
   3 으로 올리면 최악 패널이 트레이를 침범한다. 체감 후 판단.
5. **푸시 미실행** — 커밋 15건이 로컬에만 있다.

## 환경 주의

- **같은 워크트리에 다른 세션이 동시 작업 중**이다(투사체 발사 패턴 · 킨들러 적).
  `Scripts/Battle/` 의 dirty 는 그쪽 것이다 — 스테이징은 **경로 명시**로만.
- `BattleScene.unity` 는 타 세션 WIP 로 dirty 하다. **저장 금지.**
- 신규 `.cs` 는 csproj 에 없어 `dotnet build` 가 조용히 건너뛴다(1초 빌드 = 신호).
  `refresh_unity(scope=all)` 로 임포트한 뒤 콘솔을 봐야 진짜 검증이다.
