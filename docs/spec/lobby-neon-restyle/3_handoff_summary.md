# 3 — Handoff Summary

## Commit

- `09aa612f` docs: 스펙 신설
- `334e705e` feat: unit 0 — 네온 배경(밤)·글리프 3종·뎁스맵 에셋 (순수 추가 10파일)
- `5ec7251a` feat: unit 1 — 버튼 네온 리스킨 (UiRoundedSprite 오버로드 + LobbyNeonChip + 씬)
- `f8f0c89f` feat: unit 2 — 로비 배경 슬롯 스왑 (씬 참조 5개)
- `a8870408` docs: 완료 기준 기재
- 이후: unit 2 rev(SceneTransition 커버 스왑) + 뎁스맵 meta 정합 + 리뷰 반영 docs (해시는 git log 참조)

## Implemented

- 로비 배경 = 사용자 제공 네온 시티 밤 (`lobby_bg_neon_night.png`). 낮 슬롯에도 밤을 꽂아
  디졸브는 no-op 유지 — 낮 버전 도착 시 daySprite + SceneTransition.prefab daySprite 교체.
- 스쿼드/드림캐쳐/히스토리 버튼 = `LobbyNeonChip`(Chip) 런타임 베이크 다크 칩 + 절차 재작화
  흰 글리프(`neon_glyph_*.png`) + Jua 한글 라벨. 기존 스티커 Icon GO 는 비활성 보존.
- START = `LobbyNeonCta` 네온 리본 배너 + Anton 이탤릭 "START" (unit 4 에서 재구현.
  최초 구현이던 `LobbyNeonChip(Cta)` 그라디언트 사각형은 폐기).
- 뎁스 패럴랙스용 신규 저주파 뎁스맵(`lobby_bg_neon_depth.png`, 임포트 설정 구 에셋과 동일).
- 씬 전환 커버(SceneTransition.prefab) 참조 3개도 네온 밤으로 스왑 (리뷰 major 반영).

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/LobbyNeonChip.cs` — 스킨 소유 컴포넌트 (지오메트리는 씬 소유)
- `Assets/_Project/Scripts/UI/Outgame/LobbyNeonCta.cs` — START 배너 베이커 (unit 4)
- `Assets/_Project/Scenes/OutgameScene.unity` — 버튼 4개 + 배경 참조
- `Assets/Resources/SceneTransition.prefab` — 전환 커버 (로비 배경과 항상 같은 그림 유지)

## Verified

- 컴파일 에러 0, Play 콘솔 에러/워닝 0.
- Play 스크린샷 `Assets/Screenshots/screenshot-20260731-123955.png` — 배경·칩 3종·CTA 렌더 확인.
- code-reviewer 리뷰: major 1(SceneTransition 커버, 반영 완료) / minor 6(3건 반영: 뎁스맵 meta,
  문서 drift, 롤백 단서 — 나머지는 후속 후보) / nit 5(무조치). 씬 diff 오염 없음·제약 준수·
  ColorTint 무충돌·revert 실측은 리뷰가 근거와 함께 확인.
- **미실시**: 버튼 클릭→패널 오픈, START→전투 진입(커버 확인), 디졸브 파면, 패럴랙스 스와이프
  — 에디터 실기 확인은 사용자 몫으로 대기.

## Notes

- **롤백은 역순(2→1→0)만 안전** — unit 0 단독 revert 는 dangling GUID (README 단서 참조).
- 캔버스는 Screen Space Overlay — Play 스크린샷은 카메라 경로에 UI 가 안 잡히므로
  일시적으로 ScreenSpaceCamera 전환 후 촬영하고 원복하는 우회를 썼다 (m_Camera 잔여 참조 주의).
- MCP `manage_asset` 이 spriteMode 를 Multiple 로 남기는 문제 → .meta 직접 수정으로 해결.
- 글리프는 시안 추출본이 아니라 절차 재작화(512→128) — 시안 추출은 42~75px 라 화질 부족.

## Follow-up

- Unity 에디터가 MCP 명령 무응답 상태로 종료됨(포커스 필요 추정). **에디터 포커스 후
  OutgameScene 을 디스크에서 다시 열 것** — 인메모리 씬이 스테일이라 그대로 저장하면
  sed 반영분(m_Camera 원복·배경 스왑)이 클로버됨. 클로버돼도 git 에서 복원 가능.
- 낮 버전 배경 + README 후속 후보 목록 참조.
