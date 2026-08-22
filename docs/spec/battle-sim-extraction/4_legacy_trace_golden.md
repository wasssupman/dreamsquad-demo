# 4 — LegacyTraceV0 골든 하네스

## 목적

M1 신 sim과의 A/B parity 기준선. 하네스 실행(units 2·3 위)에서 28채널 이벤트를 tick 스탬프·`SimEntityId` 축으로 기록한 `LegacyTraceV0`를 만들고, seed 코퍼스를 골든으로 저장한다. **직렬화 왕복을 통과시켜 기록**한다 — 네트워크에 못 탈 페이로드(오브젝트 참조 등)를 첫날부터 걸러내는 가드다. parity 기준과 동률 예외를 여기서 명문화한다.

## 변경 대상

- 신규 trace 기록기 (예: `Assets/_Project/Tests/Harness/LegacyTraceRecorder.cs`) — Bridge의 채널 drain 지점 tap (drain 소비 자체는 무변, 관찰만)
- 신규 `LegacyTraceV0` 스키마 — 헤더(`configHash`·seed·틱레이트·버전) + tick별 이벤트 레코드 + 최종 점수(int 4종)·상태 해시
- 골든 코퍼스 저장 위치 (예: `Assets/_Project/Tests/Golden/` — 트래킹 대상) + 재생성 메뉴
- EditMode/배치 러너 — 코퍼스 실행·비교 테스트

## 구현

기록 파이프: 이벤트 → 직렬화 → 역직렬화 → 재직렬화 byte 동일 검증 → 저장. 코퍼스는 seed·맵·덱 조합 N개(최소: 일반 판·보스 웨이브 판·멀티골 맵·드림캐쳐 다용 판·강제 웨이브·동시 사망 유발 시나리오·restart). **parity 기준(명문)**: semantic 이벤트 시퀀스·킬/유출 수·점수(int)·최종 상태 해시 = exact, 연속 물리값(위치·잔여시간) = epsilon. **동률 예외 목록**: KillAttribution 등량 데미지·Aggro capacity FIFO·Cc/Stat·Stack/Dot merge 동키 충돌·(unit 1이 해소한 HazardCast tiebreak 제외)·HazardSingleton 셀 순회 — 이 지점들의 차이는 parity 실패로 치지 않되 발생 시 로그. 사전 실패 테스트(CardBuffs PlayMode — main HEAD부터 가디언 dmgTaken ×1.25 실패)는 수리 또는 코퍼스에서 명시 제외를 결정해 기록.


## unit 4 에서 실제로 한 것 (2026-08-22)

### 스키마 — `LegacyTraceV0`

`Assets/_Project/Scripts/Core/Trace/LegacyTraceV0.cs`. 줄 단위 텍스트다. 바이너리로 안 한
이유: 골든이 갈렸을 때 사람이 **어디서** 갈렸는지 diff 로 바로 봐야 하고 그게 이 파일의
존재 이유다.

두 가지를 의도적으로 강제한다:

- **엔티티 참조를 싣지 않는다.** 축은 `SimEntityId`(unit 1) 하나. 오브젝트 참조를 실으면
  「지금 이 프로세스에서만 의미 있는」 기록이 되어 파일에도 네트워크에도 못 태운다 —
  그 사실을 나중이 아니라 **첫날** 알아야 한다.
- **직렬화 왕복을 통과한 것만 저장한다.** 쓰기→읽기→다시 쓰기가 바이트로 같지 않으면
  그 기록은 골든이 될 자격이 없다(비교가 포맷 잡음을 잡게 된다). 재생성 메뉴가 저장 **전**에
  이 게이트를 통과시키고, 실패하면 로그를 남기고 **저장하지 않는다**.

레코드는 채널 무관 고정 폭이다 — `tick · channel · a · b · i · f`. 채널마다 구조체를 두면
스키마가 채널 수만큼 늘고 M2 의 upcaster 도 그만큼 늘어나는데, 정작 parity 가 보는 것은
«누가·언제·얼마나» 뿐이다.

### 관측 탭 19개 (드레인 지점)

채널은 `NativeQueue` 라 소비가 파괴적이다. 큐를 미리 훔쳐보면 드레인 순서를 재현해야 하고
그건 규칙을 두 벌 만드는 일이라, **소비되는 바로 그 자리**에서 받아 적는다. 탭은 관찰만
하며 드레인의 소비 동작은 한 줄도 바뀌지 않았다. 라이브에서는 `Active == false` 라 분기
하나로 끝난다.

채널별 필드 의미(`a`/`b` 는 `SimEntityId`, `-1` = 없음):

| 채널 | a | b | i | f |
|---|---|---|---|---|
| `EnemyKilled` | 피해자 | 처치자 | awakeningReward | — |
| `GoalReached` | 적 | — | canSiege | — |
| `GoalCollapsed` | 골 | — | goalIndex | — |
| `DefenderDeath` | — | — | cell(x*1000+y) | onDeath AoE 피해 |
| `UnitAttack` | 공격자 | 대상 | — | attackAnimPeriod |
| `ProjectileSpawn` | 발사자 | — | dataIndex | damage |
| `ProjectileHit` | source | — | dataIndex | radiusWorld |
| `DamageNumber` | 피해자 | — | — | 적용 피해 |
| `HealApplied` | pos.x×100 | pos.z×100 | — | 회복량 |
| `ShieldGranted` | pos.x×100 | pos.z×100 | — | — |
| `ShieldBreak` | host | — | tileRange | magnitude |
| `Knockup` | 대상 | — | — | durationSec |
| `DcTriggerFired` | host | — | — | — |
| `CastHazardSpawn` | 시전자 | 대상 | dataIndex | — |
| `HazardRuntime` | 대상 | — | type*100+kind | amount |
| `HazardDestroyed` | 해저드 | — | hazardSoIndex | — |
| `MeteorBarrage` | — | — | meteorCount | — |
| `PatrolSpawn` | 소환주 | — | patrolDataIndex | — |
| `AttackOutputLog` | 공격자 | — | kind | magnitude |

**탭하지 않는 채널 3개**(전부 뷰 전용): `AllyBuffZoneVisuals` · `BossLeapVisual` ·
`UltimateLeapVisual`. 앞의 둘은 점등/좌표 갱신이고, 도약 둘은 sim 이 이미 텔레포트를
끝낸 뒤의 **비행 연출**이다(CLAUDE.md 채널 표 참조).

⚠ **탭이 프레젠테이션 배선 뒤에 있다.** 몇몇 드레인은 스포너가 null 이면 큐를 `Clear()`
하고 빠져나간다(`DrainShieldGrantedEvents`·`DrainDamageNumberEvents`). 씬이 온전히 배선된
하네스에서는 문제가 없지만, **뷰를 떼면 그 채널의 골든이 조용히 비는** 구조다. M1 에서
drain 소유권이 `LegacyMatchSessionAdapter` 로 옮겨갈 때 이 결합을 끊어야 한다.

### parity 기준 (확정)

- **exact**: 이벤트 시퀀스(순서 포함) · `SimEntityId` 축 · 정수 필드(점수·kind·index) ·
  킬/유출 수 · 최종 점수 · 최종 상태 해시.
- **epsilon**: 이벤트의 연속 물리값. 저장·비교 해상도를 **같은 1e-3 격자**로 통일했다
  (`TraceEvent.Quantize`). 다르게 두면 「파일로는 같은데 메모리로는 다르다」가 생긴다.
- **판독 순서**: `DiffAgainst` 는 `configHash` 불일치를 **가장 먼저** 보고한다. 조건이
  갈렸는데 이벤트 차이부터 읽으면 드리프트를 회귀로 오진한다.

### 동률 예외 (parity 실패로 치지 않되 발생 시 로그)

`KillAttribution` 등량 데미지 · `AggroCapacity` FIFO 축출 · Cc/Stat 병합 동키 충돌 ·
Stack/Dot 병합 동키 충돌 · `HazardSingleton` 셀 순회 순서.
**`HazardCast` 최근접은 목록에서 빠졌다** — unit 1 이 tie-break 를 신설해 해소했다.

### 코퍼스 7종

`Assets/_Project/Tests/Golden/<scenario>.trace.txt` (추적 대상).
재생성 `Wassup/Battle/Sim Harness/Regenerate Golden Corpus` · 검증 `… /Verify Against Golden Corpus`
(둘 다 Play 중). 결과는 `golden-corpus.md` 에 자동 기록된다.

| 시나리오 | 축 |
|---|---|
| `basic` | 일반 판(900틱) |
| `long_boss` | 60초 — 보스 웨이브 회전(5웨이브마다)을 지난다 |
| `seed_b` · `seed_c` | 다른 seed = 다른 웨이브 플랜(+ 다른 `configHash`) |
| `no_defense` | 배치 없음 — 적이 골까지 간다(공성·유출·골 붕괴 경로) |
| `restart` | 판 중간 재시작 — 매치 경계 리셋(시계·`SimEntityId`·코스트)이 새는지 |
| `force_wave` | 웨이브 당김(`TryPullNextWave`) — 회전이 시계가 아니라 **입력**으로 앞당겨진다 |

⚠ **각 반쪽이 최소 20초여야 한다.** `restart` 를 처음엔 10초씩(1200틱/재시작 600)으로
잡았더니 두 반쪽 다 교전 전에 끝나 **이벤트 0개짜리 골든**이 됐다 — 통과하지만 아무것도
증언하지 않는 골든이라 있으나 마나다. 2400틱/재시작 1200 으로 고쳐 1050 이벤트를 얻었다.

**담지 못한 축과 이유**(추측으로 채우면 골든이 거짓이 된다):

- **멀티골 맵** — 지금 `mapPool` 에 맵이 **1장뿐**이고 그 맵은 goal 1개다(실측:
  poolCount=1, goals=1, spawns=2, paths=3). seed 를 바꿔도 맵은 안 바뀐다. 풀이 늘면
  `DevMapOverride.Index` 로 인덱스를 고정해 시나리오를 추가한다. → **갱신 트리거**.
- **드림캐쳐 다용 판** — 카드 사용은 손패 드래그/부착 UI 를 지나야 해서 지금의 입력
  스케줄(배치·웨이브 당김)로는 재현할 수 없다. 커맨드 어휘가 생기는 M1 에서 추가한다.
- **동시 사망 유발** — 정확히 같은 틱에 둘을 죽이려면 피해량·거리를 저작으로 맞춰야 하고,
  그건 시나리오가 아니라 전용 픽스처 맵이 필요하다. 동률 예외 목록(위)이 그때까지의 방어다.

### CardBuffs 사전 실패 — **수리돼 있었다**

스펙이 「수리 또는 명시 제외를 결정하라」고 지목한
`DreamcatcherEffectTest.CardBuffs_ApplyToCurrentAndFutureMatchingUnits` 를 실측했더니
**통과한다**(PlayMode 단건 실행, 3.87s). 스펙 작성(08-03) 이후 어느 시점에 고쳐진 것이라
결정할 것이 남아 있지 않다. 코퍼스에서 제외하지 않는다.

## 완료 기준

- [x] 같은 seed 2회 실행 → trace diff **0**(코퍼스 7종 전부) — `Verify` 실행 결과
      `golden-corpus.md` 에 ✓ 7건.
- [x] 직렬화 왕복 무손실 — 저장 **전** 게이트(재생성 메뉴) + `LegacyTraceV0Tests` 7건
      (왕복 바이트 동일 · 전 필드 보존 · 연속값 epsilon · 정수 exact · 깨진 파일은 throw ·
      configHash 를 먼저 보고 · **디스크 코퍼스 전량 파싱/왕복**).
- [x] 골든 코퍼스 7종 저장 + 재생성 절차 문서화(위).
- [x] parity 기준 · 동률 예외 · CardBuffs 처리 기록(위).

확인 2026-08-22 · 검증 EditMode 2579건 중 실패 1건은 사전 실패(말파이트 desc 길이, 무관).
→ **M0 완료. M1 units 는 이 기준선 위에서 시작한다.**
