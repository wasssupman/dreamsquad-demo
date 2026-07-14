# 3. Play Validation & Tuning

## 목적

포커스 Play 로 Lucid/Rim 양 분기의 실제 리듬·임팩트를 육안 확인하고, GiftConfig 튜닝 확정값을 기록한다. 비포커스 MCP 는 프레임이 멈춰 애니메이션 검증 불가(레슨) — 이 unit 만은 사용자 확인이 완료 조건.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/GiftConfig_Default.asset` (튜닝 확정값)
- 코드 변경은 튜닝 중 발견된 결함 수정만(신규 기능 금지)

## 구현

1. 스크립트 배틀 e2e(TestModeContext 우회 없이 일반 진입)로 Gift 페이즈 도달, 사용자 포커스 Play.
2. 확인 항목:
   - 딜-인이 "딜러가 돌리는" 리듬으로 읽히는가 (뭉개지면 스태거/회전 조정)
   - 선물 리빌에서 금/적 프레임이 한눈에 꽂히는가 (포일 Intensity/틴트 조정)
   - 리플이 "카드 섞기"로 읽히는가 (지퍼 스태거 조정)
   - 부채꼴에서 순서와 프레임이 동시에 읽히는가 (반경/아치각 조정)
   - 순차 흡수의 가속감 + 임팩트 틱 (first/min/decay 조정)
   - 총 시간 체감 ≤ 6초, 스킵 욕구가 안 생기는가
3. Lucid/Rim 각 1회 이상 + 재시작 1회(동일 결과 재생 확인, MatchSeed 계약).
4. 튜닝 확정값과 조정 사유를 이 문서 하단에 기록.

## 완료 기준

- 사용자 육안 승인 (Lucid·Rim·재시작 각 1회).
- 콘솔 에러/워닝 0, PrimeTween ignored callback 0.
- 튜닝 확정값 기록 + `GiftConfig_Default.asset` 커밋.
