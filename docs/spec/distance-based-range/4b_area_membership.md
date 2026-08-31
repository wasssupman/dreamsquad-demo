# 4b — 광역 멤버십

## 목적
`TileAoe` 를 같은 자로 옮긴다. **시그니처가 바뀐다** — 지금은 셀 좌표(`int2`)만 받아 몸 기준으로 못 간다.

## 변경 대상
- `Combat/TileAoe.cs` — 위임 + 시그니처 확장(월드 위치·반폭·반경)
- **소비처 4곳**(2곳은 결정 4 로 제외): `Combat/AggroTargeting.cs:55` · `Combat/DefenderDensity.cs:41` ·
  `Combat/Projectile/BounceRetarget.cs:70` · `Combat/Projectile/ProjectileHitSystem.cs:733`(광역 착탄) ·
  `Bridge/BattleBridge.cs:4617`(실드 파열)

⚠ **제외 2곳** — `Battle/Skills/EcsSkillContext.cs:440`(스킬 arm, 결정 4) ·
`Combat/DefenderDensity.cs:41`(**셀 통계**다 — `NativeArray<int2>` 만 받아 위치가 없고,
「밀집한 **칸**」이라는 물음 자체가 셀 개념이다. 보스 순간이동 착지 지점이 이 함수이므로
억지로 바꾸면 착지가 조용히 바뀐다).
따라서 `TileAoe` 는 **셀 오버로드를 남긴 채** 월드 오버로드를 추가하는 형태가 된다.
- `Effects/AllyBuffFieldSystem.cs:64` — 아군 버프 장판 멤버십
- 브리지 2종: `BattleBridge.cs:2845` `CollectAlliesInRange` · `:2869` `InTileRange` (각각 **1줄**)
- `Tests/EditMode/TileAoeTests.cs` (14 단언)

## 구현
- **브리지는 후보를 모을 뿐 판정은 이미 공유 술어다.** 3종 모두 교체는 1줄씩이고,
  `CollectAlliesInRange`·`InTileRange` 는 **로그·사전집계 전용**이다(시뮬 권위는 ECS `DefenderTile`).
- ⚠ `TileAoe` 는 지금 **정수 산술**이라 비트 정확하다. float 로 내려가면 AoE 멤버십이 **이산 결정**
  인데 결과가 `ProjectileHitEvent`·`EnemyKilledEvent`·점수(int)를 낳는다 —
  `battle-sim-extraction` parity 계약상 **exact 대상**이다.
  · 완화: sqrt 금지(계약 1). · **후퇴 경로**: M1 교차 골든에서 AoE 경계가 갈리면
  **AoE 멤버십만 정수로 되돌린다** — 셀 좌표가 정수라 `dx²+dy² ≤ (rangeTiles + rTiles)²` 로
  **원 모양을 유지한 채** 정수 비교가 가능하다(사거리 판정은 float 유지).

## 완료 기준
- [x] `TileAoeTests` 갱신 후 초록. 광역 착탄(`ProjectileHitSystem:733`)과 어그로(`AggroTargeting:55`)는
      판 흐름 직결이라 Play 육안 1회.
- [x] 고정 스텝 하네스 **2회 실행 일치**(정수→float 전환의 결정론 확인).

---

### 진행 기록 — 완료 2026-08-31

**시그니처를 바꾸지 않았다.** 소비처의 중심이 **전부 이미 칸에 물려 있어서**다 — 투사체 착탄은
발사 시점에 칸으로 고정(`impact` 는 cell-locked), 실드 파열·어그로·오라는 유닛의 칸.
월드 정밀도를 얹어도 얻는 것이 없고, 정수 입력이라 parity 계약에도 안전하다.
그래서 `TileAoe` 는 **셀 오버로드를 남긴 채** 모양만 바꾼 `IsInRadius` 를 추가하는 형태다.

| 함수 | 모양 | 용도 |
|---|---|---|
| `IsInTileRange` | 정사각형(체비셰프) | **격자 통계 전용으로 강등** — `DefenderDensity`(보스 착지) · `EcsSkillContext`(칸 조준) · `MovementSystem`(회오리 **장**) |
| `IsInRadius` | 모서리가 둥근 사거리 | 광역 멤버십 정본. **사거리 술어와 같은 본체**(`SkillMath.InBodyReach`) |

교체 7곳: `AggroTargeting` · `BounceRetarget` · `ProjectileHitSystem`(착탄, **대상 몸 반경 포함**) ·
`AllyBuffFieldSystem`(오라) · `BattleBridge` 3곳(실드 파열 · `CollectAlliesInRange` · `InTileRange`).

#### ⚠ 순수 원으로 썼다가 되돌렸다 — 반경 1 폭발이 십자가 됐다

처음엔 spec 의 후퇴 경로대로 `dx² + dy² ≤ r²` 로 썼다. `ProjectileSystemTests` 가 즉시
빨개졌다 — **반경 1 폭발이 대각 이웃을 통째로 잃는다**(대각 칸의 중심거리 1.41 > 1).
격자에서 작은 반경의 원은 그렇게 무너진다.

무엇을 잘못 읽었나: **0.5 는 「공격자의 몸」이 아니라 「칸의 반폭」이다.** 후보는 점이 아니라
한 변이 1인 정사각형이고, 「반경 r 안인가」의 옳은 물음은 그 사각형의 **가장 가까운 점**까지의
거리 — `max(|Δ| − 0.5, 0)` 다. 사거리 술어의 0.5 도 정확히 같은 것이다(그쪽은 공격자가 칸 위에
서 있다). **그래서 두 물음이 같은 식으로 수렴한다** — 이 unit 의 목표가 우회가 아니라
정면으로 달성됐다.

#### 골든 무변화 — 「무회귀」가 아니라 **반경 1 에서 두 자가 수학적으로 같기 때문**이다

재생성했는데 8건 전부 바이트가 안 움직였다. 커버리지 공백이 아니다:

- 체비셰프 반경 1 = 9칸. 둥근 반경 1 = |Δ|=(1,1) → v=(0.5,0.5) → 0.707 ≤ 1 → **같은 9칸**.
  (2,0)은 v=1.5 로 둘 다 밖. **반경 1 에서 두 집합이 완전히 일치한다.**
- 차이는 **반경 2 이상에서만** 난다(정대각이 빠진다). 저작된 광역 투사체 19종 중 반경 2+ 는
  `ArtilleryShell`(2) · `NightmareBarrage`(3) **둘뿐**이고, 코퍼스 덱에 없다.

→ **저작의 절대다수(반경 1)는 이 변경의 영향을 받지 않는다.** unit 6 이 밸런스를 볼 때
「광역이 좁아졌다」를 전 범위로 확대 적용하지 말 것 — 좁아진 것은 반경 2+ 의 **모서리 한 칸씩**이다.

계약은 골든이 아니라 `TileAoeTests` 4건이 진다: 반경 1 여덟 이웃 유지 · 반경 2 정대각만 제외 ·
두 함수가 실제로 다름(같아지면 보스 착지가 조용히 움직인다) · 대상 몸이 폭발을 넓힘.

**검증**: EditMode 2665건 / 실패 2건(선행 — `boomerang`·`bomb_man` 문안) ·
고정 스텝 **2회 실행 완전 일치**(`configHash 2a8cdc9e9597a838` 양쪽 동일 — 정수→float 전환이
결정론을 깨지 않았다) · 골든 8건 무변화.

> 부수 관측: 재생성을 **결정론 체크 직후 같은 Play 세션**에서 돌렸더니 8건 전부 빈 트레이스로
> 나왔고 **공허 게이트가 저장을 거부**했다(골든 파일 무변경). unit 1 에서 만든 게이트가 실제로
> 사고를 막은 첫 사례다. 재생성은 **Play 세션의 첫 동작**이어야 한다.
