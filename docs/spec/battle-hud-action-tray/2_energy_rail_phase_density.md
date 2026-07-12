# 2 — 코스트 레일과 Phase 밀도

## 목적

363×112 부유 코스트 배지를 트레이 상단의 compact energy rail로 축소한다. Placement/Battle 전환 때 스트립과 레일이 함께 움직여 12→44px로 벌어지는 현행 결함을 제거한다. 선행: units 0~1.

## 변경 대상

- `Assets/_Project/Scripts/UI/CostDisplay.cs`
- `Assets/_Project/Scripts/UI/DefenderSelector.cs`
- `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs`
- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

- 기존 bolt, `N/max`, segmented bar 정보는 유지하고 Config의 약 264×64 rail 치수에 맞게 재배치한다.
- rail은 bottom-center safe root 기준으로 tray top edge에 overlap되며, placement/battle tray height로 y를 계산한다.
- `DefenderSelector.OnPhaseChanged`와 `CostDisplay.OnPhaseChanged`가 같은 Config geometry를 소비해 size/position이 한 frame에 정합되게 한다.
- 표시 결정은 계속 `CostDisplay.RefreshVisible()`이 단독 소유하고 HandView는 `SetSuppressed` 신호만 보낸다.
- 첫 구현은 snap 전환으로 둔다. 160ms tween은 시각 필요성이 확인될 때만 후속으로 추가한다.

## 완료 기준

- [x] Placement에서 rail과 tray가 하나의 클러스터로 읽힘.
- [x] Battle에서 rail이 tray를 추종하고 사이에 44px 공백이 생기지 않음.
- [x] 0/부분 regen/10 상태에서 숫자·segment 가독성 유지.
- [x] Hand open 시 rail 퇴장, close 시 현재 phase 위치로 정확히 복귀.
- [x] 보드 최고 가림선이 현행 y=276보다 낮아지고 캡처로 비교 기록.

확인 2026-07-12 — 1차 구현(EnergyRail 아트 캡슐, 2줄 레이아웃)은 **시안(battle-hud-safe-action-tray-proposal.jpg)과 불일치로 사용자 기각** → rev: 레일 = 트레이 동색(fill/border 재사용) 탭(440×54, overlap 14, 시안 정합), ⚡+10/10+세그먼트 **한 줄**, 위치는 Config geometry 공유로 phase 추종. 같은 rev 에서 unit 1 비용 표기도 시안 정합(원형 chip → 다크 코너 플레이트 ⚡+숫자, 이름 밴드 0.72 다크, dim 그레이 근사). 가림선: 배지 top y=276 → 레일 top y=222(placement). `CostDisplay.trayConfig` 씬 배선 1줄, config 미할당 시 기존 부유 배지 무회귀 폴백. EnergyRail/CostChip 원본 PNG 는 미사용 잔존(다른 용도 후보). 콘솔 0. 사용자 일괄 확인은 units 3~5 체크리스트와 함께.

## rev 2 — phase 기하 고정 + 풀폭 레일 + 코스트 연출 (사용자 결정 2026-07-12)

- **"트레이 = 전투 직결 핵심 인지 UI" 전제 확정** — 배치/배틀 크기 전환(136→104)이 어색하다는 피드백으로 **phase 축소 자체를 폐지**: `battleSize = placementSize = 980×136` (battle-hud-layout 의 "Battle 슬림" 결정을 뒤집는 product 결정). 트레이·레일이 페이즈 무관 픽셀 고정 — 근육 기억 형성.
- **레일 풀폭**: 440 → **980×44** (트레이 width 일치, CR 엘릭서 바 문법). 좌측 ⚡+큰 숫자(30pt), 나머지 폭 전체 10칸 세그먼트(가로 leading fill — 리젠 진행 방향 직관화). 가림선 top 222→198.
- **코스트 연출 3종**: 정수 도달 칸 스케일 팝(1.25→1, 0.18s) · 소비 시 잃은 칸 warn 플래시(0.3s) · 10/10 풀 게이지 글로우 펄스(sin, "쓸 준비 됨" 신호). 전부 실시간(unscaled) — 슬로모 무관. 세그먼트 재구성 시 연출 기준점 리셋(_lastShownInt=-1).
- 검증: 풀(10/10)·부분(4/10) 캡처 — 상태 즉독 + affordability 연동(4 보유 시 5코스트만 dim) 확인. 콘솔 0. 팝/플래시/글로우 실감은 에디터 육안 확인 권장.
