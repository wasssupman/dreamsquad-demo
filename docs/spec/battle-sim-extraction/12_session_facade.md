# 12 — `IMatchSession` 파사드 + `LegacyMatchSessionAdapter`

## 목적

스왑 반경을 "세션 구현체 교체 **1곳**"으로 줄인다(ADR D4·설계 정본 M1-3). 파사드를 **구 ECS sim 위에**
먼저 얹어, 소비자 재배선(unit 13)을 구 sim 에서 완료·머지한 뒤 신 sim 을 붙인다. 이 unit 자체는
**소비자를 하나도 바꾸지 않는다** — 새 경로가 존재하지만 아직 아무도 쓰지 않는 상태로 끝난다.

> **제약 8 긴장 명시**: "인터페이스는 구현체 2개 이상일 때만." 이 unit 시점의 구현체는 1개
> (`LegacyMatchSessionAdapter`)다. 예외 근거는 README 이행표 — 제약 1(BattleBridge 유일 창구)의
> **후계 불변식이 이 파사드**이고, 구현 4종(Local/Remote/Replay/Ghost)이 설계 정본 §1 에 로드맵으로
> 확정돼 있다. "나중을 위한 추상"이 아니라 **이행의 수단**이므로 예외로 둔다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/Session/IMatchSession.cs` — 청사진 ①
  (`m1_blueprint_session_contract.md`) §2~§6 의 표면 그대로. 커맨드 7종·receipt·이벤트 봉투·
  스냅샷·읽기 모델·`MatchEnded`
- 신규 `Assets/_Project/Scripts/Core/Session/MatchCommand.cs` · `CommandReceipt.cs` ·
  `SessionEvent.cs` · `MatchReadModel.cs` — DTO. **Unity/ECS 타입 금지**(SimEntityId 는 int)
- 신규 `Assets/_Project/Scripts/Bridge/LegacyMatchSessionAdapter.cs` — 구 sim 위 구현체.
  커맨드를 기존 Bridge 공개면 호출로 번역하고, **Bridge 의 27채널 drain 결과를 받아** 세션 이벤트로
  방출한다

## 구현

- **drain 소유권 단일화**(설계 정본 §8 MAJOR): 어댑터가 **유일한 drain 소유자**가 되는 것이 목표지만
  이 unit 에서는 Bridge 의 기존 drain 을 **관찰**만 한다(unit 4 trace tap 과 같은 형태). 실제 소유권
  이전은 unit 13 이 소비자를 옮긴 뒤 — 두 소비자가 같은 큐를 다투는 창을 만들지 않는다.
- 커맨드 번역표: `DeployDefender`→`TryBeginDefenderDeployment` · `SetDeployFacing`→
  `ActivateDeployedDefender(cell, entity, facing)` · `RelocateDefender`→`TryBeginDefenderRelocation` ·
  `PlayCard`→`DreamcatcherHandController.Commit*` 4변종 · `ForceNextWave` · `FinishPlacement`→
  `StartBattle` · `Pause`→`TimeManager` lease. **거절 사유는 기존 enum 을 그대로 실어 보낸다**
  (통합 `CommandReject` 매핑은 표만, 값 손실 금지).
- 읽기 모델은 이 unit 에서 **기존 폴링 값을 그대로 복사**한다. 신설 카운터(점수·유출·스트레스)는
  Bridge private 필드라 아직 못 채우므로 **필드는 정의하되 미지원 표기**(unit 14 가 채운다).
- 어댑터는 `MonoBehaviour` 가 아니다 — Bridge 참조를 생성자로 받는 plain 클래스(수명은 Bridge 소유).

## 완료 기준

- compile 0 · EditMode 회귀 0(신규 경로는 미사용이라 회귀면이 없다).
- 어댑터가 커맨드 7종을 전부 받고 receipt 를 돌려준다 — EditMode 에서 **거절 경로 최소 1건/동사**
  단정(수락 경로는 Play 필요라 unit 13 과 함께).
- 골든 7종 재생성 **불필요**(sim 무변) — `configHash`·trace byte 동일 확인만.
- 소비자 재배선 0건: 기존 UI/뷰가 여전히 Bridge 직접 호출임을 grep 으로 확인(경계 지킴 증명).
