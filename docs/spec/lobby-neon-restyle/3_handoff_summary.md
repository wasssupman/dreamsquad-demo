# 3 — Handoff Summary (unit 0~5 전체, 종료 시점)

> 이 문서가 이 feature 의 유일한 handoff 다. unit 4·5 는 이 문서보다 나중에 들어왔으므로
> 여기 내용이 최종 상태다. 계약은 README/번호 문서가 우선.

## Commit

- `09aa612f` 스펙 신설 → `334e705e` unit 0 (에셋) → `5ec7251a` unit 1 (칩) → `f8f0c89f` unit 2 (배경)
- `85358ec5` unit 2 rev — SceneTransition 커버 스왑 (리뷰 major)
- `5fe90891` unit 0 rev — 뎁스맵을 정식 베이크로 교체 / `d64db459` spriteMode Single 확정
- `f020c97f` unit 4 — START 를 네온 리본 배너로 재구현 / `2160067c`·`aebcbab1` unit 4 리뷰 반영
- `b8b47823` 2차 배경 시안 revert (사용자 판단: 안 어울림) → 1차 네온 시티 유지
- `00106f15` unit 5 — 낮 배경 도입, 디졸브 시간대 전환 활성화

## Implemented

- 로비 배경 = 네온 시티 **낮/밤 페어**(`lobby_bg_neon_day/night.png`). 캐릭터 터치 →
  디졸브로 시간대 전환. 뎁스맵은 1장 공유(낮/밤 baked depth 상관 0.9997).
- 스쿼드/드림캐쳐/히스토리 = `LobbyNeonChip` 런타임 베이크 다크 칩 + 절차 재작화 흰 글리프
  + Jua 한글 라벨. 기존 스티커 Icon GO 는 **비활성 보존**(인스펙터에서 즉시 복원 가능).
- START = `LobbyNeonCta` 네온 리본 배너(180° 회전대칭 리본 + 떠 있는 흰-시안 링 + 청색 글로우
  + 45° 대각 그라디언트 + 양끝 셰브론) + Anton 이탤릭 + 보라 아웃라인.
- 씬 전환 커버(`SceneTransition.prefab`)도 같은 낮/밤 페어를 물린다.

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/LobbyNeonChip.cs` — 칩 스킨 (지오메트리는 씬 소유)
- `Assets/_Project/Scripts/UI/Outgame/LobbyNeonCta.cs` — START 배너 베이커 + 라벨 아웃라인
- `Assets/_Project/Scenes/OutgameScene.unity` — 버튼 4개 + 배경 참조
- `Assets/Resources/SceneTransition.prefab` — 전환 커버 (로비 배경과 **항상 같은 그림** 유지)

## Verified

- 컴파일 에러 0, Play 콘솔 에러/워닝 0. 시안 대비 크롭 비교로 CTA 형태 검증.
- 스크린샷: `neon_lobby_final.png`(밤), `neon_lobby_day.png`(낮).
- code-reviewer 2라운드: unit 0~2 에서 major 1(커버 스왑) + minor 6, unit 4 에서 major 1
  (라벨 머티리얼 인스턴스) + minor 5. 전부 반영 또는 후속 후보 이관. 씬 diff 오염 없음,
  제약 준수, ColorTint 무충돌, revert 실측, NaN 가드 전수 검증은 리뷰가 근거와 함께 확인.
- **사용자 확인 2026-07-31**: 로비 실기 확인 완료(디졸브 시간대 전환 포함).
- 버튼 클릭→패널 오픈 스모크는 별도로 돌리지 않았다. `onClick`·GO 이름·버튼 rect 참조가
  diff 상 불변임은 확인했으므로 회귀 위험은 낮다.

## Notes (되돌리면 안 되는 의도)

- **롤백은 역순(5→4→2→1→0)** — unit 0 단독 revert 는 씬에 dangling GUID 를 남긴다.
- 배경 아트를 바꿀 때는 씬 day/night 슬롯 + 뒤 레이어 Image + `SceneTransition.prefab` 을
  **함께** 바꾼다. 커버는 "현재 로비 배경과 같은 그림" 전제로 불투명 스냅을 숨긴다.
- `LobbyNeonCta` 가 붙은 라벨에는 TMP 의 `outlineWidth`/`faceColor` 세터를 쓰지 말 것.
  렌더 머티리얼을 원본으로 되돌리고 공유 머티리얼을 오염시킨다(코드 주석에 상세).
- CTA 는 9-slice 가 아니라 full-rect 베이크 — 사선 모서리·대각 그라디언트·셰브론은 늘리면 깨진다.
- 캔버스가 Screen Space Overlay 라 카메라 스크린샷에 UI 가 안 잡힌다. 임시로
  ScreenSpaceCamera 로 바꿔 촬영하고 `m_Camera: null` 로 원복했다(씬에 잔여 참조 없음 확인).
- MCP `manage_asset` 은 새 텍스처의 spriteMode 를 Multiple 로 남긴다 → `.meta` 직접 수정 필요.

## Follow-up

- 항목은 `docs/spec/README.md` 하단 **Follow-up Backlog → 로비 네온 리스킨** 으로 이관했다.
- 2차 배경 시안은 `b8b47823` revert 로 보존 — 되살리려면 그 커밋을 다시 revert 하면 된다.
