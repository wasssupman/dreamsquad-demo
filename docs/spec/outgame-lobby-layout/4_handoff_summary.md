# 4 — Handoff Summary

> outgame-lobby-layout 마무리 인계 지도. 최신 계약은 README/번호 문서가 우선한다.

## Commit

미커밋 상태로 종료. `OutgameScene.unity` 한 파일에 본 스펙의 로비 배선과 **다른 세션 작업물("Hello" 캐릭터 인스턴스)** 이 섞여 있어, 씬 단독 커밋 시 타 세션 작업이 딸려간다. 커밋 타이밍/분리는 사용자가 세션 상황 보고 처리하기로 함. (관련 파일 목록은 아래 Notes)

## Implemented

- OutgameScene 로비를 배경 + 3-코너 버튼 + 타이틀로 재구성.
- 배경 `LobbyBackground`(Image, `lobby_bg.png`) 를 `MenuCanvas` 첫 자식(stretch-fill cover, raycast off)으로 배치 → 로그인 게이트 밖이라 로그인 화면에서도 표시.
- 로비 버튼 3-코너 재배치 (전부 `MenuButtons`=menuRoot 하위 유지, 게이트 보존):
  - 우상단: TestMode + DevButtons(StatRefresh/ResetAccount) — TestMode는 DevOnlyGroup 미포함(항상 표시).
  - 좌하단: Squad / Dreamcatcher (세로 스택).
  - 우하단: Start(Play).
- 타이틀 `Title` 을 "꿈결특공대" + `Jua SDF` 한글 폰트(신규 생성)로 스타일링, 상단 중앙.
- Squad/Dreamcatcher/Start 버튼을 아이콘 버튼으로 리스킨 (각 버튼에 `Icon` 자식 Image + `LabelOverlay` TMP, 기존 플레이트는 alpha 0 클릭영역, 기존 `Label` 비활성). 아이콘 셀: Squad/Dreamcatcher 180px, Start 240px, 라벨은 아이콘 아래.
- 아이콘 아트는 최종적으로 **캐주얼 스티커형**으로 교체됨 (`Art/LobbyIcons/{squad,dreamcatcher,start}_icon.png`, 1024², 투명).

## Key Files

- `Assets/_Project/Scenes/OutgameScene.unity` — 로비 씬 배선
- `Assets/_Project/Fonts/Jua SDF.asset` (+ `Jua-Regular.ttf`, `Jua-OFL.txt`) — 한글 타이틀 폰트, Dynamic SDF
- `Assets/_Project/Art/lobby_bg.png` — 배경 (Sprite/Single 임포트)
- `Assets/_Project/Art/LobbyIcons/*.png` — 버튼 아이콘 3종 (Sprite)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — **변경 없음** (참조용)

## Verified

- Unity Console error 0 (compile/import 클린).
- Play 모드 ScreenCapture로 시각 검증: 배경 cover, 타이틀 한글 렌더(tofu 없음), 3-코너 버튼 배치, 아이콘 겹침 해소, 캐주얼 아이콘 슬롯 정합 확인.
- 아이콘 corner alpha 0 / 여백 확보 (제작 측 검수).

## Notes

- **코드 변경 없음**이 원칙이었고 지켜짐 — 순수 씬 wiring + 에셋.
- 아이콘 통합/교체는 별도(Codex) 작업으로 진행됨. 초기 "고대 RPG 문장/포탈" 버전 → 캐주얼 스티커형으로 교체됨.
- **다른 세션 작업물 보존**: `MenuCanvas/Hello` 인스턴스, `Assets/_Project/Prefabs/Outgame/Hello.prefab`, `Assets/_Project/Sprites/hello/` (49프레임 걷기 애니메이션). 본 스펙 범위 아님 — 절대 삭제/수정하지 말 것.
- 배경 cover는 `preserveAspect=false` + stretch-fill. 16:9 아트라 landscape 캔버스(1920×1080 기준)에 자연스러움. 극단적 종횡비에서 크롭 발생 가능.

## Follow-up

- 커밋 처리 (씬 공유 상황 정리 후).
- 아이콘 가장자리 연한 그린 림 잔여 — 버튼 스케일에선 무시 가능하나 신경 쓰이면 알파 정리.
- README "후속 후보": 프로필/재화 헤더, Play 대형 CTA, Jua 전역 폰트 승격.
