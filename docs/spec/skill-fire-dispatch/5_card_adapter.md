# 5 — 카드 어댑터 (빈사폭주 · 진동갑주)

## 목적

경계 감시에 도달하는 **카드 2행**을 같은 실행 경로로 수렴시킨다. 카드 authoring 은
바꾸지 않는다 — 이 둘은 **시트가 덮는 유일한 mechanics 경로**(`OverlayMechanics`)라
SO 로 옮기면 임포터는 카드를 덮고 런타임은 SO 를 읽어 조용히 갈린다(프로젝트가 두 번
겪은 실패 모드). 그래서 **번역만** 한다.

이 unit 이 끝나면 두 감시의 실행이 전부 스킬 로직 파일로 간다.

## 변경 대상

| 파일 | 내용 |
|---|---|
| `Bridge/BattleBridge.Dreamcatcher.cs` | 부여(attach) 시 카드 행 → 스킬 파라미터로 번역해 슬롯 구성 |
| `Battle/Combat/HealthThresholdSystem.cs` | 카드용 legacy 분기 2개 제거 → 디스패치 |
| (로직 파일 신규 0) | `SelfStatBuffSkill` 은 이 unit 에서 신설, `SelfTileAoeSkill` 은 unit 4 재사용 |
| `Data/UnitSkills/SelfStatBuffSkillDef.cs` | 신규 — 유닛 저작 대비(현재 소비자는 카드뿐) |

## 구현

- **어댑터는 슬롯 레벨에서 끝난다** — 카드 행이 이미 `DcTriggerSlot` 으로 구워지고 있고
  (계약 4 로 슬롯 형식이 그대로다), 필요한 것은 **`SkillKind` 를 함께 싣는 것**뿐이다.
  런타임 SO 인스턴스를 만들지 않는다 → rev 3 이 걱정하던 누수 문제 자체가 없다.
- **수명 문제 없음**: 유닛 부착형 카드 슬롯은 회수 개념이 없다(반환 규약: "엔티티 부착형 =
  슬롯이 엔티티와 함께 소멸"). `RevokeDreamcatcherEffects` 는 슬롯을 건드리지 않는다 —
  건드리게 만들지 마라(별 spec 영역).
- **카드 bake 의 payload 화이트리스트를 완화하지 않는다.** 특히 카드
  `EmitProjectilePattern` 의 loud 거절은 EditMode 테스트가 고정한다 — 어댑터가 그 문을
  열지 않는지 확인한다.
- **이 어댑터는 과도기 장치가 아니라 항구 경계다.** 카드 authoring 의 SO 이전(후속 후보)이
  오기 전까지 카드는 이 문으로 들어온다. 주석에 박는다.

## 완료 기준

- [ ] 컴파일 에러 0 · Burst 유지
- [ ] **골든 무수정 그린**: `DreamcatcherKillThresholdTest`(빈사폭주 공격력 버프) +
      unit 1 의 `ThresholdTileAoeCharacterizationTests`(진동갑주 경로 포함)
- [ ] **시트 무회귀**: `DcSheetImportTests` 의 `last_stand` 오버레이 고정 그린 +
      시트 값을 바꿔 임포트하면 런타임 동작이 실제로 따라오는지 1회 육안 확인
- [ ] **경계 감시에 legacy payload 분기 0** — threat drain 만 잔존
- [ ] units 1~4 무회귀 · EditMode 전량 그린
