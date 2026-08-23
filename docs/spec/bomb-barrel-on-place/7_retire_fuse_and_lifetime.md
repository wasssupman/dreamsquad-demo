# 7 — 시한과 퓨즈 틴트 은퇴 (배럴은 부서져야만 터진다)

## 목적

배럴에서 **시계를 걷어낸다.** 폭발은 「적이 부순 사건」이지 「시간이 된 사건」이 아니다.
unit 1(수명)과 unit 6(퓨즈 틴트)은 **한 몸**이라 같이 은퇴한다 — 틴트의 존재 이유가
「언제 터지나」를 예고하는 것이고, 예고할 시계가 없으면 틴트도 할 말이 없다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/BlockingHazardSO.cs` — `lifetime` · `fuseTintColor` · `fuseTintExponent` 제거
- `Assets/_Project/Scripts/Battle/Effects/ObstacleLifetimeSystem.cs` — 길막 수명 루프 제거
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs` — `remainingLife` 항상 무한
- `Assets/_Project/Scripts/Battle/Effects/BlockingHazardPresenter.cs` — `SetFuseTint` 및 틴트 상태 제거
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SyncBlockingHazardFuseTint` 및 호출 제거
- `Assets/_Project/Scripts/Battle/Effects/BlockerFuse.cs` · `Tests/EditMode/BlockerFuseTests.cs` — 삭제
- `Assets/_Project/Data/Hazards/Blocker_BombBarrel.asset` — `lifetime` · 틴트 키 제거

## 구현

- **`DeadTag` 생산자가 하나로 줄었다.** 이제 배럴에 `DeadTag` 를 다는 것은
  `DamageApplicationSystem`(체력 0)뿐이고, 그것이 폭발의 **유일한 계기**다.
- **`ObstacleLifetimeSystem` 의 길막 루프는 통째로 뺀다.** 첫 루프(장판형 해저드 수명)는
  그대로다 — 시간으로 사라지는 것은 장판이지 벽이 아니다.
- ⚠ **필드만 지우면 안 되고 루프도 같이 지운다.** 반대도 마찬가지다 — 한쪽만 남기면
  「수명을 저작했는데 아무 일도 안 일어난다」는 조용한 실패가 된다.
- ⚠ **`BarrelExplosionSystem` 의 `[UpdateAfter(ObstacleLifetimeSystem)]` 은 남긴다.**
  의미는 잃었지만 M0 unit 0 이 얼린 `BattleSimGroup` 총순서를 유지하는 장치다. 떼면
  정렬기가 자리를 옮겨 **골든 트레이스가 이유 없이 갈린다**.
- 틴트 제거는 `MaterialPropertyBlock` 경로 전체 철거다. 벤더 메시 공유 머티리얼 함정
  (unit 6 의 ⚠)은 이 코드가 사라지면서 같이 사라진다.

## 이 변경이 건드리는 것 (알고 하는 것)

- **`configHash` 가 바뀐다.** `BlockingHazardSO` 에서 필드 3개가 빠지므로 리플렉션
  다이제스트가 달라진다 — M0 골든 코퍼스는 **재생성 대상**이다. 판독 장치는 이걸
  「코드 회귀」가 아니라 「조건이 실제로 바뀜」으로 먼저 말한다(설계대로).
- **배럴이 영구물이 됐다.** 아무도 안 때리면 판 끝까지 남아 그 칸을 막는다. 배럴은
  2타일 안 최근접 적의 칸에 떨어지므로 실질적으로 길 위에 서고, 길이 막힌 적은 벽을
  때리므로(기본 타겟 마스크에 `BlockingHazard` 비트) 스스로 해소된다. 그래도
  **길 밖에 떨어진 배럴은 영영 남는다** — 체감이 나쁘면 그때 「배치 한 판에 1개」 같은
  개수 상한이 시한보다 나은 도구다(시한은 폭발의 성격을 되돌린다).

## 완료 기준

- [x] compile 0 에러.
- [x] `BarrelExplosionTests.Barrel_NeverExpires_NoMatterHowMuchTimePasses` — 60초를 밀어도
      `DeadTag` 가 안 붙고 차단 칸에 계속 남는다.
- [x] 전체 EditMode 2574건 중 실패 1건 = 사전 실패(말파이트 desc 30자, 무관).
- [x] (Play) 배럴을 세우고 **24.6초 방치** — `remainingLife=Infinity` · `DeadTag` 없음 ·
      생존. 구 수명 12초를 두 배 넘겨도 안 터진다.

확인 2026-08-23 · Play 실측(BattleScene · 콘솔 에러/경고 0).
