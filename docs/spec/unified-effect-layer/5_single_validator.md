# 5 — 검증 한 함수 (Unity · H5)

## 목적
저작 검증을 출처별 블랙리스트에서 **출처가 입력이 아닌 한 함수**로(계약 5). 같은 (트리거 × 주체 × 효과 × 탄 결합)은 카드·유닛 능력·악몽이 같은 답을 받는다. 같이 착탄 비산 인라인 자를 정본으로(제약 13).

## 변경 대상 (unit 0 표 3 으로 확정)
- 신설 `Scripts/BattleCoreUnity/EffectComboRule.cs`(가칭) — 순수 함수. 거절 사유 셋: 「원점을 못 낸다」 · 「효과가 그 원점 형을 못 받는다」 · 「붙는 순간 이미 지난 자기 사건(영영 안 터짐)」.
- `CardDefinitionBuilder.cs` · `BindingDefinitionBuilder.cs` — 각자의 거절 분기 → 위 함수. 「출처 관례」 사유였던 거절이 풀리는 조합은 라이브 0 이어야 한다(표 1).
- `Scripts/Data/Dreamcatcher/DcMechanic.cs` — 트리거에 **주체 축**(`Self` 기본 · 「남의 배치」) append · 효과에 **예고 체크**(U1) append. 코어엔 축이 이미 있다(`BindingSubject.Any` + `BindingSubjectFilter.PlacedDefender`) — 코어 쪽 「저작 노출 없음」 주석 갱신 · `CoreTriggerEnumPinTests` 에 새 값 핀. 시트 열은 후속(H4+시트).
- `TickProjectilePhase.cs` 착탄 비산 → `SkillMath.ReachFromImpact`(자리형). `SplashRadius` 는 월드 단위라 칸 단위로 변환(÷ 칸 크기)해 넘긴다. 비산 저작 탄(`Projectile_CannonBall`) 참조 0 — 커밋 전 재확인.
- **`CombatDefinitionBuilder.Translate` 는 손대지 않는다** — `SkyFallOnTarget` 은 캐논 폭격(라이브)이 쓰고, 전원 손잡이가 대상 결합 탄에서만 돈다.

## 완료 기준
- 표 테스트: 전 조합의 허용/사유가 출처 셋에서 같다.
- 빌더 스냅샷: 카드 52 · 유닛 능력 26 · 악몽 저작 6 을 굽힌 `BindingDef` 가 이 unit 전과 필드 동치.
- 메모리 SO 픽스처로 AA · 타격 운석을 빌더에 넣으면 경고 0 으로 굽히고, 탐침의 손조립 `BindingDef` 와 필드 동치(가이드 §5). 에셋은 만들지 않는다.
- 헤드리스 · 골든 11 · EditMode 3 어셈블리(선행 2 외 0) · PlayMode Core · `core-reviewer` APPROVE.
