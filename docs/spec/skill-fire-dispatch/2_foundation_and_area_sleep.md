# 2 — 토대 + 첫 스킬(자장가)

## 목적

토대 4종(`UnitSkillDef` · `SkillKind` · `SkillContext` · params 뷰 규약)을 세우고
**자장가를 첫 스킬로 이전**해 토대가 실물로 증명되게 한다. 토대만 먼저 커밋하면 소비자
없는 죽은 코드다.

## 변경 대상

| 파일 | 내용 |
|---|---|
| `Data/UnitSkills/UnitSkillDef.cs` | 신규 — 저작 베이스 SO (unit 0 시그니처) |
| `Data/UnitSkills/AreaSleepSkillDef.cs` | 신규 — 재울 인원·반경·지속 + `Validate` |
| `Data/AttackUnitData.cs` | `UnitSkillDef[] skills` 필드 append (legacy `nightmareMechanics` 병존) |
| `Battle/Combat/Skills/SkillContext.cs` | 신규 — 프레임 스코프 ctx(계약 5) |
| `Battle/Combat/Skills/AreaSleepSkill.cs` | 신규 — params 뷰 + static Execute |
| `Battle/Combat/BossPeriodicTriggerSystem.cs` | AreaSleep 분기 → `AreaSleepSkill.Execute` 호출 |
| `Bridge/BattleBridge.cs` | bake: `skills` 배열 번역(case) — 슬롯 산출은 legacy 와 동일 |
| `Data/UnitSkills/UnitSkill_Lullaby.asset` · 마메모 에셋 | 자장가 행 → 스킬 SO 참조 |

## 구현

- **슬롯 산출이 legacy 와 바이트 동일해야 한다** — 새 저작 경로가 굽는 `DcTriggerSlot` 이
  기존 `nightmareMechanics` 경로와 같은 값이면 골든이 무수정으로 살아난다(계약 4).
  이전 커밋에서 **에셋 값 대조표**를 남긴다(float→int 절삭·클램프 규칙 포함).
- **`BossTag` 부착은 legacy 와 동일 조건으로 유지** — `skills` 가 비어있지 않아도 보스가
  된다(현행 "능동 스킬 = 보스" 계약). 분리는 후속 콘텐츠 결정.
- **`SkillContext` 가 캡처할 표면을 이 unit 에서 확정**한다(자장가에 필요한 것만 노출하되,
  두 감시가 이미 들고 있는 큐·lookup 목록을 문서에 적어 다음 unit 이 놀라지 않게 한다).
  lazy 풀 플래그는 ctx 안, Dispose 는 `OnUpdate` 말미 단일 지점.
- **로직 파일은 UnityEngine 을 참조하지 않는다**(계약 1). 이 규칙을 파일 머리 주석에 박고,
  위반 시 무엇이 깨지는지(이식 게이트) 한 줄로 남긴다.
- 자장가 규칙은 현행 그대로: 전 범위 후보 → 거리² 정렬 → **«내가 때릴 대상»
  (attackTargetCount 기, 사거리 안일 때만) rank 제외** → cap. **도넛 금지**(실측 두 번 폐기).

## 완료 기준

- [ ] 컴파일 에러 0 · Burst 유지(두 감시의 `[BurstCompile]` 무제거를 diff 로 검산)
- [ ] **골든 무수정 그린**: `BossLullabyTest` 2종 + `BossLullabyLiveTest`(boolean) —
      테스트 파일 diff 0
- [ ] rank 제외 회귀 가드가 여전히 문다(규칙 훼손 시 빨개짐 1회 확인)
- [ ] 마메모 나머지 2능력(legacy 경로 잔존) 무회귀: `BossShieldTest`·`EnemyShieldTest`
- [ ] unit 1 특성화 4종 그린 · EditMode 전량 그린
- [ ] **읽기 검증**: 자장가 조건→실행이 파일 2개(`UnitSkill_Lullaby` 저작 + `AreaSleepSkill`
      로직)로 읽히는지 육안 확인 — 이 spec 의 상위 목표
