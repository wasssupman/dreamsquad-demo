# Gift Phase Presentation — Handoff Summary

## Commit

- `00c4efb2`·`439e2cf3` docs — 스펙 + 서사 계약/액센트 확정
- `cb3d99d9` unit 0 — GiftPhaseLayout 순수 수학 + EditMode 12
- `eeebb728` unit 1 — GiftCardWidget + 금/적 홀로 프레임
- `d52523ff`·`35fc78a7` unit 2 — 5 서사 비트 안무 재작성
- `c9571fc0` 코드리뷰 rev1 — 8앵글 findings 10건 반영
- `f8b6d9a3` unit 3 튜닝 — 리빌 홀드 1s + 근접감(2.1) + 편입 축소

## Implemented

- 선물 페이즈 안무 전면 재작성: 딜-인(하단중앙→5×2 그리드) → kind별 리빌(Lucid 강림/Rim 침투, 뒷면 플립, 내 덱 움찔) → 스택 수렴(출렁) → 리플 셔플(지퍼+잔상 트레일+글로우 리플) → 부채꼴 제시(최종순서 1→12, 스윕) → 순차 흡수(수신 앵커 링 n세그먼트, 가속 케이던스, 12번째 피니셔 찰칵).
- 프레임 3종: 내 덱 무프레임 / Lucid 금 / Rim 적 — `UiRoundedSprite` 링 + `DraftCardFoil_UI` 시머 2층(신규 셰이더 0). 판정은 `GiftAddedEntryIds`+`GiftKind`만.
- 카드 180×252, 리빌 2.1x 근접 등장 → 1s 읽기 홀드 → 스택 편입 비행 중 1.0 축소(원근 서사).
- 전 수치 GiftConfig(Accent tuning 포함 ~50필드) + default asset. 총 시간 실측 5.95s(홀드 제외)/~6.9s(포함).
- 덱 순서 계약 불변(`GiftFinalOrder()` = 부채꼴 좌→우 = 소비 순서), ECS/씬 배선/카드 스키마 변경 0.

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` — 라우팅+7스테이지 시퀀스
- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftCardWidget.cs` — 위젯+Shared(스프라이트/포일 캐시)
- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseLayout.cs` + `Tests/EditMode/GiftPhaseLayoutTests.cs`
- `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` + `GiftConfig_Default.asset`

## Verified

- 컴파일 0에러(dotnet+Unity), EditMode 755(753p/0f/스킵2 기존).
- Play 스모크: Rim 분기 완주→Placement, 재진입, 중단경로 leak 0(fx자식 0), 콘솔 에러/경고 0. Gift→Placement 실측 5.95s.
- 사용자 포커스 Play 육안 승인(2026-07-14). Lucid 분기는 스모크 상태검증만 — 육안은 자연 플레이 커버.

## Notes (되돌리면 안 되는 의도)

- **DraftCardFoil 은 _MainTex 미샘플 오버레이** — 단층으론 프레임 링 불가. 2층(크리스프 링+포일)이 정답, 셰이더 신작 금지.
- **총시간 판정 = PhaseChanged 실측** — 스테이지 명목치 합산은 스태거/펀치 꼬리를 놓침(리뷰에서 7.0s 적발 이력).
- **틱/링 스프라이트는 Shared 캐시** — 호출별 MakeCircle 재베이크 금지(판당 ~28장 텍스처 누수 이력). Dispose 는 텍스처까지.
- **피니셔 타이밍 삼자 일치**(위치·스케일·완료 Delay 모두 diveDur) — 하나만 늘리면 공중 증발 재발.
- **giftConfig null = 연출 생략+즉시 배치** 폴백 유지(소프트락 방지).
- PrimeTween 풀 400 선예약(Awake) — 한 시퀀스 ~249 트윈.
- 리빌 축소는 스택 편입 비행에서(사용자 결정) — 리빌 직후 축소로 되돌리지 말 것.

## Follow-up

- README 후속 후보 참조: 실제 아트 교체(guid 유지)·fly 타깃 런타임 정렬·각성 게이지 연동·SFX·포일 팩토리 공용화·그라데이션 UiRoundedSprite 이관·GiftConfig 참조 이원화 경고.
- 워킹트리에 무관 dirty 존재(폰트 SDF 4종·Mobile_RPAsset·probuilder Settings — TMP/Play 잔류, 이 spec 커밋에서 제외함).
