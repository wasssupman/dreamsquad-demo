# 6 — handoff summary

## Commit

- `b556129d` unit 0 — footprint 데이터 레이어 (W×H 필드·시트 계약·`FootprintMath`)
- `7febe93f` unit 1 — 브리지 점유 모델·판정 확장 (대표 셀 키 + owner 맵 + per-tile 사유)
- `77926a92` unit 2 — 드래그 Ghost UX (4색·자석·하단 중앙 앵커·뷰 기하 중심)
- `41c0f94c` unit 3 — 셀 소비자 규약 (시너지 인접 = footprint 둘레 접촉)
- `4f8783e4` unit 4 — 선택·부착 픽 재설계 (앞면 우선·패딩·자석·히스테리시스 구멍)
- `49901a9e` unit 5 — 배치·부착 취소 유예 (연출 구간 = 유예)
- `b03adc81` 투트랙 리뷰 반영 (code-reviewer + ecs-reviewer, 양측 REQUEST CHANGES → 전건 반영)
- `99a71cd2` unit 2 rev — 사용자 피드백 3건 (배치불가 **보드 전역** 표시·자석 기본 비활성·footprint 저작 4종)
- `969540be` 버스터즈 3×2 → 2×3 정정
- `12b59010` rev 2 — 사거리 링 양보·부착 자석 유효 필터·락온 스케일 펀치
- `060a8524` rev 3 — 탭 선택에서 자석 제거 (비판적 재검토: 탭은 즉시 커밋이라 자기 교정 루프 없음)
- `a1795f0e`+`d039611a` unit 7 — armed 보드 드래그 실루엣 (+리뷰 반영)
- `68cefb16` unit 8 — D&D 키링 은퇴, 보드 실루엣으로 통일
- `66d4d6f6` unit 9 — 드롭 비행 복구(출발점=트레이) + 되돌리기 버튼 은퇴
- `eb2386ad` footprint 저작 철회 — 전 유닛 1×1 (사용자 결정)

## Implemented

- `DefenderUnitData.footprintWidth/Height`(기본 1) + 시트 리플렉션 계약 자동 편입. `FootprintMath` 가 앵커(min 코너)·대표 셀(홀수 중앙·짝수 floor)·하단 중앙 손끝 규약·기하 중심 오프셋·rect 체비셰프 거리를 단독 소유.
- 브리지 점유 = `_occupiedTiles`(전 칸) + `_defenderByTile`(대표 셀 키·유닛당 1엔트리) + `_defenderCellOwner`(셀→대표 셀). 등록/해제는 `Occupy/ReleaseDefenderFootprint` 2함수만. 셀-키 공개 API 는 `TryResolveDefenderKey` 로 footprint 투명.
- `SpatialFootprintCheck`(per-tile 사유, Occupied>NotBuildable>OutOfBounds) — 셀 규칙은 기존 `SpatialPlacementCheck` 재사용. UI seam = `GetPlacementCellReasons`.
- 드래그 3경로(트레이 D&D·탭·armed) 공통: 손가락 셀(기존 히스테리시스) → 하단 중앙 앵커 → 공간 무효 시 자석(`TryFindNearestPlaceableAnchor`, 결정론) → 고스트 4색 + 컨텍스트(점유 노랑/지형 무채색). 배치가능 전체 하이라이트는 `PlaceableAreaHighlightEnabled=false` 스위치 은퇴.
- 뷰만 footprint 기하 중심(짝수 변 +0.5칸): sync·RestViewPos·비행 앵커. sim 위치·`DefenderTile.cell` = 대표 셀 불변(**sim 무변** — `Scripts/Battle/` 변경 0).
- 시너지 인접 = 두 footprint rect 거리 1(1×1 = 기존 8이웃 동치), 전수 재계산·등록 스냅샷 rect.
- 픽킹: 포함 후보 앞면(렌더 순서) 우선 + 패딩·자석(`DreamcatcherFocusConfig` 노브, 선택·부착 공유) + 락온 히스테리시스 거리 일원화.
- **드래그 중 유닛 그림 = 보드 실루엣**(units 7~9): 손끝 키링 대신 footprint 뷰 중심에 서는 반투명
  Spine 이 두 제스처(탭-드래그·트레이 D&D) 공통으로 따라온다. 릴리즈하면 **트레이에서** 하마가
  날아와 착지한다(키링을 걷은 뒤 유닛이 있는 곳은 손이 아니라 트레이라서). 스위치 2개로 왕복 가능
  (`armedSilhouetteEnabled` / `dndSilhouetteEnabled`).
- **footprint 실값은 전 유닛 1×1**(2026-08-30 사용자 결정). multi-cell 시스템은 그대로 살아 있고
  SO 값만 1 이다 — 되켜는 데 코드 변경 0.
- 취소 유예: 배치 = `PendingDeployment` 중 «되돌리기» 버튼(**현재 기본 off** — unit 9 rev) → `TryCancelPendingDeployment`(점유·코스트·쿨다운·카드 회수·엔티티 수거, `_cancellableDeployments` 자격 셋). 부착 = 커밋을 흡수 도착 프레임으로 이연(`FlyDeferred`), 비행 중 고스트 탭 = 취소.

## Key Files

- `Assets/_Project/Scripts/Data/FootprintMath.cs` · `DefenderUnitData.cs` · `DragSwaySettings.cs`(⑬⑭)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`(점유·판정·픽·취소) · `BattleBridge.Relocation.cs`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`(앵커·고스트·되돌리기 버튼) · `DefenderRelocationController.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`·`DreamcatcherCardDragSlot.cs`·`CardAbsorbFlightPresenter.cs`(지연 커밋) · `DcInspectController.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`(취소 회수) · `Core/PlacementCooldownRuntime.cs`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs`(고스트 레이어) · 테스트 2종 `FootprintMathTests`/`FootprintPlacementCheckTests`

## Verified

- 컴파일 에러 0 · EditMode 코어 lane **2494 전건 실패 0**(스킵 3 = 선행 Ignore) — 각 unit 커밋마다 + 리뷰 반영 후 재확인.
- 투트랙 리뷰(code-reviewer + ecs-reviewer, opus): 양측 REQUEST CHANGES → C-1(카드 소실)·H-1×2·H-2·M-1·M-2·M-5·M-6·L-2·L-5·L-6 코드 반영, M-3·M-4 문서 정정, 나머지 M/L 은 README 후속 후보 등재. ecs 축(sim 무변·게이트웨이·structural change 타이밍·dangling)은 전부 클린 판정.
- **육안 Play 확인 완료**: 2026-08-28 units 2·4·5(multi-cell 4종 저작 상태에서 고스트 4색·전역 불가
  표시·앞면 픽·되돌리기 버튼·부착 비행 탭 취소) · 2026-08-30 units 7~9(드래그 실루엣·트레이 발
  드롭 비행·되돌리기 버튼 미노출). 모두 사용자 확인.
- **`DropDismountTest`(PlayMode)는 red — 이 spec 원인 아님.** 이분 확인(실루엣을 꺼도 동일 실패).
  독립 원인 3겹 중 둘은 이번에 고쳤고(고정 화면좌표 vs 카메라 팬 · 뷰 오버라이드 튜플화),
  남은 하나(셀↔화면 roundtrip 불성립)는 좌표계 쪽으로 보인다 — 상세·다음 시작점은
  `8_dnd_silhouette.md` 의 «선행 red 조사 기록».

## Notes (되돌리면 안 되는 의도)

- `_defenderByTile` 은 **유닛당 1엔트리(대표 셀 키)** — «엔트리 수 = 기수» 소비자(DeployedCountOf·뷰 동기)가 이 위에 서 있다. 셀→유닛 해석은 owner 맵 경유만.
- 점유 해제·시너지·재배치 from-rect 는 **등록 스냅샷**(owner 맵) 기준 — SO 재독으로 되돌리면 시트 임포트 드리프트 시 유령 점유가 재발한다.
- 효과 타일 정확 일치 조회 = «대표 셀만 발동» 규약(무변경이 곧 구현).
- `TryCancelPendingDeployment` 의 자격 셋 가드를 `PendingDeployment` 존재 검사로 되돌리지 말 것 — 재배치 비행이 같은 컴포넌트를 쓴다.
- OnUndoPressed 에서 `_activeDismounts` 키를 직접 지우지 말 것 — 코루틴의 «바인딩 붕괴» 분기가 잔류물·오버라이드를 걷는다.
- 부착 유예는 **되감기가 아니라 커밋 이연** — handle==0(엔티티 부착형) 카드에 revoke API 가 없어 되감기 방식은 성립하지 않는다.
- BattleBridge 일부 블록의 CRLF→LF 정규화가 diff 에 섞였다(리뷰 L-1) — 의도적으로 LF 로 수렴시켰고 재-CRLF 화는 하지 않는다.
- 이 spec 의 커밋 전량(b556129d~)은 **미푸시** — push 는 사용자 승인제.

## Follow-up

- ~~육안 Play 확인~~ **완료**(2026-08-28 · 08-30). Android 실기기 체감(픽 패딩·자석 반경)은 README 후속 후보로 잔존.
- **`DropDismountTest` 3번째 원인 규명** — 위 Verified 참조. 이 spec 밖(좌표계/디오라마 스테이지 의심).
- **제스처 시작점 통합**·**철수의 환급 유예** — `8_dnd_silhouette.md` 후속 후보 참조.
- README 후속 후보 참조: 거리 기반 판정 전환 · 적 통행 차단 · 사거리 다중 셀 · 보조 타일 경계 · 튜토리얼 하이라이트 · SO 재독 잔여 · 부착 유예 실기기 튜닝.
- 시트 실컬럼(footprintWidth/Height) 추가는 콘텐츠 저작 시점 작업(부재 시 SO 값 유지가 계약이라 안전).
