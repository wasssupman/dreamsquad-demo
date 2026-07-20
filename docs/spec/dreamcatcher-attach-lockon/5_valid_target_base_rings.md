# 5 · Valid-Target Base Rings (레이어 B)

## 목적

부착 조준 드래그가 시작되면 **붙일 수 있는 배치 유닛**에 얕은 base-ring 을 점등해 사전 조준을 돕는다(불편 ②의 사전 표시). 부착 상한 도달 유닛은 링 없음 → 유효성이 곧 시각. 소형 가로폰 클러터는 근접 reveal 로 완화.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — **읽기전용** 배치 defender 스크린렉트 열거 API
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — **신규 public** attachable 조회 API
- `DreamcatcherHandView.cs` (또는 리티클 곁) — base-ring 풀 소유
- `DreamcatcherCardDragSlot.cs` — 부착 조준 시작 시 attachable 스냅샷→링 점등, 종료/취소 소등

## 구현

- **bridge 열거(순수 공간 read, 계약 #8)**: `EnumerateDefenderScreenRects(Camera cam, List<(Entity, Rect)> outBuf)` — 배치 defender(`_defenderByTile`) 순회 + `SpineUnitView.TryGetScreenRect`. component write 0, 신규 EntityQuery/Temp 할당 0. **`outBuf` 재사용**(매프레임 `new` 금지).
- **컨트롤러 public API(기술 critic MED)**: 현재 `CountAttachedTo`/`maxAttachPerUnit` 이 private — `bool CanAttachMore(Entity host)`(또는 attachable 집합 산출) public 추가. **attachable 은 드래그 시작 1회 스냅샷**(부착수는 드래그 중 커밋 없어 불변; 매프레임 dict 전수 금지).
- **mode 별 유효성(기술 critic MED)**: Unit/Squad = attach 여유(`CanAttachMore`), **Active-DefenderUnit = attach 아님(셀 캐스트)이라 cap 필터 제외**(전부 유효 or base-ring 비대상), `EnemyMark`/`ActiveTile`/`ActivePortal` = 비적용.
- **base-ring 풀**: attachable 유닛마다 링 UI 쿼드(`baseRingColor/Radius/Thickness`), 얕은 호흡(`baseRingPulseSec`). dim 위·화살표 아래(계약 #3, sibling index 강제). **근접 reveal**: `baseRingRevealRadius`/`baseRingDistanceFade` 로 포인터 근처만 강조(소형폰 클러터 완화). 락온 유닛 링은 `baseRingLockedFade`(리티클과 겹침 회피).
- **성능(계약 #11)**: 락온 pick 과 base-ring 열거가 **프레임당 렉트 집합 1회 산출** 공유. 링 스프라이트 1회 생성·재사용, 공용 material 배칭.

## 완료 기준

- 부착 카드 드래그 시작 시 붙일 수 있는 유닛에 링 점등, **부착 상한 도달 유닛엔 링 없음**. Active-DefenderUnit 은 cap 오필터로 소등되지 않음.
- 락온되면 그 유닛 링이 리티클에 자리 양보(이중 표시 없음). 근접 reveal 동작.
- 종료/취소/`Close`/`ForceClose`/`OnDisable`/`OnPhaseChanged` 에서 전 링 소등(계약 #10, 잔류 없음).
- bridge 추가분 읽기 전용(component write 0). 대량 유닛 보드 프레임 코스트 실측 OK. 콘솔 클린.
