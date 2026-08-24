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

## 완료 기준

- [ ] 11행 전부가 concrete + 저작 SO 로 존재하고 legacy arm 이 죽었다
- [ ] `BossPeriodicTriggerSystem` · `HealthThresholdSystem` 에 payload arm 이 남아 있지 않다
      (감지와 트리거 판정만 잔존)
- [ ] 궁극기 개시의 원자 부착이 보존됐다 (같은 프레임 가시화 유지)
- [ ] 드래곤 `AreaBreath` 가 포함됐다
- [ ] 그물 전건 초록 + Play 로 보스 3종 육안 확인
- [ ] 행 단위 커밋 (11행 한 커밋 금지)
