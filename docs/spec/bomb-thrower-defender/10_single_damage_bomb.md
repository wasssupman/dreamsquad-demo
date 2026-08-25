# 10 — 무작위 은퇴: 피해 폭탄 한 종

## 목적

폭탄맨의 평타에서 **3종 무작위(피해/수면/기절)를 폐기**하고 **피해 폭탄 한 종**만 던지게
한다. 무엇이 떨어질지 모르는 유닛은 플레이어가 계획을 세울 수 없다.

사용자 결정 2026-08-21: 수면·기절 폭탄은 **다른 곳으로 이사하지 않고 완전 폐기**한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 3종 추첨·분기 삭제
- `Assets/_Project/Scripts/Battle/Combat/BombLauncherState.cs` — `rng`·`sleepSec`·`stunSec` 삭제
- `Assets/_Project/Scripts/Data/Abilities/BombThrowAbility.cs` — `sleepSec`·`stunSec` 삭제
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — bake 에서 시드 계산·CC 필드 삭제
- `Assets/_Project/Scripts/Core/MatchSeed.cs` — `BombSalt`·`DeriveBombSeed` 삭제(소비처 0)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs`·`ProjectileSpawnRequest.cs` — `bombType` 삭제
- `Assets/_Project/Data/Abilities/Ability_Bomb_BombMan.asset`
- `Assets/_Project/Tests/EditMode/AttackSystemUnifiedLoopTests.cs`

## 구현

- 발사 시 `rng.NextInt(0,3)` 추첨과 3-way 변종 매핑을 삭제하고 요청에 `damage` 만 싣는다.
  `ccKind`/`ccDuration` 은 **안 싣는다**(0 = CC 없음).
- **캐스터별 결정론 시드가 함께 은퇴한다.** 뽑을 것이 없으므로 `rng` 도 `DeriveBombSeed` 도
  소비처 0 이다. 결정론은 오히려 강해진다 — 폭탄 결과에 난수가 없다.
- **`bombType` 은 삭제한다.** 뷰 변종 색(후속 후보)을 위해 request/state 에 실어 두었지만
  **읽는 뷰가 끝내 생기지 않았고** 이제 생산자도 없다. 순수 사장 필드다.
- ⚠ **`ProjectileSpawnRequest.ccKind`/`ccDuration` 과 `ProjectileHitSystem` 의 TileAoe CC arm 은
  남긴다.** 지금 생산자가 0 이지만 그건 «폭탄이 안 쓴다»는 뜻이지 «칸 광역이 CC 를 걸 수
  없다»는 뜻이 아니다. 투사체 시스템의 일반 능력이라 폭탄맨 정리에 딸려 지우지 않는다.

## 완료 기준

- [x] compile 0 에러.
- [x] EditMode 회귀 없음(폭탄 테스트 4건 포함).
- [x] (Play) 폭탄맨이 던지는 폭탄이 항상 피해만 준다 — 요청에 `ccKind` 를 아예 안 싣는다.

## 후속 후보

- **TileAoe CC arm 회수** [S] · 생산자 0 인 채로 남는다. 다른 콘텐츠가 「칸 광역 + CC」를
  쓰지 않기로 확정되면 arm 째 걷어낸다.

확인 2026-08-22 · compile 0 + 전체 EditMode 2548개 중 실패 1(기존 말파이트 desc, 무관).
