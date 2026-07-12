# 6 — Handoff Summary (에디터 범위 완료 2026-07-12)

## Commit

Codex 세션: u0 셸 `1d467a3b` + 아트 `1dedf923`. Claude 인수 후: u0 마감 `dd7f646a`(procedural 배킹 확정) → u1 `bc1875e0`(비용·role·affordability) → u2 `f3699f32`(코스트 레일, 시안 정합 rev) → u3 `b96aef1e`·`58eb752a`(핸드 정합) → u4 `a6dce3fc`(거절 피드백) → u5 기록 `40f323a4`. 브랜치: `feat/mobile-ui-safe-area`.

## Implemented

- **배킹 (u0)**: procedural 라운드(네이비 0.96 + 골드 2px) = 프로덕션. TrayPlate_v2/CostChip/EnergyRail PNG 는 **미사용 잔존** — 코너 장식이 9-slice·소형 슬라이스와 부적합(계약 변경 기록 참조).
- **슬롯 정보 (u1, 시안 정합)**: 좌상단 다크 플레이트 ⚡+비용, 우상단 role 배지(원/수/근/술/보 + neutral 폴백), 하단 다크 밴드 한 줄 autosize 이름. affordability = `CurrentInt` diff 프레임만 갱신(dim+빨간 숫자+X glyph 3중).
- **코스트 레일 (u2)**: 부유 배지(363×112, y164) → 트레이 동색 탭(440×54, overlap 14) ⚡+10/10+세그먼트 한 줄. 위치 = Config geometry 공유(phase 전환 한 프레임 정합). 가림선 top 276→222. config 미할당 = 기존 부유 배지 무회귀 폴백.
- **핸드 정합 (u3)**: HandView 배킹 = 트레이 문법(라운드22+골드+네이비, handSize Config). Flip/slomo/suppression 계약 무접촉.
- **거절 피드백 (u4)**: 비용 부족 → 슬롯에서 세션 차단 + "코스트 N 부족" 레일 펄스(0.6s, 단일 코루틴 리셋). 드래그 중 사유 라벨(포인터 추종): X 코스트 부족(coral)/■ 점유됨(amber)/— 배치 불가(neutral). 배치 권한·차감은 `TryBeginDefenderDeployment` 에 유지.

## Key Files

- `Data/BattleHudTrayConfig.cs` + `Data/Config/BattleHudTrayConfig.asset` — 전 수치/색/스프라이트 소유 (트레이·레일·핸드·슬롯·펄스)
- `UI/DefenderSelector.cs`(슬롯 빌드+affordability) · `UI/CostDisplay.cs`(레일+펄스) · `UI/DefenderDragSlot.cs`(차단 게이트) · `UI/DefenderDragPlacementController.cs`(사유 라벨) · `UI/Dreamcatcher/DreamcatcherHandView.cs`(배킹)
- 씬 배선 3건: CostDisplay.trayConfig / HandView.trayConfig / Selector.costDisplay

## Verified

- EditMode 703 중 701, 실패 0(최종). 콘솔 에러/경고 0. 캡처 세트: placement/battle/hand/펄스/사유 라벨/available↔unaffordable 경계.
- 스크립트 배틀 실측: 세션 차단(reflection sessionActive=False), Occupied/NotBuildable 라벨, 유효 배치 defender 0→1, 연속 핸드 토글 x4 + battleScale=1 복귀.
- code-review: u1 low(1건 반영 — X glyph 폰트 안전), u2~4 통합 low pass findings 0.

## Notes (되돌리면 안 되는 의도)

- **시안(battle-hud-safe-action-tray-proposal.jpg)이 시각 기준** — u2 1차(EnergyRail 캡슐+2줄)는 시안 불일치로 사용자 기각됨. 레일=트레이 동색 탭+한 줄이 확정안.
- 장식성 벤더/생성 아트를 소형 UI 에 9-slice 로 쓰지 말 것 — 코너 장식이 border 를 넘으면 "밧줄" 왜곡(u0 기각 사유). procedural 이 기준.
- 펄스는 `PulseInsufficient` 단일 진입점(코루틴 핸들 1개) — 새 이벤트 채널 만들지 말 것.
- 거절 라벨은 색+글리프+한글 3중 — 색 단독 표기로 단순화 금지.

## Follow-up

- **실기 QA 배치 (기기 연결 시)**: 20:9 시각 스윕(에디터 게임뷰 강제 해상도 비정상 — 1080×2160 캡처 이슈) · Android 터치/양방향 landscape · 3분 플레이 미스그랩 · PlayMode smoke. safe-area unit 3 터치/unit 4 와 한 배치.
- README 비목표 유지: role 아이콘 원화(현 한글 글자 배지), 슬롯 게이팅, survival HUD, idle 축소 A/B.
