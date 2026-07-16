# 1. 몽마의 계약 — 유출 허용치 선불 스쿼드 저주

## 목적

부착 순간 **유출 허용치 1을 비가역 지불**하고(점수 축 직불 — 리스크 선불), 호스트 생존 중 전 아군 공격력 +25% 를 유지하는(리턴 후불·지속) Squad 저주. 카드가 전투 밖 매치 자원을 건드리는 첫 사례 — 지불 게이트만 신규이고 버프는 금이 간 성배의 hosted 골격을 그대로 쓴다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `public int leakAllowanceCost` append (끝에 추가, 기존 카드 0 역직렬화 = inert)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — **유출 허용치 판정의 실소유처 (critic M1 실측)**: 패배 판정은 `BattleBridge.cs:3093` 의 `_goalReachedCount >= deck.defeatGoalReachedCount` 이며 `defeatGoalReachedCount` 는 **AttackDeck ScriptableObject 필드**다(GameManager 아님). 변경: ① 런타임 오프셋 필드 `_leakAllowancePenalty` 신설 ② 판정식을 `_goalReachedCount >= deck.defeatGoalReachedCount − _leakAllowancePenalty` 로 보정 ③ 매치 리셋 지점(`:1026` 인접, `_goalReachedCount` 리셋과 같은 곳)에서 0 초기화 ④ `TryPayLeakAllowance(int)` + 잔여 허용치 조회 API
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `CommitAttach` 선불 게이트
- `Assets/_Project/Data/Dreamcatcher/Card_IncubusPact.asset` 신규 + `DreamcatcherCardCatalog.asset` 등록
- 테스트 (PlayMode 또는 컨트롤러 수준)

## 구현

**카드 인코딩** — `id=sub_incubus_pact`, `displayName=몽마의 계약`, `type=Squad`, `category=Subconscious`, `axis=All`,
`effects=[{ AttackDamage, +25 }]`, `leakAllowanceCost=1`. mechanics 없음 — hosted 버프는 기존 `ApplyDreamcatcherCardHosted` 가 전부 처리.

**CommitAttach 순서** (contract 9 "실패 시 무변화" 계승):
1. `TryGetUsableAttach` (기존)
2. **선불 가능 판정**: `leakAllowanceCost > 0` 이면 `잔여 허용치 − cost ≥ 1` 요구 — 지불로 즉시 패배가 되는 상태를 구조적으로 금지. 미달 시 커밋 거절(게이지·허용치·큐 무변화).
3. cap check → `ApplyDreamcatcherCard` (기존)
4. **apply 성공 후에만 지불** (`TryPayLeakAllowance`). 실패한 부착이 지불하는 일 없음.
5. `AttachAndSpend` (기존)

**비가역 계약**:
- host 사망 revoke 는 hosted 버프만 회수(기존 `RevokeDreamcatcherEffects`). **허용치는 돌아오지 않는다** — 이것이 §6 세탁 차단의 핵심.
- 카드는 host 사망 시 큐 맨 뒤 복귀(기존). 재부착하면 다시 지불한다(잔여 허용치가 조건을 만족할 때만).

**유의 (critic M1)**: **SO 는 절대 불변** — `deck.defeatGoalReachedCount` 를 직접 감소시키면 에디터에선 자산 파일에 영구 저장되고, 기기에선 매치 간 공유 인스턴스라 패널티가 누적된다(매치 2가 매치 1의 차감을 물려받는 점진적 패배 버그). 지불 = **BattleBridge 런타임 오프셋 증가**뿐이다. 잔여 허용치 = `deck.defeatGoalReachedCount − _leakAllowancePenalty − _goalReachedCount`. BattleBridge 는 다중 세션 공유 편집 핫스팟 — 커밋 시 hunk 선별 주의. HUD 표기는 backlog "남은 허용 유출 HUD" 와 합류(본 unit 범위 밖).

## 완료 기준

- [x] compile 0 에러
- [x] 테스트: ① 부착 성공 → 허용치 −1 + 전군 DamageMul 활성 ② 잔여 허용치 2 미만 → 커밋 거절 + 게이지/허용치 무변화 ③ host 사망 → 버프 중립화(기존 revoke 경로) + 허용치 불변
- [x] 유출 누적 → 패배 판정이 차감된 허용치 기준으로 동작 — 판정식(`− _leakAllowancePenalty`) 반영 + `RemainingLeakAllowance`(동일 산술) 테스트로 갈음. 실제 유출→패배 e2e 는 wave 하네스 필요 → Play 스모크는 unit 4 통합 검증에서.
- [x] 매치 재시작 시 `_leakAllowancePenalty` 0 리셋 — 이전 매치 지불이 새 매치로 새지 않음 + AttackDeck SO 자산 diff 0 확인

확인 2026-07-16 — compile 0 에러 · PlayMode 6/6(IncubusPactTest 2 신규: 지불→버프→revoke 비가역 / 바닥 거절+리셋 SO 불변 + DreamcatcherEffect/CursedRelic 회귀) · EditMode 카탈로그 suite 8/8(풀 로스터 5장 + pact 에셋 계약). 컨트롤러 `CommitAttach` 게이트는 `TryPayLeakAllowance` 와 동일 산술 — 코드 리뷰 + Play 는 unit 4 통합에서.
