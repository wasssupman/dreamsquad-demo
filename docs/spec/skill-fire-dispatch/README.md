# skill-fire-dispatch — 스킬의 1급 객체화(저작 SO + 순수 로직) + 단일 발동 경로

> 상태: **홀드 2026-08-12 (rev 4 작성 완료 · 미착수)**
> **재개 시점 = 다음 보스 제작 때**(사용자 결정 2026-08-12). 신규 보스는 스킬 3~4개를
> 저작하므로, 그때 이 리팩터의 이득(스킬당 파일 2개 · 타입 필드 · 재사용)이 처음으로
> **실사용에서** 회수된다. 지시 없이 재개하지 않는다.
>
> rev 3 은 critic 3트랙에서 **착수 불가** 판정을 받았다. 사실관계 3건이 틀렸고(아래
> "rev 3 오류"), 검증 질문이 거짓이었으며, 이식성 대가가 과소평가됐다. rev 4 는 그 셋을
> 고치고 **로직을 SO 밖으로** 뺀다(사용자 결정 2026-08-12).

## 재개할 때 먼저 볼 것 (홀드 인계)

1. **표는 그대로 믿어도 된다** — unit 0 표 1~3 은 에셋 YAML·코드 직접 검산본이다(rev 3 이
   죽은 이유가 미검산 추정이었다). 단 **재개 시점에 보스/카드가 늘어 있으면 표부터 갱신**한다.
   갱신 절차 = `AttackUnitData.nightmareMechanics` 보유 적 에셋 전수 + 카드 mechanics 전수.
2. **순서 재판정 필요** — 계약 12(`battle-sim-extraction` M0 앞)는 2026-08-12 기준 판단이다.
   재개 시 M0 가 이미 착수됐으면 **이 spec 은 M1 이후로 미룬다**(골든 기준선 충돌 금지).
3. **신규 보스와의 관계**: 새 보스를 legacy 형식(`nightmareMechanics`)으로 먼저 만들고 나중에
   옮길지, 이 spec 을 끝낸 뒤 새 형식으로 저작할지가 첫 결정이다. 후자면 unit 1(특성화
   골든)이 새 보스 저작 **전에** 끝나 있어야 한다 — 골든 없는 이전이 rev 3 의 두 번째 사인.
4. **악몽의 늪(장판) 축**도 "다음 보스에서 딥하게 논의"로 같이 대기 중이다
   (`docs/spec/README.md` Follow-up Backlog, boss-mamemo 그룹) — 재개 시 함께 꺼낸다.

## 상위 목표

스킬 하나가 **저작 SO 1개 + 로직 파일 1개**로 존재한다. 유닛 에셋은 스킬 SO 목록을
소유한다. 발동 조건을 지켜보는 기존 시스템 2개는 조건 판정과 디스패치만 남고, 실행은
`Battle/Combat/Skills/{Name}Skill.cs` 의 순수 static 함수가 한다.

```
Data/UnitSkills/AreaSleepSkillDef.cs   ← 저작: 타입 필드(재울 인원·반경·지속) + 발동 조건 + Validate
        │  (유닛 에셋이 이 SO 를 참조 소유)
        ▼  브리지 bake = 유일한 번역자 (기존 DefenderAbilityData 선례와 동일)
   DcTriggerSlot (unmanaged, 현행 그대로)
        ▼  감시 2개: 조건 판정 → switch 1곳
Battle/Combat/Skills/AreaSleepSkill.cs ← 로직: static Execute(in AreaSleepParams, ref SkillContext, Entity)
                                          + AreaSleepParams = 슬롯 위의 이름 붙은 뷰
```

**로직을 SO 안에 두지 않는 이유**(rev 3 대비 유일한 구조 변경): 전투 코드 243 파일 중
UnityEngine 참조는 10개뿐이고, `battle-sim-extraction` 의 하드 게이트가 "sim 이
UnityEngine 참조 = 컴파일 에러"다. 스킬 로직을 `ScriptableObject` 안에 두면 게임 규칙이
가장 많이 사는 신규 파일군 전체가 이전 시 재작성 등급이 된다. 밖으로 빼면 **Burst 유지 ·
managed 테이블 불필요 · 이식 부채 0** 인데 이득은 거의 그대로다. 대가는 스킬당 파일 2개.

얻는 것:

- **읽기 진입점** — "자장가가 무슨 일을 하나" = `AreaSleepSkill.cs` 한 파일.
- **의미 있는 이름** — `magnitude` 하나가 6가지(데미지/이속%/강공 배율/밀집 반경/재울
  인원/실드량)를 겸직하는 고통이 저작(SO 타입 필드)과 읽기(params 뷰) 양쪽에서 끝난다.
- **유닛이 스킬을 소유** — 같은 스킬 에셋을 복제해 파라미터만 바꿔 다른 유닛에 장착.
- **명시적 확장** — 신규 스킬 = SO 1개 + 로직 1개 + 등록 2줄(bake case·dispatch case).
- **관측 단일점** — 발동이 한 switch 를 지나 "조우당 몇 번"이 잡힌다.

## 검증 질문

> 기존 12행 전부가 **골든 대비 동작 동일**하고(무보호 4종은 unit 1 에서 골든을 먼저
> 만든다), **궁극기 도약 스킬 에셋을 복제해 파라미터만 바꾼 인스턴스를 다른 보스가
> 장착하면 코드 0줄로 동작하는가?** 자장가의 조건→실행을 파일 2개로 읽는가?

## 작업 단위

| 파일 | 문서 | 목적 |
|---|---|---|
| 0 | `0_inventory_and_contracts.md` | 전수 조사 **확정본**(rev 4 검산 결과) + 시그니처 고정. 코드 0 |
| 1 | `1_characterization_goldens.md` | **무보호 4종 특성화 골든 신설** — 궁극기·도약·채찍질·경계 자폭. 이전 **앞**에 빨간 것을 볼 수 있게 |
| 2 | `2_foundation_and_area_sleep.md` | 토대(`UnitSkillDef`·`SkillKind`·`SkillContext`·params 뷰·브리지 번역) + **첫 스킬 자장가** |
| 3 | `3_periodic_skills.md` | 주기 잔여 — 가호·채찍질·발사 명세 (전부 보스 유닛 베이크. 카드 0) |
| 4 | `4_threshold_unit_skills.md` | 경계 유닛 스킬 — 장막·자폭·도약×2·궁극기 개시 |
| 5 | `5_card_adapter.md` | **카드 어댑터** — 빈사폭주·진동갑주(체력경계 카드 2장). authoring·시트 무변경으로 같은 경로 수렴 |
| 6 | `6_fire_observability.md` | 발동 관측 — 스킬명 로그 + 자장가 발동 횟수 단언 |
| 7 | `7_handoff_summary.md` | 인계 |

## Feature-wide 계약

1. **스킬 = 저작 SO 1개(`Data/UnitSkills/`) + 로직 1개(`Battle/Combat/Skills/`).** 저작은
   UnityEngine 을 알고 Battle 을 모른다. 로직은 UnityEngine 을 **모른다**(참조 시 이식
   게이트 위반). 둘을 잇는 유일한 번역자는 **브리지 bake** — `DefenderAbilityData` 선례.
2. **발동 조건은 SO 의 데이터 필드다.** 클래스 코드에 넣지 않는다. 같은 로직 × 다른
   조건 = 에셋 2개(마메모 장막·가호가 실증).
3. **무상태.** SO 필드 = 읽기 전용 설정값. 로직은 static. 진행형 상태(도약 비행 등)는
   지금처럼 컴포넌트+시스템 소유 — 스킬은 개시와 수치까지다.
4. **`DcTriggerSlot` 은 그대로 굽는다** — payload kind 도 스칼라도 잔존한다(rev 3 계약 6
   폐기). 골든 3파일이 이 스칼라를 직접 읽고 있어 제거하면 무회귀 증인이 죽는다.
   타입 안전은 **params 뷰 struct**(`AreaSleepParams(in DcTriggerSlot)` → `SleepCount`/
   `Radius`/`Duration`)가 담당한다. Burst 유지·bake 계약 무변경·골든 무수정.
5. **`SkillContext` 는 프레임 스코프 mutable 값이다.** 전 경로 `ref` 전달, lazy 풀
   플래그를 자기 안에 갖고, Dispose 는 감시 `OnUpdate` 말미 단일 지점. 로직은 ctx 를
   저장하지 않는다. 동사 표면이 `battle-sim-extraction` 의 "sim 이 스킬에게 주는 API"
   선행 작업이다.
6. **진영은 상대적으로 부른다** — `OpponentsOf`/`AlliesOf(시전자)`, 축은 유닛 태그
   (`FactionTag` 은 거점 포함이라 CC·실드 버퍼 부재로 예외). **단, 지금 라이브 경로는
   전부 적 시전이므로 하드코딩 리터럴 교체는 이 spec 밖**(후속) — 미사용 경로를 만들지
   않는다.
7. **범위 = 가벼운 감시 2개에 도달하는 12행 전부.** 보스 3종 10행은 SO 이전, 카드 2장은
   **authoring 무변경 + 어댑터**(시트 왕복 유지). 무거운 arm(AttackN·OnKill·OnDamagedN·
   OnShieldBreak·OnDeath)과 즉발 카드는 구방식 — 후속.
8. **공격 출력 수식자는 스킬이 아니다.** `HeavyStrike`·`NextAttackDoubleFire`·게이트 합성은
   발동한 공격 자신의 출력을 바꿔 pre-scan 합성 불변식이 공격 계산 내부 거주를 강제한다.
9. **시트 무손실**(검산 확정): 시트가 덮는 mechanics 는 **카드뿐**(`OverlayMechanics`).
   보스 `nightmareMechanics` 는 시트 비구동이라 SO 이전 자유. 카드 2장은 계약 7 대로
   authoring 을 그대로 두므로 **이 spec 의 시트 손실은 0**이다.
10. **골든 없이 이전하지 않는다.** 무보호 4종(궁극기·도약·채찍질·경계 자폭)은 unit 1 에서
    특성화 테스트를 먼저 세운다. `LullabyLive` 는 **boolean 골든으로만** 쓴다(sim 이
    실프레임 델타라 수치 재현 불가 — rev 3 의 "계측 수치 동일" 기준은 폐기).
11. **이름 충돌 회피**: `Data/Skills/Skill_*.asset` 은 이미 액티브 드림캐쳐 스킬
    (`SkillData`)이 점유 중. 이 spec 은 `Data/UnitSkills/UnitSkill_*.asset` +
    `UnitSkillDef` 를 쓴다.
12. **순서**: `battle-sim-extraction` M0 **앞**(사용자 결정 유지). 다만 rev 3 이 M0 골든에
    기대던 안전망을 unit 1 이 자급한다. M0 착수 후에는 시작하지 않는다.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설·생성→렌더 경로 변경 없음(코드 재배치 + 저작 형식 변경).

## rev 3 오류 (같은 실수 반복 금지)

critic 3트랙 + 직접 검산으로 확인된 사실 오류. **unit 0 에서 하기로 한 조사를 하지 않고
추정을 units 1~5 에 확정문으로 박은 것**이 근본 원인이다.

- **통통구슬은 주기×발사 카드가 아니다** — `mechanics: []` 이고 공격 수식자(튕김)다.
  카드 전수 중 **주기 트리거 0장**, 카드 `EmitProjectilePattern` 은 bake 가 loud 거절
  (그 거절을 고정한 EditMode 테스트가 이미 있다). rev 3 unit 3(카드 어댑터)은 대상이
  없는 작업이었다.
- **last_stand 는 방어유닛 능력 에셋이 아니라 카드다**(진동갑주도). 경계 감시의 카드
  출처 2건 — rev 3 은 이걸 "에셋 이전"으로 처리해 자기 계약 7 과 시트를 동시에 깼다.
- **채찍질은 짱쎈놈이 아니라 나이트메어 소유.**
- **니들러 실증은 거짓 검증이었다** — 악몽 메커니즘 보유 = `BossTag` 부착이 코드로
  강제(CC·도발 면역·유닛 사냥·보스 경보)라 "코드 0줄"이 아니고, 니들러는 적이라
  `OpponentsOf` 가 현행 하드코딩과 같은 값이어서 진영 파라미터화를 되돌려도 초록이다.
  → 검증 질문을 "같은 스킬 에셋 복제 장착"으로 교체, 진영 상대화는 후속.

## 후속 후보

- **진영 상대화 + 방어유닛 스킬 경로** — 방어유닛이 유닛 스킬을 장착하는 authoring 이
  열릴 때. 그때 `UltimateLeapSystem` 의 `Defender` 리터럴을 상태 필드로.
- **스킬 보유 ≠ 보스 분리** — `BakeNightmareMechanics` 의 `BossTag`·보스 경보 부착을
  별도 authoring 축으로. **콘텐츠 결정**(현 blueprint 정의: "능동 스킬을 가진 적 = 보스").
- **무거운 arm 이관** — 발동 지점이 전 유닛 순회 Burst 코드 내부라 별도 seam 설계 필요.
- **카드 authoring 의 SO 이전** — 어댑터 은퇴. 시트 연동 재설계와 함께.
- **(시전자, 스킬) → 고유 연출 매핑** · **스킬 툴팁 노출** · **호스트당 슬롯 스케줄러**
  (발동 중재 콘텐츠가 실재할 때).

## 결정 기록 (재론 전 필독)

- **08-11 ISkill 기각** — 실근거는 위험 변형 3종(에셋 SerializeReference / 보스별 클래스 /
  스킬의 상태 소유).
- **08-12 무상태 채택**(사용자 재결정) → **저작 SO + 순수 로직으로 확정**(사용자 결정).
  로직을 SO 안에 두는 안은 이식 게이트 충돌로 기각. 계속 금지: 스킬의 상태 소유(계약 3) ·
  보스별 클래스(변형은 에셋) · 발동 조건의 코드화(계약 2).
- **재론 조건**(슬롯 간 발동 중재: 우선순위·인터럽트·콤보) 유지 — 그때의 답도 구조 변경이
  아니라 호스트당 슬롯 스케줄러.
