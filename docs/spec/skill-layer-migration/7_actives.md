# 7 — 액티브 (`SkillData`) 6에셋

## 목적

**세 번째 어휘를 죽인다.** `SkillData`/`SkillEffectType` — 플레이어가 손패에서 써서 타일을
지정하는 스킬 6종(SlowField · PowerSurge · RapidFire · Tornado · Meteor · Portal),
그중 **라이브 3**(`Active_Meteor` · `Active_Tornado` · `Active_Portal`).

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
- [ ] 라이브 3종(Meteor·Tornado·Portal)을 Play 로 육안 확인
- [ ] 그물 초록
