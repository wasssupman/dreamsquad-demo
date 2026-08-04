# 18 — 맥락 4 이식 (Units → Movement → Effects → Combat)

## 목적

M1 의 본체. ECS 44시스템·컴포넌트 97+21 을 순수 C# 틱 파이프라인으로 옮긴다. 정본은 **청사진 ③**
(`m1_blueprint_tick_pipeline.md`) — phase 배치·내부 채널 26쌍의 같은틱/1틱-지연·사망 4단계 릴레이·
ECB "루프 중 기록, 루프 후 적용"·RNG write-back 이 전부 그 문서에 있다. 이 unit 은 그것을 **코드로
옮기는 작업**이고 새 설계 결정을 하지 않는다.

## 변경 대상

의존 역순으로 4단계, **단계별 독립 커밋**(각 단계 후 골든 대조):

1. **Units** (7시스템 · 컴포넌트 22+5): Health·IncomingDamage 인박스 3종·사망 마킹/파괴 릴레이·
   `SimEntityId` 발급·HitFlash 는 **discard**(뷰 이관 — salvage §1)
2. **Movement** (2시스템 · 4+0): 위치 갱신 단일 권한·flowfield/patrol/chase 하강·포탈·`PastGoalTag`·
   Blink 소비
3. **Effects** (26시스템 · 40+8): CC/DoT 병합(**duration 정책 비대칭 보존** — 청사진 ③ §6)·모디파이어
   3단(Apply→Tick→Aggregate)·해저드/존·기믹 4종·픽업·캐리어 TTL
4. **Combat** (9시스템 · 30+8): 공격 루프(최대 클러스터 1,600줄)·투사체 2축·발사 명세·임계/도약·
   위협 테이블(**하류 소비자 0 — discard 여부 사용자 확인**, salvage §2)

각 단계에 딸린 테스트 포팅: World-조립 38파일은 "**어서션만 salvage, 골격 재작성**"(정본 M1-5).

## 구현

- **게이트 35개를 phase early-return 으로 번역**(청사진 ② §3). 함의 보존 필수 3건:
  `DamageApplication` 게이트는 버퍼 **부재**만 본다 · `Attack` 정지 시 Cast 드레인 동반 정지 ·
  `StackModifierTick` 3중 AND 비대칭. 채널 소멸로 자연 해소되는 것은 **명시 변경으로 기록**.
- **부재-상태 20건은 개별 체크**(청사진 ② 부속 B-2). 최우선 함정: 궁극기 이탈 무적은 `WithNone` 이
  아니라 **버퍼 Clear + continue** — 직역하면 착지 프레임 지연 폭탄이 된다.
- 내부 9채널은 함수 호출로 접되 **26쌍의 타이밍을 재현**한다. 1틱 지연 14쌍은 버그가 아니라 unit 0 이
  박제한 계약(특히 AggroHit 의 구조적 영구 지연, EnemyCc 의 생산자별 지연 혼재).
- **A/B 병행 구동**: 이 단계들 동안 구 sim 이 정본이고 신 sim 은 그림자로 돈다. 스왑은 unit 20.

## 완료 기준

- 단계별 compile 0 · EditMode 회귀 0.
- **단계별 골든 대조**: 그 맥락이 담당하는 시나리오가 구 sim 골든과 parity 통과(exact 축 = semantic
  이벤트·점수·상태 해시 / epsilon = 연속값). 실패 시 **그 단계에서 멈춘다**(누적 금지).
- 컴포넌트 97+21 · 게이트 35 · 부재-상태 20 각각 이식 체크리스트 100%(청사진 ② 부속을 체크박스로 사용).
- 포팅 테스트에서 어서션 손실 0 — 재작성한 골격이 같은 것을 단정하는지 리뷰.
