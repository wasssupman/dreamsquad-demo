# 3 — 씬 배선 + Play 검증

## 목적

`DcInspectController` / `DcInspectPanelView` 를 BattleScene 에 배선하고 e2e 로 검증한다. `unity-feature-wiring` 스킬 대상 — 배선을 "사용자 수작업"으로 미루지 않는다.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

`DcIconStripSpawner` 와 같은 계층(루트)에 GameObject 2개 신설. **패널은 별도 GameObject** — `UiCanvasSetup.Ensure` 가 host 에 Canvas/CanvasScaler/GraphicRaycaster 를 붙이므로 컨트롤러와 합칠 수 없다.

- **`DcInspect`** ← `DcInspectController`. SerializeField 배선 7점:
  `bridge`(GameManager/BattleBridge) · `mainCamera`(Main Camera) · `hand`(DreamcatcherHandController) · `handView`(DreamcatcherHandView) · `defenderSelector`(UIRoot/DefenderSelector) · `config`(`Assets/_Project/Data/Dreamcatcher/AwakeningConfig.asset`) · `panel`(DcInspectPanel)
- **`DcInspectPanel`** ← `DcInspectPanelView`. 배선 1점: `labelFont`(`Assets/_Project/Fonts/Jua SDF.asset`)

**씬 저장 위생** (`docs/reference/lessons/` + memory):
- 저장 전 `git diff Assets/_Project/Scenes/BattleScene.unity` 로 **무엇이 함께 베이크되는지 확인**한다. 사용자의 미저장 in-memory WIP(Main Camera transform 등)가 통째로 박힐 수 있다.
- 내 delta 만 남기고 unrelated hunk 는 제외한다.

## 완료 기준

Play 검증 (스크립트 배틀 또는 수동, 사용자 포커스 필요 — MCP 비포커스면 프레임 정지):

1. **표시**: 카드를 유닛에 부착 → 그 유닛 탭 → 패널이 유닛 옆에 뜨고 내용이 정확(이름/코스트/타입/effects/description).
2. **슬로우**: 패널 열림 중 `TimeManager.Instance.ScaleOf(TimeDomain.Battle)` == `slomoTimeScale`(0.3). 적 행진/공격이 눈에 띄게 느려진다. **UI 는 realtime 유지**(`Time.timeScale` == 1).
3. **토글**: 같은 유닛 재탭 → 닫힘 + `ScaleOf` 1 복귀.
4. **전환**: 다른 부착 유닛 탭 → 패널이 그 유닛으로 이동(lease 유지, 1 로 튀지 않음).
5. **빈 보드 / 부착 0장 유닛 탭** → 닫힘 + `ScaleOf` 1.
6. **배타**:
   - 손패 오픈(각성 게이지 버튼) → 패널 닫힘. `ScaleOf` 는 손패 lease 로 인계(1 로 튀지 않음).
   - 배치 드래그 시작 → 패널 닫힘.
   - Active 카드 조준 / 포탈 2탭 → 패널 안 열림. **포탈 출구를 확정한 그 탭이 패널을 열지 않는다**(계약 4 실행 순서 검증 — 이게 핵심 회귀 지점).
   - 카드를 유닛에 부착(touchup 커밋) → **그 유닛의 패널이 열리지 않는다**(계약 3 press 규약 검증).
7. **수명주기**:
   - 선택 유닛 사망 → 패널 닫힘 + `ScaleOf` 1.
   - 페이즈 이탈(Battle 종료 → Result) → 패널 잔류 없음 + `ScaleOf` 1.
   - 매치 재시작 → 잔류 없음.
8. **좌표**: 보드 좌/우 끝 유닛 탭 → 패널이 safe area 안. 카메라 킥(마일스톤/임팩트) 중 패널이 유닛에서 미끄러지지 않음.
9. **비간섭**: 패널 표시 중 카드 드래그/조준/유닛 호버 틴트가 기존과 동일.
10. **콘솔 에러 0.**

## 검증 환경의 함정 (실측 2026-07-15 — 다음 작업자용)

- **`execute_code` 안의 `Screen.width/height/safeArea` 는 게임뷰가 아니라 에디터 창을 반환한다** (실측: `Screen`=519x830 vs `Camera.main.pixelRect`=1920x1080). 이걸 모르면 패널 위치를 오판한다 — 실제로 이 조사 중 "좌측 플립이 안 먹는 버그"로 오진했다가, 플레이어 루프의 `LateUpdate` 는 올바른 값을 본다는 걸 확인하고 철회했다. **게임뷰 해상도는 `cam.pixelRect` 로 읽을 것.**
- 같은 이유로 `Show()` 를 execute_code 에서 직접 호출하면 그 안의 `Follow()` 가 잘못된 safe area 로 1프레임 위치를 잡는다. 다음 `LateUpdate` 가 교정하므로 무해하지만, 그 1프레임을 캡처하면 오판한다.
- **`ScreenSpaceOverlay` 캔버스는 `manage_camera screenshot`(카메라 렌더)에 안 잡힌다.** HUD 전체가 통째로 빠진다. `ScreenCapture.CaptureScreenshot`(최종 합성 프레임)을 쓸 것.
- 스크립트 배틀은 캡처하는 사이 매치가 끝나 `Result` 로 넘어간다(→ 패널이 계약대로 닫혀 "버그"로 오인하기 쉽다). `TimeManager.Request(Battle, 0.02f, priority:1000)` 로 배틀 클럭을 스톨해 시간을 벌 것 — `Time.timeScale` 은 건드리지 않는다(프로젝트 계약).

## 검증 결과

에이전트 자율 검증 통과 (2026-07-15, Play + reflection 구동):

- 표시 / 내용 정확 / 슬로우 0.3 / 토글 / 빈 보드 / 부착 0장 / 손패 배타 / 사망 회수 / 페이즈 이탈 — 전부 통과. lease 대칭 표는 `1_tap_select_and_slomo.md` 참조.
- 패널 위치: `LateUpdate` 교정 후 유닛 우측 정확 배치(unit.x + gapFromUnit).
- 콘솔 에러 0 (Play 진입/이탈 teardown 포함).
- 씬 diff: **112줄 추가 / 0줄 삭제.** 이 중 **109줄이 신설 GameObject 2개**(`DcInspect`/`DcInspectPanel`)이고, **나머지 3줄은 내 변경이 아니라 재직렬화**다 — 정직하게 적는다(코드리뷰 m5):
  - `deployCutscenePlayer: {fileID: 0}` · `depthParallaxSettings: {fileID: 0}` (DefenderSelector) · `enableAdjacencySynergy: 0` (BattleBridge)
  - 셋 다 **HEAD 씬에 아예 없던 키**다(실측). 씬이 마지막 저장된 뒤 클래스에 추가된 SerializeField 라, **누가 씬을 저장하든 기본값으로 베이크된다** — 이번 작업이 유발한 게 아니고 피할 수도 없다.
  - 동작 중립 확인: `enableAdjacencySynergy` 는 초기화자가 없어 C# 기본 `false` = 베이크된 `0`. 나머지 둘은 null 이고 `DefenderSelector.EnsureDragController` 가 `GetComponent`/`AddComponent` 로 폴백한다(키 부재 시와 동일 결과).

## 해상도/노치 분석 (실기기 미검증 축의 정량화)

Play 검증은 전부 1920x1080 · `scaleFactor=1` · `safeArea=전체화면` 에디터 게임뷰에서 이뤄졌다. 즉 `Follow()` 의 flip/clamp 는 **에디터에서 사실상 안 탄다**. 실기기 위험을 손으로 계산해 남긴다:

- 타깃은 **가로**다 (`ProjectSettings.defaultScreenOrientation: 2` = LandscapeRight).
- `UiCanvasSetup`: 레퍼런스 1920x1080 + `matchWidthOrHeight = 1`(**높이 매칭**) → `sf = 화면높이 / 1080`.
- 패널 가로 점유 = `(panelWidth · sf) / 화면폭` = `460/1080 × (h/w)`:

| 기기 | sf | 가로 점유 | 세로 점유(3장 기준 492) |
|---|---|---|---|
| 1920x1080 (검증 환경) | 1.00 | 24% | 46% |
| 2400x1080 (20:9) | 1.00 | **19%** | 46% |
| 1280x720 | 0.67 | 24% | 46% |

**결론: 넓은 기기일수록 패널이 상대적으로 더 좁아진다.** sf 가 패널을 화면 높이에 정확히 비례 스케일하고 20:9 는 16:9 레퍼런스보다 가로 여유가 크므로, flip/clamp 는 에디터보다 **덜** 발동한다 — 이 축의 잔여 위험은 낮다. 노치는 `safe.xMin/xMax` 를 좁힐 뿐이고 클램프가 그대로 존중한다.

flip+clamp 분기 자체는 위 "검증 환경의 함정"의 `Screen` 컨텍스트 사고 덕에 **우연히 1회 실행됐다**(폭 519 → 좌측 마진 10 에 핀 = 정상 열화). 단 이건 의도된 테스트가 아니므로 실기기 육안 확인으로 갈음할 것.

**사용자 확인이 남은 항목** (에이전트가 판정할 수 없는 것):

- 실제 손가락 탭 체감(press 규약이 자연스러운가) 및 슬로우 강도 0.3 의 체감.
- 패널 크기/위치가 실기기 종횡비(20:9)에서 보드를 과하게 가리지 않는가.
- 카메라 킥/브리딩 중 미끄러짐 없음(코드상 `LateUpdate` 보장, 육안 확인 권장).
- 포탈 2탭 직후 패널이 열리지 않는가(계약 4 실행 순서 — 실제 포인터 입력이 필요해 자율 검증 불가).
- 카드 부착 touchup 직후 패널이 열리지 않는가(계약 3 press 규약 — 동일).

확인 2026-07-15 — **사용자 Play 통과 확인** ("느낌은 괜찮다"). 커밋 `71fc4679`(기능) · `f54909de`(merge).

단, 확인 과정에서 **탭 입력 결함 1건이 발견돼 별도 수정**됨 → `5d7a2585` 참조:
사용자 보고 "머리는 눌리는데 몸통은 자주 실패". 원인은 이 spec 이 아니라 `WavePatternStripView`
가 숨김을 `CanvasGroup.alpha=0` 으로만 처리해 화면 x[24~1624] y[430~590] 에 **보이지 않는
입력 벽**을 남긴 것. 유닛이 띠 경계(y=590)에 걸치면 머리는 통과/몸통은 차단된다.
이 spec 의 픽킹은 결백했다(렉트 161x101px 가 스프라이트를 정확히 덮음, 수정 후 유닛 8기 ×
머리/몸통/발 24/24 성공). **보드 raw 탭 소비자가 이 spec 이 처음이라 이제야 드러난 잠복 결함.**
함정 상세는 `docs/reference/lessons/03-rendering-assets.md` "CanvasGroup.alpha = 0 은 숨기지 않는다".
