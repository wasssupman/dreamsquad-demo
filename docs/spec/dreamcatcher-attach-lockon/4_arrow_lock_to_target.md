# 4 · Arrow Lock-to-Target + 시인성

## 목적

화살표 코어 2건을 손본다 — ① **Defender 락온 시** 끝점을 유닛 중심으로 ~0.7 당겨 선이 유닛에서 끝나게(불편 ② 보강), ② 아웃라인/글로우/최소알파로 **선 자체 시인성** 상향(불편 ① 잔여). 제스처 골격·베지어 아치는 유지.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherTargetArrow.cs` — 끝점 블렌드 인자 + 아웃라인/글로우/알파
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `UpdateDragVisual`에서 Defender 락온 유닛 중심 전달

## 구현

- **끝점 당김(계약 #5)**: `SetPath` 에 락온 유닛 중심(nullable screen 좌표) 인자 추가(오버로드/optional, append-only). 지정 시 `end = Lerp(pointer, unitCenter, focusConfig.arrowLockBlend)`(0.7). 무지정이면 기존 `end = pointer`. 베지어 컨트롤 포인트(위로 볼록)는 그대로.
- **mode gate (기술 critic MED — EnemyMark 누수 차단)**: `UpdateDragVisual` 은 Defender·EnemyMark 양쪽에서 실행되고 `_hoverEntity` 는 EnemyMark 에선 **적 엔티티**다. 끝점 당김은 **Defender 락온(계약 #4의 단일 defender entity)에만** 적용 — EnemyMark/ActiveTile/ActivePortal 은 유닛 중심 인자 없이 호출(pointer raw 유지).
- **시인성**: 각 대시 쿼드 뒤 어두운 **아웃라인 쿼드**(살짝 큰, `arrowOutlineColor`/`Width`) + 소프트 **글로우**(`arrowGlowColor`). tail 최소 알파 `arrowMinAlpha`(기존 0.45 → SO). 머리 다이아몬드 동일.

## 완료 기준

- Defender 락온 시 **선이 유닛에서 끝남**, 무락온·EnemyMark 는 pointer 추종(기존 거동 회귀 없음, 적으로 끌려가지 않음).
- 밝은 배경·밀집 VFX 위에서도 선/머리 읽힘(아웃라인 대비). 무효색(반투명 흰)도 dim+아웃라인으로 식별.
- 콘솔 클린. 검증은 밀집 배경 오프스크린/Play 스크린샷.
