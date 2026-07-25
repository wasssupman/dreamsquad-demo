# 2 — 궁지폭발 (cornered_burst): Self×HpBelow(30%) × OnDamagedN × SelfTileAoe

## 목적

트리거 조합의 발단 카드. "HP 30% 이하일 때 2번째 피격마다 자기 주위 폭발" — 진동갑주(30% 이하 1회)와 짝을 이루는 **반복형 위기 카드**. 진동갑주는 유지, 이 카드는 신규.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/Card_CorneredBurst.asset` (신규) + 카탈로그 등록
- 카드 수 검증 3종 갱신 (이름 맵 · structuredCount · 축약 매핑 표)

## 구현

unit 0(OnDamagedN 범용화)·unit 1(게이트) 위에서 **코드 0줄 데이터**:

- id `cornered_burst` · displayName `궁지폭발` · axis All · type Unit · category Unique
- mechanics[0]:
  - trigger `{ kind: OnDamagedN, period: 2, gate: HpBelow, gateSubject: Self, gateValue: 0.30 }`
  - payload `{ kind: SelfTileAoe, magnitude: 20, tileRange: 1, projectile: AOE-view(작별 선물 재사용) }`
- description: formatter 미러 (예상: `HP 30% 이하일 때 2번째 피격마다 → 반경 1칸 피해 20`)
- 수치 전부 초안 — period 2 는 다굴 스팸 제동용, Play 튜닝 대상.

## 완료 기준

- [ ] EditMode 전체 green (카탈로그/이름/미러 자동 검증)
- [ ] e2e: 호스트 HP 를 30% 이하로 만든 뒤 피격 2회 → 폭발 발동·인접 더미 −20 확인. **30% 초과 상태의 피격은 counter 를 올리지 않음**(카운트 게이트) 확인
- [ ] 콘솔 unhandled payload/bake 경고 없음
