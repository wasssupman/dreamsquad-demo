# 4b — 광역 멤버십

## 목적
`TileAoe` 를 같은 자로 옮긴다. **시그니처가 바뀐다** — 지금은 셀 좌표(`int2`)만 받아 몸 기준으로 못 간다.

## 변경 대상
- `Combat/TileAoe.cs` — 위임 + 시그니처 확장(월드 위치·반폭·반경)
- **소비처 6곳**: `Combat/AggroTargeting.cs:55` · `Combat/DefenderDensity.cs:41` ·
  `Combat/Projectile/BounceRetarget.cs:70` · `Combat/Projectile/ProjectileHitSystem.cs:733`(광역 착탄) ·
  `Battle/Skills/EcsSkillContext.cs:440` · `Bridge/BattleBridge.cs:4617`(실드 파열)
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
- [ ] `TileAoeTests` 갱신 후 초록. 광역 착탄(`ProjectileHitSystem:733`)과 어그로(`AggroTargeting:55`)는
      판 흐름 직결이라 Play 육안 1회.
- [ ] 고정 스텝 하네스 **2회 실행 일치**(정수→float 전환의 결정론 확인).
