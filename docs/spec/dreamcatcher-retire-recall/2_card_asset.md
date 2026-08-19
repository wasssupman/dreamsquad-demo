# 2 — 카드 에셋 「인수인계」 + 등록 + Play 확인

## 목적

카드를 실제로 뽑을 수 있게 만들고, 사용자가 화면에서 확인한다.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/Card_Handover.asset` **(신규)**
- `Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset` (등록)
- 시트 `DcCards` 탭 (feature 종료 시 push 1회)

## 구현

### 1) 에셋

`id=handover` · `displayName="인수인계"` · `type=Unit` · `category=Normal` · `axis=All` ·
`visible=1` · `art=null` · `attachType=None`
`mechanics[0]`: trigger `OnRetire` × payload `RecallAttachedToFront` · **payload 수치 칸 전부 0**
(이 payload 는 값을 하나도 읽지 않는다 — 계약 4 rev 2). 게이트도 저작하지 않는다(bake 가 거절).
`description` 은 formatter 정확 미러: 「이 유닛이 퇴근하면 → 함께 붙은 다른 드림캐쳐가 손패
맨 앞으로」.

> **밸런스 축이 하나뿐이다** — 타입 공용 각성 코스트(`AwakeningConfig.costUnit`). 카드-로컬
> 손잡이는 의도적으로 없다(계약 4 rev 2 의 근거). 세 보이면 그때 장수 상한을 **테스트와 함께**
> 도입한다 — 부착 상한이 4 이상이 되기 전에는 실효 구간이 사실상 없다.

### 2) 등록

`DreamcatcherCardCatalog.asset` 의 카드 목록에 추가한다(덱 페이지 컬렉션 노출 + 저장 덱 검증이
카탈로그 경유). `DreamcatcherDeck_Default.asset` 은 **건드리지 않는다**.

### 3) 시트

feature 종료 시 `DcCards` 탭에 1회 push(비파괴 업서트). 카드별로 하지 않는다.
⚠ 시트 임포트가 로비 진입마다 돌아 카드 문안·값을 되돌리므로, 등록 전에는 에셋만 고쳐도
다음 로그인에 덮일 수 있다.

## 완료 기준

- 컴파일 0 에러 · 콘솔 경고 0.
- **Play 확인(사용자)** — 덱에 인수인계를 넣고 판 진입 → 유닛에 인수인계 + 다른 드림캐쳐를
  붙임 → **퇴근**:
  ① 그 드림캐쳐가 **손패 맨 앞**에 있다. 2장 이상이면 붙인 순서대로.
  ② 인수인계 카드 자신은 앞에 없다(계약 2).
  ③ 같은 구성을 죽였을 때는 앞으로 오지 않는다.
- **연출 판정(이 카드의 보상이 읽히는가)** — 지금 회수는 완전히 조용하다
  (`HandChangeReason.Recovered` → `Refresh()`, 무연출). 맨 뒤로 갈 때는 안 보였으니 그래도 됐지만,
  앞으로 오는 건 **처음으로 눈에 보이는 회수**다. 둘을 본다:
  ① 새로 들어온 카드가 딜인으로 구분되는가
  ② 뒤로 밀려 손패에서 사라지는 카드가 "내 카드가 없어졌다"로 읽히지 않는가
  안 읽히면 unit 3(회수 비행 연출)을 연다 — 후속이 아니라 이 카드의 완성 조건이다.
- **문안 확인**: 손패 툴팁이 "함께 붙은 **다른**"을 읽히게 말하는가(단독 부착이면 무효라는 사실).
