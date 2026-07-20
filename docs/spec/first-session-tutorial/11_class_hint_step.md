# 11 — 클래스 안내 스텝

## 목적

첫 배치 성공 직후, 방어 유닛 4+1 클래스가 무엇을 하는지 한 번 읽히고 넘어간다.
탭으로 넘긴다(사용자 결정 2026-07-21).

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceView.cs`
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs`

## 구현

### 탭 seam — `TutorialGuidanceView`

이 뷰는 지금 Skip 버튼 외에 레이캐스트를 받는 요소가 없다(모든 Image 가 `raycastTarget = false`).
탭으로 진행하려면 받는 지점이 필요하다.

```csharp
public event Action ContinueTapped;
public void SetTapToContinue(bool active);
```

- `SafeAreaRoot` 가 아니라 `FullBleedRoot` 아래에 풀스크린 `Image`
  (`color = (0,0,0,0.35)`, `raycastTarget = true`) 하나를 lazy 생성하고 `SetActive` 로 토글한다.
  **완전 투명으로 두지 않는다** — 차단 중이라는 시각 신호가 없으면 유닛을 탭했을 때 무반응이
  버그로 읽힌다. 약한 dim 이 "지금은 읽는 시간"을 알린다.
- 탭 수신은 `Button` 이 아니라 **`IPointerDownHandler`** 로 한다(`OutgameTutorialTapZone` 재사용).
  `Button` 은 드래그 임계를 넘기면 클릭이 취소되는데, 배치 화면은 드래그가 일상이다.
- 기본 비활성. `Hide()` 는 이것도 함께 끈다.

**캐처를 `FullBleedRoot` 아래에 두는 것이 Skip 을 살리는 조건이다.** `UiCanvasSetup.Ensure` 는
`FullBleedRoot` → `SafeAreaRoot` 순으로 자식을 만들고, 같은 캔버스 안에서는 **나중 sibling 이 렌더와
레이캐스트를 둘 다 이긴다**. Skip 은 `SafeAreaRoot` 하위이므로 캐처보다 위에 남아 계속 눌린다.
캐처를 `SafeAreaRoot` 아래나 Skip 뒤 sibling 으로 두면 **이탈구가 사라진다.**

> 이 스텝 동안에는 배치 입력이 막힌다. spec 의 "입력은 항상 열려있다" 계약에서 **의도적으로
> 벗어나는 유일한 구간**이며, 읽고 탭하는 것 말고 할 일이 없게 만드는 것이 목적이다.
> Start 는 아직 잠겨 있고(`UnlockTutorialStart` 는 `BeginStart` 소유) 카운트다운도 hold 상태다.

### 새 스텝 — `CoreStep.ClassHint`

enum 에 값을 추가한다: `{ None, Goal, Pick, Place, WaitingAim, ClassHint, Start }`.

**주의**: `_coreStep == CoreStep.Start` 를 "종료 상태"로 쓰는 negative 가드는
`OnUserDragStarted`·`OnPlacementCommitted` **2곳**이다. 여기에만 `ClassHint` 를 함께 묶는다 —
안내를 읽는 중에 배치 이벤트가 들어와 스텝이 뒤로 되감기면 안 된다.
`OnDisarmed`/`OnArmed` 는 `== Place` / `== Goal|Pick` 양성 검사라 ClassHint 에서 이미 아무 일도
하지 않는다(손대지 말 것). `BeginStart` 자신의 멱등 가드(`_coreStep == Start`)에도 넣으면 안 된다 —
넣으면 탭 진행이 죽는다.

전이:

- `OnPlacementCommitted` 의 `BeginStart()` 호출 → `BeginClassHint()` 로 교체
- `Update()` 의 `WaitingAim` → `BeginStart()` 경로도 `BeginClassHint()` 로 교체
- `ContinueTapped` → `BeginStart()`

```csharp
private void BeginClassHint()
{
    if (!_coreActive || _coreStep == CoreStep.ClassHint) return;   // 형제 메서드와 같은 멱등 가드
    _coreStep = CoreStep.ClassHint;
    guidance.ClearFocus();
    guidance.ShowMessage(ClassHintText, showSkip: true);
    guidance.SetTapToContinue(true);
    _classHintRoutine = StartCoroutine(ClassHintFallbackRoutine());   // 12초
}
```

**시간 만료 안전장치가 필수다.** 이 교체로 `BeginStart()` 호출처가 `ContinueTapped` 하나만 남는데,
탭 수신이 실패하면 `UnlockTutorialStart()` 가 영영 안 불려 카운트다운이 무기한 hold 되고 캐처가
배치까지 막아 **첫 판이 Skip 외 탈출 불가**가 된다. 12초 만료 시 `BeginStart()` 로 자동 진행한다.

`BeginStart()` 초입과 `EndCore()` 에서 `guidance.SetTapToContinue(false)`. Skip 은 그대로 노출한다
(`showSkip: true`) — 탭 캐처가 화면을 덮으므로 Skip 이 유일한 이탈구다.

### 문구

```
수 가디언 · 적을 붙잡아 두는 방패
근 파이터 · 붙어서 때리는 주먹
원 레인저 · 멀리서 쏘는 사수
술 캐스터 · 바닥에 장판·바리케이드 설치
보 서포터 · 치유와 강화로 아군 보조
상황에 맞게 골라서 배치해보세요.
```

**배지 글리프를 앵커로 박는다.** 배치 트레이의 role 배지는 `원/수/근/술/보` 단일 글자이고
(`BattleHudTrayConfig.roles`), 클래스 이름과 겹치는 글자가 하나도 없다. 앵커가 없으면 6줄을
읽어도 트레이에서 찾을 수가 없어 이 스텝의 가치가 0이 된다.

**캐스터는 4종 중 `BlockingCaster` 만 경로를 막는다**(`hazardCastKind: 2`). Fire/Ice/Poison 은
장판(`hazardCastKind: 1`)이고 기본 스쿼드에 Blocking·Fire 가 **둘 다** 들어있다. 두 갈래를 모두 적는다.

`어그로`·`탱커` 대신 게임이 실제로 쓰는 어휘 쪽으로 낮췄다(유닛 desc 는 `도발`).

`Fighter`(Bruiser) 를 포함한다(사용자 결정 2026-07-21) — Bruiser 는 기본 스쿼드 7개에 들어있다.

> **서포터는 첫 판 스쿼드에 없다.** 기본 스쿼드는 카탈로그 앞 7개(Archer·Bastion·BlockingCaster·
> Bruiser·Cannon·FireCaster·Guardian)이고 유일한 Support 인 Healer 는 8번째다. 즉 첫 판 트레이에
> `보` 배지는 한 칸도 없다. 사용자 요청대로 유지하되, 나중에 만날 클래스를 미리 알리는 줄임을 인지할 것.

> 6줄 말풍선 높이는 폰트/기기별 랩핑에 좌우된다(텍스트 폭 = 880 − 56 = 824, 폰트 42). 스펙에 픽셀
> 수치를 단정하지 않는다. 완료 기준은 실측 스크린샷으로 확인한다.

> 클래스 라벨 표기는 4중으로 갈려 있다 — 배지 `보`, 코드 `UnitLabels.ClassLabel` = `서포트`,
> 유닛 desc = `서포트 · 아군 치유형`, 안내문 = `서포터`. 통일은 이 unit 범위 밖이며 README 후속 후보.

## 완료 기준

- [ ] 컴파일 통과
- [ ] 첫 배치 성공 직후 6줄 안내가 뜨고, **화면 아무 곳이나 탭하면** 기존 Start 안내로 넘어간다
- [ ] 안내 중에는 유닛 배치·슬롯 선택이 되지 않는다
- [ ] 안내 중 Start 버튼은 계속 숨겨져 있고 카운트다운은 `배치 연습` 으로 hold 된다
- [ ] 방향 지정 유닛(조준 필요)을 배치한 경우에도 조준 종료 후 이 스텝을 거친다
- [ ] 안내 중 Skip → 정상 종료되고 탭 캐처가 남지 않는다(로비 복귀 후 배치 입력 정상)
- [ ] 탭 없이 12초 방치 → 자동으로 Start 안내로 넘어가고 카운트다운이 재개된다
- [ ] 차단 중임이 화면에 보인다(약한 dim) — 무반응이 버그로 읽히지 않는다
- [ ] 말풍선이 safe area 안에 들어오고 상단 HUD 를 가리지 않는다(스크린샷)
- [ ] EditMode `TutorialDragGuidanceTests` 회귀 없음
