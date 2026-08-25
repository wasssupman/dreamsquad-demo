# 1 — 그물: 골든 재생성 + arm 전수 특성화

## 목적

**골든은 이 리팩터의 증인이 될 수 없다.** 그 사실을 확인하고 상환한다.
이 unit 없이 unit 2b(진영 리터럴 56곳)를 시작하면 회귀를 아무것도 못 잡는다.

실측 근거:

- 골든 코퍼스 7종 전체에서 스킬 발화 기록이 **0회** — `ch13(DcTriggerFired)=0` ·
  `ch10(ShieldGranted)=0` · `ch11(ShieldBreak)=0` · `ch12(Knockup)=0`
- **StatModifier · Cc · Aggro · Blink 는 채널 자체가 없다**(`Core/Trace/LegacyTraceV0.cs:24~44`)
- `battle-sim-extraction/4_legacy_trace_golden.md:114~122` 가 «드림캐쳐 다용 판» 부재를 명시
  (카드 사용이 UI 경유라 하네스에서 재현 불가 — M1 로 미뤄져 있었다)
- 코퍼스가 **stale** — `configHash` 에 항목 3개가 추가돼 기준선이 어긋나 있다
- 배치 스킬 PlayMode 커버리지가 **9종 중 3종**(`DotNearby`·`ApplyStackNearby`·`ForwardProjectile`)

## 변경 대상

- `Assets/_Project/Tests/Golden/` — 코퍼스 재생성
- `Assets/_Project/Scripts/Core/Trace/LegacyTraceV0.cs` — 스킬 축 채널 추가(판정에 따라)
- `Assets/_Project/Tests/EditMode/` · `Tests/PlayMode/` — arm 특성화 테스트 신설
- `Assets/_Project/Editor/Battle/SimGoldenMenu.cs` — 하네스 입력 스케줄(판정에 따라)

## 구현

1. **코퍼스 재생성을 먼저, 단독 커밋으로.**
   ⚠ 워킹트리에 무관 dirty 가 있으면 **그것이 기준선에 구워진다.** 현재 dirty 75건에
   `Assets/_Project/Data/Maps/MapDocument_Test.asset`(configHash 반응 축)이 포함돼 있다.
   → 격리 확인 후 재생성, 재생성만 담은 커밋.
2. **스킬 발화를 코퍼스가 보게 만든다** — 둘 중 택일(unit 0 이 판정):
   - **(a)** 하네스 입력 스케줄에 카드 부착·유닛 메커닉 발화 경로를 추가한다.
     선례: placement 가 `PlaceDefenderAs` 로 UI 를 우회했다.
   - **(b)** 골든을 포기하고 **arm 전수 특성화**로 간다.
3. **특성화 테스트를 arm 전수로 깐다.** `skill-fire-dispatch` 의 「무보호 4종」은 옛 12행
   스코프 기준이라 지금은 부족하다. 확장 스코프의 무보호:
   - 레거시 배치 9종 중 **6종**
   - 캐스트 8에셋 · 소환 · 액티브 6종
   - 카드 heavy-arm 행(`AttackN` 11 · `OnKill` 4 · `OnDamagedN` 2 · `OnShieldBreak` 2 ·
     `OnDeath` 2 · `OnRetire` 2)
   - 보스 무보호 4종(궁극기 · 도약×2 · 채찍질 · 경계 자폭)
   선례: `ProjectileEmitterIntegrationTests` 의 EditMode bare world.
4. **`LullabyLive` 류는 boolean 골든으로만 쓴다.** sim 이 실프레임 델타라 수치 재현이
   원리적으로 불가하다(`skill-fire-dispatch` 계약 10 계승).
5. **Burst-off 재검증 게이트를 세운다.** 이전이 arm 산술을 Burst → managed 로 옮기므로
   ulp 차(mad 축약 등)로 near-tie 대상 선택이 flip 될 수 있다. parity 기준이 exact 라
   재베이스라인 외 답이 없는데, 재베이스라인하면 「동작 무변경」 증언이 사라진다.
   → **구 sim 을 Burst-off 로 돌린 골든**을 1차 판독기로 둔다(컴파일 도메인 차 vs 로직 차 분리).
   M1 이 어차피 Burst-off 교차 골든을 게이트로 갖는다 — 그 규율을 앞당기는 것이다.

## 완료 기준

- [ ] 코퍼스가 dirty 격리 상태에서 재생성되고 **단독 커밋**으로 들어갔다
- [ ] 스킬 발화가 코퍼스에 기록되거나(경로 a), arm 전수 특성화가 그 자리를 대신한다(경로 b)
- [ ] 이전 대상 **모든 행**에 골든 또는 특성화 테스트가 하나 이상 붙었다 (계약 11 의 일반화)
- [ ] Burst-off 골든 재검증 절차가 문서화되고 1회 실행됐다
- [ ] EditMode 코어 lane 초록. **완료 기준에 「골든이 증인」 문구를 쓰지 않는다**
