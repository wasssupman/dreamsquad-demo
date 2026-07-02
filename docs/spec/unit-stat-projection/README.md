# Unit Stat Projection

상태: **완료 2026-07-02** — unit 0~5 구현·커밋 (handoff `6_handoff_summary.md`). Play 시각 확인 + 실 엔드포인트 왕복은 후속.

## 목표

기획 시트의 직관 필드(`atk`, `heal`)가 실제 런타임 데이터 모델(`AttackOutput[] outputs`)로 **투영**되는 인터페이스를 구축하고, 아무도 읽지 않으면서 UI 표기만 오염시키는 레거시 `attackDamage` 스칼라를 제거한다. (근거: 전투 데미지 SoT는 outputs이며, Archer는 SO `attackDamage: 25` vs 실데미지 15로 이미 어긋나 있음.)

ralplan 합의(2026-07-02, Planner→Architect SOUND-WITH-REVISIONS→Critic APPROVE) 결과물. 선행 hotfix는 `docs/spec/unit-stat-spreadsheet-schema/2_importer_robustness_hotfix.md` (커밋 f404bfe).

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 계약 (docs) | `0_projection_contract.md` | 투영 규칙 + JSON 스키마 v2 delta 확정 |
| 1 | 구현 | `1_attack_output_stats_helper.md` | outputs 조회/갱신 static helper + 단위 테스트 |
| 2 | 구현 | `2_draft_card_stats_from_outputs.md` | 카드 DMG 표기를 outputs 파생으로 전환 |
| 3 | 구현 | `3_importer_projection_fields.md` | DTO `atk`/`heal` + 임포터 투영 + deprecation shim |
| 4 | 구현+마이그레이션 (severable) | `4_legacy_attackdamage_removal.md` | SO 양측 `attackDamage` 삭제 + 베이크 라인 정리 |
| 5 | 구현 (severable) | `5_attackstate_damage_removal.md` | ECS `AttackState.damage` dead 필드 제거 |
| 6 | handoff | `6_handoff_summary.md` | 종료 인계 |

순서: 0 → 1 → {2, 3} → 4 → 5 → 6. unit 4는 2·3 완료 후에만 착수(UI가 attackDamage를 읽는 상태에서 필드 삭제 금지). 각 유닛 종료 시 code-review → 수정 → 다음.

## Feature-wide 계약

- **outputs[]가 유일한 런타임 SoT** — 기획 필드는 임포트 시점에 outputs로 투영되는 입력이지 병렬 소스가 아니다. SO에 기획용 필드를 신설하지 않는다.
- **투영 규칙**: 해당 kind(`Damage`/`Heal`) 항목이 **정확히 1개**일 때만 그 항목의 magnitude를 갱신. 0개/2개+는 skip + 결과 로그에 사유 출력. 항목 생성/삭제/kind 변경은 Inspector(엔지니어) 영역.
- **런타임 무변경**: 변환은 Editor 임포트 단계에서 완결. BattleBridge 베이크 체인과 ECS 시스템은 투영을 모른다 (unit 4~5의 dead 코드 제거 제외).
- **페이즈 분리**: 투영(unit 0~3)만으로 feature의 검증 질문("시트 숫자가 게임에 반영되는가")에 답이 된다. 삭제(unit 4~5)는 위생 페이즈로 **둘 다 severable** — 드랍/연기해도 0~3 성립.
- **`aggroAttackDamage`는 유지** — `AggroAttackProfile`→`TauntAttackGrantSystem`이 소비하는 live 스칼라 (dead 필드 아님). 리플렉션 매핑도 유지.
- **로스터 불변식**: 전 유닛 asset에 Damage 항목 ≤1, Heal 항목 ≤1, id 유일. `UnitRosterInvariantTests`(EditMode asset 스캔)로 고정. 실패 메시지는 금지가 아닌 **재협상 프롬프트** — "Damage 2개+ 유닛이 필요하면 투영 규칙(unit 0)을 갱신하거나 해당 유닛을 시트 비관리로 표기".
- ApplyStat/ApplyStack/hazard/knockback/onPlace 수치는 시트 범위 밖 (후속 후보).

## 후속 후보

- **hazard/스택 틱뎀 시트 노출** [M] · `HazardSO.effects[].param1`(존 DPS), `StackModifierSO.thresholds[].magnitude`(스택 발동 DPS)를 동형 패턴(id + 시트 탭 + 투영 규칙)으로. 사용자 확정: 본 spec 이후 후속 (2026-07-02).
- **DTO `attackDamage` deprecation shim 최종 제거** [S] · 시트 v2 전환 확인 후. unit 6 handoff 시 본 항목의 Follow-up Backlog 등재 여부 확인 필수.
- **임포트 결과 요약의 기획자 노출** [S] · Damage 0개 유닛(7종)의 atk skip이 콘솔 로그만으로는 기획자에게 안 보임. 결과 리포트 전달 수단 검토.
- **카드 HEAL 표기 / 툴팁 확장** [S] · unit 2는 DMG 정합까지만. Healer 카드의 heal량 표기는 별도.
