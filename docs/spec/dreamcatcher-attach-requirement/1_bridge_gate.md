# 1 — bridge 게이트 배선 (커밋 preflight + UI 판정)

## 목적

unit 0 의 순수 함수를 bridge 의 두 소비처에 각각 배선한다: ① UI attachable 스냅샷(`WouldDreamcatcherCardApply`) ② 커밋 경로(`ApplyDreamcatcherCardToUnit`) preflight. 두 지점이 같은 함수를 부르므로 invalid 리티클과 무차감 거절이 일치한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs`
- `Assets/_Project/Tests/PlayMode/` — 신규 e2e 1건

## 구현

1. **host 데이터 조회 — 기존 `FindDefenderData(Entity)` 재사용**(`BattleBridge.cs:2843`). entity 키로 `_defenderByTile` 를 선형 스캔해 `DefenderUnitData` 를 돌려주는 헬퍼가 **이미 있다** — 새로 만들지 말 것. (구 계획은 "entity 키 헬퍼 없음"으로 오판해 `TryGetDefenderDataByEntity` 를 신설했고, 리뷰 M2 에서 중복으로 지적돼 제거했다. cell 키인 `TryGetDefenderData` 와 entity→cell 인 `TryGetDefenderCell` 만 보고 내린 잘못된 결론이었다.)
2. **`WouldDreamcatcherCardApply`** (라인 693 근처): Unit 분기에서 host data 를 조회해 `MeetsAttachRequirement` 를 **기존 `WouldApply` 호출과 AND** 로 합친다. 조회 실패 시 제한 카드만 false(무제한 카드는 기존 4-flag 경로 그대로 — `attachRequire==None` 이면 조회 자체가 불필요).
3. **`ApplyDreamcatcherCardToUnit`**: DefenderUnitTag 검사(라인 234 근처) 직후, LethalTimer/DreamCocoon 이중상태 preflight **앞**에 삽입. 그 구간은 전부 순수 읽기이고 첫 쓰기(mechanics bake 루프)·`attached`·`auraHandle` 초기화는 모두 뒤에 오므로 부분 적용 위험 0 (ecs-review 확인).
   - 실패 → `Debug.LogWarning`(카드 id + 요구 조건 + host role/id) + `return -1`. `-1` 은 `DreamcatcherHandController.cs:342` 의 `handle < 0` 에 걸려 `AttachAndSpend` 전에 반환되므로 각성 무차감·카드 잔류가 보장된다(리뷰 확인).
   - 무효 설정(Class×None / UnitId×빈문자열)은 **별도 문구로** 경고 — 데이터 실수를 즉시 드러낸다.
4. `ApplyBountyMark`(적 타겟)·Squad hosted 경로는 손대지 않는다 — 적용 범위 계약.

## 완료 기준

- compile 통과, 콘솔 에러 0.
- **PlayMode e2e 1건** (`Assets/_Project/Tests/PlayMode/DreamcatcherGateE2ETest.cs:29-56` 패턴 재사용): `ScriptableObject.CreateInstance<DreamcatcherCard>` 로 `attachRequire=Class, attachRequireClass=Guardian` 인 Unit 카드를 코드로 만들고 —
  - 가디언 host 에 `ApplyDreamcatcherCardToUnit` → `>= 0`
  - 비가디언 host → `== -1`
  - `WouldDreamcatcherCardApply` 가 두 host 에 대해 같은 답(true/false)
- 무제한 카드 부착 무회귀(기존 PlayMode 회귀 green).

확인 2026-07-25 — 컴파일 에러 0 · PlayMode e2e 1/1 pass(경고 3종 구분 확인: 제한 불일치 Class·UnitId / 무효 설정) · EditMode 1322건(1320 pass / 0 fail / 2 기존 Ignore).
PlayMode 전체 53건 중 6건 실패는 **회귀 아님** — 변경을 stash 한 clean 트리(52건)에서 **같은 6건이 같은 메시지로** 실패함을 직접 재현해 확인했다(AuthE2E 서버 500 중복키 · 폴백 덱 0장 · CardBuffs 가디언 dmgTaken · Gift↔Placement 흐름 drift 2건 · SceneTransition 전체실행 순서의존[단독 실행은 통과]). 전부 기존 backlog 기록분.

> **에셋 편집으로 검증하지 않는다.** append 필드는 인스펙터로 값을 넣는 순간 YAML 에 키가 기록되고 None 으로 원복해도 키가 남는다(`DreamcatcherCard.cs:56-57` 의 `visible` 필드 선례 · orphan 키 정리 불가). "에셋 diff 0" 은 달성 불가능한 기준이므로 코드로 카드를 만드는 e2e 가 유일하게 깨끗한 검증이다.
