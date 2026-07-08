# 2 — 메뉴 팝업 (정지형)

## 목적

메뉴 버튼을 즉시 씬 이탈에서 **정지형 팝업**으로 바꾼다. 팝업 열림 시 전투를 일시정지하고, [나가기](→아웃게임)/[재개](→닫고 속행) 를 제공한다. (공격 패턴 스트립 연동은 작업 3.)

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/MenuPopup.cs` (`Wassup.UI`)
- `Assets/_Project/Scripts/UI/Outgame/ReturnToMenuButton.cs` — 즉시 씬 전환 → 팝업 오픈으로 변경
- `BattleScene.unity` — `MenuReturnCanvas` 아래(또는 별도) `MenuPopup` 배선

## 구현

### MenuPopup (신규)
- 자체 ScreenSpaceOverlay Canvas. sortingOrder **900**(게임플레이 위, 메뉴 버튼 order 1000 아래로 둬 버튼이 팝업 위에 남거나, 팝업 열림 시 버튼 raycast 차단 여부는 아래 참조). CanvasScaler 1920x1080, `UiLayer.Apply`.
- 구성:
  - **dim 백드롭**: 풀스크린 반투명 Image(raycastTarget=true) — 뒤 게임플레이 입력 차단.
  - **버튼 2개**(화면 중하단, 스트립 아래 영역): `[재개]`, `[나가기]`.
    - `[재개]` → `Close()`
    - `[나가기]` → `SceneManager.LoadScene(SceneNames.Outgame)`
  - (공격 패턴 스트립은 작업 3에서 `Open()` 시 `FadeIn()` 으로 노출.)
- `Open()`:
  - 이미 열려 있으면 무시(멱등).
  - `_pauseLease = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);` (Dreamcatcher 선례와 동일 패턴; Battle 도메인 0 → 시뮬·타이머 동결.)
  - 백드롭/버튼 활성. (작업 3: 스트립 `FadeIn()`.)
- `Close()`:
  - `_pauseLease.Dispose();` (이중 Dispose 안전 — TimeLease 는 멱등 no-op).
  - 백드롭/버튼 비활성. (작업 3: 스트립 `Roll()`.)
- 나가기 경로: 씬 전환으로 GameManager(non-persistent) teardown → lease 는 자연 소멸. 안전을 위해 나가기 직전 `Dispose()` 호출도 무해(멱등). 매치 경계 `TimeManager.ResetAll` 이 별도로 orphan 정리.

### ReturnToMenuButton 변경
- `OnReturnToMenu()` 본문을 `SceneManager.LoadScene(...)` 에서 `menuPopup.Open()` 호출로 교체.
- SerializeField `MenuPopup menuPopup` 추가, 씬에서 주입. (씬의 `MenuButton` `m_OnClick` 은 여전히 `ReturnToMenuButton.OnReturnToMenu` 를 가리킴 — 핸들러 내부만 변경, 배선 유지.)

### 입력 레이어 주의
- 팝업 열림 중 게임플레이(배치/드래그/스킬) 입력이 백드롭에 막히는지 확인. 메뉴 버튼(order 1000)이 백드롭(order 900) 위에 있어 다시 눌릴 수 있으니, 열림 중 메뉴 버튼 비활성 또는 `Open()` 멱등으로 무해 처리.

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러 없음.
- [ ] 메뉴 버튼 클릭 → 팝업 오픈 + 전투 정지(유닛/타이머 정지 육안 확인, 작업 1 타이머로 검증).
- [ ] [재개] → 팝업 닫힘 + 전투 속행(타이머 재개).
- [ ] [나가기] → OutgameScene 로드(기존 동작).
- [ ] 팝업 정지 중 게임플레이 입력이 뒤로 새지 않음.
- [ ] `Time.timeScale == 1` 유지(TimeManager 경유 확인).
