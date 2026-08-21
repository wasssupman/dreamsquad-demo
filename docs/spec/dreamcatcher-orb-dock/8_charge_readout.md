# 8. 회차 판독면 — 평소엔 멈추고, 한 회분이 오를 때만 터진다

## 목적

항아리 탭이 꺼지면서(`JarTapEnabled = false`, 2026-08-19) 이 독은 **입력을 유도하지 않는
수치 판독면**이 됐다. 그런데 시각 어휘는 아직 «눌러달라»고 말하고 있다 — 소나 펄스 링,
주기적 통통 바운스, 중앙 넛지, 림 브리딩, 피규어 홉, 그리고 피규어 더미의 주기적 튕김.
누를 수 없는 대상이 계속 손짓하는 상태라 노이즈만 남는다.

동시에 이 독이 실제로 전달해야 하는 정보가 하나 더 있다: 코스트가 전부 20 이고 상한이
100 이라 **드림캐쳐는 최대 5회가 쌓인다**. 「지금 한 장 더 낼 수 있게 됐다」는 사건은
플레이어의 행동을 바꾸는데, 지금은 1점만 올라도 숫자가 튀어서 그 사건이 잡음에 묻힌다.

그래서: **평소에는 완전히 정지**하고, **각성치가 한 회분 경계를 넘는 그 순간에만** 짧게
터진다. 회차를 칸으로 그리지는 않는다(사용자 결정) — 선을 긋는 대신 사건으로만 말한다.
화폐 단위도 점(0~100) 그대로 둔다. 회 단위로 정규화하면 카드 비용 라벨·`+N` 플로팅·
스쿼드 상세의 각성 보상 표시가 서로 다른 단위로 말하게 되고, 킬 보상이 2~5점이라
대여섯 번의 킬 동안 숫자가 멈춰 정보량이 오히려 준다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningCharge.cs` (신규, 순수 계산)
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/JarFigurePile.cs`
- `Assets/_Project/Tests/EditMode/AwakeningChargeTests.cs` (신규)

## 구현

**한 회분 = 가장 싼 카드 코스트.** `AwakeningCharge.UnitCost(costSquad, costUnit, costActive)`
가 `AwakeningConfig` 에서 뽑고(0/음수는 «비용 없음» 이라 무시), `CountOf(gauge, unit)` 이
회수를 낸다. 하드코딩 없음 — 코스트가 다시 15/20/30 으로 갈라져도 값이 따라간다. ready 림
임계도 같은 값을 쓴다: 「쓸 수 있게 된 순간」은 하나여야 한다.

**연출 트리거를 값 변화에서 회차 상승으로 옮긴다.** `Refresh` 는 이제 킬마다 숫자를
조용히 갱신하고, `CountOf(current) > CountOf(previous)` 인 프레임에만 `ChargeBurstRoutine`
을 돌린다. 0.3초 안에 숫자 punch + 독 미세 팝(1.08, 오버슛 없음) + 림 골드 플래시 +
피규어 1회 들썩(`Hop`)을 끝내고 평소 상태로 복귀한다. 두 회분을 한 번에 넘겨도 한방으로
합친다. 전투 진입 시 최초 표시(`gaugeStart`)는 사건이 아니라 상태라 터뜨리지 않는다.

**상시 어휘를 전량 걷는다.** `AttentionRoutine` 일체(펄스 링 2개·바운스·넛지·림 브리딩·
홉 루프)와 `attentionPeriod`/`attentionLean`, `JarFigurePile` 의 주기적 튕김(`JostleAll`
타이머)을 제거한다. `Hop` 은 살려서 회차 획득 사건에 붙이고, 임펄스 세기는 씬이 들고 있는
authored 값을 `FormerlySerializedAs` 로 승계한다(`figureJostleStrength` → `figureHopStrength`).
코스트 눈금(`—— 20` 한 줄)도 걷는다 — 회차를 선으로 안 그리기로 한 이상 그 선만 혼자
남으면 어중간하고, 「지금 쓸 수 있다」는 이미 골드 림이 말한다.

**유지**: 흡수 비행(킬마다), `+N`/`-N` 플로팅, 오버플로우 경고(좌우 흔들림만 제거하고 림
플래시는 유지 — 손실 회피), 항아리 탭 킬 스위치, 클래스명·씬 배선.

**알려진 한계**: 회차 연출은 게이지가 바뀌는 프레임에 터지고, 그 킬의 피규어는 0.44초
뒤에 착지한다. 숫자와 연출은 같은 시점이라 어긋나 보이지 않지만, 채움 높이는 한 박자 늦게
따라온다. 채움을 기준으로 터뜨리려면 어느 비행이 경계를 넘겼는지 추적해야 해서 두지 않았다.

## 완료 기준

- [x] 컴파일 통과 · 콘솔 에러 0 (2026-08-21)
- [x] EditMode `AwakeningChargeTests` 5/5 초록 (2026-08-21, 전체 2355개 중 2354 통과 —
      유일한 실패 `CameraComposeMathTests.ShakeWeight_...` 는 병행 세션의 미커밋
      camera-direction unit 16 WIP 소유, 이 변경과 무관) — 특히 `ChargeBurst_FiresOnlyWhenCountRises`
      (킬 단위 획득으로 경계를 넘기 전엔 조용, 넘는 한 번만, 두 회분 동시 상승도 1회,
      소비/리셋 하강은 무발화)
- [ ] **사용자 확인 대기** — BattleScene Play 육안: 전투 중 아무 사건이 없을 때 독이 **완전히 정지**한다
      (링·바운스·피규어 튕김 없음). 각성치가 20 배수를 넘는 순간에만 짧게 터지고 즉시
      평소 상태로 돌아온다.
