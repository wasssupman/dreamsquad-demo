# 4. 전방 관통 일격 — 조준 방향 + 통로 폭

## 목적

배치 순간의 전방 관통 일격(`OnPlaceEffectType.ForwardProjectile` — 머신거너·마크스맨·피어서·스나이퍼)이
**적을 향해** 나가게 한다. 현재는 총구가 사실상 항상 남쪽을 보고 있어 한 마리도 맞히지 못한다.

⚠ **이 수정은 명중만 고친다.** 맞게 돼도 "배치 스킬이 따로 있다"는 체감은 생기지 않는다 — 이 4종의
배치 스킬이 각자의 평소 공격과 **같은 방향·같은 직선 모양**이고 크기만 1.3~2.8배이기 때문이다
(머신거너: 평소 5딜×10발=50/1.9초, 배치 70 1회. 방향은 둘 다 조준 방향). 정체성 재설계는 별건이며
README 후속 후보에 있다. 이 문서는 "0마리를 맞힌다"는 결함만 닫는다.

## 증상과 원인 (실측 2026-08-15)

사용자 플레이 세션 콘솔 — 배치 4회 전부 `affected=0`:

```
On-place 마크스맨: ForwardProjectile affected=0 at (8, 6)
On-place 머신거너: ForwardProjectile affected=0 at (8, 4)
On-place 마크스맨: ForwardProjectile affected=0 at (7, 6)
On-place 마크스맨: ForwardProjectile affected=0 at (5, 6)
```

**원인 1 — 방향이 남쪽으로 고정.** `FindNearestPathDirection` 은 맵 전체를 y 오름차순·x 오름차순으로
훑으며 `d2 >= bestDistSq` 로 갱신을 막는다 = **동점이면 먼저 스캔된 칸이 이긴다.** 배치 셀의 이웃
8칸이 전부 `Walk` 이면(배치 마스크가 Walk 위 배치를 허용한 뒤로는 보통) 거리 1 동점자 넷 중 y 가
가장 작은 **남쪽 이웃이 항상 이긴다.** 실측: 21×12 맵 252칸 중 **173칸이 정확히 (0,-1)**, 나머지는
대부분 남쪽 이웃이 없는 맨 아랫줄. 적 위치·길 방향·플레이어 조준과 **무관하다.**

**원인 2 — 통로가 유닛 한 칸보다 좁다.** 반폭 `tileSize * 0.45`(총 0.9칸)인데 적은 레인 오프셋으로
±0.5 흩어져 걷는다. 실측에서 바로 옆 적 2마리가 `lat=1.0` 으로 탈락했다.

측정(같은 판, 적 7마리가 0.1~1.0칸 거리에 있는 상태에서 (7,6) 판정 재현):
`HIT=0 BEHIND=5 OFF-LANE=2`.

**부수 — 조준 무시.** `ActivateDeployedDefender(cell, entity, facing)` 는 `DeployedFacing` 을
**on-place 앞에** 붙여 두는데(그 주석이 명시), 정작 이 분기가 읽지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyForwardOnPlaceProjectile`, `FindNearestPathDirection`
- `Assets/_Project/Tests/PlayMode/OnPlaceForwardProjectileTest.cs` (신규)

## 구현

1. **방향 결정 (사용자 결정 2026-08-15: 조준 우선 + 없으면 최근접 적)**
   - `DeployedFacing` 이 있고 0이 아니면 그 방향. 방향 지정 유닛(머신거너)의 조준이 스킬에 실린다.
   - 없으면 **사거리 안에서 가장 가까운 타겟 가능한 적** 방향. 조준 UX 가 없는 3종의 규칙.
   - **사거리 안에 적이 없으면 조준이 있어도 일어나지 않는다.** 조준은 방향만 정하고 사건 성립은
     적이 있어야 한다 — 적 없는 배치 페이즈에 허공을 쏘지 않는다. 배치 페이즈 발동 정책 자체
     (전투 시작 시점으로 미루기 등)는 이번 범위 밖(README 후속 후보).
   - `FindNearestPathDirection` 은 소비처가 이 함수뿐이므로 **삭제한다.** 남겨두면 "길 쪽으로"라는
     죽은 규칙이 다음 사람을 오도한다.
2. **통로 반폭** `0.45` → `0.6` (상수). 레인 오프셋 ±0.5 를 덮는 최소치.
3. 판정식(`along` 0..length, `lateral` ≤ 반폭)과 피해 적용 경로는 그대로 둔다.

## 완료 기준

- [x] PlayMode `OnPlaceForwardProjectileTest` green (3/3, 2026-08-15)
  - 조준 없음 + 적이 **북쪽**(고장난 코드가 못 맞히는 방향) → 피해 들어감
  - **조준 4방향을 각각 다른 칸에서** 검사 → 전부 그 방향의 적이 맞음.
    ⚠ 한 방향만 검사하면 안 된다 — 고장난 코드도 셀마다 고정 방향이 있어 우연히 통과한다
    (초판이 실제로 새는 통과를 냈고, 4방향으로 바꾸자 두 번째 방향에서 무너졌다)
  - 전방 3칸 · 옆 0.55칸 적 → 피해 들어감(폭 확장 회귀 방지)
- [x] compile 0 error
- [x] 인접 PlayMode 회귀 15/15 green (Beam·OnPlaceDot·OnPlaceStack·Relocation·PlacementAura·BoardLimit)
- [ ] 실제 플레이에서 배치 시 콘솔 `On-place ...: ForwardProjectile affected=N` 의 N > 0
      (적이 사거리 안에 있는 배치에서) — **사용자 육안 확인 대기**

커밋: `e3c231bf` (2026-08-15, 자동 검증분만 확인)
