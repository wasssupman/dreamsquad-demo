# 5 — 짱쎈놈 배선 + 통합 Play 검증

## 목적

궁극기를 짱쎈놈에 실제로 물리고, 전 유닛을 통합 Play 로 검증한다. 여기까지 와야 처음으로 발동한다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Boss_Jjangssen.asset` — `nightmareMechanics` 에 슬롯 추가
- `CLAUDE.md` — NativeQueue 채널 목록에 `UltimateLeapVisualEventsSingleton` 27번째 등재 (+ 근거 한 줄)

## 슬롯 값 (초안 — Play 튜닝 대상)

```yaml
- trigger:  { kind: 5 (HealthThreshold), fraction: 0.7 }   # 30% 에서 1회. 0.5 이상 = 생존당 1회 보장
  payload:
    kind: UltimateLeap
    duration: 2            # 예고 초 (계약: 하드코딩 금지 — 여기가 소유)
    magnitude: 2           # 밀집도 탐색 반경 (기존 SelfBlink 와 동일 의미)
    tileRange: 6           # 착지 링 스냅 상한 (동일 의미)
    slamDamage: 100        # 일반 도약 슬램 50 의 2배 — 궁극기 체급. Play 로 확정
    slamTileRange: 2       # 예고·피해 반경. 일반 도약 1 보다 넓게
    projectile: (EarthSlamSpikes 계열 — 기존 착지 VFX 재사용, 필요시 별도 지정)
```

- `nightmareMechanics` 는 시트 동기 대상이 아니다(임포터 참조 0) — SO 직접 수정으로 확정된다.
- 경계 배치 확인: 0.7 은 0.2 의 배수가 아니고 0.5·0.9 와도 안 겹친다(README 계약 10).

## 통합 Play 검증 (이 spec 의 검증 질문에 답하는 항목들)

- [ ] 체력 30% 도달 → 보스가 위로 이탈, 뷰·HP 바 사라짐
- [ ] 착지 예고 빨간 타일이 즉시 표시되고, **그 타일 위 방어유닛을 재배치로 빼는 회피가 성립**
- [ ] 이탈 2초 동안: 방어유닛들이 타겟을 다른 적으로 전환 · 보스 체력 불변(DoT 포함) · 보스 위치 불변
- [ ] 2초 후 예고 타일 정중앙에 강하 착지, 예고된 타일과 **정확히 같은 범위**에 슬램 피해
- [ ] 착지 후 즉시 정상 행동(공격·사냥) 복귀
- [ ] 일반 도약(50%·10%)은 이전과 동일 — 피격 가능, 아치 비행
- [ ] 이탈 중 배틀 종료(전멸·나가기) 시 콘솔 에러 0, 예고 잔류 0
- [ ] 슬로모(손패 오픈) 중 발동 시 예고·카운트다운이 함께 느려짐
- [ ] EditMode 전체 무회귀

## 완료 기준

- 위 체크리스트 전부 + **사용자 Play 감각 확인**
- CLAUDE.md 채널 목록 갱신이 같은 커밋에 포함
- 종료 시 `6_handoff_summary.md` 작성 + `docs/spec/README.md` 등재

## 배선 결과 (2026-08-02)

슬램 VFX 는 기존 `Projectile_JjangssenLeap` 재사용(신규 에셋 0). Unity 런타임 판독 확인:

```
[0] fraction=0.2 | kind=2  (진동갑주)
[1] fraction=0.5 | kind=6  (일반 도약)
[2] fraction=0.9 | kind=6  (일반 도약)
[3] fraction=0.7 | kind=18 dur=2 slamDmg=100 slamR=2  ← 궁극기
```

`nightmareMechanics` 는 시트 DTO 가 아니라 로그인 임포트가 덮지 않는다.

## 검증 기록

- 2026-08-02 · EditMode 1809 중 1807 통과·실패 0 · compile 클린 · 에셋 런타임 판독 확인.
- **Play 체크리스트 9항목은 전부 미확인** — 사용자 확인 대기.

## ⚠ 관찰용 임시값 (2026-08-02)

빠른 확인을 위해 `fraction` 을 **0.7 → 0.1** 로 내렸다(체력 90% 에서 첫 발동).

**대가**: `fraction >= 0.5` 라야 두 번째 경계가 음수가 되어 1회로 끝난다. 0.1 은 경계가
90·80·…·10% 라 **9회 발동**하고, 0.2(진동갑주)의 약수라 80·60·40·20% 에서 폭발과, 50%·10% 에서
일반 도약과 **동시 발동**한다(boss-jjangssen 계약 5 가 피해둔 배치).
→ 이 값으로는 "궁극기다운 1회성"을 판정할 수 없다. 연출·회피·차단만 본다.

이탈 중에는 피해가 0 이라 HP 가 안 내려가므로 **비행 중 재발동은 구조적으로 없다**(안전).

**확인 후 `0.7` 로 복귀할 것.** `nightmareMechanics` 는 시트 DTO 가 아니라 로그인 임포트가
고쳐주지 않는다 — 사람 책임이다.
