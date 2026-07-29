# 14 — 부착 거절 시 아주 짧은 카메라 킥

> 추가 2026-07-30 (사용자 발의). unit 3 의 움찔 피드백에 화면 신호를 한 겹 얹는다.

## 목적

즉발 부착이 거절되면 카드가 좌우로 움찔한다(unit 3). 그런데 **그 카드는 손가락 밑에 있다** —
모바일에서 자기 손에 가린 24px 진폭 셰이크는 놓치기 쉽다. 화면 전체가 한 번 튀면
"안 먹혔다"가 주변시로도 읽힌다. 사유 문구(브리핑)는 읽어야 알지만 킥은 읽지 않아도 안다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — `FeedbackKick`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — `FlinchSlot` 에서 호출

## 구현

`FlinchSlot` 이 거절 피드백의 **단일 소유자**다(움찔 + 거절음). 킥도 여기 얹으면 호출처
2곳(드래그 거절·탭 즉발 거절)이 자동으로 얻고, 끄고 싶을 때 한 곳만 본다.

### 함정 — `Kick()` 은 지금 no-op 이다

기존 `CameraDirector.Kick()` 은 첫 줄이 `if (!config.enableNonDragEffects) return;` 인데
**라이브 에셋이 `enableNonDragEffects: 0`** 이다. 그대로 부르면 아무 일도 일어나지 않는다.

그 토글은 **앰비언트 연출**(브리딩·페이즈 비행·펄스·shake heat) 억제용이다. 거절 킥은
앰비언트가 아니라 **사용자 행동에 대한 응답**이므로 묶이면 안 된다 — 인스펙트 줌이 같은
이유로 이 토글에 묶이지 않는다("명시적 제품 기능이라 묶으면 조용히 죽는다", `CameraDirector`).

그래서 게이트 없는 `FeedbackKick(strength, duration)` 을 신설한다:

- **지속시간은 호출처가 준다** — "얼마나 짧은가"는 그 피드백의 성격이지 카메라의 성질이 아니다.
- **진폭은 config 소유 그대로**(`kickPosAmp`/`kickRotAmp`) — 킥의 물리적 느낌은 한 곳에서 튜닝된다.
- 킥 적용부(`_kickRemaining > 0`)와 `anyActive` 는 원래 게이트 밖이라 손대지 않는다.

### 값

`rejectKickStrength 0.45` · `rejectKickDuration 0.1`(둘 다 뷰의 SerializeField).
카드 움찔(0.26s)보다 **짧게** 잡아 화면이 먼저 튀고 카드가 이어 흔들리는 순서를 만든다.
거슬리면 `rejectKickDuration` 을 0 으로 두면 킥만 꺼진다(`FeedbackKick` 이 `duration <= 0` 가드).

## 완료 기준

- [ ] compile 클린
- [ ] Play: 게이지 부족 / 부착 캡 / 적용 불가 카드를 **탭** → 카드 움찔 + 화면이 짧게 튄다, 무차감
- [ ] Play: 같은 3케이스를 **드래그**로 거절 → 같은 피드백(호출처 2곳 공통)
- [ ] Play: 정상 부착에는 킥이 **없다**(성공과 실패가 구분된다)
- [ ] Play: 연속 거절 연타 시 킥이 누적돼 화면이 요동치지 않는다(타이머 재시작이라 겹치지 않음)
- [ ] Play: `rejectKickDuration = 0` 으로 두면 킥만 사라지고 움찔·사유는 남는다
