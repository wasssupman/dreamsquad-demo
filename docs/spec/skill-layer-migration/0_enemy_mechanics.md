# 0 — 적 mechanics 11행

## 목적

가장 큰 가족이자 가장 잘 보호된 가족부터 옮긴다. 보스 3종(짱쎈 4 · 마메모 3 · 나이트메어 3) +
**드래곤 `AreaBreath` 1행**(tier 1 — 보스가 아니다).

## 변경 대상

- `Assets/_Project/Scripts/Skills/Concrete/` — concrete 신설 (자장가는 토대 unit 5 에서 완료)
- `Assets/_Project/Data/UnitSkills/` — 저작 SO
- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — arm 제거 (~500/733줄)
- `Assets/_Project/Scripts/Battle/Combat/HealthThresholdSystem.cs` — arm 제거 (~220/358줄)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — bake 가 `skillId` 를 굽는다

## 구현

1. **행 단위로 쪼개 커밋한다.** 11행을 한 커밋에 넣지 않는다.
   `skill-fire-dispatch` 는 같은 범위를 세 파일로 배정했었다.
2. **무보호 4종은 그물이 선행이다** — 궁극기 · 도약×2 · 채찍질 · 경계 자폭.
   토대 unit 1 이 이미 깔았어야 한다. 안 깔렸으면 여기서 멈춘다.
3. **진행형 상태는 옮기지 않는다**(토대 계약 5). `UltimateLeapSystem` · `LeapFlight` ·
   `LethalTimerSystem` 은 그대로다 — 스킬은 개시와 수치까지다.
   ⚠ 궁극기 개시는 `HealthThresholdSystem.cs:233~242` 에서 `UltimateLeapState`+`LeapFlight` 를
   **원자 동시 부착**하고 로컬 Temp ECB 를 즉시 재생해 같은 프레임 가시화를 보장한다.
   intent 화하면 **구조 변경 수행 주체 · ECB 재생 시점 · 원자성**을 전부 명시해야 한다.
4. **드래곤을 빠뜨리지 않는다.** `Enemy_Dragon.asset` 은 `tier:1` 이라 「보스 10행」 프레임에
   안 들어온다. 이 행이 남으면 「능동 스킬 = 보스」 전제가 코드에 되살아난 것처럼 보인다.
5. **채찍질은 나이트메어 소유**다(짱쎈놈 아님 — rev 3 이 여기서 틀렸다).
6. **진영 리터럴이 이미 풀려 있어야 한다**(토대 unit 2b). 안 풀렸으면 concrete 가 진영을
   갖게 되어 「누구든」이 첫 가족에서 깨진다.

## 진행 상태 (2026-08-25)

**9/11 이전 완료.** 자장가 · 채찍질 · 실드 2행(주기 확산 · 경계 자기) · 경계 자폭 ·
도약 2행 · 궁극기.

⚠ **`EmitProjectilePattern` 2행은 unit 1 로 이월한다** — 회피가 아니라 순서 판단이다:

1. 이 payload 의 실행이 **이미 두 곳에 사본**이다(`AttackSystem` · `BossPeriodicTriggerSystem`).
   `on-place-skill-rework` 계약 5 가 「세 번째를 만들지 말고 공용 헬퍼로 뽑는다」고 예약했고,
   지금 이전하면 **세 번째 사본**이 생긴다.
2. 같은 payload 가 **unit 1 의 5행 중 3행**에도 있다(SkyStrike · OnPlaceBlast · BombMan).
   두 unit 이 각자 옮기면 헬퍼 추출을 두 번 하거나, 한쪽이 다른 쪽을 기다려야 한다.
3. 포트 비용도 다르다 — 패턴 슬롯 읽기·쓰기, emitter 인스턴스 push, 이동 바인딩 분류,
   조준 epsilon 까지 **5개 동사**가 필요하고 전부 투사체 시스템 모양이다. 그 표면을
   한 payload 를 위해 열고 unit 1 에서 다시 만지는 것보다 한 번에 여는 것이 싸다.

**unit 1 이 이 두 행을 함께 처리한다.** 그때 「발사 성사와 카운터 전진의 원자성」
(unit 0 미결 4의 결론)이 처음 시험받는다.

## 완료 기준

- [x] **9행** 이 concrete 로 존재한다 (legacy arm 은 아직 살아 있다 — 철거는 unit 8)
- [ ] `EmitProjectilePattern` 2행 → **unit 1 로 이월**(위 사유)
- [ ] `BossPeriodicTriggerSystem` · `HealthThresholdSystem` 에 payload arm 이 남아 있지 않다
      (감지와 트리거 판정만 잔존)
- [ ] 궁극기 개시의 원자 부착이 보존됐다 (같은 프레임 가시화 유지)
- [ ] 드래곤 `AreaBreath` — **미이전.** `AttackN` 트리거라 감지자가 `AttackSystem` 이고
      그쪽 라우팅이 아직 없다(unit 0 은 주기·경계 두 감지자만 배선했다)
- [ ] 그물 전건 초록 + Play 로 보스 3종 육안 확인
- [ ] 행 단위 커밋 (11행 한 커밋 금지)
