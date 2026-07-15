# 3 — 트리거 연결 + 씬 주입 + Play 검증

## 목적

드래그 스와이프 시작(BeginDrag)에 컷신을 연결하고, 씬에 재생기를 주입한 뒤 Play 검증.

## 변경 대상

- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- Modify: `Assets/_Project/Scripts/UI/DefenderSelector.cs`
- Scene: `DefenderSelector` GameObject 에 `DeployCutscenePlayer` 주입(씬 배치 권장)

## 구현

- `DefenderDragPlacementController`:
  - 필드 `private DeployCutscenePlayer _cutscenePlayer;`
  - `Configure(...)` 시그니처에 `DeployCutscenePlayer cutscenePlayer = null` 추가(옵셔널,
    기존 호출부 하위호환). 주입값 보관.
  - `BeginDrag` 내부, 세션 구성 후 (기능 토글 `Cfg.enableDeployCutscene` 게이트):
    `if (Cfg.enableDeployCutscene && _cutscenePlayer != null && unitData.deployCutsceneFrames != null && unitData.deployCutsceneFrames.Length > 0) _cutscenePlayer.Play(unitData.deployCutsceneFrames, unitData.deployCutsceneFps);`
  - 온/오프는 `DragSwaySettings.enableDeployCutscene`(이미 주입된 SO 재사용).
  - **CleanupSession 이 `EndCutscene()` 를 호출한다**(rev 2026-07-15: 스와이프 종료 시 컷신 슬라이드-아웃). 구 "독립 재생" 계약 폐지.
- `DefenderSelector`:
  - SerializeField `DeployCutscenePlayer deployCutscenePlayer;` 추가.
  - 미할당 시 GetComponent→AddComponent 폴백(dragPlacementController 패턴과 동일).
  - `dragPlacementController.Configure(...)` 호출에 `deployCutscenePlayer` 전달.
- UnityMCP 로 씬에서 `DeployCutscenePlayer` 를 `DefenderSelector` 오브젝트(또는 전용 GO)에
  붙이고 인스펙터 튜닝값 확인 → Play 모드 실측.

## 완료 기준

- 컴파일 통과(`read_console` clean).
- Play: Ranger 슬롯을 드래그하면 좌상단에 컷신이 뜨고 33프레임+1초 후 사라짐.
- 드래그를 즉시 드롭/취소해도 컷신은 끝까지 재생(독립).
- 컷신 프레임 없는 다른 유닛 드래그 시 컷신 미출현, 기존 배치 흐름 정상.
- 확인 일자 + 커밋 해시 기록.

_확인: 2026-07-14 — BeginDrag 트리거 + DefenderSelector AddComponent 폴백 주입, 컴파일 클린, 사용자 Play 확인._
