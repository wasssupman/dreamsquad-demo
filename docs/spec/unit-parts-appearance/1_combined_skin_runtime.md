# Unit 1 — combined skin 런타임 합성

> rev 2026-07-07: critic 리뷰 반영 — 드래그 프리뷰 경로 편입(F1), 캐시 키를 SkeletonData 인스턴스로(F2), eye 슬롯 틴트 제약(F3), comboKey 순서 의미(F5/F6).

## 목적

스킨 적용을 공유 헬퍼로 일원화하고, 파츠 목록이 있으면 combined skin 을 합성·적용 + 슬롯 색상 틴트한다. 스폰 경로와 드래그 프리뷰 경로가 같은 함수를 타게 해 기존 코드 중복도 제거한다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineCombinedSkinCache.cs` — 신규 (합성 + 캐시 + 공유 적용 헬퍼)
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — Spawn 의 스킨 블록을 헬퍼 호출로 교체
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 프리뷰 스킨 블록(현재 Spawn 과 코드 중복)을 동일 헬퍼 호출로 교체. critic 확인: 프리뷰 누락은 추측이 아니라 **확정** — 여기서 처리하지 않으면 unit 3 왕복 검증 때 프리뷰만 full_skins 로 렌더되는 불일치가 버그로 오인된다
- `Assets/_Project/Editor/SpineUpgradeSmoke.cs` — 합성 검증 추가

## 구현

1. `SpineCombinedSkinCache` (static):
   - **캐시 키: `(SkeletonData 인스턴스, comboKey)`** — `ConditionalWeakTable<SkeletonData, Dictionary<string, Skin>>`.
     SkeletonDataAsset 을 키로 쓰면 안 된다: 이 프로젝트는 도메인 리로드 OFF(`EnterPlayModeOptions`)라 static 캐시가 플레이 세션을 넘어 생존하는데, 에디터 리임포트가 `SkeletonDataAsset.Clear()` 후 **새 SkeletonData 인스턴스**를 만들면 stale Skin(파괴된 아틀라스 참조)이 히트된다. 인스턴스 키면 자연 캐시 미스 + GC 회수로 정리 로직 자체가 불필요.
   - `GetOrBuild(SkeletonData, IReadOnlyList<string> parts)` → `new Skin(comboKey)` 에 `FindSkin(part)` 를 `AddSkin` 누적. 없는 경로는 경고 + 스킵.
   - **comboKey 는 문서 순 join, 정렬 금지.** `AddSkin` 은 (slotIndex, placeholder) 단위 교체 병합이라 순서가 결과에 영향 — "순서가 의미를 갖는다(뒤가 슬롯 단위로 덮음)" 가 계약. 같은 집합 다른 순서의 캐시 중복은 상한(유닛 종수)상 무해.
   - 공유 적용 헬퍼 `Apply(Skeleton, ISpineUnitVisualData)`: 파츠 목록 있으면 combined, 없으면 기존 단일 `SpineSkinName` 경로. `SetSkin → SetSlotsToSetupPose` **이후** 슬롯 틴트.
2. 슬롯 색상: `skeleton.FindSlot(name)?.SetColor(color)`. 사망 페이드(`Skeleton.A`)·피격 틴트(R/G/B)와 곱연산 독립 (critic 검증 완료).
   - **제약: 애니메이션이 rgba 를 키잉하는 슬롯은 틴트 불가** — Casual Character 실측으로 `eye` 슬롯이 Idle/Walk/Die/Hit 에서 키잉됨(스폰 틴트가 첫 프레임에 덮임). 눈 색은 `eyes/` 파츠 스킨으로 해결. `beard` 는 setup 색이 비백색(`c2bbb4ff`)이라 틴트가 순수 곱이 아님을 주의.
3. 스모크 확장: 파츠 2~3개 임시 조합으로 합성 어태치먼트 수 검증 + 같은 조합 2회 요청 시 캐시 히트(동일 Skin 인스턴스) 검증.

## 완료 기준

- [ ] 파츠 목록 있는 유닛이 스폰/드래그 프리뷰 **양쪽에서** 조합 외형으로 렌더 (에디터 확인)
- [ ] 빈 목록 유닛은 기존과 동일 (full_skins), SpineUnitView·프리뷰의 기존 중복 스킨 코드 제거됨
- [ ] 없는 스킨 경로는 경고 후 생존 (에러/크래시 없음)
- [ ] 같은 조합 N마리 스폰 시 Skin 합성 1회 (캐시 히트 검증)
- [ ] 배치 스모크 PASS
