# Unit 4 — Defender 16종 1차 외형 적용

## 목적

도구 체인(unit 1~3)을 실전 투입해 Defender 16종에 서로 구분되는 외형 조합을 적용한다. 이 unit 은 컨텐츠 작업이며 도구 검증을 겸한다.

## 변경 대상

- `Assets/_Project/Data/Defenders/Defender_*.asset` 16종 — partSkins/slotColors 채움

## 구현

1. 클래스 정체성 기준 1차 조합 (예: Guardian=중갑+방패, Sniper=후드+장총, Caster=로브+지팡이).
   조립 주체는 사용자/아트 — 데모 씬에서 조립 후 unit 3 도구로 이관. 아트 리소스가 아닌
   임시 시안이어도 "16종이 서로 구분된다" 를 만족하면 통과.
2. 클래스별 gear(무기) 파츠는 공격 모션(Attack1)과 어울리는지 Play 로 확인.
3. 드래그 프리뷰 반영 확인 — 코드 수정은 unit 1 에서 공유 헬퍼로 이미 처리됨(critic F1 반영으로 이관), 여기서는 확인만.

## 완료 기준

- [x] Defender 16종 조합이 서로 유일 + validator 무경고 + combined skin 합성 성공 (배치 스모크: 16종, unique=16, empty=0). 배치/전투/프리뷰의 눈 확인은 에디터 항목 (사용자 확인 대기)
- [x] 콘솔 경고 0 — 최초 실행에서 validator 가 `gear_right/gear_right_c_8` 결번을 즉시 검출(실측: gear_right 는 c_8/c_9 결번으로 38종) → c_7 로 교정. 도구 실효성의 첫 실사례
- [ ] Android 실기기 1회 확인 — 잔여

확인 2026-07-07 (배치 검증 기준). 시안 조립 주체 = 프로그램 생성(사용자 결정 2026-07-07): 클래스 정체성 기반 16종 — 헬멧조 6종(Artillery/Bastion/Cannon/Guardian/Piercer/Sniper), 캐스터는 eyewear/머리색 틴트(fire/heal/ice/poison 4종), 방패조 gear_left(Bastion/Guardian). 아트 정식 교체는 데모 씬 조립 → 인스펙터 "가져오기" 버튼.
