# 8 — Handoff (squad-character-page)

> feature 종료 인계. 최신 계약은 각 번호 문서·README·코드가 우선.

## Commit

- `576df8da` unit 0 — 기믹 요약문 생성기 `UnitKitSummary`
- `82df4182` unit 1 — 상세 패널 뷰(라이브 Spine + 통합 카드)
- `68592039` unit 2 — 리스트 브라우저 그리드
- `f42bef11` unit 3 — 헤더 편성/스톤 스트립 + 스톤 모드
- `feb029aa` unit 4·5 — 오케스트레이터 + 실제 squadPanel 배선
- `c1eb407b` unit 6 — `desc` SO 필드 + 현재 요약문 시드(17 자산)
- `42215dd7` unit 7 — desc 시트 import/export 왕복

## Implemented

- 스쿼드 페이지를 옛 "슬롯+모달"에서 **캐릭터 페이지**(상세 1/3 + 헤더 편성7·스톤4 + 브라우저 2/3)로 재설계, 모달 폐기.
- 좌 상세: **라이브 Spine 풀바디**(SkeletonGraphic, 전투와 동일 파츠/색) 백드롭 + 이름/등급·클래스·코스트 배지/스탯5/설명문/[출전] 통합 카드.
- 리스트 셀 탭→상세 갱신, [출전]/[편성해제] 토글(dedup·first-empty append·자동저장), 헤더 슬롯 탭=빠른 해제.
- **스톤 모드**: 헤더 스톤 슬롯 탭→같은 브라우저가 64 스톤 그리드, 상세가 스톤 정보(장착/해제), 활성 슬롯 아웃라인. 모달 없음.
- 유닛 설명 = **plain SO 필드 `desc`**(체력 등과 동형). 비면 `UnitKitSummary.Describe`가 자동 요약문 폴백. 17 Defender SO에 현재 요약문 시드.
- desc **시트 왕복**: `DefenderStatDto.desc` 1필드 → 매퍼 리플렉션이 import(빈 셀=유지)/export 흡수(매퍼·윈도우·익스포터 무변경).

## Key Files

- `Assets/_Project/Scripts/Data/UnitKitSummary.cs`(Build/Describe), `UnitLabels.cs`, `DefenderUnitData.cs`(desc)
- `Assets/_Project/Scripts/UI/Outgame/`: `SquadCharacterPage.cs`(런타임 빌더·씬 facing), `SquadCharacterPageController.cs`(오케스트레이터), `SquadUnitDetailView.cs`, `SquadRosterBrowser.cs`, `SquadHeaderStrip.cs`, `UnitRarityStyle.cs`, `DreamstoneStyle.cs`
- `Assets/_Project/Scripts/Data/StatImport/UnitStatImportDto.cs`(desc)
- `Assets/_Project/Scenes/OutgameScene.unity`(SquadPanel/CharacterPage GO + 옛 UI 비활성)
- 저작 킷: `desc_authoring_kit.md`, `defenders_full.json`/`.tsv`, `defenders_desc.json`

## Verified

- EditMode: `UnitKitSummaryTests` 13/13, `UnitStatImportTests` 44/44. 컴파일 클린.
- Play e2e: 로비 스쿼드 열기→실화면 렌더(라이브 Spine + 카드 + 헤더 + 그리드), 콘솔 에러 0, 브라우즈→상세 비파괴 확인. 유닛/스톤 2모드 프리뷰 시각 검증.
- 시트 왕복: Defenders 시트 read-only 대조 — 17행 정합, desc 일치, 스탯 드리프트 0(재검증 통과).

## Notes (되돌리면 안 됨)

- **SkeletonGraphic 런타임 렌더 조건**: `SkeletonGraphicDefault-Straight.mat` + 루트 Canvas `additionalShaderChannels`(TexCoord1/2·Normal·Tangent). 빌더가 세팅.
- **컨트롤러 주입 순서**: Controller GO inactive 생성→필드 주입→활성(OnEnable 준비완료 후 실행).
- **옛 SquadBuilderView 비파괴 보존**(enabled=false + 옛 자식 비활성). 되돌리기 = 역순.
- **desc 폴백**은 표시용(빈 desc→요약문). 시드값이 있으면 desc가 SoT. **숫자는 desc에 넣지 말 것**(스탯란이 SoT).
- 스톤 `SetStoneSlot`은 중복 허용 설계 → 장착은 "one item one slot" 이동으로 처리.

## Follow-up

- 사용자 실기기/에디터 hands-on(로그인→스쿼드에서 출전·스톤·저장 지속) 체감.
- 상세 패널 등급 글로우(`rarityFrame`) 미배선 — 원하면 배선.
- 캐스터 4종 desc는 이름 테마로 속성 분리 필요 시 저작 킷 프롬프트로 개선(자동 요약문은 "해저드 설치"로 동일).
- 시트 POST 엔드포인트 생기면 `defenders_full.json` 직접 전송(스키마 후속 후보).
- (무관) `Defender_Guardian.asset` sprite 참조 dirty — 이 feature와 별개.
