# 3 — 툴팁 위치를 화면 상단 중앙 고정으로 (rev 5)

## 목적

모바일 실기기에서 카드를 press/스와이프할 때 툴팁이 **손에 가려 읽히지 않는다**(사용자 보고
2026-07-21). rev 1 의 "선택 카드 우측 + 손패 바로 위(`tooltipRise=90`)" 배치는 손패와 같은
하단 대역에 있어, 오른손 그립의 손·손가락 occlusion 영역 안에 그대로 들어간다.

위치를 **화면 상단 중앙 고정**으로 옮긴다. 손패는 하단 앵커이므로 상단 대역에는 손이 닿지
않는다 — 손잡이(좌/우) 무관하게 가림이 구조적으로 해소된다.

### 왜 포인터 추종이 아닌가 (사용자 판단 2026-07-21)

포인터 추종(+상단 offset)을 함께 검토했고 기각했다. 근거:

- **추종의 최선이 곧 상단 고정이다.** 손가락이 보드로 올라가면 패널이 화면 상단에 닿아
  클램프되어 결국 고정 위치로 degrade 한다. 추종은 거기에 흔들림·배선 비용만 얹는다.
- **읽기 안정성.** 본문은 다줄 텍스트(`DreamcatcherCardText.Body`)다. 조준 중 손가락은 쉬지
  않으므로, 추종하면 읽는 대상이 계속 흔들린다.
- **추종의 유일한 강점(연결감)이 여기선 무가치.** 툴팁은 동시에 1개만 뜨고 `OnPointerDown`
  즉시 뜬다 — "어느 카드 설명인가"는 시간적 결합으로 이미 확정이라 해소할 모호함이 없다.
- **가리는 대상이 나쁘다.** 손가락 바로 위 = 지금 조준하려는 유닛이 있는 곳이다. 상단 중앙은
  Battle 중 비어 있다.
- **시선 경로.** 조준 구간의 시선은 보드(화면 중상단)에 있다. 손패 옆 낮은 위치가 오히려
  시선에서 멀다.

### 배치 여유 (조사 완료)

Battle 중 상단 점유는 **우상단 `ScoreHudView` 396x278 하나뿐**이다. 툴팁 폭 480 을 중앙에
두면 우측 끝 +240, 스코어 패널 좌측 끝은 `safeWidth/2 - 396` — 4:3(1440) 에서도 324 로
겹치지 않는다(가로 전용 프로젝트). 보스 워닝은 화면 **정중앙**(900x240)이라 무관하다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`
  - 튜닝 필드 L82-86
  - `BuildTooltip()` 앵커/피벗 L832-834
  - `ShowDragTooltip()` 위치 계산 L909-911
  - `CardPeeked` 이벤트 신설 (튜토리얼 배너 해제 신호 — 아래 "해결된 충돌" 참조)
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs`
  — `CardPeeked` 구독 + `OnCardPeeked` + `_cardInstructionShowing` 단계 플래그
- `docs/spec/dreamcatcher-hand-drag-tooltip/README.md` — 위치 계약 rev 5 로 갱신

## 구현

1. **앵커/피벗 전환** (`BuildTooltip`)
   - `anchorMin = anchorMax = (0.5f, 1f)` — 상단 중앙
   - `pivot = (0.5f, 1f)` — 상단 중앙
   - 부모는 `SafeAreaRoot` 유지. 이미 세이프에어리어가 적용된 사각형이므로 노치 대응이
     앵커만으로 끝난다.
   - 피벗이 상단이 되면 패널이 **아래로** 자란다 → 카드마다 설명 길이가 달라도 **상단
     모서리(읽기 시작점)가 고정**된다. 의도된 부수효과다.

2. **위치 계산 교체** (`ShowDragTooltip` L907-918)
   - 슬롯 파생 계산(`halfCard` / 좌우 플립 / `panelY + homePos.y + rise`)을 전부 제거한다.
   - `_tooltipBasePos = new Vector2(0f, -tooltipTopOffset)` 로 단순화.
   - `slotIndex` 는 여전히 **내용**(카드/코스트/본문)에만 쓰인다. 위치와 무관해진다.

3. **튜닝 필드 정리**
   - `tooltipGap`, `tooltipRise` 제거(유일 소비처가 삭제된 계산 블록이다).
   - `tooltipTopOffset = 40f` 신설 — 세이프에어리어 상단 모서리에서의 여백.
   - `tooltipWidth`, `tooltipBob*` 은 유지.

4. **bob 유지**
   - 카드 idle bob 문법의 둥실거림은 그대로 둔다(HUD 가 아니라 플로팅 패널이라는 신호).
   - 진폭이 상단에서 과해 보이면 `tooltipBobY/X` 를 낮춘다 — 인스펙터 knob 으로 남긴다.

**하지 않는 것**: 트리거(press 기반) / 숨김 깔때기(`EndInteraction`) / 비간섭
(`raycastTarget=false`) / 텍스트 소스 계약은 손대지 않는다. 위치만 바꾸는 변경이다.

## 커밋 분할 (주의)

이 unit 의 변경은 **커밋 2개에 나뉘어 있다**. 병행 세션이 작업 중인 워크트리라 위치 변경분이
남의 커밋에 쓸려 들어갔다(사용자 판단 2026-07-21 — 코드는 정상이므로 히스토리는 정리하지
않고 그대로 둔다).

- **`7a33ab7d`** `fix(score-tally-sequence): 코드 리뷰 반영 — 마지막 킬 연출 유실 외 6건`
  — 제목과 달리 `DreamcatcherHandView.cs` 의 **툴팁 위치 변경 전량**(튜닝 필드·앵커/피벗·
  위치 계산)이 여기 들어 있다. score-tally 와 무관하다.
- **이 unit 의 커밋** — `CardPeeked` 이벤트, 튜토리얼 배너 조기 해제, spec 문서.

위치 변경의 이력을 찾을 때 `git log` 제목으로는 못 찾는다. `git log -S"tooltipTopOffset"` 로 찾을 것.

## 완료 기준

- [x] 컴파일 통과, 콘솔 에러 0
- [x] Play — Battle 페이즈에서 손패 카드 press 시 툴팁이 **화면 상단 중앙**에 뜬다
- [x] 설명 길이가 다른 카드 2종 이상을 press 했을 때 **상단 모서리 y 가 동일**하다
      (아래로만 자란다)
- [x] 손패 좌·중·우 끝 슬롯 전부에서 툴팁 x 가 동일하다(좌우 플립 잔재 없음)
- [x] 우상단 `ScoreHudView`(점수/스트레스 배지)와 겹치지 않는다 — 스코어 캔버스가
      sortingOrder 6 으로 더 위라 겹치면 툴팁이 잘려 보인다
- [x] 드래그로 보드를 조준하는 동안 툴팁이 조준 지점을 가리지 않는다
- [x] 커밋/취소/손패 닫힘/페이즈 이탈에서 기존과 동일하게 사라진다(회귀 없음)
- [x] **첫 세션 튜토리얼** — 신규 프로필로 첫 판 진입 시 "끌어보세요" 배너가 카드 press
      순간 즉시 걷히고 툴팁이 온전히 보이는지 확인 (아래 "해결된 충돌" 참조)
- [x] Placement 카운트다운 배너와는 배타적 페이즈임을 Play 로 확인
      (손패는 Battle 전용이므로 이론상 충돌 없음)

## 해결된 충돌 — 첫 세션 튜토리얼 배너 (코드 리뷰 2026-07-21 적발 → 같은 unit 에서 수정)

unit 3 초안은 "상단 중앙을 쓰는 다른 요소는 전부 Battle 과 배타적 페이즈"라고 전제했으나
**사실이 아니었다**. `FirstSessionTutorialController.OnHandOpened` 는 **Battle 페이즈에서
손패가 열리고 usable 슬롯이 있을 때** `"포커스된 카드를 원하는 캐릭터로 끌어보세요!"` 를
`guidance.CardInstructionSeconds`(기본 3초) 동안 띄운다.

| 요소 | 좌표(세이프 상단 기준) | 캔버스 |
|---|---|---|
| 드래그 툴팁 | y −40 ~ −(40+H), 폭 480 | sortingOrder **5** |
| 튜토리얼 배너 | y −184 ~ −300, 880x116 | sortingOrder **10** |

배너가 툴팁 **위에** 그려져 본문 하단 `H−144`px 를 덮는다. 최장 카드
`Card_IncubusPact`(H≈280) 기준 **116px**(설명문 4~5줄)이 가려진다. 하필 툴팁이 가장
필요한 대상(카드를 처음 보는 신규 플레이어)이 정확히 이 상황에 있다.

영향 범위는 **프로필당 1회, 3초**다 — `ShouldRunAwakeningHint` 게이트가 있고 표시 즉시
`CompleteAwakeningHint` 가 기록된다. 전면 dim 도 없다(`FocusUi` 는 카드 주위 링/포인터만).

검토한 선택지:

- **(A) 튜토리얼 배너를 card press 시 즉시 해제** ← **채택 (사용자 결정 2026-07-21)**.
  지시("끌어보세요")를 플레이어가 이행한 순간이므로 정보 가치가 소진됐다. 툴팁과 무관하게도
  타당한 개선이다.
- **(B) 툴팁을 배너 아래로 상시 이동**(`tooltipTopOffset` ≈ 320) — 첫 세션 3초를 위해
  전 플레이어의 상시 배치를 희생한다. 최장 카드 하단이 −600 까지 내려가 보스 워닝
  (−420~−660)과 새로 충돌한다. **기각**.
- **(C) 툴팁 sortingOrder 를 배너 위로** — 툴팁은 손패 캔버스를 공유하므로 손패 전체가
  튜토리얼 위로 올라가 dim/홀 컷아웃을 깨뜨린다. README 계약("새 캔버스 신설 금지")과도
  충돌. **기각**.

### (A) 구현

- `DreamcatcherHandView` 에 `public event Action CardPeeked` 신설 — `ShowDragTooltip` 이
  실제로 툴팁을 띄운 직후 발화한다. 툴팁이 안 뜨는 press(`CanPeek` 거부)는 가림도 없으므로
  발화하지 않는 것이 맞다.
- `FirstSessionTutorialController` 가 구독해 `OnCardPeeked` 에서 남은 대기를 버리고
  `guidance.Hide()`.
- `_cardInstructionShowing` 플래그를 신설했다. `_awakeningRoutine` 은 A 단계 프롬프트와
  카드 지시가 **공유**하는 필드라, 플래그 없이 걷으면 다른 단계를 잘못 끌 수 있다.
  `ResetAwakeningSession` 에서 함께 리셋한다.

이 변경은 `first-session-tutorial` 범위를 건드리지만, 원인이 unit 3 의 위치 이동이므로
같은 unit 에서 처리한다.

## 검증된 사항 (코드 리뷰 2026-07-21)

- **피벗 전환 무해**: UGUI 에서 자식 앵커 기준점은 부모 `rect` 로 산출되고, 피벗을 바꾸면
  부모 사각형과 자식 기준점이 같은 양만큼 함께 이동한다 → top-anchored 자식 스택의 상대
  배치 불변. 등장 스케일(0.92→1)도 이제 상단 모서리를 고정한 채 펴져 rev 5 의도와 일관.
- **ScoreHudView 겹침 없음**: 세로는 겹치나(스코어 36~278 vs 툴팁 40~321) 가로가 보호한다.
  겹침 임계 = `safeW < 1278`(종횡비 < 1.183). 가로 전용 최소 4:3(safeW 1440)에서 **여유
  81px**. 단 여유가 얇으므로 `tooltipWidth`(480) ↔ `ScoreHudView.plateSize.x`(360) +
  `cornerPadding`(36) 은 **결합된 상수**다. 툴팁 폭을 642 이상으로 올리면 즉시 깨진다.
- **손패·보스워닝 여유**: 최장 카드 H≈280 → 툴팁 하단 −327. 손패 상단 ≈−748(여유 420),
  보스 워닝 −420~−660(여유 93). 겹침 없음.
- **bob 누적 없음**: `TickTooltip` 은 `+=` 가 아니라 `_tooltipBasePos + bob` 대입.
- **씬 stale 필드 무해 실증**: 씬 재저장 시 `tooltipGap`/`tooltipRise` 가 소멸하고
  `tooltipTopOffset: 40` 이 기록됨을 관측. YAML 에 키가 없는 동안에도 필드 이니셜라이저
  `= 40f` 가 적용된다(Unity 는 객체 생성 후 직렬화 값을 덮어쓴다).
- [x] **모바일 실기기** — 오른손 그립에서 손에 가리지 않고 읽힌다 (원 결함의 검증)

확인: 2026-07-21 사용자 Play 확인("잘 나온다") — 커밋 `83ab82b4`
(위치 변경 코드는 `7a33ab7d` 에 혼입 — 위 "커밋 분할" 참조).
