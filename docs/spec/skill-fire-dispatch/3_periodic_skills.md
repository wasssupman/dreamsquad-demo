# 3 — 주기 잔여 스킬 (가호 · 채찍질 · 발사 명세)

## 목적

주기 감시의 남은 3분기를 이전한다. **전부 보스 유닛 베이크다 — 카드 출처 0**(unit 0
표 1). 이 unit 이 끝나면 주기 감시에 legacy 분기가 남지 않는다.

## 변경 대상

| 파일 | 내용 |
|---|---|
| `Data/UnitSkills/GrantShieldSkillDef.cs` | 신규 — 실드량 + 반경(0=self) |
| `Data/UnitSkills/AllyMoveSpeedAuraSkillDef.cs` | 신규 — 이속% · 반경 · TTL |
| `Data/UnitSkills/EmitPatternSkillDef.cs` | 신규 — 패턴 SO 참조 |
| `Battle/Combat/Skills/GrantShieldSkill.cs` · `AllyMoveSpeedAuraSkill.cs` · `EmitProjectilePatternSkill.cs` | 신규 로직 3종 |
| `Battle/Combat/BossPeriodicTriggerSystem.cs` | 3분기 → 디스패치. legacy 분기 소멸 |
| `Bridge/BattleBridge.cs` | bake case 3종 (에셋→인덱스 번역은 **계속 bake 가 한다**) |
| 마메모(가호) · 나이트메어(채찍질 · 발사 ×2) 에셋 + `UnitSkill_*.asset` | 저작 이전 |

## 구현

- **`GrantShieldSkill` 은 self·반경을 한 로직으로 겸한다** — 구분은 필드. 반경형의
  **host 제외**는 계약이다(`ShieldMath` 가 source 를 병합 키로 써서 host 를 포함하면
  장막·가호가 한 슬롯을 공유해 "경계 실드가 상시 실드로 붕괴"). VFX 는 **부여 성공한
  수혜자 각각의 위치에**(가디언 선례). self 형 발동 경로 연결은 unit 4.
- **`projectileDataIndex` 는 계약 6 의 예외다** — 이건 수치가 아니라 브리지 소유
  레지스트리 인덱스이고, 미지정이면 드레인이 요청을 통째로 버려 **연출이 아니라 피해가
  사라진다**. 에셋→인덱스 번역과 loud 거절은 bake 에 남긴다. 패턴 template(`owner` 가
  박힌 채 만들어짐)도 마찬가지로 bake 유지.
- **발사 명세의 host 상태 쓰기는 남는다** — arm 이 host `PatternSlot` 의 `fireCountBase` 를
  되쓰는 영속 카운터가 있다. 계약 3("진행형 상태는 컴포넌트 소유")에 해당하므로 ctx 동사가
  host 버퍼 write 를 포함한다.
- **채찍질 소유자는 나이트메어다**(rev 3 은 짱쎈놈으로 오기재). `duration > periodSeconds`
  authoring 계약(merge-refresh 유지)은 `Validate` 로 이사한다.

## 완료 기준

- [ ] 컴파일 에러 0 · Burst 유지
- [ ] **골든 무수정 그린**: `BossShieldTest.NightWard`(가호 host 제외) ·
      `ProjectileEmitterIntegrationTests` 8종 · `PatternBakeTests`(카드 거절 고정 포함)
- [ ] unit 1 의 `WhipAuraCharacterizationTests` 그린 (채찍질 무회귀 — 이 unit 의 유일한 증인)
- [ ] **주기 감시에 legacy 분기 0** — diff 로 확인
- [ ] units 1·2 무회귀 · EditMode 전량 그린
- [ ] 나이트메어 Play 육안 1회 (융단폭격 + 채찍질 이속)
