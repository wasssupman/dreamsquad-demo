# 2 — 덱-드로우 딜 (하단 → 아치)

## 목적

손패가 열릴 때 카드가 트레이 **하단 바깥(덱)에서 곡선으로 솟아올라** 아치 부채에 확대·오버슛으로
안착한다. "패를 뽑아 손에 쥐는" 인상. 각성 버튼은 pulse 발광으로 인과만 힌트.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` (버튼 pulse 공개)

## 구현

1. **딜 소스 = 하단 덱**: 각 카드 시작 = `(base.x * clusterK, baseY - dealRise)`(하단 바깥, 살짝 모임
   `clusterK≈0.3`), `scale = dealStartScale`, `rotZ` 약간 흐트러짐(index 결정론).
2. **곡선 상승 안착**: unit 0 스프링을 잠깐 우회하고 PrimeTween `Sequence` 로 카드별 스태거:
   - 위치: 하단→base 로 `Tween.UIAnchoredPosition(..., Ease.OutBack)` (OutBack 이 상승 오버슛=솟구침).
     더 확실한 아치가 필요하면 y 를 base 위로 초과했다 복귀하는 2-스텝 or `Tween.Custom` 곡선.
   - 스케일: `dealStartScale→1` `Ease.OutBack`. 회전: 흐트러짐→base rotZ.
   - **입체감(①)**: 시작 X 틸트(누운 카드)→0(`dealTiltX≈50`), 안착에 미세 초과 복귀. **flex(②-B)**: 안착
     순간 `scaleY` squash→stretch 오버슛 1회(4버텍스로 충분, 진짜 커브는 후속 spec).
3. **트레이 backing 페이드**: 카드 상승 시작과 함께 backing 알파 0→기본(딜 무대). (unit 0 이전 구현 유지 가능.)
4. **버튼 pulse**: `AwakeningGaugeView.Pulse()` 공개(패널 스케일 punch + fill 순간 발광, unscaled). `Open` 에서 1회 호출.
5. **전이 종료**: 시퀀스 완료 시 슬롯 target=base 로 넘겨 스프링/호버가 이어받음. `_flip`/`_dealSeq` 정리.
6. **튜닝 SerializeField**: `dealRise=220f`, `dealStaggerSec=0.05f`, `dealDurationSec=0.34f`, `dealStartScale=0.62f`,
   `dealTiltX=50f`, `clusterK=0.3f`.

## 완료 기준

- compile 성공, 콘솔 CS 에러 0.
- Play(포커스) — 각성 클릭 시 버튼이 pulse, 카드가 **하단에서 솟아올라** 좌→우 스태거로 아치에 오버슛 안착.
  이전 "UI 버튼→평면 행" 느낌이 사라지고 "패를 뽑는" 카드감.
- 안착 후 스프링/호버로 자연 전환(딜 끝 튐 없음). 딜 중 드래그 불가, 완료 후 정상.
- 강제 클로즈/페이즈 이탈 시 트윈 잔류·late-land 0.
- 채택 flex(②-B squash) 및 ①/③ 이관 결정 handoff 1줄 기록.
