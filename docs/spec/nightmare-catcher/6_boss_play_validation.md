# 6 — 보스 콘텐츠 authoring + Play e2e + 렌즈 B

## 목적

첫 나이트매어캐쳐 보스를 실제 콘텐츠로 만들어 두 메커닉을 실전투에서 검증하고, 배선 전체를 렌즈 B(ECS 경계/회귀)로 리뷰한다.

## 변경 대상

- 신규 에셋: 보스 `AttackUnitData` + 프리팹/뷰 + 웨이브 스폰 배선
- (테스트) EditMode 순수함수 이미 units 1~3, PlayMode e2e 1개

## 구현

### 보스 콘텐츠 authoring
- 보스 `AttackUnitData`: 스탯(HP/이동/기본공격 output=100) + `nightmareMechanics`:
  - `PeriodicTimer(periodSeconds=10)` × `AreaBarrage(tileRange=3, magnitude=10, projectile=SkyFall, duration=낙하 텔레그래프 초 — unit 0 rev 2)`
  - `HealthThreshold(fraction=0.10)` × `SelfBlink(tileRange=blink 탐색반경)`
- (rev 2) `awakeningReward` — 보스 처치 각성 보상. awakening-hand 백필 스케일(대형/특수 3) 준용 또는 보스 전용 상향, authoring 시 결정.
- 프리팹/뷰: 적 아키타입(Spine or Quad) — `object-pipeline-map` §적유닛 대조.
- 웨이브 스폰: WavePattern/스폰 경로에 보스 1기 배선.

### Play e2e (검증 질문 분해, rev 2 갱신)
1. 스폰 후 **10초 주기** 진앙(임의 방어유닛) 3타일 AoE, 방어유닛 10 데미지(적/보스 자해 없음).
2. 보스 HP **누적 10% 감소마다** 위협 리더(가장 많이 딜한 방어유닛 — 원거리 포함) 근처 blink.
3. 폭격·텔레포트·기본공격이 **동시(직교)** — 서로 timer/AttackState 안 리셋.
4. **무회귀**: 기존 드림캐쳐 트리거/스탯 카드 + **Active 카드 Meteor**(awakening-hand 손패 경로, `skillRuntime` dormant 상태에서 캐스트) 정상 — 진영 플래그 기본값=enemy 확인, 플레이어 Meteor 가 보스를 때려도 threat 엔트리 없음(owner=Null 가드).
5. 힐로 HP 회복 시 같은 경계 **재blink 없음**(래치).
6. (rev 2) **각성 경제 편입**: 보스 처치 → `awakeningReward` 게이지 가산. 폭격으로 방어유닛 사망 → 각성 +4 + 부착 Unit/Squad 카드 회수(큐 맨 뒤) 자동 동작 — 신규 코드 0 로 성립해야 함.
7. (rev 2) **손패 열림(슬로모 0.3x) 중** 폭격 주기·텔레그래프·blink 가 감속된 채 정상 동작, 카드 커밋/취소와 충돌 없음.

### 렌즈 B (ECS 도메인 리뷰)
게이트/베이크/arm 코드 이후 ecs-reviewer: 맥락 경계(위치=Movement, threat 쓰기=Combat), NativeQueue lifecycle(BlinkRequest/ThreatHit), Burst 호환, teardown leak, 시스템 순서.

## 파이프라인 커버리지

`object-pipeline-map` §적유닛 + §투사체 대조 — 이 unit 에서 확정:

| 정거장 | 이번 spec | 비고 |
|---|---|---|
| 데이터 SO | `AttackUnitData`(nightmareMechanics) + 기존 `ProjectileData`(SkyFall) | 신규 SO 타입 0 |
| 스폰 진입점 | `BattleBridge.SpawnUnit`(`:4126`) 보스 분기 베이크 | defender 부착 API 미사용 |
| ECS 컴포넌트 | 기존 + `BossTag` + `ThreatTable`(buffer) + `DcTriggerSlot` | |
| 시뮬 시스템 | 신규 3(Periodic/Threshold/BlinkApply) + 기존 Projectile/Movement | |
| 이벤트 큐 | 신규 `ThreatHitEventsSingleton`·`BlinkRequestEventsSingleton` + 기존 ProjectileHit | 채널 +2 |
| View/Pool | 기존 적 뷰(Spine/Quad) + `ProjectileViewPool`(SkyFall) | |
| Teardown | `DestroyEntitiesByType<AttackUnitTag>`(`:373`) — 적 경로 상속 | 신규 teardown 0 |

## 완료 기준

- [x] 위 Play e2e 7개 실기/에디터 확인. (사용자 확인 2026-07-10 — 폭격 데미지 정상(피드백 부재였음), 텔레포트 동작, blink 연출 확인)
- [x] 렌즈 B 통과(반영 후). (1·4·5 PASS + 2·3 arm PASS — CRITICAL/HIGH 0)
- [x] 채널 목록(CLAUDE.md) +2 갱신(16→17: ThreatHit·BlinkRequest). `object-pipeline-map` 신규 아키타입 0 → 맵 갱신 불요.

## rev 3 — 실플레이 피드백 반영 (2026-07-10)

- **blink 연출**: 무연출이 어색 → `Projectile_BlinkPuff`(hit-VFX 전용 ProjectileData) 신설, 출발지+도착지 퍼프를 기존 `ProjectileHitEvents` 채널로 재생(신규 시스템/채널 0). 베이크(SelfBlink `projectile` 슬롯) + arm enqueue.
- **VFX 렌더 함정**: GA 벤더 ProjectileData 의 `hitPrefab` 이 Muzzle(빔 2개·사실상 무연출)을 가리킴 — 오프스크린 렌더 픽셀검증으로 실물 임팩트(`vfx_Hit_RotatingSpheres03`, 2643px) 선별해 교체. (README 후속 후보 = GA hitPrefab 전수 정비)
- **튜닝**: 퍼프 `hitVfxScale` 2, 텔레포트 `fraction` 0.10→**0.30**(HP1000 = 700/400/100 발동). config/SO 값이라 코드 무변경.
- **보스 콘텐츠**: `Enemy_Boss_Nightmare`(HP1000·근접100·reward5·Tanker 외형 스케일2.1) + `WavePlan_BossTest`(t=6 보스1) + TestModeConfig/EnemyCatalog 등록.
- **범위 밖 발견**: 보스가 타겟 없으면 goal 로 걸어가 누수 — 별도 spec `enemy-hunter-targeting` 으로 분리(사용자 확정).
