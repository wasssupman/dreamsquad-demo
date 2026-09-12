# 5 — handoff summary (2026-09-12)

## Commit
- `619eae71` docs rev 1 · `ad8c75c9` docs rev 2 · `6f8473c4` unit 0 · `eb2cc3ef` units 1~3 · `26ef25a6` 리뷰 반영 ·
  `88cc8f14` unit 4(rev 2 저작) · `d1ed601d` **rev 3 복귀(정본)** · `a8b9520f` 참격 VFX. 전부 **미푸시**.

## Implemented (rev 3 = 안 1)
- 획득·락 유지·정지 판정은 **원**(종전 `AttackReach.InReach`). 「사거리 안이면 반드시 반응」.
- 부가 타격만 **주 대상을 향한 실제 방향** 도형으로 거른다 — `AttackReach.InReachShaped(shape, dirToPrimary)`,
  소비처 2곳(`AttackSystem` pass 루프 · `AggroTargeting.FillNearest`). 술어 본체 `SkillMath.SectorGateX/BandGateX`(+X 프레임,
  몸 걸침 SDF, sqrt·삼각함수 0). 회전은 래퍼가.
- 저작 `Data/AttackShape`(kind + 형마다 파라미터 하나 · 길이는 `attackRange`) → bake `Data/AttackShapeBake`(폴백 Omni · reflex 거절)
  → `AttackState.shape`(bake 0 = Omni 안전값). 브리지 스폰 3곳 `BakeAttackShape` 헬퍼.
- 표기 = 원 링 그대로. 방향 = **공격 순간 참격 자국 VFX** `SlashMark_SKELETON`(공격자 자리 · 타겟 방향, `attackVfxAtAttacker` 신설).
- 저작: 브루저·말파이트 `Circle/60` + 참격 VFX. 적 저작 없음. 문안 「휘두르는 쪽 60° 안 최대 3체 동시 타격」.
- 에셋 lane 허용 목록(`AttackShapeAuthoringTests`): 다중 타격 파이터 = 60°, 나머지 Omni, 적 전부 Omni.

## Key Files
`Scripts/Skills/SkillMath.cs` · `Scripts/Battle/Combat/AttackReach.cs` · `Scripts/Data/AttackShape.cs`·`AttackShapeBake.cs`·`AttackShapeBaked.cs` ·
`Scripts/Battle/Combat/AttackSystem.cs:1490~` · `AggroTargeting.cs:FillNearest` · `Scripts/Bridge/BattleBridge.cs`(BakeAttackShape · 히트 VFX 원점) ·
`Scripts/Data/UnitKitSummary.cs` · `VFX/SlashMark_SKELETON.prefab` · `Tests/EditMode/AttackShape*Tests.cs` · `Tests/EditModeAssets/AttackShapeAuthoringTests.cs`

## Verified
- EditMode 코어+에셋 **2837건 · 선행 실패 2건(bomb_man·boomerang 문안) 외 0** — 리그 배치 + 인-에디터 양쪽.
- 참격 VFX: 오프스크린 렌더로 +X·+Z 방향 부채꼴 확인(`scratchpad/slash_sheet2.png`).
- **골든 코퍼스: 미재베이크.** Verify 결과 9건 전부 `configHash` 드리프트 — 원인은 **다른 세션의 미커밋 map 프리팹 WIP**
  (`MapStage_StreetDay.prefab` 의 Blocker 7개 `m_IsActive` 토글 → 맵 tiles/placeMask 가 해시에 들어간다). 지금 재생성하면
  그 WIP 가 기준선에 구워진다(메모리 함정) → 그 세션이 커밋/원복한 뒤 재베이크. `wide_body` 골든은 원래 없음(unit 7 미베이크).
- Play 육안: 미실행(사용자).

## Notes
- **rev 2(좌/우 축 · 획득부터 도형 · 나비넥타이 표기)를 되살리지 말 것** — Play 에서 본 사용자가 폐기. 자동전투엔 던파의 「교정 수단」이
  없어 «사거리 안인데 가만히 선 유닛»이 반복 노출된다. 비대칭 + 플레이어 방향 지정으로 로스터 기둥이 될 때 별도 아키타입.
- `InReachShaped` 의 `shape` 는 기본값 없음(소비처가 선언). 획득 소비처 10곳은 도형을 **모른다** — 그게 rev 3 의 정의다.
- 단일 타겟 유닛의 도형은 효과 0 → `OnValidate` 경고. 저작하지 말 것.
- `in AttackShapeBaked.Omni`(프로퍼티 rvalue)는 CS8156 — `in` 없이 넘긴다.
- 브루저·말파이트의 **기존 히트 VFX(FireBlast · 흙 폭발)는 참격 자국으로 대체됐다**(슬롯 하나). 2슬롯·유닛별 팔레트는 후속.

## unit 6 (2026-09-12 추가)
- 배치 프리뷰 공격 가이드: `TilemapMapView.SetShapeGuide/ClearShapeGuide`(부채꼴 메시 2장, 링 색·대역) · `BattleBridge.RefreshRangeTargetMarks`
  가 `NearestTargeting.RanksBefore` 로 최근접을 뽑아 방향을 넘긴다. 라이브 검증 완료(`guide_f.png`). 참격 자국은 부채꼴 쿼드로 재작업(`94e41c6d`).

## Follow-up
- 골든 재베이크(map WIP 정리 후) · Play 육안(링 원 복귀 · 위쪽 적도 때림 · 참격 자국 방향) · `wide_body` 골든 첫 베이크.
- 참격 VFX 정식화(`_SKELETON` 접미사 제거 = 사용자 폴리시) · 유닛별 톤 · 타격점 히트 + 공격자 참격 2슬롯.
- README 후속 후보: 긴창 유닛(Rect 라이브 저작) · 스킬 광역 방향 항 · frontmost swap × 도형 · `SkillCone` 흡수 · 각 경계 히스테리시스.
