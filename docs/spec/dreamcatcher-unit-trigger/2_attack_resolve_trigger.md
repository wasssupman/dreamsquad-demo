# 2 — AttackSystem RESOLVE 카운트/발동 arm

## 목적

부착된 `DcTriggerSlot` 을 RESOLVE 시점에 카운트하고, period 도달 시 페이로드(첫 케이스: 공격 대상에게 투사체)를 기존 파이프라인으로 발행한다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` (static 순수함수)
- 수정: `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- 신규: `Assets/_Project/Tests/EditMode/DcTriggerTests.cs`

## 구현

순수함수 (Burst-호환 static, EditMode 대상):

```csharp
public static bool Tick(ref ushort counter, ushort period)
// counter++; counter >= period 면 counter=0, true(발동). period==0 가드 → false(발동 없음).
```

AttackSystem:

- `BufferLookup<DcTriggerSlot>` 추가 (RW).
- RESOLVE 블록(`doResolve && bestTarget != Entity.Null`) 끝에, **defender 이고**(`defenderTagLookup`) 슬롯 버퍼가 있으면 슬롯 순회:
  - `trigger == AttackN` 만 처리(그 외 skip). `Tick` 이 true 면 payload switch:
  - `ProjectileToTarget`: `var e = ecb.CreateEntity();` 후 `ProjectileSpawnRequest { movement=HomingToEntity, payload=SingleSplash, target=bestTarget, origin=atkPos, damage=slot.magnitude, speed/hitThreshold/visualScale/dataIndex=슬롯 값, splashRadius=0, splashDamageMul=0, onHitEffect=None }` + `ProjectileRequestCarrier` 태그를 `e` 에 add.
    - **ECB deferred 생성 필수** — foreach 안 `state.EntityManager.CreateEntity` 는 throw. 캐리어는 `ecb.Playback` 시점(같은 프레임, BattleBridge drain 이전)에 실체화. AttackSystem 첫 `ecb.CreateEntity` 사용처.
    - `bestTarget` 생존 보장: 데미지는 IncomingDamage 버퍼로 지연 적용되므로 RESOLVE 블록 내 즉사 경로 없음 — dc arm 시점에 타겟은 살아 있다 (코드 주석으로도 남길 것).
  - `attackOutputLogWriter` 에 `AttackOutputLogEvent { attacker=attackerEntity, kind=Damage, magnitude=slot.magnitude, duration=0f, sourcePos=atkPos, targetPos=bestTargetPos }` enqueue (배틀 로그 일관성, stat/stackKind 는 default).
- 카운트는 원거리/근접 공통 — `projectileRefLookup`/outputs 유무와 무관하게 RESOLVE 성립 = 1카운트.
- `damageMul` 미적용 (계약 7 — flat).
- Burst: 슬롯/enum 전부 unmanaged, `ecb.CreateEntity` 는 ECB 기록이라 job 내 가능.

## 완료 기준

- [x] EditMode `DcTriggerTests`: period=5 에서 4회 false→5회째 true+counter 리셋 / period=1 매회 true / period=0 항상 false / 독립 counter 2개 비간섭
- [x] 기존 EditMode/PlayMode 무회귀
- [x] execute_code Play 확인: 부착 유닛의 5회째 타격마다 캐리어 request 가 생성·드레인되어 투사체 엔티티 스폰 (unit 3 에서 e2e 확정)

완료 확인: 2026-07-09 — 컴파일 클린, EditMode 565 (신규 DcTriggerTests 4/4, 실패는 기지 사전실패 2건뿐). 실전투 Play(HP 부스트 아처, period=2): 세션 로그에 `Archer Damage 20.0 × 5` 가 기본 공격(15.0×8)과 나란히 기록 — 위상 정확, 전투 중 캐리어 잔존 0. 패배 종료 프레임의 미드레인 캐리어 1개 잔존 경로 실측 → 설계대로 teardown 안전망 대상(무해). ecs-reviewer: CRITICAL/HIGH 0, MEDIUM 1건 반영(미지원 payload 발동 시 침묵 소비 → LogWarning). 이 문서와 동일 커밋.
