# 3 — 부착 카드 발동 신호: 머리 위 아이콘 펄스

## 목적

부착한 카드가 **실제로 일한 순간**을 보이게 한다. 현재는 발동 전용 신호 채널이 없어
(payload 가 기존 파이프라인 VFX 를 빌려 쓸 뿐), 조건부 카드(궁지폭발·처형타·진동갑주 등)는
조건이 충족된 적이 있는지조차 알 수 없다 — 피드백 루프의 유일한 빈 칸.

최소 신호: 발동 순간 유닛 머리 위 아이콘 행의 **해당 카드 아이콘이 펄스**(스케일 펀치 + 플래시).

## 변경 대상 (조사 후 확정)

- `Assets/_Project/Scripts/Battle/Combat/DcTriggerFiredEvents.cs` — 신규 채널(이벤트+싱글턴)
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 발화 3지점 enqueue
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 큐 lifecycle 3점 세트 + 드레인 +
  ShieldBreak 드레인에 펄스 편승(payload != None 일 때)
- `Assets/_Project/Scripts/Presentation/UnitOverheadUiLayer.cs` — `PulseCards(host)` 중계
- `Assets/_Project/Scripts/Presentation/UnitOverheadView.cs` — 행 스케일 펀치(기존 Update 에
  병합 — Update 가 이미 있어 별도 정의 불가). 화이트 플래시는 뺐다(UI Image 틴트는 곱셈이라
  밝힘 불가 — Spine 틴트와 같은 이유)
- `Assets/_Project/Scripts/Data/UnitOverheadUiStyle.cs` — `cardPulseScale 1.25 / cardPulseSec 0.28`

## 구현

### A. 조사 결과 (2026-07-29) — 확정 계약

1. **발화 지점**: AttackN 계열은 3곳 전부 `AttackSystem`(Combat) 안 — RESOLVE / 폭탄 발사
   훅 / 캐스트 드레인 (`DcTriggerSlot` "owned write 세 지점"과 동일). `OnShieldBreak` 는
   기존 `ShieldBreakEventsSingleton` 드레인(bridge)에서 payload 실행 — 추가 채널 불요.
2. **기존 큐 재사용 기각**: `AttackOutputLog` 는 opt-in 로깅(상시 신호 불가),
   `UnitAttackVisual` 은 의미 오염, `CastEvents` 는 방향 반대(Effects→Combat).
3. **귀속**: `instanceId ↔ entryId` 매핑이 없다(Unit 카드 부착 handle 0, recall registry 는
   후속 spec). → **host 단위(행 전체 펄스)로 확정** (사용자 결정 2026-07-29). 카드 정밀
   귀속은 recall registry 와 함께 후속.
4. **신규 채널 승인** (사용자 결정 2026-07-29): `DcTriggerFiredEventsSingleton`
   (Combat→Bridge, 23번째 채널 — CLAUDE.md 목록 갱신). 발동 = 카운터 소비 성사 프레임이며
   payload arm/대상 유무와 무관하게 신호한다. 생산자는 Combat 단독.
5. **후속 확장**: Units 계열 발화(OnDamagedN/HealthThreshold/OnKill/OnDeath/PeriodicTimer)는
   생산 맥락이 달라 이 채널에 쓰지 않는다 — 필요 시 별도 결정.

### B′. 연출 rev 2 — 부착 임팩트 재사용 (사용자 피드백 "이펙트가 없어 보인다")

발동 순간 **카드 부착 임팩트를 다시 친다**: 유닛 몸 펀치(`PlayPunch`) + 흰 플래시
(`FlashWhite`) + 카드 흡수 링/버스트 VFX(`SpawnCardAbsorbVfx`). 부착될 때 박히던 그
임팩트가 카드가 일할 때 다시 울린다 — 인과 언어 일치, 신규 에셋 0.

- **카메라 킥·흡수 SFX 는 제외** — 주기 발동 연타에 멀미/소음.
- **연발 정책 (3단)**:
  1. 같은 프레임 같은 host = **1회 코얼레스** (`_dcFiredScratch`)
  2. 다른 프레임 연발 = 월드 임팩트(펀치/플래시/VFX)는 **host 당 최소 간격 스로틀**
     (`dcProcImpactMinIntervalSec` 0.25, bridge SerializeField). UI 펄스는 스로틀 없음 —
     발동 사실은 매번 알리되 도배는 막는다.
  3. UI 펄스 자체는 타이머 재시작 코얼레스(과누적 없음).
- **동반 수정 — FlashWhite 연발 stray-tint 가드**: 앞 flash 가 skel 을 흰빛으로 밀어둔 채
  새 flash 가 "현재 색"을 복귀 목표로 캡처하면 유닛이 밝게 굳는 잠재 버그(발동 임팩트가
  연발 경로를 만들며 노출). 진행 중이면 기존 restore 승계(`_flashActive`/`_flashRestore`,
  `SpineUnitView`). lockon 의 hover-flash 가드와 같은 계열.
- 링(rev 1)은 **행폭 ×1.6 으로 축소** (사용자 2026-07-29).
- rev 1 의 행 펀치+링(아래)은 유지 — 아이콘 행 위치를 짚어주는 보조 신호.
- 더 강한 시그니처가 필요하면 후속: ShieldGranted 선례(vfxSpawner 원샷)로 전용 벤더
  파티클(unity-vfx-authoring) — 아트 선택이 필요해 별도 결정.

### B. 연출 — 행 펀치 + 링 버스트 (rev 1)

- 최초 구현(행 스케일 펀치 ×1.25)은 **아이콘이 ~29px 라 사실상 비가시** — 사용자 피드백
  2026-07-29. 신호를 아이콘 행 밖으로 꺼낸다:
  - **행 펀치** ×1.8, 0.4s 단봉(빠른 팽창 → 완만 복귀)
  - **링 버스트** — 행 중심에서 행폭 → ×2.6 으로 ease-out 확산 + 선형 페이드. 시안
    (락온 확정 펄스와 같은 시각 문법 = "드림캐쳐가 일했다"의 색). 아이콘 뒤 sibling.
- 화이트 플래시는 제외 — UI Image 틴트는 곱셈이라 밝힘 불가(Spine 틴트와 같은 이유).
- 값은 전부 `UnitOverheadUiStyle` SO (`cardPulseScale/Sec/RingScale/RingColor`).
- 같은 프레임 다발 발동은 코얼레스(타이머 재시작) — 과누적 방지.
- **bridge 에 kind 분기 금지** (계약 6): bridge 는 "entryId 가 발동했다"만 중계하고,
  어떤 연출인지는 드림캐쳐 프레젠테이션이 정한다.
- 적 표식(BountyMark)은 아이콘 행에 없으므로 스코프 밖.

### C. 스코프 아님

- 발동 로그/히스토리, 토스트, 사운드 — 후속 후보.
- 트리거 진행도 뱃지("4/5")는 별개 후속(스냅샷 경로 필요, unit-dreamcatcher-icons 후속과 병합).

## 완료 기준

- [x] 조사 메모: 발동 지점·기존 큐 재사용 가능 여부·entryId 귀속 지점 (§A 확정 계약)
- [x] 신규 채널 사용자 승인 (2026-07-29, host 단위 귀속 포함)
- [x] 컴파일 통과, EditMode 신규 실패 0 (리그 1540/1543 — 신규 채널이 리플렉션 규약
      테스트 2건에 자동 편입돼 통과)
- [x] Play — 전투 줌아웃 상태에서 발동 신호가 **실제로 눈에 들어오는가** (rev 2 임팩트로 충족)
- [~] ~~진동갑주(HP 30% 1회) 발동 펄스~~ — **커버리지 밖 정정**: 진동갑주는 Units 맥락
      발화(§A.5)라 이 채널이 나르지 않는다. AttackN 계열(니들 등)로 검증. Units 확장은 후속
- [x] Play — 주기형 발동(예: 5회마다)이 연타될 때 펄스가 과누적되지 않는가 (스로틀 0.25s)
- [x] Play — 발동 없는 카드(패시브 스탯형)는 조용한가 (오발 없음)

확인: 2026-07-29 사용자 Play 확인("이상없음") — rev 2(부착 임팩트 재사용 + 링 1.6 축소 +
연발 3단 정책 + FlashWhite stray-tint 가드) 기준. 커밋 해시는 handoff 참조.
