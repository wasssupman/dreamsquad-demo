# 6 — 인계 요약

## Commit
- 0 `d04a2229e` 전수 표 · 1 `c14630663` `2d7aa12fc` 발사 요청 한 갈래 · 2 `19db40d8d` 원점 읽기 한 벌 · 3 `c547ad38a` 버스트 슬롯 발동 주체 · 4 `ce21da147` 탄 그림은 사건만으로 · 5 `042979b2a` `7f3a44804` `816107653` `dfce6ccd3` `ab89b8a46` `18443a52d` 검증 한 함수 · 저작 축 · 비산 자 · 스크립트 `d0edbd22d` · 7 `bc8c4f61b` 브레스 콘 도달 정본화 · `31179517c` 굽기 스냅샷 2종(반각 표현만 1:1). 브랜치 `unified-effect-layer`(워크트리 `wassup-core`), **미푸시**.

## Implemented
- 탄 발사 요청은 궤적 결합 종류(대상 · 칸 · 방향) 한 갈래. 자리형 concrete·퇴근 기믹 운석이 「하늘 낙하 × 칸 광역」을 스스로 명시(강제 제거 · 누락은 경고 후 버림).
- 원점 = `SkillOrigin`(발사 자리 · 효과 좌표 · 원점 몸 · 지정 칸) — 드레인 한 곳이 채우고 concrete 21개는 `target.Origin` 만 읽는다.
- 착탄 예고는 효과 파라미터(U1) — `SkillParams.Telegraph` · `BindingDef.Telegraph` · 저작 `EffectData.values.telegraph`(`skill-data-table` 이후 — 옛 `DcPayloadSpec.telegraph` 는 보기 창에만 남았다).
- 발사 명세 버스트는 발동 주체 자리에서 그 몫으로(U3). 발동 주체나 호스트가 사라지면 멈춘다(U2).
- 뷰는 월드 탄을 되묻지 않는다 — 생성 사건 보류 · 같은 배달 묶음 소멸이면 버림.
- 저작 검증 `EffectComboRule`(코어 `Trigger/`) 하나를 카드·유닛 능력·악몽이 공유. 출처 관례 거절 해제(카드 AA · 타격 운석 가능).
- 저작 축: 트리거 주체 = 코어 `BindingSubject { Self, Any }`(저작 `TriggerSpec.subject` · 「남의 배치」 = `Any` — 옛 거울 `DcTriggerSubject { Self, OthersPlacement }` 는 `skill-data-table` unit 4 에서 은퇴).
- 착탄 비산 도달 → `SkillMath.ReachFromImpact`(자리형).
- 브레스 콘 = 후보 원 AND `SkillMath.SectorGate`(대상 몸 걸침 · 발사 자리 기준 · 반각 bake = (sin, cos)) — unit 7, 도달이 넓어지는 규칙 변경을 사용자가 승인(계약 6 의 예외).

## Key Files
`Scripts/BattleCore/Trigger/{IntentApplier,TriggerDispatcher,EffectComboRule}.cs` · `Scripts/Skills/ISkill.cs`(`SkillOrigin`) · `Scripts/BattleCore/Phases/{CombatPhase,TickProjectilePhase}.cs` · `Scripts/BattleCoreUnity/{BindingDefinitionBuilder,CardDefinitionBuilder}.cs` · `Scripts/BattleCoreUnity/View/CoreProjectileViewPool.cs` · `Scripts/Skills/Concrete/ConeBreathSkill.cs` · `Scripts/Skills/SkillMath.cs`(`SectorGate`) · 테스트 `Tests/EditModeCore/{HardCase*ProbeTests,SpawnAssemblyEquivalenceTests,EffectComboRuleTests}.cs` · `Tests/EditModeAssets/{BindingBakeSnapshotTests,UnifiedEffectBakeFixtureTests}.cs` · 기준선 `Tests/EditModeAssets/Fixtures/binding_bake_snapshot.txt`.

## Verified (2026-09-28, HEAD `40d5cfe41` 기준)
- 헤드리스 `tools/battle-core-rebuild/headless/verify-fresh-skills.sh`: 981/0 · BattleCoreUnity.Check 0 · Retire.Check 0.
- Unity EditMode 3 어셈블리 2589 — 실패 = 선행 2(bomb_man · boomerang 시트 문안)뿐 · 굽기 스냅샷 2종 초록(라이브 무변) · 골든 11 일치 · PlayMode Core 97/97 · 콘솔 오류 0.
- 탐침 보류 5건 전부 해제 · core-reviewer APPROVE ×3(발견 0).
- unit 4 의 PlayMode 계측(「비행 0 × 프리팹 있는 탄은 비행 그림이 안 선다」)은 자리 폭발 카드 6장 중 **대표 1장**(진동갑주 — `Tests/PlayModeCore/CorePlayThreeSymptomTests.Tremor.cs`)으로 했다.
- unit 7(2026-09-28): 헤드리스 981/0 · EditMode 3 어셈블리 2620 중 실패 = 선행 2 + 카드 아트 1 · 골든 11 일치 · 굽기 스냅샷 2종은 반각 표현만 바뀜(`31179517c`).

## Notes
- **헤드리스는 `Wassup.Skills.dll`·`Wassup.Runtime.dll` 을 워크트리 `Library` 에서 받는다.** Skills 는 스크립트가 새로 굽지만 Runtime 은 아니다 — `Data/` 저작 형이 바뀐 커밋은 Unity 재컴파일 전까지 BattleCoreUnity.Check 가 거짓 빨강.
- 굽기 스냅샷 기준선은 Unity 에서만 생성된다(테스트가 파일이 없으면 쓰고 실패). 의도된 저작 변경 시 파일을 지우고 재생성 → 커밋.
- 「주인이 떠난 사건 × 살아 있는 주체 효과」는 이제 모든 출처에서 거절(라이브 0 — 전엔 유닛·악몽에서 조용히 무동작).
- 발동 주체 없는 발동의 발사 자리 = 발화 스냅샷(전엔 (0,0,0) — 라이브 0).

## Follow-up
- ~~사용자 플레이 확인~~ 2026-09-28 통과(실사용 카드 개사기 · 별똥 타격 `1824f1fd2` `3c5562541` — 시트는 2026-09-30 8탭 재구성 때 반영).
- 두 카드 **전용 아트** 필요 — `DreamcatcherCardArtTests.VisibleCards_HaveUniquePortraitSpriteArtwork` 빨강(복사 원본 그림 공유).
- ~~효과 정체(H4) + 시트 Effects 탭 한 spec · 새 저작 축 2개의 시트 열~~ → `docs/spec/skill-data-table/` 로 해소(시트 `SkillOwners.subject` · `Skills.telegraph`).
- 부착 판정의 도발 가디언 검사 · `CastHazardSkill` 미등록 죽은 파일 처리.
- 머지·푸시는 사용자 승인 후.
