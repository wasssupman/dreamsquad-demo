# 3 — 상시 미세 흔들림 (idle ambient)

## 목적

입력이 없어도 손패가 살아있게 한다. 카드들이 index 위상차로 가볍게 상하 bob + 미세 sway →
"숨쉬는" 손패. 터치가 필요 없어 **모바일에서 항상 동작**하는 수동 역동감. unit 0 스프링 위에 얹는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`

## 구현

1. **스프링 target 에 idle 오프셋 합성**(`SpringSlots`): 각 슬롯의 유효 목표 =
   `targetPos + idle(i, t)`, 단 focus/드래그 슬롯은 제외(그 카드는 안정적으로 들려 있어야 함).
   - `ph = t·idleFreq + i·idlePhase` (t = `Time.time`, timeScale=1 고정이라 실시간)
   - `idle = (Sin(ph·0.7)·idleSwayX, Sin(ph)·idleBobY)`
   - 회전에도 아주 미세하게 얹을 수 있음(`targetRotZ + Sin(ph)·idleRot`, 선택).
   - 스프링이 움직이는 target 을 뒤쫓아 부드러운 bob(스냅 아님). 전이(딜/수렴) 중엔 SpringSlots 가
     이미 skip → idle 도 자동 정지.
2. **SpringSlots 를 index 접근 for 루프로**(위상 `i` + `_focusIndex` 비교 필요).
3. **튜닝 SerializeField**: `idleBobY=5f`, `idleSwayX=3f`, `idleFreq=1.6f`, `idlePhase=0.7f`.
   과하면 산만 → 작게(≤6px). 0 이면 완전 정지(비활성 토글 역할).

## 완료 기준

- compile 성공, 콘솔 CS 에러 0.
- Play — 손패가 무입력 상태에서 카드마다 위상 다르게 가볍게 상하로 숨쉬듯 흔들림(과하지 않게).
- 카드를 누르면(focus) 그 카드는 흔들림이 멈추고 안정적으로 들림, 떼면 다시 idle 합류.
- 딜/수렴 중엔 idle 이 관여하지 않음(트윈이 소유).
- `idleBobY=0` 등으로 즉시 끌 수 있음.
