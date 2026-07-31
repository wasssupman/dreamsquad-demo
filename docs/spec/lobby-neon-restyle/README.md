# Spec — Lobby Neon Restyle

> 상태: 진행 중 (2026-07-31 시작)

## 상위 목표

네온 시티 시안(외부 AI 목업)에서 **배경 + UI 스타일만** 로비 1차 화면에 도입한다.
시안의 캐릭터는 제외한다(사용자 결정). 모든 작업 단위는 **독립 원자 커밋**으로,
문제가 생기면 해당 커밋만 `git revert` 해서 완전 복원 가능해야 한다.

## 검증 질문

로그인 후 로비에서 네온 시티 배경 위에 다크 칩+네온 테두리 메뉴 버튼과 그라디언트
START CTA 가 보이고, 기존 기능(로그인 게이트, 패널 열기, 캐릭터 키링/리액션,
디졸브 전환, 뎁스 패럴랙스)이 회귀 없이 동작하는가? 각 커밋을 revert 하면 이전
모습으로 완전히 돌아가는가?

## 작업 단위

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_neon_assets.md` | 네온 배경(밤)·글리프 3종·뎁스맵 임포트 | 순수 추가 에셋 커밋 |
| 1 | `1_button_neon_reskin.md` | LobbyNeonChip + UiRoundedSprite 그라디언트 오버로드 + 버튼 와이어링 | 메뉴 버튼 네온 리스킨 |
| 2 | `2_background_swap.md` | 디졸브/패럴랙스 슬롯을 네온 배경으로 스왑 | 배경 교체 |
| 3 | `3_handoff_summary.md` | 인계 요약 | 종료 시 작성 |

## Feature-wide 계약

- **롤백 계약**: 구 에셋(스티커 아이콘 `LobbyIcons/*.png`, 항구 배경 `lobby_bg_day/night.png`,
  `Depth/lobby_bg_depth.png`)은 삭제·수정하지 않는다. 각 unit 커밋은 단독 revert 가능해야 한다.
- **스테이징은 경로 명시만** (공유 워크트리). `.png` 는 반드시 `.meta` 짝과 함께 add.
  `ProjectSettings/ProjectSettings.asset` 불가침.
- **코드 변경 최소**: 신설은 `LobbyNeonChip`(MonoBehaviour) 1개 + `UiRoundedSprite`
  수평 그라디언트 오버로드 1개. `OutgameMenuController`/`LobbyBackgroundDissolve`/
  `LobbyBackgroundParallax` 코드는 불가침 — 배경 교체는 **인스펙터 슬롯 스왑만**.
- **칩 스프라이트는 에셋이 아니라 런타임 베이크**: `UiRoundedSprite.Make()` 재사용
  (선례: score-hud-impact-upgrade, result-screen-visual-upgrade).
- **팔레트 (시안 실측, SerializeField 기본값으로 탑재)**: 칩 채움 `rgb(16,15,40)`,
  칩 테두리 네온 퍼플 `rgb(168,85,247)` 근사, CTA 그라디언트 핑크→퍼플
  `rgb(244,114,182)→rgb(168,85,247)`, CTA 림 시안 근사. 정확값은 unit 1 에서 실측 확정.
- **낮/밤**: 사용자가 밤 버전만 제공한 상태. 낮 슬롯에도 밤 텍스처를 꽂아 디졸브를
  no-op 으로 유지한다(기능·와이어링 보존). 낮 버전 도착 시 슬롯만 교체.
- **스코프**: 로비 1차 화면(코너 메뉴 버튼 + START) 한정. dev 클러스터(`TestModeButton`,
  `DevButtons`)·패널 내부·로그인 패널은 건드리지 않는다. GO 이름 불변(튜토리얼 참조 보호).

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설/생성→렌더 경로 변경이 아니라 OutgameScene UI 리스킨 +
배경 텍스처 슬롯 스왑이다. `docs/reference/object-pipeline-map.md` 대상 아님.

## 후속 후보 (이번 스코프 밖)

- 네온 시티 **낮 버전** 배경 도착 시 daySprite 슬롯 스왑 (디졸브 전환 부활).
- 스쿼드/덱/히스토리 패널 프레임·헤더의 네온 스타일 확장.
- 시안의 프로필/재화 헤더 UI (outgame-lobby-layout 후속 후보와 병합).
- 시안 캐릭터 컷아웃 3종 활용처 검토 (스크래치패드에 분리 완료 상태였음).
