# 상호 비판 리뷰 — 「Battle 트리거-발동 시스템 구현 설계」(타 세션, 2026-09-22) ↔ 본 세션 종합(`00_synthesis.md`)

> 리뷰 방식은 상대 문서 §8 제안대로: 「이 설계가 깨지는 구체 시나리오」를 census 실측(`census-*.md`)으로 제시한다.
> 상대가 옳고 본 세션이 고쳐야 할 것도 같은 잣대로 적는다(§3).

---

## 0. 먼저 맞춰야 할 것 — 기준선이 다르다

상대 문서 §6 환원표의 행(잔영 드랍 · 조건 게이트 엘리트 · 감정 표현층 · 각성 잔여 횟수 · 꿈런/경쟁모드 · death-only recycle)은
**현행 구현이 아니라 `docs/blueprint/` 기획서의 원형 콘텐츠**다. 본 세션의 기준선은 사용자 지시대로 「현시점 구현과 동일한 스펙」이고,
그 어휘는 census 가 전수 확정했다(`DcTriggerKind` 10 · `DcPayloadKind` 33 · `SimIntent` 24+2 · concrete 34 · seam 7).
같은 이유로 상대 §8-A 「어휘 전수 조사(선행 과제)」는 **이미 끝나 있다** — `census-skills.md` §2~§5 가 그 결과다.
두 문서를 합치려면 **어느 기준선인지**를 사용자가 먼저 정해야 한다. 아래 리뷰는 현행 기준선으로 쓴다.

---

## 1. 상대 설계가 깨지는 지점 (구체 시나리오)

### 1-1. 전제 P1 의 근거 「록스텝 · 서버 재계산 검증자」 — 이미 기각된 길
- `battle-sim-extraction/6_decision_record.md` **D2** 가 「커맨드로그+재계산 정본」을 기각했다(IL2CPP↔CoreCLR float 편차 → 고정소수점 전면 이식 + 전 수치 재튜닝이라는 최대 리스크).
- 사용자 결정 2026-09-22 ③: 서버권위 규율(AMR·커맨드로그·receipt)은 이번 설계에서 **뗀다**.
- 결정론 자체는 유지한다(같은 기기·같은 빌드에서 같은 시드 = 같은 판). 그러나 그 근거는 토너먼트 공정성·헤드리스 테스트·리플레이(D1)이지 서버 재계산이 아니다.

### 1-2. §7 「고정소수점 코어 · float/double 컴파일 차단」 — 현행 전투를 통째로 재튜닝하는 결정
- 판정 산식(제약 13)·이동 평활화·분리·히스테리시스(0.1칸, 실측 지터 2배)·`lengthsq > 1e-6` 분기 11곳·부채꼴 45° 금지(float 경계) — 전부 float 위에 튜닝돼 있다.
- `Wassup.Skills` 가 허용하는 유일한 외부 타입이 `Unity.Mathematics` 값 타입이다(foundation 계약 1). 고정소수점이면 이 asmdef 의 벡터 어휘까지 갈아엎는다.
- `unit-stats-and-modifiers/2·3` 스펙이 같은 요구를 했고 스스로 「가장 큰 변경」이라 못박았다(`census-effects.md` 열린 질문 1). 본 세션 권고는 **float 유지**. 서버권위를 뗀 순간 고정소수점의 유일한 근거(이기종 bit-exact)가 사라진다.

### 1-3. §4 `BattleEvent` = 「Type / Source / Target / Value 최소 집합」 — 값 스냅샷 계약 위반
- 불변식 5: **이벤트는 값 스냅샷**이다. 자기 죽음 seam 은 정의상 파괴 뒤라 드레인 시점에 시전자가 없다. `Source` 핸들로 되물으면 자리·피해·반경·**진영**이 전부 빈손이다.
  실측 사고: 종전 폴백 「플레이어 시전 = 방어유닛 편」 때문에 **적의 작별 선물이 적을 때렸다** → `CasterFaction` 을 값으로 실어 해결.
- `distance-based-range` unit 23b: 반경은 **자리와 짝**으로 둘 다닌다(`FiredPosition↔CasterBodyRadius`, `TargetPosition↔EventBodyRadius`). 단일 필드면 시체폭발이 **킬러의 몸**으로 적 시체 위에서 터진다.
- `bestTarget` 은 9단계 오버라이드 합성물이라 드레인 시점 재현 불가(`census-skills.md` 행 「SkillFiredEvent 값 스냅샷」 — 재질의 가능 건수 0).
- 「`Value` 하나」는 `DcTriggerSlot.tileRange` 가 **7~13가지 뜻을 겸직**했던 결함의 재현이다. `SimIntent` 가 필드를 25개로 펼친 이유가 「필드 겸직 금지」다. 무할당(P4)은 struct 필드 수와 무관하다 — 값이 싸다.

### 1-4. §3 「입력은 이벤트로만 진입 · 큐 소비」 — Immediate seam 의 동기 계약을 깬다
- 부착·액티브는 **동기 트랜잭션**이다: 큐에 넣고 프레임을 기다리면 **소모(코스트 차감·쿨다운·큐 순환) 뒤에 실행이 도착**한다. 그래서 `SkillSeam.Immediate` 는 자기 순서를 갖지 않고 브리지 콜스택에서 끝난다.
- 「실패한 부착은 차감·순환 없음」 — 거절이 **즉시** 손패로 돌아와야 한다(UI preflight 와 커밋 bake 가 같은 함수 `DcApplicability`).
- 해법은 「입력 = 이벤트」가 아니라 **커맨드 ≠ 이벤트 분리**다: 커맨드는 틱 시작 phase 0 에서 동기 적용되고 receipt(수락/거절 사유)를 돌려준다. 이벤트는 「일어난 사실」만. 본 세션 §3.3 phase 0 이 이 형태.

### 1-5. §5 틱 페이즈 8개 — 감지자의 프레임 창에서 도출된 seam 7 을 잃는다
- seam 이 7인 이유는 계약 7: 「감지자가 다른 프레임 창을 가지면 seam 이 따로 난다」. 상대 페이즈에 없는 창:
  - **Death(내가 죽였다 · 대상이 아직 있다) ≠ Lifecycle(내가 죽는다 · 파괴 뒤 · `RequiresLiveCaster=false`)** — 상대의 단일 `Death` 페이즈에서 시체폭발과 작별 선물이 한 창으로 접히면 둘 중 하나가 빈손이 된다.
  - **Cast → Attack 같은 틱**(캐스터는 사거리 0 이라 RESOLVE 에 못 가고, 캐스트 성사가 그 host 의 공격 사건) · **Periodic → Emitter 같은 틱**(발사 명세) · **Threshold → UltimateLeap** — 1틱 늦으면 공격·텔레포트가 늦는다(v6 리뷰 CRITICAL 4).
  - 모디파이어 적용·CC·존·투사체 이동/착탄·배치 활성화·경계·순간이동 페이즈가 없다.
- 「같은 틱 소비 vs 다음 틱 이월」(§8-B)은 트레이드오프가 아니라 **이미 load-bearing 한 계약**이다. 현행 규칙: 남의 seam 것은 큐 꼬리로 → **같은 틱의 후속 seam 이면 같은 틱, 이미 지난 seam 이면 다음 틱**. 결정적이고 지금 확정 가능하다.

### 1-6. §5 「DamagePre 트리거 체인으로 피해 수정」 — 공격 출력 수식자를 트리거로 풀면 pre-scan 불변식이 깨진다
- foundation 결정 기록: `HeavyStrike` · `DcAttackModSlot` 3종 · `FrontmostAttackLock.damageMulSnapshot` · 충전 소비는 **스킬이 아니다**. 「이번 공격의 출력 숫자/타이밍 조립에 곱·합으로 참여」= 밖. 근거: `WouldFire ∧ GatePass` 예측과 실제 counter `Tick` 이 **같은 프레임·같은 bestTarget·같은 pre-damage HP** 를 읽어야 한다 — 큐 드레인 뒤의 `Execute` 는 이미 만들어진 출력에 개입할 수 없다.
- §8-D 답: **별도 고정 피해 파이프라인 + `AttackMod` 순수 함수**(GAS 형). 트리거는 결과(Post)에만 반응. 본 세션 §3.5 「어휘 밖 잔여 = HeavyStrike 하나」와 같은 결론.

### 1-7. §6 「씬 바인딩 = 게임 규칙 → 모드 = 바인딩 세트」 — 매치 규칙은 바인딩이 아니다
- 불변식 9 「`EndMatch` 호출처 정확히 3, 넷째 = 패배 부활」. 종료 통로가 데이터 바인딩이면 이 불변식을 grep·테스트로 못 지킨다.
- 마음 스트레스는 별도 자원이 아니라 **마음 `Health` 의 표시 반전**(`StressMath`). 「OnHeartHit → 스트레스 가산」 바인딩은 정본을 둘로 만든다.
- 웨이브(단일 RNG 소비 순서가 계약)·코스트 재생·당김 상한·보너스 래치는 **상태를 가진 연속 규칙**이고 순서 계약(`DrainEnemyKilled → QueueDueWaves`, `SyncGoalStability → BonusPullOffer`)이 있다. 트리거 형이 아니다.
- §8-F 답: 승격하지 않는다. **기믹까지만** Match 호스트 바인딩(본 세션 변형 ①), 핵심 매치 규칙은 phase 6 고정 파이프라인.

### 1-8. 규칙 2 정렬 키의 「우선순위」 — 저작 축 신설은 제약 8 위반
- 현행 순서 = 생산 순서 + 슬롯 순서, 동률은 `SimEntityId`. 저작 가능한 priority 는 소비처 0 인 「나중을 위한 층」이다. 키는 `(seam, 세대, 생산 순번, SimEntityId)` 로 충분하고 동률이 안 남는다.

### 1-9. §3 「ECS 컴포넌트 모델 밖 · 하이브리드 ECS 노선과 정합」 — 전제가 stale
- 사용자 결정 2026-09-22 ①: ECS 를 제거하고 순수 C# 전투 코어 으로 간다. 「컴포넌트와 궁합이 나쁘다」는 문제 자체가 사라진다.
- `VContainer·R3·UniTask·MessagePipe` 는 이 프로젝트에 없다(CLAUDE.md 가 범용 라이브러리를 근거 없이 금지).

### 1-10. 「드림캐처 death-only recycle」 — 현행 순환 규칙과 다르다
- 현행: host 소멸 → 큐 뒤 / **퇴근 → 큐 뒤(인수인계 있으면 다른 카드는 앞)** / 액티브 사용 → 즉시 뒤 / 실패한 부착은 무변. 수명 enum 에 「퇴근 시」를 둔 것은 맞지만 recycle 자체는 death-only 가 아니다.

---

## 2. 상대 §8 미결에 대한 본 세션 답

| 항목 | 답 |
|---|---|
| A 어휘 전수 조사 | 끝남(`census-skills.md`). 트리거 10 · 페이로드 33(살아 있는 26) · intent 24+2 · concrete 34. 「효과 프리미티브 20 미만」 추정은 intent 24 로 이미 초과 — P2 재검토가 아니라 추정치 수정 |
| B 잔여 큐 정책 | 후속 seam 이면 같은 틱, 지난 seam 이면 다음 틱(현행 계약). 세대(generation) BFS 로 창 안 소진 |
| C 깊이 캡 | 8 제안. 라이브 연쇄 최장은 3(시체폭발→OnKill→잿불; DoT 는 미귀속이라 OnKill 을 못 낸다). 초과는 `Report` intent 로 loud 폐기 |
| D 피해 파이프라인 | 별도 고정 파이프라인 + AttackMod 순수 함수. 트리거는 Post 만 |
| E 데이터 포맷 | 현행 유지: SO 저작(시트 왕복, enum 값 append-only) → 매치 시작 시 Materializer 가 plain 정의표로. JSON/정적 테이블 신설 불필요 |
| F 모드 = 바인딩 세트 | 승격 안 함. 기믹까지만 |
| G 프레젠테이션 구독 | `SimEvent` 값 스냅샷 리스트(뷰 전용 20종), `SimEntityId` 키. 「이벤트가 곧 연출 훅」에 동의하되 `PlayVisual` intent(「언제」는 스킬 판단, 「어떻게」는 뷰)는 유지 |

---

## 3. 상대가 옳고 본 세션이 고칠 것

1. **Lifetime 을 바인딩의 1급 필드로.** 현행은 수명이 암묵(슬롯 존재·HandController 순환·`_activeDcEffects` 리스트)이다. `{영구 · N회 · 판 종료 · 소유자 소멸 · 퇴근}` enum 채택. 만료가 `OnDetach` 사건을 낳는다는 규칙도 채택.
2. **P4 무할당을 원칙으로 명시.** 본 세션 「plain class 개체」는 풀링 + 사전 할당 리스트가 전제여야 한다. 이벤트·intent 는 이미 struct.
3. **전순서 키를 명문화.** `(seam, generation, productionSeq, SimEntityId)` — 동률이 남으면 결함으로 취급한다는 규칙을 계약에 올린다(priority 축은 제외).
4. **호스트 = `SimEntityId` 하나로 통일**(Unit / Match / Player 3종 대신). Match 호스트에 예약 id 를 둔다(현행 미발급 센티널이 `int.MaxValue` 라 0 예약이 가능 — 골든 축 변경은 재구축에서 수용).
5. **검증 인프라 동시 착수·골든 러너가 첫 소비자** — 채택. `LegacyTraceV0` 포맷 계승.
6. 본 세션 변형 ②(skillId 은퇴)의 구멍: 자기 죽음 중복 억제 마스크가 `skillId < 64` 에 기댄다. 대체 키 = **바인딩 instanceId**(현행 `DcTriggerSlot.instanceId` 계승).

---

## 4. 수렴안 (두 문서 교집합 + 정정)

- 5개념(이벤트·바인딩·조건·효과·소유자/수명) — 채택. 단 이벤트는 **값 스냅샷 struct(필드 겸직 금지)**, 소유자는 `SimEntityId`, 조건은 현행 게이트 축(사건×술어 직교, 미개방 조합은 fail-closed).
- 커맨드 ≠ 이벤트. 커맨드는 phase 0 동기 적용 + receipt.
- seam 은 틱 파이프라인의 감지자 창에서 도출(개수를 계약에 안 적음). 연쇄 = 세대 BFS + 깊이 8 + loud 폐기.
- 공격 출력 수식자·매치 핵심 규칙은 바인딩 밖(고정 파이프라인). 기믹·Squad·PlacementAura 는 Match/Unit 호스트 바인딩 안.
- float 유지 · 서버 재계산 전제 제거 · 순수 C# 전투 코어(ECS 없음).
