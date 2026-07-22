# 1 · 항아리 독 뷰

## 목적

우하단 코너 각성 버튼을 **트레이 우측 분리 항아리 독**으로 교체한다. 큰 숫자(1순위 판독) +
세로 채움 높이 + 코스트 눈금 + ready 림 + 발견성 라벨을 갖춘 세로 항아리를 트레이 오른쪽에
인접 배치한다. 피규어(unit 2)가 들어오기 전까지 채움은 단색 액체 면(placeholder)이 대신한다.
탭=`Toggled`(기존 계약 유지), 손패 플립 불참·상주.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` — **클래스명·public API 유지**
  (씬 프리팹 GameObject `1012444853` 와 `DreamcatcherHandView.gaugeView`·
  `FirstSessionTutorialController.gaugeView` 배선을 깨지 않기 위해 in-place 재작성).
  `Toggled`/`SetOpen`/`SetSuppressed`/`Pulse`/`HitRect`/`GaugeChanged` 구독/phase 표시 유지.
  코너 배치 → 트레이 우측 배치, 원형 액체 디스크 → 세로 항아리. `BindTray(RectTransform)` 추가.
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `public AwakeningConfig
  Config => config;` getter 추가(코스트 눈금·ready 임계 파생용).
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — `Start()` 에서
  `gaugeView.BindTray(defenderSelector.PanelGO.transform as RectTransform)` 호출(씬 배선 없이
  기존 두 참조로 트레이를 gaugeView 에 전달; Start 는 모든 Awake 이후라 PanelGO 존재 보장).

## 구현

- **배치**: 독 패널 anchor `(0.5,0)`, pivot `(0,0)`. `LateUpdate` 에서 트레이 RectTransform 의
  `rect.width` 를 읽어 `anchoredPosition = (trayHalfWidth + gap, baselineY)` 갱신(트레이 우측
  엣지 + gap). 트레이·독의 SafeAreaRoot 는 UiSafeAreaFitter 로 congruent 하므로 폭 반값으로
  정렬. 트레이 미bind 시 폴백 위치(우측 안쪽).
- **항아리**: 세로 rounded-rect(`UiRoundedSprite.Make`) 배킹+테두리. 안에 세로 채움 면
  (`Image.Type.Filled/Vertical/Bottom`, `fillAmount=Gauge/GaugeMax`) = 2순위 판독(unit 2 에서
  피규어가 덮거나 대체할 별도 오브젝트로 둔다).
- **큰 숫자**: `Gauge` 값, 큰 폰트(1순위), 아웃라인. 획득 시 punch + `+N` 플로팅(기존 연출 이식).
- **코스트 눈금**: `handController.Config` 의 distinct 코스트값(`{costSquad,costUnit,costActive}`)
  마다 `y=cost/gaugeMax*innerH` 에 얇은 틱. **하드코딩 금지 — 데이터 파생**. 현재 라이브 값은
  3종 모두 20 → 틱 1개(20%). 값이 갈리면 자동으로 틱 증가.
- **ready 림**: `Gauge ≥ 최저 코스트`면 테두리 발화(색+저진폭). 그 이하 잠잠. (오버플로우·정밀
  affordability 는 unit 4.)
- **발견성 라벨**: 항아리 하단에 소형 `드림캐쳐` 라벨(기존 라벨 계약 계승, 없애지 않음).
- **히트존**: 패널 자체(폭 ~150 · 세로 항아리라 세로 히트 면적 충분, ≥140px). 최우측 슬롯과
  gap 확보.
- **코너 은퇴**: 기존 `(1,0)/(-24,20)/244²` 코너 배치·원형 디스크·Mask well 제거.

## 완료 기준

- **compile**: `Wassup.Runtime` 그린. 씬 배선 무변경(기존 GameObject/참조 유지).
- **오프스크린/Play 시각검증**: 트레이 우측에 세로 항아리가 뜨고, 큰 숫자·채움 높이·코스트 틱·
  라벨이 보이며, 게이지 변화 시 숫자·채움·ready 림이 반응. 코너에 옛 버튼 잔존 없음.
- **탭 토글**: 항아리 탭 → 손패 open/close (기존 `Toggled → DreamcatcherHandView` 경로 유지).
- 회귀: 튜토리얼 `SetSuppressed`, `FirstSessionTutorialController.HitRect` 포커스 여전히 동작.
