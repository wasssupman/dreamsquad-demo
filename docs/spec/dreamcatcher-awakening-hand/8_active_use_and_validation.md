# 8 — Active 카드 필드 사용 + SkillBar dormant + 전체 Play e2e

## 목적

Active(공용) 카드의 스와이프 사용을 완성하고 구 SkillBar 를 은퇴시킨다. feature 전체 e2e 로 검증 질문에 답한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` / `DreamcatcherHandView.cs` (Active 분기)
- 씬: `SkillBar` 오브젝트 비활성 (dormant)

## 구현

1. **Active 타겟팅** (SkillBar 의 조준 로직을 카드 드래그로 번역 — 셀 변환·범위 프리뷰 재사용):
   - `TilePoint` 스킬: 드래그 중 포인터 셀에 **범위 프리뷰**(기존 `SetSkillAimRange`/`ClearSkillAimRange` 재사용) → 필드 touchup → pending → 커밋 `CommitActive` → `CastSkillAtTile`.
   - `DefenderUnit` 스킬: Unit 카드와 동일하게 유닛 하이라이트 → touchup → pending → `CastSkillOnDefender`.
   - **Portal(2탭)**: touchup(입구 셀) → pending 없이 **2탭 모드 진입**(라벨 "출구 타일 선택", SkillBar 의 상태기계 이식) → 두 번째 탭(출구) → pending → `CastPortal`. 취소 = ESC/손패 영역 탭.
   - 캐스트 실패(false) = 무차감·손패 복귀.
2. **비용/쿨다운**: `costActive` 만 차감. `SkillRuntime`/`CostRuntime` 미호출(계약 7). **BattleBridge 의 `skillRuntime` SerializeField 배선을 씬에서 해제**(critic C1 — 캐스트 3종 내부의 `skillRuntime?.IsReady` 게이트/`Consume` 이 no-op 이 되어 순환 재사용이 쿨다운에 막히지 않는다. SkillBar 도 dormant 라 다른 소비자 없음). 커밋 성공 시 큐 맨 뒤(`UseAndRecycle`) + 자동 복귀.
3. **SkillBar dormant**: 씬에서 SkillBar 오브젝트/컴포넌트 비활성(코드·에셋 유지). **IsAiming 수명주기 이식(critic M1)**: 카드 조준(Active 드래그)/Portal 2탭 진입 시 `GameManager.IsAiming = true`, 커밋·취소·phase 클로즈 시 `false` — `PlacementInput` 의 기존 IsAiming 게이팅과 배타 유지. 카드 드래그 시작 시 배치 선택 해제(last-pressed-wins 관례).
4. **로그**: Active 사용을 기존 스킬 로그 채널에 이어 기록(가능하면) — 아니면 handoff Follow-up.

## e2e 검증 시나리오 (에디터 Play)

1. 매치 시작 → 게이지 0, 손패 5장(큐 = 부착 10 + Active ≤2, 시드 셔플 — 재시작 시 동일 순서).
2. 적 처치로 게이지 상승 → Unit 카드(15) 부착 → 차감·순환·자동 복귀. 같은 유닛 4번째 부착 거절.
3. Squad 카드(30) 필드 touchup → 축 버프 + 큐 맨 뒤.
4. Active 카드(20): Meteor 계열 = 타일 지정 착탄, Portal = 입구/출구 2탭 — 효과 발동 + 큐 맨 뒤 + 쿨다운 없이 순환 재등장 시 재사용 가능.
5. 손패 열림 중 슬로모 체감. 드래그 중 손패 영역 복귀/ESC = 취소(무차감). touchup = 즉시 적용(rev 4 — pending 없음). 유닛 타겟 드래그 중 호버 유닛 하이라이트가 유닛 위로 보임.
6. 부착 유닛 사망 → 카드 큐 맨 뒤 복귀 + 각성 +4. (선택) Unit 카드 전량 부착 → 손패 축소.
7. **손패 열린 채(및 Portal 2탭 중) 매치 종료** → 강제 클로즈, 슬로모 lease 누수 없음(다음 매치 정상 속도), pending 무차감 (critic H2).
8. 무회귀: 구 3중1 모달·SkillBar 미출현, 유닛 드래그 배치·전투·기존 카드 효과·코스트(유닛 배치용) 정상.

## 완료 기준

- [ ] 위 시나리오 1~5, 7~8 전부 통과 + 6 의 회수 확인 (사용자 육안).
- [ ] 콘솔 에러/워닝 0.
- [ ] Android 실기기 터치 확인은 후속(에디터 마우스 기준 완료 인정 — handoff Follow-up 에 기재).

> 확인 2026-07-10 — 커밋 a6b9dd2d (skillRuntime 배선 해제 · SkillBar dormant) · e2e 사용자 종합 확인 "플레이 감각 좋음" · 데이터 동기화 e8acc531/c56a20d2/8dd6e621 · 콕콕바늘 발사 로그 검증(세션 033157, 20뎀×3)
