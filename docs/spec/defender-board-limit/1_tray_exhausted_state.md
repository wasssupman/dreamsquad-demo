# 1 — 트레이 소진 상태 (차단 + 탈색 + 테두리 순환)

## 목적

이미 상한만큼 나가 있는 유닛의 트레이 셀을 **죽은 칸**으로 만든다. 드래그/arm 세션을 시작하지 않고,
포트레이트가 탈색되고, 셀 테두리를 빛줄기가 돈다. 세 가지 "못 씀" 상태가 겹칠 때 무엇을 보여줄지도
여기서 확정한다.

> 이 단위까지는 소진 셀을 만져도 **아무 일이 안 일어난다**(차단만). 만졌을 때 판 위 그 유닛으로
> 데려가는 것은 unit 2 다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragSlot.cs` — 사전 차단
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 소진 표현·리페인트·튜토리얼 후보
- `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs` — knob
- `Assets/_Project/Shaders/SlotRimFlow_UI.shader` (신규)
- `Assets/_Project/Data/Materials/SlotRimFlow.mat` (신규, **공유 1개**)

## 구현

**사전 차단** — `OnBeginDrag` / `OnPointerClick` 의 **최상단**(쿨타임 체크보다 앞). 소진은 기다려도
안 풀리는 사유라 가장 먼저 걸러야 한다. 드래그는 기존 `_suppressedDrag` 패턴 그대로.

**리페인트 트리거 3곳** (폴링·버전 카운터를 만들지 않는다 — 계약 7):

1. `RebuildSlots` 말미 — 초기 페인트
2. `OnDefenderPlaced`(`PlacementCommitted` 구독, 이미 있음) — 배치로 소진
3. `bridge.DefenderDied` 구독(신규, `OnEnable`/`OnDisable` 대칭) — 사망으로 복귀

**표현** — `SlotVisual` 에 `rimFlow`(GameObject) 추가. 소진이면:

- 포트레이트에 회색 틴트를 곱한다(`Image.color`). UGUI 기본 Image 로 하는 것이라 **진짜 그레이스케일이
  아니라 채도가 죽은 것처럼 보이는 틴트**다 — 실제 채도 제거는 셰이더가 하나 더 필요해 하지 않는다.
  색은 `trayConfig.exhaustedPortraitTint`.
- `rimFlow` 활성 — 셀 전체를 덮는 `Image`(스프라이트 없음, 공유 머티리얼만, `raycastTarget = false`).
- 이름 밴드·코스트 칩은 **건드리지 않는다**(어느 유닛인지·얼마인지 계속 읽혀야 한다).

**우선순위 규칙 = 소진 > 쿨타임 > 코스트** — 소진이면 `cooldownRoot` 와 `warnGlyph` 를 **끈다**.
⚠ 기존 `Update` 의 코스트 리페인트가 `warnGlyph` 를 다시 켜므로, **그 경로에도 소진 가드를 넣어야
한다.** 한쪽만 고치면 한 프레임 뒤 경고가 되살아난다.

**셰이더** `Wassup/UI/SlotRimFlow` — `UICordShine.shader` 골격 재사용(UI/Default 기반이라 스텐실·마스크
클립 호환이 이미 풀려 있다). 프래그먼트에서 둘만 계산한다:

- **둥근 사각 테두리 마스크** — 셀 가장자리로부터의 거리(SDF). 테두리 모양을 셰이더가 그리므로
  `UiRoundedSprite.Make` 로 테두리 텍스처를 굽지 않는다.
- **도는 밴드** — 셀 중심 기준 각도(종횡비 보정)를 시간으로 밀어 밝기 밴드를 순환. 꼬리는 각도 거리
  감쇠.

`uv0` 만 읽는다 — `Canvas.additionalShaderChannels` 를 건드릴 필요가 없다(이 프로젝트가 한 번 밟은
지뢰). **머티리얼은 전 슬롯이 하나를 공유**한다: 슬롯마다 달라질 유니폼이 없고 애니메이션은 셰이더
내부 시간이다. 쿨타임 오버레이는 셀마다 `_Fill` 이 달라 **슬롯당** 인스턴스를 뜨는데, 이쪽은 1개다.

단 공유 에셋을 그대로 쓰지는 않고 **인스턴스 1개**를 뜬다 — 셀 종횡비(`_Aspect`)는 런타임에 밀어야
하는데(페이즈 전환으로 셀 높이가 바뀐다) 에디터에서 공유 에셋에 `SetFloat` 하면 `.mat` 파일 자체가
더럽혀지기 때문이다. 수명은 기존 `OnDestroy`(쿨타임 머티리얼 정리 자리)에 한 줄로 합친다.

**knob** — **룩은 머티리얼 에셋(`SlotRimFlow.mat`)이 소유**하고 config 는 `rimFlowMaterial` 참조와
`exhaustedPortraitTint` 둘만 갖는다. 색·두께·속도·밴드 수를 config 에도 두면 진실원이 둘이 된다 —
이 셰이더는 소비처가 하나뿐이라(쿨타임 액체가 코스트 물통과 색·방향을 달리해야 했던 것과 다르다)
역할 구분용 오버라이드가 필요 없다. 머티리얼도 데이터 에셋이라 제약 6(하드코딩 금지)을 만족한다.
코드가 미는 유니폼은 `_Aspect` 하나뿐 — 그건 룩이 아니라 기하다.

**튜토리얼** — `TryGetAffordableTutorialSlot` 이 소진 슬롯을 후보에서 제외한다(놓을 수 없는 칸을
가리키지 않게).

## 완료 기준

- 컴파일 통과. 셰이더 컴파일 경고 0.
- Play 육안: 유닛 배치 → 그 셀이 탈색되고 테두리 빛이 돈다. **0.3초 간격 스크린샷 2장에서 빛의 위치가
  다르다**(정지 이미지 1장으로는 "돈다"를 증명할 수 없다).
- 소진 셀에서 드래그/탭 시 프리뷰·슬로모·arm 이 시작되지 않는다.
- 소진 셀에 쿨타임 오버레이와 코스트 경고 글리프가 **동시에 뜨지 않는다**. 코스트가 부족해지는
  프레임에도 되살아나지 않는다(`Update` 가드 확인).
- 유닛 사망 → 같은 셀의 탈색·테두리가 사라지고 정상 셀로 돌아온다.
- `maxOnBoard = 100` 유닛의 셀은 이 표현이 **한 번도** 뜨지 않는다.
- 이름 밴드·코스트 칩이 소진 상태에서도 읽힌다.

> **확인 2026-08-13** · 커밋 `9b629bfd`(구현) · `e8cb3f50`(리뷰 반영) — 사용자 Play 확인 완료.
> 육안 확인은 레인저 `maxOnBoard=2` · `placementCooldown=4` 조합으로 했다: 1기 배치 → 쿨타임
> 오버레이(아직 소진 아님) → 4초 뒤 2기 배치 → **테두리 순환 + 쿨타임 억제**. 우선순위 규칙이
> 눈으로 갈리는 자리는 이 조합뿐이다(상한 1 이면 쿨타임이 뜨지도 않는다 — 계약 10).
> 자동 검증은 `BoardLimitTrayStateTest` 가 같은 시퀀스를 덮는다.
