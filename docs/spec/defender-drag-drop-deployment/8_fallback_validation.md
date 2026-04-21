# Fallback Validation

**작업 구분**: Phase 8

## 목적

D&D 전환 후 기존 click placement fallback 과 전체 배치 sequence 를 검증한다.

## 검증

- Drag valid tile drop 성공.
- Drag invalid tile drop 실패 + reject flash.
- 배치 중 cost 부족이면 실패.
- Drop 후 deploy presentation 실행.
- on-place skill 1회 발동.
- PendingDeployment 중 공격/피격 없음.
- activation 후 공격/피격 정상.
- click placement fallback 이 남아 있으면 기존 경로도 정상.

## 완료 기준

- Unity compile 0 errors.
- Console error/warning 0.
- 사용자 Play smoke 통과.
- deployment sequence 완료 전 combat 참여가 없다.
- deployment sequence 완료 후 combat 참여가 정상이다.
