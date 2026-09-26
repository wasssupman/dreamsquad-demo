# 4 — 검증 한 함수 (Unity · H5)

## 목적
저작 검증을 출처별 블랙리스트에서 **출처가 입력이 아닌 한 함수**로 바꾼다(계약 6). 같은 (트리거 × 주체 × 효과) 조합은 카드든 유닛 능력이든 악몽이든 같은 답을 받는다. 같이, 착탄 비산의 인라인 자를 정본으로(제약 13).

## 변경 대상 (unit 0 표 3 으로 확정)
- 신설 `Scripts/BattleCoreUnity/EffectComboRule.cs`(가칭) — (트리거, 주체, 효과 종류, 탄 결합) → 허용 / 거절 사유. 사유는 「원점을 못 낸다」「효과가 그 원점 형을 못 받는다」「수명 규칙상 영영 안 터진다(붙은 뒤 지난 자기 사건)」 셋.
- `CardDefinitionBuilder.cs` · `BindingDefinitionBuilder.cs` — 각자의 거절 분기를 지우고 위 함수 호출. 오늘 라이브 저작이 거절되던 조합은 표 3 에 따라 **여전히 거절**되거나(사유가 원점) 허용으로 바뀐다(사유가 출처 관례) — 후자는 라이브 저작이 0 이어야 무변.
- `Scripts/Data/Dreamcatcher/DcMechanic.cs` — 트리거에 **주체 축**(`Self` 기본 · 「남의 배치」) append. 코어엔 이미 있는 축(`BindingSubject.Any` + `SubjectFilter.PlacedDefender`)을 공유 저작 struct 가 표현 못 하는 것이 H5 의 일부라서. 효과 파라미터에 예고 체크(U1) append.
- `CombatDefinitionBuilder.Translate` — `SkyFallOnTarget` 번역은 unit 1 의 칸 결합 규칙으로 흡수(라이브 참조 0 확인 후).
- `Scripts/BattleCore/Phases/TickProjectilePhase.cs` 착탄 비산 → `SkillMath.ReachFromImpact`(자리형). 비산 저작 탄 `Projectile_CannonBall` 참조 0 — 커밋 전 재확인.

## 구현
- 검증 함수는 순수(에셋 입력 없이 enum·수치만) → EditMode 표 테스트.
- 새 콘텐츠 에셋은 만들지 않는다. 조합 검증은 메모리 SO 픽스처로.

## 완료 기준
- 표 테스트: 트리거 × 주체 × 효과 × 결합 전 조합의 허용/사유가 출처 셋에서 같다.
- 빌더 스냅샷: 오늘의 카드 60 · 능력 26 · 악몽 24 를 굽힌 `BindingDef` 가 이 unit 전과 필드 동치.
- 헤드리스 · 골든 11 · EditMode 3 어셈블리(선행 2 외 0) · PlayMode Core · `core-reviewer` APPROVE(unit 1~4).
