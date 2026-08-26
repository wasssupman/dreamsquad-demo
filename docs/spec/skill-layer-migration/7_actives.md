# 7 — 액티브 (`SkillData`) 6에셋

## 목적

**세 번째 어휘를 죽인다.** `SkillData`/`SkillEffectType` — 플레이어가 손패에서 써서 타일을
지정하는 스킬 6종(SlowField · PowerSurge · RapidFire · Tornado · Meteor · Portal).

⚠ **6종 전부 라이브다**(census 실측 2026-08-25). `Active_*` 6장이 전부 `visible: 1` 이고,
`DreamcatcherCardCatalog` 에 Active 카드가 없어 `FilterHiddenSkills` 를 전부 통과하며,
BattleScene 배선이 `defaultPool` = 6종 전체 + `defaultCount: 2` 다 — **풀 6종에서 판마다 2종 시드 추첨.**
초기 초안의 「라이브 3」은 미검증 수치였다.

이 가족이 「호출자 = 소유자」 모델의 **가장 강한 시험대**다 — 시전 주체 엔티티가 없다.

## 변경 대상

- `Assets/_Project/Scripts/Data/SkillData.cs` — `SkillEffectType` enum
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:2505~2583` — `CastSkillAtTile`/`CastPortal` switch
- 같은 파일 `:2819~2958` — 6 arm 구현
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs:93~98` — `skill` 필드
- (잔존) `Assets/_Project/Scripts/Core/SkillRuntime.cs` — 쿨다운·코스트

## 구현

1. **편입 비용은 낮다**(실측): 신규 질의 **0** · 신규 의도 **1**(존 캐리어 스폰 —
   `TornadoField`·`PortalLink`·`AllyBuffField` 3종이 한 형태).
   액티브는 **경로장을 읽지 않고**, 유일한 읽기가 `aliveAttackers` 순회 + Position 인데
   `SlowField` 의 대상 선정 1곳 외엔 전부 **로그 preview** 다
   (코드가 *"로그 전용 — 성공/실패 판정에 쓰지 않는다"* 라고 명시).
2. **caster 가 없다.** `Battle/Combat/ThreatTable.cs:20~22` —
   *"bridge-cast skills (player Meteor, owner == Null)"*.
   토대 unit 2a 의 `SkillEntityId.None` + 별도 진영 인자를 쓴다.
3. **Portal 은 타일 2개**(entry/exit)를 받는 유일한 스킬이고 **입구==출구 거절 규칙**까지
   대상 축에 걸려 있다(`BattleBridge.cs:2554~2581`). `SkillParams` 의 대상 셀 A/B 축이
   여기서 실제로 쓰인다 — 토대 unit 0 이 이 축을 고정하지 않았으면 이 가족이 못 들어온다.
4. **쿨다운·코스트는 호출자 소유로 남긴다** — `skillRuntime.IsReady/Consume`(`:2509`·`:2536`).
   이미 「호출자 = 소유자」와 정합이다. 스킬 안으로 끌어들이지 않는다.
5. **진행형 상태는 남긴다** — 당김·버프 적용·텔레포트는 이미 Effects/Movement 시스템 소유고
   `MovementSystem` 이 `TornadoField`/`PortalLink` 를 매 프레임 질의한다(토대 계약 5).
6. **판 밖 요소는 어댑터/호출자 몫** — 텔레그래프 뷰 등록부(`:2916~2919`) 등.

## 완료 기준

- [ ] 6에셋이 concrete + 저작 SO 로 존재하고 `CastSkillAtTile`/`CastPortal` switch 가 죽었다
- [ ] `SkillEffectType` enum 이 **삭제**됐다 (세 번째 어휘 소멸)
- [ ] caster 없는 시전이 동작한다 (`SkillEntityId.None`)
- [ ] Portal 이 타일 2개를 받고 입구==출구 거절이 유지된다
- [ ] 쿨다운·코스트가 호출자에 남아 있다
- [ ] **롤 풀 6종 전부** Play 로 육안 확인 (판당 2종 추첨이므로 여러 판 또는 시드 고정 필요)
- [ ] 그물 초록


---

## 진행 (2026-08-26)

| 조각 | 내용 | 상태 |
|---|---|---|
| **7a** | 대상 셀 축 개통 + `SlowField` | 완료 |
| **7b** | `PowerSurge` · `RapidFire`(아군 버프 장판) | |
| **7c** | `Tornado` · `Portal` | |
| **7d** | `Meteor` + switch·enum 철거 | |

## 7a 에서 나온 것

**대상이 엔티티가 아니라 «칸»인 경로가 처음 열렸다.** 토대가 `SkillTarget.CellA/CellB` 를
깔아 뒀는데 **이벤트가 그걸 안 나르고 있었다** — `BuildTarget` 이 엔티티가 없으면
`SkillTarget.None` 으로 접었다. 액티브는 손패에서 칸을 찍어 쓰므로 겨눌 엔티티가 없고,
그대로 두면 여섯 스킬이 전부 조준을 잃는다.

**`affectedCount` 문제는 「셈은 읽기다」로 풀린다.** 이 값은 로그 전용이고(호출처가
`out _` 로 버린다) 「몇 기가 걸렸나」는 실행 **전에도** 셀 수 있는 읽기다. 그래서
fire-and-forget 포트를 안 깨고 로그를 지킨다 — 브리지가 preview 를 세고 스킬은 실행만 한다.
⚠ 이 해법은 **셈이 결과가 아니라 예측일 때만** 성립한다. 판정에 쓰는 값이었다면
`PlacementAura` 의 회수 토큰과 같은 벽이었다.

**액티브도 부착 seam 을 쓴다.** 시전이 동기 트랜잭션이라(쿨다운 게이트 → 실행 →
`Consume` + 로그) 프레임을 기다리면 소모 뒤에 실행이 도착한다. unit 4a 가 연 seam 이
여기서 두 번째 사용자를 얻었다 — 「시전 주체가 없다」는 이 가족의 특징과 무관하게
**호출이 동기라는 성질**이 같아서다.

⚠ **`SlowField` 는 장판이 아니라 스냅샷이다.** 이름과 달리 그 순간 반경 안에 있던 적에게
TTL 모디파이어를 한 번 거는 것이고, 늦게 들어온 적은 안 걸린다(그물이 그 축을 단언한다).
진짜 장판(`AllyBuffField`·`TornadoField`)과 다른 형태이므로 concrete 도 다르다.

⚠ **출처(source)가 대상 자신이다.** 시전 주체가 없어 병합 키의 source 축을 채울 엔티티가
없다 — 레거시가 그렇게 했고(`EnqueueStatModifier` 의 `source = target`), 바꾸면 같은
스킬을 두 번 써도 슬롯이 갈린다.

⚠ **`SimEntityId` 미발급이 다섯 번째다.** 액티브 그물 셋의 더미가 안 받고 있었다.
이제 이 증상의 서명이 확실하다 — **감지·드레인은 초록인데 효과만 없다.**
