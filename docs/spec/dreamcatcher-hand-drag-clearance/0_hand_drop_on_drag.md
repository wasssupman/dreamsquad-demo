# 0 — 조준 중 손패 하강 + 복귀

## 목적

카드를 집어 조준하는 동안 손패 패널을 아래로 내려, 큰 맵의 최하단 행 유닛이 카드 부채 위로
드러나고 그 위에서 드롭해도 취소되지 않게 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — 하강/복귀 전부
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `IsPointerFollowing`
  읽기 전용 프로퍼티 1개만 추가(§C 의 추종 카드 보정 판정). 드래그 로직은 불변.

하강 여부는 View 가 기존 `IsDragging` / `IsPortalAiming` 을 폴링해서 정한다.

## 구현

### A. 노브

```
[SerializeField] private float dragClearanceDrop    = 210f;  // 조준 중 손패 하강량(px)
[SerializeField] private float dragClearanceSpring  = 320f;
[SerializeField] private float dragClearanceDamping = 24f;
```

`210` 근거는 README "해결" 절. 요약: 카드 헤더(이름) 띠만 남기는 깊이 — 바깥 카드 헤더
하단이 화면 y 4 로 내려오는 지점. 이때 카드 top 132 가 가장 큰 맵의 보드 하단 모서리(167)
아래라 최하단 행 셀이 통째로 드러난다.

스프링은 헤드룸(`handHeadroomSpring 90 / damping 14`)과 같은 감성을 목표로 하되 이동량이
크고(210px) "짧은 시간"이 요구라 더 단단하게 잡았다: `ω=√320≈17.9`, `ζ=24/(2ω)≈0.67` →
오버슈트 약 6%, 안착 0.33초. 하강·복귀가 같은 스프링을 쓰고 target 만 바뀐다.

### B. 기준 y 캡처

`BuildCanvas` 에서 패널 `anchoredPosition` 을 잡은 직후 `_panelBaseY` 에 저장한다.
`trayConfig.anchoredY` 를 다시 읽어 재계산하지 않는다 — 기준은 실제 배치된 값 하나다.

### C. 스프링 하강·복귀 + 추종 카드 위치 보존

**카드를 누른 순간 내려가고 뗀 순간 올라온다**(사용자 결정 2026-07-28). 판정은
`_focusIndex >= 0 || AnyInteractionActive()` 하나다:

| 시점 | 호출 | 잡히는 상태 |
|---|---|---|
| press | `OnPointerDown` → `SetFocus(_index)` | `_focusIndex >= 0` |
| drag 전환 | `OnBeginDrag` → `_dragging = true` **먼저**, 그 다음 `SetFocus(-1)` | `IsDragging` |
| release | `OnPointerUp` → `ClearFocus` / `OnEndDrag` → `_dragging = false` | 둘 다 해제 |

전환에 빈 프레임이 없는 이유가 핵심이다 — `OnBeginDrag` 가 `_dragging` 을 `SetFocus(-1)`
**보다 먼저** 세우고 둘이 같은 콜백 안이라 `Update` 가 끼어들 수 없다. 이 순서가 뒤집히면
press→drag 사이에 한 프레임 복귀가 튄다. 덕분에 **단순 탭 · 드래그 후 미부착 · 부착 성공이
전부 "포인터를 뗀 순간 복귀" 로 통일**된다. 예외는 포탈 2탭 대기(`IsPortalAiming`) 하나 —
손을 뗐어도 출구를 보드에서 골라야 하므로 하강을 유지한다.

오프셋(`0` ↔ `-drop`)은 `KeyringSim.SpringStep` 스칼라 오버로드로 밀어 **하강·복귀 모두
부드럽게** 처리한다. 짧은 탭은 다 내려가기 전에 target 이 뒤집혀 스프링이 자연히 흡수한다.
안착 판정은 **변위와 속도를 함께** 본다 — 언더댐핑이라 target 을 스쳐 지나므로 변위만
보면 복귀 도중 얼어붙는다(헤드룸이 같은 이유로 같은 판정을 쓴다).

**⚠ 카드가 손가락에서 미끄러지는 함정.** ActiveTile/ActivePortal 은 카드가 포인터를 따라가는데,
그 재고정이 `DreamcatcherCardDragSlot:458` 의 `Slot.rect.position = screenPos` — **스크린 좌표**다.
부모(패널)가 그 뒤에 움직이면 카드도 딸려 내려간다. 그런데 이 재고정은 `OnDrag` 에서만
일어나고 `OnDrag` 는 **포인터가 움직일 때만** 호출되므로, 하강 중 손가락을 멈추면 카드가
손가락에서 최대 `dragClearanceDrop` 만큼 미끄러진다(사거리 프리뷰도 함께 어긋난 채 정지).

그래서 **패널을 움직이는 순간 그 슬롯의 화면 위치를 잡아 이동 후 복원한다**
(`ApplyClearanceOffset`). 드래그는 동시 1개만 성립하므로(`CanStartDrag`) 후보도 하나뿐이고
할당이 없다. 판정은 `DreamcatcherCardDragSlot.IsPointerFollowing` 이 소유한다 —
Defender/EnemyMark 는 카드가 손패 고정(화살표만 추종)이라 패널과 함께 내려가는 게 맞으므로
보정 대상이 아니다. 이 보정이 있어야 부드러운 하강과 카드 추종이 양립한다.

하강/복귀 중 취소 판정이 모호해지는 구간이 남지만 무해하다 — 그 밴드에서 갈리는 결과가
"취소" vs "손가락 밑 유닛에 부착" 인데, 거기 유닛이 있다는 건 큰 맵 하단 행이라는 뜻이고
사용자 의도는 부착 쪽이다.

**왜 폴링인가.** 헤드룸(hand-drag-tooltip unit 6)의 피드 주도 패턴을 그대로 쓰면 위와 같은
이유로 조준 중 손가락을 멈출 때 피드가 끊겨 손패가 도로 올라온다. 상태 소유는 드래그 슬롯에
두고 View 가 읽기만 하면, 피드 주도의 이점(종료 경로 누락 원천 차단)은 그대로 얻으면서
이 함정을 피한다.

### D. 리셋 지점 — `Close()` 가 아니다

`Update` 는 `State != Hand` 에서 early-return 하므로 리셋 지점이 필요하지만,
**`Close()` 에서 리셋하면 안 된다**:

`Close()` 는 패널을 감추지 않고 `StartSink()` 로 침강 애니메이션만 시작한다(실제
`SetActive(false)` 는 `OnSinkComplete`). 게다가 부착 성공 시 `HandChanged` 가 **동기**로
발화해 `commit()` 안에서 `Close()` 가 즉시 실행되고, 이는 `onSuccess`(`FlyCardToUnit`)보다
**먼저**다. 따라서 `Close()` 에서 즉시 리셋하면 가장 흔한 성공 경로에서 손패가 150px 위로
튄 뒤 가라앉고, 고스트는 내려간 위치에서 출발해 둘이 어긋난다.

리셋은 **`Open()` · `ForceClose()` · `OnSinkComplete()`** 세 곳에 둔다. 앞의 둘은 같은
호출에서 패널이 비활성/재구성되고, `OnSinkComplete` 는 침강이 끝나 패널이 꺼지는 시점이라
어느 쪽도 팝이 보이지 않는다. 침강은 내려간 위치에서 시작해 자연스럽다.

### E. 건드리지 않는 것

- 슬롯 `homePos` / `targetPos` / `SpringSlots` — 슬롯은 패널 로컬이라 자동으로 따라간다.
- `HandPanelRect` 를 읽는 판정 3곳 — 패널 이동을 자동 승계한다. 하강량을 판정에 더하지 않는다(계약 1).
- 툴팁(`SafeAreaRoot` 직속), 항아리 독, `NextWaveDock`, `_dismissCatcher`, 카메라 헤드룸.

### F. 알려진 트레이드오프

하강 중 카드 하단이 화면 밖으로 나가 **효과 본문이 잘린다**(바깥 카드 로컬 y 8~162 → 하강 후
화면 밖). 조준 중인 카드 자신도 마찬가지다. 다만 조준 중 카드 정보는 **상단 중앙 툴팁**이
담당하고(hand-drag-tooltip unit 3·4·6) 코스트/이름/태그는 카드 상단이라 남으므로 실질 손실은
작다. "잃는 정보가 없다" 가 아니라 "잃는 정보를 툴팁이 이미 대체한다" 가 정확한 근거다.

기각한 대안: "포인터가 패널 top 위로 올라간 동안만 하강"(미세 드래그 출렁임 방지). **문제
상황을 정확히 깬다** — 하단 행 조준은 손가락이 y 203 으로 패널 top(264) **아래**에 있으므로
하강이 발동하지 않는다.

## 완료 기준

- [x] 컴파일 통과, CS 에러 0
- [x] EditMode 전량 통과 (신규 실패 0 — 유일 실패는 MobileBuild 프리플라이트 사전 실패, clean HEAD 재현 확인)
- [x] Play — Serpent / Twin / Spiral 에서 **최하단 행 유닛에 부착 성공** (핵심 검증 질문)
- [x] Play — 그 유닛이 카드 부채 위로 **실제로 보이는가** (조작만이 아니라 시각 확인)
- [x] Play — 하강이 **짧고 부드럽게 스프링**으로 내려가는가, 남는 것이 **카드 헤더(이름) 띠**인가
- [x] Play — **카드를 누르는 순간** 내려가고 **떼는 순간** 올라오는가
- [x] Play — 단순 탭(집었다 그냥 놓기) / 드래그 후 빈 곳에 놓기 / 부착 성공 **셋 다 같은
      순간에 복귀**하는가 (통일 검증)
- [x] Play — press→drag 로 이어질 때 손패가 중간에 한 번 튀지 않는가 (전환 갭 검증)
- [x] Play — 내려간 손패 위에서 손을 떼면 **여전히 취소**되고 카드가 잔류하는가
- [x] Play — 메테오/포탈(카드 추종 모드)에서 드래그 시작 직후 손가락을 **멈춰도** 카드가
      손가락에 붙어 있는가 (§C 의 미끄러짐 검증)
- [x] Play — **부착 성공** 시 손패가 위로 튀지 않고 내려간 자리에서 침강하는가 (§D 검증)
- [x] Play — 커밋 / 취소 / ESC / 손패 닫기 / 페이즈 이탈 후 다음 열림이 정상 위치인가
- [x] Play — 포탈 카드의 출구 탭 대기 중에도 하강이 유지되는가
- [x] Play — Zig / Hook (작은 맵) 에서 기존 조작감 회귀 없음
- [ ] 실기기 — 하강분이 SafeArea 인셋 대역으로 들어가므로, 취소 드롭이 홈 인디케이터/시스템
      제스처와 경합하지 않는지 확인 (미확인 — handoff Follow-up)
- [x] 값을 튜닝했다면 **코드 기본값과 씬 직렬화 값을 동기** — 튜닝 없음(기본값 210/320/24 유지, 씬에 키 없음)

맵 강제는 `fixedMapSeed` 또는 개발용 맵 강제 override 로 지정한다.

확인: 2026-07-29 사용자 Play 확인("이상없음") — rev3(press/release 통일 + 스프링 하강 + 추종
카드 위치 보존) 기준. 커밋 해시는 handoff 참조.
