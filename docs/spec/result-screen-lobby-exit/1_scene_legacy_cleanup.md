# 1 — 씬 레거시 자식 제거

## 목적

`BattleScene` 의 `ResultScreen` 밑에 남은, 어떤 코드도 참조하지 않는 오브젝트 3개를 지운다.

선행: unit 0 (버튼 이름이 `LobbyButton` 이 되면서 레거시 `RestartButton` 과 이름 충돌 여지도 사라진다).

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

`UIRoot/ResultCanvas/ResultScreen` 밑에서 삭제:

| 오브젝트 | 실체 | 코드가 만드는 대체물 |
|---|---|---|
| `ResultLabel` | TMP (빈 텍스트) | `Header/Tab/ResultLabel` |
| `RestartButton` | Image + Button, 자식 `Text`="다시 시작" | `Footer/LobbyButton` (unit 0) |
| `RedraftButton` | Image + Button, 자식 `Text`="REDRAFT" | 없음 — REDRAFT 는 이미 폐기 (`BattleBridge.cs:365`) |

`ResultScreen.BuildCanvas()` 는 기존 자식을 지우지 않고 새 UI 를 덧붙이기만 하므로, 이 셋은 `SetActive(true)` 때 함께 살아났다(실측: `childCount=5` = 레거시 3 + `FullBleedRoot`/`SafeAreaRoot`).

**제거 근거는 "죽은 오브젝트" 이지 "보이는 결함" 이 아니다.** 셋 다 `raycastTarget=true` 인 실그래픽이지만:

- 클릭을 뺏지 않는다 — 새 UI 가 뒤 sibling 이라 위에 그려지고, `onClick` persistent 리스너도 0개(실측).
- 시각 영향은 패널 알파 0.98 뒤로 새는 **1~2/255** 수준이다. 제거 전후 패널 내부(레거시 버튼 자리) 픽셀 평균 blue 23.0 → 21.7, 표준편차 0.6 → 0.2. 육안으로는 사실상 구분 불가.

즉 이건 **위생 작업**이다. 없앨 이유는 충분하지만(참조 없는 오브젝트 3개 + 죽은 Button 2개), "유령이 보여서 고쳤다" 로 기록하면 다음 사람이 과장된 증상을 찾다 헤맨다.

`BuildCanvas()` 에 자식 청소 로직을 넣지 않는다 — 씬에서 지우는 것으로 끝낸다 (제약 8: 지금 안 쓰는 방어 구조 금지).

### 씬 저장 주의

`SaveScene` 은 미저장 WIP 를 통째로 베이크한다. 저장 전 `git diff` 로 확인하고 이 unit 의 변경 외에 섞이면 분리한다. 작업 시작 시점 BattleScene 은 HEAD 와 동일했다.

## 완료 기준

- [x] Play: `ResultScreen` GameObject 의 `childCount == 2` (`FullBleedRoot`/`SafeAreaRoot` 만). 변경 전 5.
- [x] 씬 diff = 오브젝트 3개 + 그 `Text` 자식 2개 + 딸린 MonoBehaviour 7개 삭제뿐. **추가 0건**, `GameManager` 블록 md5 동일(무변경) 확인.
- [x] 콘솔 에러 0.
- [ ] 사용자 Play 육안 확인.

확인 2026-07-16 — 씬 위생. `ResultCanvas.sortingOrder` 는 **건드리지 않았다**(아래 참조).

> ## 정렬은 손대지 않는다 — 오진 기록
>
> 초안은 `ResultCanvas` 루트가 `sortingOrder=0` 이라 결과창이 모든 HUD 아래 깔린다고 보고 2000 으로 올리려 했다. **틀렸다.** 실측으로 반증됨(2026-07-16):
>
> - `order=0` 상태의 결과창 스크린샷에서 HUD 픽셀이 이미 전부 검정에 가깝다 — 점수 라벨 `(2,12,10)`, 각성 패널 `(2,7,10)`, 덱 도크 `(0,0,0)`, MENU 버튼 `(4,8,21)`.
> - `order=0` → `2000` 으로 바꾼 뒤 MENU 버튼 픽셀은 `(4,8,21)` 로 **완전히 동일**. 달라진 게 없다.
>
> **중첩 캔버스의 `overrideSorting=true` 는 그 캔버스를 전역 오버레이 정렬에 자기 `sortingOrder` 로 참여시킨다** — 루트의 order 에 갇히지 않는다. `ResultScreen.cs:296-300` 의 기존 주석이 정확하며, 그 주석을 "루트가 결정한다" 로 고치려던 시도는 맞는 문서를 틀리게 만드는 것이었다.
>
> 루트가 0 인 채로 결과창은 이미 `MenuReturnCanvas`(1000) 위, `SceneTransition`(10000) 아래에 정확히 위치한다. **`ResultCanvas.sortingOrder` 를 만지지 말 것.**
