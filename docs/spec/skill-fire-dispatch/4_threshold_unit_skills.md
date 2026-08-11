# 4 — 경계 유닛 스킬 (장막 · 자폭 · 도약 ×2 · 궁극기)

## 목적

체력 경계 감시의 **유닛 소유 5행**을 이전한다. 카드 2행(빈사폭주·진동갑주)은 unit 5 의
어댑터가 맡으므로, 이 unit 이 끝나도 경계 감시엔 카드용 legacy 분기가 남는다.

**검증 질문의 주인공이 여기 있다** — 궁극기 스킬 에셋 복제 장착(재사용 축)의 실증.

## 변경 대상

| 파일 | 내용 |
|---|---|
| `Data/UnitSkills/SelfTileAoeSkillDef.cs` · `SelfBlinkSkillDef.cs` · `UltimateLeapSkillDef.cs` | 신규 저작 3종 (`GrantShieldSkillDef` 는 unit 3 재사용) |
| `Battle/Combat/Skills/SelfTileAoeSkill.cs` · `SelfBlinkSkill.cs` · `UltimateLeapSkill.cs` | 신규 로직 3종 |
| `Battle/Combat/HealthThresholdSystem.cs` | 유닛 5분기 → 디스패치 (카드 2분기·threat drain 잔존) |
| `Bridge/BattleBridge.cs` | bake case 3종 |
| 짱쎈놈(4행) · 마메모(장막) 에셋 + `UnitSkill_*.asset` | 저작 이전 |
| `Data/UnitSkills/UnitSkill_UltimateLeap_Mamemo.asset` | **신규 — 복제 실증용**(파라미터만 다름) |

## 구현

- **`UltimateLeapSkill` 의 경계가 이 spec 의 경계다**: 스킬이 갖는 것 = 개시(착지점 해석 →
  `LeapFlight`+`UltimateLeapState` **쌍** 부착 → 이탈 신호)와 수치(예고·슬램 피해·반경).
  2초 시퀀스(카운트다운·텔레포트·슬램·해제)는 **회피 창이자 피해 게이트 = 게임 규칙**이라
  `UltimateLeapSystem` 소유 불변. 착지 실패 시 "임계 소모·재시도 없음"도 현행 유지.
- **장막은 `GrantShieldSkill` 재사용**(반경 0 에셋) — 신규 로직 0. "같은 로직 × 다른
  조건 = 에셋 2개"(계약 2)가 spec 안에서 실증되는 지점.
- **도약 2행은 같은 스킬 에셋 2개**(임계 0.5 / 0.9) — 재사용 축의 두 번째 실증.
- **진영 리터럴은 건드리지 않는다**(계약 6). `UltimateLeapSystem` 의 `Defender` 리터럴과
  방어유닛 밀집 착지 풀은 **현재 라이브 시전자가 전부 적이라 옳다.** 파라미터화는
  방어유닛 경로가 열릴 때(후속) — 미사용 경로를 지금 만들지 않는다.
- **복제 실증(검증 질문)**: 마메모에 궁극기 스킬 SO 를 복제해 파라미터만 바꾼 에셋을
  참조시킨다. **코드 0줄**이어야 한다. 마메모는 이미 보스라 `BossTag` 부작용이 없다
  (니들러가 실증 대상으로 부적격했던 이유 — README "rev 3 오류").

## 완료 기준

- [ ] 컴파일 에러 0 · Burst 유지
- [ ] **unit 1 특성화 골든 무수정 그린**: 궁극기 · 도약 · 경계 자폭 (이 unit 의 주 증인)
- [ ] `BossShieldTest` 장막 경계 발동 그린
- [ ] **복제 실증**: 마메모 궁극기 에셋 참조만으로 발동 — PlayMode 1건 + `git diff` 로
      코드 변경 0줄 검산. 실증 후 마메모 에셋에서 되돌릴지는 콘텐츠 판단(사용자 확인)
- [ ] 경계 감시에 **유닛** legacy 분기 0 (카드 2분기 잔존은 정상 — unit 5)
- [ ] units 1~3 무회귀 · EditMode 전량 그린
