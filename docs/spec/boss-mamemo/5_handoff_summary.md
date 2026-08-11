# 5 — Handoff Summary

> 상태: **units 0~4 종료 2026-08-11** (사용자 Play 확인 + 투트랙 코드리뷰 반영).
> 남은 것은 **밸런스 관측 1축**뿐이고 아래 Follow-up 에 있다.

## Commit

| 커밋 | 내용 |
|---|---|
| `7c285efc` | 스펙 (rev 2 — 투트랙 스펙 리뷰 반영) |
| `1e74b932` | unit 0 — 에셋 + `EnemyCatalog` + `bossPool` 3종 (잡몹 편성 무회귀) |
| `77bd1836` | unit 1 — 자장가 (주기 × 수면) |
| `5630f42f` | 육안 확인용 웨이브 플랜 |
| `02d60bb0` | units 2·3 — 적 실드 개통 + 꿈의 장막·악몽의 가호 |
| `ec4f793e` | unit 4 — 실드 부여 연출을 가디언 채널로 |
| `8ae31a04` | 튜닝 — 자장가 반경 5 → 3 |
| `9dbe48e0` | **투트랙 코드리뷰 반영** — 도넛 자기무효화 외 6건 + 반경 3 → 4 |

설계 근거·접은 대안: `docs/plans/2026-08-11-boss-mamemo-design.md`

## Implemented

- **마메모 = 웨이브 회전을 멈춰 점수를 깎는 보스.** 아무도 안 죽이면서 시간을 가져간다.
- **① 자장가** `PeriodicTimer(3.5s) × AreaSleep(3명 / 2.5초 / 도넛 3~4)` — **신규 페이로드 0**
- **② 꿈의 장막** `HealthThreshold(0.34) × GrantShield(350, self)` — 66%·32% 2회
- **③ 악몽의 가호** `PeriodicTimer(2.5s) × GrantShield(60, 반경 4, host 제외)`
- **`GrantShield = 19`** 신설 — `tileRange` 0/>0 로 두 능력 겸용, 배선은 트리거별로 갈린다
- **적 전원에 `ShieldSlot`/`IncomingShield` 쌍 부착** + 적 오버헤드 실드 게이지 개통
- `bossPool` 3종 로테이션 (8개 덱) — 잡몹 편성 무회귀 EditMode 고정
- **신규 맥락 0 · 신규 시스템 0 · 신규 이벤트 채널 0** — CLAUDE.md 채널 목록 무변경

## Key Files

- `Battle/Combat/BossPeriodicTriggerSystem.cs` — 자장가 · 악몽의 가호 arm
- `Battle/Combat/HealthThresholdSystem.cs` — 꿈의 장막 arm
- `Battle/Combat/AuraPulse.cs` — `SelectRing`(도넛) · `SelectTargets` 는 위임
- `Battle/Combat/DcTrigger.cs` — `EnemyTriggerArmed`(적 트리거 화이트리스트 단일 SoT)
- `Bridge/BattleBridge.cs` — bake 가드 5건 · `SpawnUnit` 버퍼 쌍 · `ShieldRatioOf`
- `Data/Enemies/Enemy_Boss_Mamemo.asset` — 스탯 + mechanic 3개

## Verified

- **EditMode 2166 중 2163 통과 · 실패 0** · 스킵 3(전부 기존 `[Ignore]`)
- **PlayMode 마메모 5/5** — 자장가 2 · 실드 2 · 적 실드 1. 전부 **실스폰 경로**로 세우고
  버퍼를 직접 읽는다(슬롯을 손으로 만들지 않으므로 에셋 저작이 틀리면 빨개진다)
- **회귀 가드가 실제로 무는지 확인함** — `+1` 을 빼고 돌려 `BossLullabyTest` 가 «사거리 링은
  안 잔다» 단언에서 빨개지는 것을 봤다
- **PlayMode 전체 101 중 17 실패는 전부 사전 존재** — 이 spec 이 만진 세 파일을 unit 1 이전
  커밋으로 되돌려 재실행해도 같은 테스트가 그대로 실패했다. `SceneTransitionSmokeTest` 는 flaky
- 사용자 Play 확인 2026-08-11

## Notes — 되돌리면 안 되는 것

1. **자장가 도넛의 `attackTiles + 1`.** 공격 판정이 `tileDist > tileRange` 라 **경계 링 자체가
   사거리 안**이고 `SelectRing` 의 min 은 inclusive 다. `+1` 을 빼면 마메모가 자기 평타로
   자기가 재운 유닛을 깨우고, 이 보스는 방어유닛을 사냥해 **붙기 때문에 그게 최빈 케이스**다.
2. **`GrantShield` 의 host 제외(가호).** `ShieldMath` 가 `source` 를 병합 키로 써서, host 를
   포함하면 ②③이 한 슬롯을 공유하고 ③이 매 주기 ②를 재충전해 **경계 실드가 상시 실드로 붕괴**한다.
3. **실드 버퍼는 쌍으로, 적 전원에게.** 드레인이 `ShieldSlot` 존재로 게이팅돼 있어 한쪽만
   붙이면 부여가 영영 안 빠지고 **버퍼가 무한 성장**한다.
4. **`AreaSleep` 은 브리지 실드파열 드레인을 재사용하지 않는다.** 그쪽 대상 풀이 `AttackUnitTag`
   하드코딩이라 손대면 실드파열 카드가 깨진다. payload kind 만 공유하고 경로는 별개다.
5. **진영 축은 유닛 태그다.** `battle-structures` 이후 `FactionTag` 의 진영 비트가 거점을
   포함하는데 거점엔 CC·실드 버퍼가 없다.
6. **`DcTrigger.EnemyTriggerArmed` 를 완화하지 마라** — 적 실드 파열을 열려면 브리지 파열
   드레인의 **진영 파라미터화가 선행**이다. 안 그러면 보스의 폭발이 자기 진영을 때린다.
7. **`duration < periodSeconds`(자장가).** 같거나 크면 매 주기 같은 대상이 갱신돼 "잠시 재운다"가
   **생존 내내 고착**이 된다. bake 가 경고한다.
8. **신규 payload kind 는 `DcApplicability` 분류가 필수** — 총체성 테스트가 빨개진다.

## Follow-up

**밸런스 관측 1축 (unit 5 — 유일하게 안 닫힌 것)**
README 계약 10 이 요구한 **«1회 조우에서 자장가 N회 이상 발동»** 을 아직 아무도 재지 않았다.
추산: 실효 HP 1800(버스트 관통 시 1450) / 보스 실효 DPS 140~250 ⇒ 생존 7~13초, 주기 3.5s ⇒
2~3회. 그런데 마메모 이속이 1.4(3보스 중 최저)라 **방어유닛까지 걸어가는 시간이 앞의 1~2회를
먹어** 실발동이 1~2회로 떨어질 수 있다. 이 축이 무너지면 "구현은 됐는데 게임에서 안 보인다"가
정확히 재현된다. **관측 1번 = 마메모 웨이브의 소요 시간**(이 보스의 정체가 회전 지연이다).

**리뷰가 남긴 관측 항목**
- **악몽의 가호가 골 앞에서 유지되는가** — 호위 이속(2.2~2.5, 러너 7.2) vs 마메모 1.4 라 초당
  ~1타일씩 벌어져 반경 4 를 약 4초에 이탈한다. 실드는 **스폰 직후**에 붙고 골 근처에선 마메모가
  이미 뒤에 있다 → README 의 「호위가 골에 눌러앉아 전멸 지연」 논지가 성립하는지 직접 볼 것.
- **가호에 대상 수 상한이 없다** — 반경 4 원판(81칸) 전부에 부여한다. 실드량도 flat 이라 수혜자
  EHP 배율이 러너(HP 20) 4.0× ~ 뱅가드(HP 120) 1.5× 로 흔들린다. 밀집 lane·escortType 편차를
  관측하고, 필요해지면 cap 축 신설.
- **적 오버헤드 바 압축** — HP 20 러너가 실드 60 을 받으면 HP 구간이 바의 25% 로 줄어
  "만피인데 체력바가 1/4" 로 보인다. 적 게이지가 이번에 열렸으므로 육안 확인 대상.

**미결 판정 (사용자)**
- **자는 캐스터가 계속 시전한다** — 사용자 Play 관측(2026-08-11). 계약 3 이 서술한 그대로이고
  `shield-guardian-defender` 계약 7 의 의도지만 **사용자는 버그로 읽었다**. 당장 고치지 않기로
  했다(지시). 고치면 가디언·해저드 캐스터 **전원**의 동작이 바뀌므로 별 spec 이다.

**범위 밖 (README 후속 후보 참조)**
악몽의 늪(장판) · 네 번째 보스(소환형) · 보스 `OnShieldBreak` 개방(실행기 진영 파라미터화 선행) ·
면역으로 죽은 카드·유닛 · 실드 재생 · 보스 3종 등장 빈도.

**PlayMode 사전 존재 실패 17건**은 이 spec 소관이 아니다. 목록과 확인법은 unit 4 완료 기준에 있다.
