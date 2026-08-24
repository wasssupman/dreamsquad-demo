# 2a — 도메인 핸들과 캐리어 ID 대역

## 목적

`ISkill` 이 `Entity` 를 만지지 않으려면 중립 핸들이 필요하다. `SimEntityId` 가 그 자리에
가장 가깝지만 **그대로는 못 쓴다** — `IComponentData` 라서 도메인이 참조하는 순간 계약 1 위반이다.

동시에, 순진하게 부착 범위를 넓히면 **골든이 전건 발산한다.**

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/SimEntityId.cs`
- 신설: `Wassup.Skills` asmdef 안의 plain 핸들 타입
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 발급 지점(`:541`·`:818`), 매치 리셋(`:1683`)

## 구현

1. **도메인 핸들 타입을 신설한다.** `Wassup.Skills` 는 Entities 를 참조하지 않으므로
   `IComponentData` 인 `SimEntityId` 를 쓸 수 없다. plain struct(`SkillEntityId { int value; }`)를
   두고 **ECS 컴포넌트가 그 값을 싣는다.** 미발급 sentinel 은 `SimEntityId.Unassigned` 계승
   (`int.MaxValue` — 0 을 폴백으로 쓰면 «0번 유닛» 과 충돌해 조용히 순위를 훔친다).
2. **caster 없음을 표현한다.** 액티브 스킬은 시전 주체 엔티티가 존재하지 않는다
   (`Battle/Combat/ThreatTable.cs:20~22` — *"bridge-cast skills (player Meteor, owner == Null)"*).
   → `SkillEntityId.None` sentinel + 진영을 별도 인자로. `Execute` 가 caster 로부터 진영을
   파생시키는 경로 하나에만 의존하면 이 가족이 못 들어온다.
3. **캐리어 ID 는 별도 대역으로 발급한다.** 장판 캐리어는 매치 **중간**(스킬 발동 시점)에
   스폰된다. 같은 카운터를 쓰면 이후 스폰되는 모든 유닛의 ID 가 밀리고, 그 번호가
   **타겟팅 동률 승자와 발사 RNG 열을 정하므로**(`SimEntityId.cs:9~11`) 골든이 전건 발산한다.
   → 캐리어는 별도 대역(음수 또는 상위 오프셋) 또는 부착 자체를 「포트가 캐리어를 실제로
   참조하는 unit」까지 지연.
4. **발급 싱글턴 승격은 하지 않는다.** `SimEntityId.cs:22~23` 이 승격 시점을 「M1 이 이벤트·
   스냅샷 키로 ID 를 쓰기 시작하면」으로 못박아 뒀고, 캐리어 생성처가 전부 managed 쪽
   (`BattleBridge` 드레인 · `Effects/EffectSpawner.cs`)이라 Bridge 필드 카운터로 충분하다.
   지금 승격하면 발급 지점과 매치 경계 리셋이 두 조각 난다. **M1 로 반환.**

## 완료 기준

- [ ] 도메인 핸들 타입이 `Wassup.Skills` 안에 있고 Entities 를 참조하지 않는다
- [ ] `SkillEntityId.None` 이 액티브(플레이어 시전)를 표현한다
- [ ] 캐리어 ID 발급이 기존 유닛 ID 열을 **밀지 않는다** — 골든 코퍼스 전건 초록으로 확인
- [ ] 발급 싱글턴 승격이 이 spec 에 **들어오지 않았다**(M1 후속 후보로 이관 기재)
- [ ] EditMode 코어 lane 초록
