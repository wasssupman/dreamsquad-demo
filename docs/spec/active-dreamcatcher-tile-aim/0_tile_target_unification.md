# 0 — 대상축 폐기 + 아군 광역 버프를 타일 캐스트로 수렴

## 목적

Active 의 대상축(`SkillTargetType`)을 없애고, 유닛 대상이던 공격폭증·속사를 **지정 타일
반경 내 아군 전부** 버프로 재정의한다. 캐스트 창구를 `CastSkillAtTile` 하나로 모아 UI 가
분기할 이유를 제거한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/SkillData.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs`
- `Assets/_Project/Scripts/UI/Outgame/CardCategoryStyle.cs`
- `Assets/_Project/Data/Skills/Skill_PowerSurge.asset` · `Skill_RapidFire.asset`
- 테스트: `Assets/_Project/Tests/EditMode/DreamcatcherCardTextTests.cs` · `HandCardStyleTests.cs`

## 구현

1. **`SkillTargetType` enum + `SkillData.target` 필드 삭제.** `range` 주석을 "효과 반경
   (체비셰프 타일). 0 = 지정 타일 1칸" 으로 갱신. 아군 대상 판별은 `SkillData.TargetsAllies`
   (`effect == PowerSurge || RapidFire`) 프로퍼티 — 필드가 아니라 파생값이라 직렬화 무변경.
   에셋의 `target:` 키는 orphan 으로 남아도 무해하나, 손대는 6개 스킬 에셋에서는 함께 제거.
2. **`CastSkillAtTile` 에 아군 버프 흡수.**
   - `skill.target != TilePoint` 게이트 삭제.
   - `case PowerSurge:` → `ApplyAllyBuff(tile, skill, StatKind.DamageMul)`,
     `case RapidFire:` → `ApplyAllyBuff(tile, skill, StatKind.AttackSpeedMul)`.
   - **아군 버프에서 `affected == 0` 이면 `false` 반환**(무차감). 적 장판(SlowField/Tornado/
     Meteor)은 기존대로 0기여도 성공 — 계약 5.
3. **`CollectAlliesInRange(center, tileRange, outList)` 신설**(private). `_defenderByTile`
   순회 + `_em.Exists` + `PendingDeployment` 제외 + `GridMath.ChebyshevDistance <= tileRange`.
   선례: on-place 오라 루프(`ApplyOnPlaceAura` 계열)와 같은 형태.
   - `ApplyAllyBuff` 가 이걸로 모아 `EnqueueStatModifier(..., ModifierOrigin.Skill)`.
   - `public int CountDefendersInRange(Vector2Int center, SkillData skill)` 도 같은 함수를
     호출한다(조준 UI 전용 읽기 조회, `SetSkillAimRange` 와 같은 인자 shape).
4. **`CastSkillOnDefender` 삭제** + 컨트롤러 `CommitActiveDefender` 삭제. 호출처는 unit 1 에서
   `CommitActiveTile` 로 합류한다(이 커밋에서는 드래그 슬롯의 Active-Defender 분기를 임시로
   `CommitActiveTile` 로 돌려 컴파일·동작을 유지).
5. **에셋**: `Skill_PowerSurge` · `Skill_RapidFire` 의 `range: 0` → `1`.
6. **문안**: 두 스킬의 카드 텍스트를 `타일 지정 → 반경 N칸 아군 공격력 ×2.0 · 8초` 형태로,
   태그 칩(`CardCategoryStyle.ActiveTargetTag`)은 `타일 지정` 으로. 기존 "아군 유닛 지정"
   문안 제거.

## 완료 기준

- [ ] 컴파일 통과, `SkillTargetType` 잔존 참조 0.
- [ ] EditMode: 카드 텍스트·태그 칩 기대값 갱신 후 그린.
- [ ] PlayMode(unit 3 에서 작성): 3×3 내 아군 2기 배치 → 공격폭증 캐스트 → 2기 모두
      `damageMul` 상승, 반경 밖 1기는 불변. 아군 0기 타일 캐스트는 `false`.
- [ ] 콘솔 에러/워닝 0.
