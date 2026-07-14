# 2. Choreography — 7 스테이지 시퀀스 재작성

## 목적

`GiftPhaseView.PlayGiftSequence()` 를 "횡 12장 축 이동"에서 5 서사 비트(내 덱 → 존재의 개입 → 융합 → 제시 → 장전)를 구현하는 카드 안무로 재작성한다. 총 시간 ≤ 6초(README 계약 8), 서사 계약(README 계약 9).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` (시퀀스 전면 재작성)
- `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` + `GiftConfig_Default.asset` (스테이지별 타이밍/기하)

## 구현

전 좌표는 `GiftPhaseLayout`(unit 0), 위젯은 `GiftCardWidget`(unit 1). 모든 트윈은 `_seq` 멤버(unscaled). 스테이지:

1. **인트로 = 존재의 등장 (0.6s, 딜-인과 겹침)** — 타이틀 펀치 인/아웃 + **kind별 앰비언스 온셋**: Lucid 는 타이틀 금색·배경 상단에서 따뜻한 금빛 틴트가 내려오고, Rim 은 타이틀 적색·화면 가장자리 비네트가 붉게 물든다(풀블리드 그라데이션 Image 알파, GiftConfig 색). 이 앰비언스는 페이즈 내내 유지되어 "누가 개입했는가"를 계속 상기시킨다.
2. **딜-인 = 내가 짠 덱 (1.2s)** — 내 덱 10장이 **하단 중앙 화면 밖**("나로부터", 손패 딜인 덱 소스와 문법 일관)에서 한 장씩 스태거로 `GridSlot` 착지. 비행 중 회전(딜러 스핀), OutBack 랜딩 + 미세 틸트 정착. 프레임 없음·차분한 리듬 = 익숙한 내 것. **10번째 착지 순간 그리드 전체 미세 스케일 펄스**(덱 완성 박자). pre-shuffle 슬롯 k = entryId k 매핑 유지.
3. **선물 리빌 = 존재의 개입 (1.0s)** — 등장 방향부터 kind 로 갈린다: **Lucid 는 화면 위에서 금빛과 함께 강림**(위→센터 하강 + 방사 글로우), **Rim 은 화면 하단·그림자에서 스며올라옴**(아래→센터 부상 + 어두운 펄스). 두 장은 **뒷면으로 등장**(위젯 BackRoot) → 개입 순간 **그리드 10장이 존재 반대쪽으로 살짝 밀렸다 복귀**(내 덱의 움찔) → 센터에서 **플립**(scale.x 1→0 face 스왑 0→1)으로 정체 공개 + 스케일 1.6 과시(홀로 프레임 점화, 배경 딤 펄스, `vibrateOnSpecialReveal` 유지) → 1.0 축소. 그리드(10칸)에는 끼우지 않고 센터에 떠 있다가 스택 수렴에 함께 빨려든다.
4. **스택 수렴 = 융합 시작 (0.5s)** — 12장이 중앙으로 수렴, `StackJitter(k)` 회전/오프셋 적층. 선물 2장이 **마지막에 파고들며 스택이 출렁**(squash 펀치) — 이물이 몸에 들어오는 순간.
5. **리플 셔플 = 융합 (0.9s)** — 좌/우 두 뭉치로 분리(바깥 틸트 ±12°) → `RiffleOrder` 순서로 중앙 재적층(카드당 ~0.04s 지퍼 스태거). **프레임 카드가 교차할 때 금/적 잔상 트레일**(잔상 Image 2~3장 페이드 — 섞이는 궤적이 보임). **재적층 완료 순간 kind 색 글로우 리플이 스택을 훑는다**(변화 각인 — 선물의 기운이 덱 전체에 퍼짐).
6. **부채꼴 딜 = 이번 판의 내 덱 (0.7s)** — 스택이 하단으로 슬라이드하며 `FanSlot(f)` 로 좌→우 전개. f = `GiftFinalOrder()` 인덱스(entryId→f 매핑, 기존 finalPos 딕셔너리 재사용). 전개 직후 **좌→우 하이라이트 스윕**(딜러가 손으로 훑듯, 카드별 순차 밝기 펄스 — 1→12 읽기 유도, 프레임 카드에서 소형 스파크). 스윕이 읽기 구간을 겸한다(hold 없음).
7. **순차 흡수 = 각성치에 장전 (1.4s)** — `FlyTarget` 위치에 **수신 앵커 링**(UiRoundedSprite 코드빌드, 12 세그먼트) 을 페이드 인. f=0 부터 `AbsorbDelay(i)` 가속 케이던스로 미니 아치 비행 + **링 근처 흡입 가속·세로 스트레치**(빨려듦 — 소멸이 아니라 저장). 닿을 때마다 **세그먼트 점등 + 임팩트 틱**. **12번째 카드는 피니셔**: 반박자 멈춤 → 강슬램 → 링 "찰칵" 잠금 팝(스케일 펀치+플래시) → **링이 각성 버튼 위치로 수축 소멸**(각성 게이지와의 연결 암시) → `ProceedToPlacement()`. 링은 gift 페이즈 전용 코드빌드 UI — 실제 각성 게이지/패널 무변경.

기타:
- 기존 계약 유지: `fastForwardInTestMode` 스킵, `OnPhaseChanged` 이탈 시 `StopSequence()`+패널 숨김, 이펙트 UI 는 중단 경로에서도 파괴.
- 구 필드 정리: `CardW/CardH/CardSpacing/PreX/FinalX` 상수 제거, GiftConfig 로 대체. 폐기 타이밍 필드(`holdSec` 등)는 asset 에서 제거하지 말고 [Obsolete] 없이 그냥 삭제 — 컴파일이 소비처 부재를 보증.

## 완료 기준

- Unity 컴파일 클린 + 기존 EditMode 전체 무회귀.
- 비포커스 execute_code 스모크: Gift 진입 → 시퀀스 완주 → `ProceedToPlacement` 도달, finalOrder==부채꼴 순서 로그 일치, 런타임 에러/PrimeTween ignored callback 0.
- `GiftConfig_Default.asset` 기본값 합산 총 시간 ≤ 6.0s (수치 명시 검증).
- 재시작/페이즈 강제 이탈 시 leak 없음(이펙트 잔존/NRE 0).

확인: 2026-07-14 — 스모크 완주(Rim·재진입·중단경로 leak 0), 콘솔 0, 실측 5.95s(홀드 제외). 커밋 `d52523ff` + `35fc78a7` + 리뷰 rev1 `c9571fc0`.
