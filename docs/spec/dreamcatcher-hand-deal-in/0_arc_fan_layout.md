# 0 — 아치 부채 레이아웃 + 스프링 추종

## 목적

손패를 평면 UI 행에서 **포물선 아치 부채**(겹침 + 접선 회전)로 바꾸고, 슬롯을 정적 위치가
아니라 **목표값 + 매프레임 스프링** 모델로 전환한다. 카드감의 토대. (호버/딜은 unit 1·2.)

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`

## 구현

1. **아치 기하** (`EnsureSlots` 의 위치 계산 교체). 슬롯 i, 개수 N:
   - `t = N==1 ? 0 : (i/(N-1))*2 - 1`  (−1..+1)
   - `x = -((N-1)*step)/2 + i*step`, `step = cardW - overlap`(겹침: step<cardW)
   - `y = baseY + arcHeight*(1 - t*t)`  (가운데 솟음)
   - `rotZ = -t * rotMax`  (접선; 왼쪽 top-left, 오른쪽 top-right)
   - 이 값을 슬롯 **base**(`homePos`/`homeRotZ` 재활용 = 아치 rest)로 저장.
2. **스프링 모델**: 슬롯에 `targetPos`(Vector2)·`targetRotZ`·`targetScale` 추가. 기본 target = base, scale 1.
   `Update` 에서 State==Hand 이고 전이/드래그 아닌 슬롯만
   `rect.anchoredPosition = Vector2.Lerp(cur, targetPos, 1-Mathf.Exp(-springK*Time.deltaTime))`
   (rotZ 는 `Mathf.LerpAngle`, scale 동일). `springK ≈ 14`. 드래그/포탈-조준 슬롯은 skip(DragSlot 소유).
3. **z-order**: base sibling = i(오른쪽이 위). 호버 최상단은 unit 1.
4. **RestoreSlotHome 조정**: 즉시 스냅 대신 target=base 로 설정(스프링이 복귀). 단 드래그 취소 직후
   깜빡임 방지가 필요하면 위치만 즉시 base 로 스냅 후 target=base(현 계약 유지 범위에서 선택).
5. **튜닝 SerializeField**: `cardOverlap=54f`, `arcHeight=46f`, `rotMax=10f`, `baseY=16f`, `springK=14f`.
   기존 `fanAngle` 은 `rotMax` 로 대체(제거 또는 별칭).

## 완료 기준

- compile 성공, 콘솔 CS 에러 0.
- Play — 손패가 가운데 솟은 겹친 아치로 보이고, 바깥 카드가 접선으로 기울어짐(평면 행 아님).
- 카드를 코드로 밀어보면(또는 딜 전 임시) 목표로 부드럽게 스프링 복귀(스냅 아님).
- 드래그/포탈-조준 중 카드는 스프링과 안 싸움(DragSlot 이동 정상, 취소 시 아치로 복귀).
