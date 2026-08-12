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
지뢰). **머티리얼은 공유 에셋 1개**로 충분하다: 슬롯마다 달라질 유니폼이 없고 애니메이션은 셰이더
내부 시간이다. 쿨타임 오버레이는 셀마다 `_Fill` 이 달라 인스턴스를 뜨고 `RebuildSlots` 에 누수 방지
`Destroy` 가 붙어 있는데, **이 이펙트는 그 코드가 아예 안 생긴다.**

**knob** (`BattleHudTrayConfig`, 하드코딩 금지 — 제약 6):
`exhaustedPortraitTint` · `rimFlowMaterial` · `rimFlowColor` · `rimThickness` · `rimSpeed`(회/초) ·
`rimBandCount` · `rimTailLength` · `rimStrength`. 기본값은 "느리게 도는 2가닥, 은은한 밝기"로 잡고
육안으로 맞춘다.

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
