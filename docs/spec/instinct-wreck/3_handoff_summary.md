# instinct-wreck — 인계 요약

## Commit

- `3400ca9e` — feat(instinct-wreck): units 0~2 — 부서진 본능이 잔해로 남는다.
  코드 파일 하나(`StructureWreckView.cs`)가 세 unit 의 내용을 함께 담아 파일 단위로
  쪼갤 수 없어 한 커밋으로 묶었다.

## Implemented

- 붕괴가 프랍에 닿는다 — `BattleBridge.SyncGoalStability` 의 else 분기가 셀로 잔해 프리젠터를
  찾아 `Collapse()` 를 한 번 부른다. **신규 ECS 컴포넌트·큐·시스템 0.**
- 그을림(MPB `_BaseColor`/`_Color`) + 주저앉음(0.72배, 0.25초 ease-out) + 포신 조준 정지.
- 포신이 떨어져 구르고 바깥으로 눕는다. 방향은 **포신이 마지막으로 겨눈 쪽** — 난수 없이
  프랍마다 다른 그림이 나온다. 물리(RB/Collider) 미사용, 결정론 아치.
- 파괴 직후 연기 버스트(1회, `stopAction = Destroy`) + 파괴 이후 잔불 연기(루프, 프랍 수명).
  둘 다 프랍 프리팹 안의 **비활성 자식**이고 코드는 `SetActive(true)` 한 줄만 갖는다.
- 잔해가 되어도 편이 읽힌다 — 그을림 0.45 에서 아군(호박빛)·적(붉음)이 갈린다.

## Key Files

- `Assets/_Project/Scripts/Presentation/StructureWreckView.cs` — 잔해 프리젠터 전부
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_structureWrecksByCell` · `SpawnStructureViews`
  등록 · `ClearStructureViews` 비움 · `SyncGoalStability` 붕괴 분기
- `Assets/_Project/Prefabs/Structures/Instinct_{Ally,Enemy}.prefab` — 잔해 리그 저작
- `Assets/_Project/VFX/InstinctWreck_{Burst,Smolder}.prefab` — `VFX_Smoke` 사본 2벌
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — `StructureWreckOrder = -2`(대역 문서)

## Verified

- 컴파일 0 · 콘솔 신규 에러/경고 0
- EditMode 2 lane 2,543개 — **내 diff 로 추적되는 실패 0**
- 육안은 **오프스크린 렌더**(에디터 비포커스로 Play sim 이 안 돌아서). 수치도 같이 확인:
  `brokenScale 0.29 = viewScale 0.4 × settle 0.72` · `barrelWorldY 0.020 = 지면 + groundLift`
- **미검증**: 라이브 Play 체감. 특히 연기 정렬(−2, 유닛 아래)이 유닛이 오가는 실제 판에서
  맞는지. 오프스크린 flat 배경은 실제보다 관대하다.

## Notes — 되돌리면 안 되는 것

1. **`StructureTurretView` 를 끄는 한 줄**(`turret.enabled = false`). 안 끄면 그 컴포넌트가 매
   `Update` 에 `barrel.rotation` 을 되써서 떨어지는 포신이 «누운 채 조준하는» 상태가 된다.
2. **잔해 컨테이너를 `_structureViews` 에 넣는 것.** `OnDestroy` 로 직접 지우는 형태로 바꾸면
   씬 언로드 시점의 fake-null 레이스를 탄다 — 이 파일에 `retireFlight?.CancelAll()` 이 정확히
   그렇게 터져 정리 전체가 중단된 실측 사고가 있다(`BattleBridge.cs:648-658`).
   컨테이너가 `OnDisable` 로 자기를 치우는 안도 안 된다(브리지 자식 트리는 아무도 비활성화하지
   않아 `OnDisable` 이 안 불린다).
3. **연기 크기를 벤더 기본값으로 되돌리지 말 것.** `A_Smoke_2` 는 퍼프 한 장이 아니라 «퍼프
   한 무더기»의 3×3 flipbook 이라 **작게 여러 개 뿌리면 돌조각으로 읽힌다.**
4. **`gravityModifier` 는 배율(×9.81)이다.** 잔불을 `-0.2` 로 올리면 초당 2m 로 솟아 연기가
   잔해를 떠나 하늘로 간다.
5. **잔불 `startSpeed` 를 올리지 말 것.** 점 스피어 방사 속도의 수평 성분이 연기를 옆으로
   흘린다 — 상승은 gravity 가 갖는다.
6. **매치 종료 프레임의 구멍은 의도적 수용**이다(`_resultShown` 조기 리턴). 고치려면 종료
   판정과 붕괴 관측의 순서를 건드려야 한다.

## Follow-up

- **라이브 Play 체감**(위 Verified 참조) — 이 spec 을 닫으려면 이것 하나가 남았다.
- 적 마음·방어 마음의 잔해 [S] — 붕괴 분기는 이미 공유한다. 그 프랍에 컴포넌트를 붙이는
  저작만 남는다.
- 정식 파괴 아트로 교체 [S] — 계약 4 덕분에 코드 0(프리팹 슬롯).
- ⚠ **이 spec 과 무관한 stale 을 하나 발견했다**(별도 작업 필요). 드림캐쳐 카드
  `Card_BouncyBead` 의 이름이 **디스크·HEAD 는 `튕구슬`, 시트·Unity 메모리는 `바운스샷`** 이고
  **시트가 정본이다**(사용자 확인 2026-08-21). 즉 디스크 SO 와 `DreamcatcherCardNameTests`
  양쪽이 옛 이름에 묶여 있어 Assets lane 이 빨갰다. **이 spec 의 커밋에는 넣지 않고 별도로
  처리했다** — 52장 중 어긋난 것이 그 1장뿐이라 임포터 전체 실행 없이 해당 에셋만 기록했다.
  같이 빨갰던 `CardAssets_UseStructuredSummaryWhenDataExists`(boomerang)는 그 뒤 재현되지
  않는다(손대지 않았다 — 같은 상태 의존 문제였던 듯). Assets lane 에 남은 실패는
  `UnitKitCatalogTests`(말파이트 설명 30자) 하나다.
