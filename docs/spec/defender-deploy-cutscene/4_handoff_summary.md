# 4 — Handoff Summary

## Commit

`9e5221fc` — feat(defender-deploy-cutscene): 드래그 배치 시 좌상단 유닛 컷신 (Ranger·Archer)

## Implemented

- 드래그 배치 스와이프 시작(`BeginDrag`) 시 화면 좌상단에 유닛 컷신을 원샷 스프라이트
  플립북으로 재생. 프레임 없는 유닛은 조용히 skip.
- 연출: 화면 왼쪽 '바깥'에서 빠르게 슬라이드-인(EaseOut, 애니 동시 재생) → 1초 hold →
  왼쪽으로 슬라이드-아웃(EaseIn) → 숨김. 드래그 세션과 **독립**(드롭/취소가 중단 안 함).
- 스프라이트: 원본(검정 불투명 배경)을 역순 리넘버 + 검정 누끼 + (Ranger 만 50% 축소,
  Archer 는 이미 640×360) → `Sprites/Cutscene/{Ranger 33, Archer 49}/`. Sprite(Single)+
  alphaIsTransparency+mipmap off+비압축.
- 데이터: `DefenderUnitData` 에 `deployCutsceneFrames`/`deployCutsceneFps`/
  `deployCutsceneScale`(유닛별 배율)/`deployCutsceneOffset`(유닛별 도착 오프셋) 추가.
- 크기 = 네이티브 × displayScale(공유) × deployCutsceneScale(유닛별).
  도착 = cornerMarginPx(공유 baseline x=-100) + deployCutsceneOffset(유닛별).
- Ranger: scale 1 / offset 0 → 도착 -100. Archer: scale 1.5 / offset (-150,0) → 도착 -250.
- 기능 온/오프: `DragSwaySettings.enableDeployCutscene`(기존 주입 SO 재사용).

## Key Files

- `Assets/_Project/Scripts/UI/DeployCutscenePlayer.cs` — 루트 ScreenSpaceOverlay 캔버스 +
  Image 플립북 재생기(슬라이드 인/아웃, 유닛별 scale/offset).
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `BeginDrag` 트리거.
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 재생기 AddComponent 폴백 + Configure 주입.
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — 컷신 필드 4종.
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — `enableDeployCutscene` 토글.
- `Assets/_Project/Data/Defenders/Defender_{Ranger,Archer}.asset` — 프레임/튜닝 할당.

## Verified

- 컴파일 클린(`read_console` error 0), 도메인 리로드 후 정상.
- 누끼 육안 검증(어두운 배경 합성, 검은 테두리/배경 잔상 없음) — Ranger·Archer.
- 사용자 Play 반복 피드백으로 위치/크기/슬라이드 튜닝 수렴.

## Notes

- **캔버스는 반드시 루트**(부모 없이 생성). `DefenderSelector` 자체가 Canvas 라, 자식으로
  두면 nested Canvas 가 부모 좌표계를 상속해 화면이 아닌 슬롯 rect 기준으로 렌더된다(초기 버그).
- 시간은 `Time.unscaledDeltaTime` — 드래그 배틀 슬로우모/일시정지 영향 배제.
- 원본 raw 프레임은 사용자 요청으로 삭제(재-매팅 필요 시 원본 재수급). 전처리 스크립트는
  scratchpad(레포 밖)에 있음.
- 유닛별 `deployCutsceneScale`/`deployCutsceneOffset` 미기재 시 기본값(1 / 0,0).

## Follow-up

- 나머지 유닛 컷신 프레임 제작/할당(현재 Ranger·Archer 2종).
- in/out 트랜지션(페이드), 프레임 아틀라스화, 컷신 사운드 동기 — README 후속 후보 참조.
