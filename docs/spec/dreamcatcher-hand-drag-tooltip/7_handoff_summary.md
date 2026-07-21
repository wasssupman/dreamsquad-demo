# 7 — handoff summary (units 3·4·6)

> units 0~1 의 인계는 `2_handoff_summary.md`. 이 문서는 2026-07-21 의 위치·가독성
> 재작업(units 3·4·6)을 다룬다.

## Commit

| 커밋 | 내용 |
|---|---|
| `7a33ab7d` | **주의** — 제목은 score-tally 지만 툴팁 **위치 변경 전량**이 여기 있다(병행 세션이 쓸어담음) |
| `83ab82b4` | unit 3 — `CardPeeked` 이벤트 + 튜토리얼 배너 조기 해제 + spec |
| `bdcf030e` | unit 4 — `BodyCompact` + 패딩/간격/offset 압축 |
| `a47b53dd` | unit 6 — 폰트 확대 + 손패 연동 카메라 헤드룸 |

위치 변경 이력은 `git log` 제목으로 못 찾는다. `git log -S"tooltipTopOffset"` 을 쓸 것.

## Implemented

- 툴팁 위치: 카드 우측/손패 위 → **세이프에어리어 상단 중앙 고정**(슬롯 무관, 좌우 플립 없음)
- 상단 피벗이라 패널이 아래로 자란다 → 설명 길이와 무관하게 **읽기 시작점 고정**
- 첫 세션 튜토리얼 "끌어보세요" 배너를 **카드 press 시 조기 해제**(`CardPeeked` seam)
- 인게임 툴팁 전용 `BodyCompact` — 블록 사이 빈 줄 제거. 덱빌더 계열은 `Body()` 그대로
- 폰트 헤더 22→27, 본문 19→23. compact 의 축·타입 줄만 상대 `115%`
- `CameraDirector` 에 **손패 연동 헤드룸 채널** — 열림 중 pitch −2(60°→58°) + dolly −1.5,
  닫으면 스프링 복귀. 피드 주도 + 2프레임 자동 해제
- `KeyringSim.SpringStep` float 오버로드

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — 툴팁 build/show/tick,
  `CardPeeked` 발화, 헤드룸 피드(`Update`)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` — `Body`/`BodyCompact`
  (private `Assemble` 로 수렴)
- `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — `SetHandHeadroom` + 헤드룸 채널
- `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — 헤드룸 노브 4개
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — `OnCardPeeked`
- `Assets/_Project/Scenes/BattleScene.unity` — `tooltipTopOffset: 24`

## Verified

- 컴파일 0 에러 (각 unit 마다)
- EditMode **1135건 중 1133 통과 · 0 실패** (스킵 2 = 기존 의도적 Ignore)
- 사용자 Play/실기기 확인 완료 (2026-07-21, "이상없음")

## Notes (되돌리면 안 되는 것)

- **툴팁 폭 480 ↔ `ScoreHudView`(plate 360 + cornerPadding 36) 은 결합된 상수.**
  겹침 임계 `safeW < 1278`, 4:3 에서 여유 81px 뿐. 642 이상으로 올리면 즉시 겹치고
  손패 캔버스(5) < 스코어 캔버스(6) 라 툴팁이 잘려 보인다.
- **헤드룸은 피드 주도 + 자동 해제.** 명시적 `Release()` 로 바꾸면 teardown 경로 하나만
  빠뜨려도 카메라가 기운 채 남는다. `headroomActive` 를 `anyActive` 에서 빼면 Director
  idle 최적화가 덮어써 조용히 죽는다. `enableNonDragEffects` 로 게이팅해도 죽는다.
- **헤드룸은 손패 연동이지 툴팁 연동이 아니다.** press 마다 카메라가 움직이면 멀미가 나고,
  손패가 열린 동안은 하단을 카드가 이미 덮고 있어 보드를 내릴 때 손해가 작다.
- **씬에 직렬화된 SerializeField 는 코드 기본값을 이긴다.** `tooltipTopOffset` 을 코드만
  바꿔서 무효였던 전례가 있다. 씬 값도 함께 확인할 것.
- 보드 침범 ~83px 이 **의도적으로 남아 있다.** −6°/−2.5 면 0 이지만 카메라 이동이 눈에
  띄어 현 값을 택했다. "가림이 남았네" 하고 카메라 값을 키우기 전에 이 트레이드오프를 볼 것.

## 이 작업에서 틀렸던 판단 (같은 실수 방지)

1. **"스폰이 다 가려진다"** — 세로 겹침만 보고 내린 오진. 스폰 셀은 보드 좌·우 끝단
   (화면 x 467/1453)이고 툴팁은 중앙(x 720~1200)이라 애초에 안 겹쳤다.
   → **화면 점유는 2D 로 볼 것.**
2. **"상단에 메뉴 버튼 없음"** — 코드 생성 UI 만 조사한 결과. 좌상단 `MenuButton`
   (앵커 (0,1), pos (24,−24), 170×64)은 씬 authoring 이라 grep 에 안 걸렸고, 이를 놓쳐
   배너형 툴팁(unit 5)을 만들었다가 정통으로 겹쳐 철회했다.
   → **씬 `m_Name:` grep 을 반드시 병행할 것.**

둘 다 "계산으로 단정하고 화면을 안 봤다"가 뿌리다. 오버레이 캔버스는 카메라 스크린샷에
안 잡혀 자율 시각검증이 막히므로, 레이아웃 판단은 사용자 Play 확인을 앞당기는 편이 빠르다.

## Follow-up

- 남은 침범 83px 을 더 줄이려면 **툴팁 쪽** 카드가 남아 있다 — 설명문 줄 수 제한,
  상단 offset 24 추가 축소. 폰트 축소는 이번 unit 의 목적과 충돌하므로 제외.
- `2_handoff_summary.md` 의 rev 1 우측 배치 서술은 당시 계약의 이력이다. 최신 계약은
  README 와 units 3·4·6 이 source of truth.
