# 2. Choreography — 7 스테이지 시퀀스 재작성

## 목적

`GiftPhaseView.PlayGiftSequence()` 를 "횡 12장 축 이동"에서 그리드 딜-인 → 선물 리빌 → 스택 → 리플 → 부채꼴 → 순차 흡수의 카드다운 안무로 재작성한다. 총 시간 ≤ 6초(README 계약 8).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` (시퀀스 전면 재작성)
- `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` + `GiftConfig_Default.asset` (스테이지별 타이밍/기하)

## 구현

전 좌표는 `GiftPhaseLayout`(unit 0), 위젯은 `GiftCardWidget`(unit 1). 모든 트윈은 `_seq` 멤버(unscaled). 스테이지:

1. **인트로 (0.6s, 딜-인과 겹침)** — 타이틀 펀치 인/아웃. 아웃 시작과 동시에 딜-인 개시.
2. **딜-인 (1.2s)** — 내 덱 10장이 좌하단 화면 밖 덱 위치에서 한 장씩 스태거로 `GridSlot` 착지. 비행 중 회전(딜러 스핀), OutBack 랜딩 + 미세 틸트 정착. pre-shuffle 슬롯 k = entryId k 매핑 유지.
3. **선물 리빌 (1.0s)** — 선물 2장이 그리드 위 센터에서 스케일 0→1.6 펀치 등장(홀로 프레임 켜짐, 배경 딤 펄스, `vibrateOnSpecialReveal` 유지) → 1.0 으로 축소. 그리드(10칸)에는 끼우지 않고 센터에 떠 있다가 다음 스테이지의 스택 수렴에 함께 빨려든다.
4. **스택 수렴 (0.5s)** — 12장이 중앙으로 수렴, `StackJitter(k)` 회전/오프셋 적층. sorting 은 자식 순서로 제어.
5. **리플 셔플 (0.9s)** — 좌/우 두 뭉치로 분리(바깥 틸트 ±12°) → `RiffleOrder` 순서로 중앙 재적층(카드당 ~0.04s 지퍼 스태거, InOutQuad 짧은 호).
6. **부채꼴 딜 (0.7s)** — 스택이 하단으로 슬라이드하며 `FanSlot(f)` 로 좌→우 전개. f = `GiftFinalOrder()` 인덱스(entryId→f 매핑, 기존 finalPos 딕셔너리 재사용). 프레임 카드가 부채꼴 안에서 금/적으로 드러남.
7. **순차 흡수 (1.2s)** — f=0 부터 `AbsorbDelay(i)` 가속 케이던스로 `FlyTarget` 을 향해 미니 아치(솟구침 소량 + InBack 수렴 + scale→0). 카드마다 타깃 지점 임팩트 틱(플래시 스프라이트 팝, 풀링·시퀀스 정리 대상). 완료 → `ProceedToPlacement()`.

기타:
- 기존 계약 유지: `fastForwardInTestMode` 스킵, `OnPhaseChanged` 이탈 시 `StopSequence()`+패널 숨김, 이펙트 UI 는 중단 경로에서도 파괴.
- 구 필드 정리: `CardW/CardH/CardSpacing/PreX/FinalX` 상수 제거, GiftConfig 로 대체. 폐기 타이밍 필드(`holdSec` 등)는 asset 에서 제거하지 말고 [Obsolete] 없이 그냥 삭제 — 컴파일이 소비처 부재를 보증.

## 완료 기준

- Unity 컴파일 클린 + 기존 EditMode 전체 무회귀.
- 비포커스 execute_code 스모크: Gift 진입 → 시퀀스 완주 → `ProceedToPlacement` 도달, finalOrder==부채꼴 순서 로그 일치, 런타임 에러/PrimeTween ignored callback 0.
- `GiftConfig_Default.asset` 기본값 합산 총 시간 ≤ 6.0s (수치 명시 검증).
- 재시작/페이즈 강제 이탈 시 leak 없음(이펙트 잔존/NRE 0).
