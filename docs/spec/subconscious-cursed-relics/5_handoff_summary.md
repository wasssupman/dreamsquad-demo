# 5 — 인계 요약

> 완료: 2026-07-15

## 결과

- 무의식 플레이스홀더 2장을 `재앙의 심장`, `금이 간 성배`로 GUID 보존 교체했다.
- 두 카드 모두 기존 mechanic/effect 조합만 사용하며 신규 인터페이스, ECS 타입·시스템·이벤트 채널을 추가하지 않았다.
- 기존 `LethalTimer` 호스트에 시한부 복합 카드가 부분 적용되는 문제를 부착 전 사전검증으로 차단했다.
- 신규 2장과 문자 placeholder였던 기존 3장의 카드 아트를 완성하고 기존 Gift·Hand·Inspect 표시 경로를 재사용했다.

## 검증

- EditMode: 관련 테스트 11/11 통과
- PlayMode: 관련 테스트 4/4 통과
- 카드 아트 import 계약: 5장 모두 1024×1536, Single Sprite, mipmap off 확인
- 씬, 카탈로그 배열, 전용 UI 변경 없음

## 변경 범위

- 런타임: `BattleBridge.Dreamcatcher.cs`의 SelfBuffLethal 중복 사전검증
- 데이터: 재앙의 심장·금이 간 성배 ScriptableObject
- 테스트: 카탈로그·텍스트·효과 철회·중복 LethalTimer 회귀
- 아트: `dreamcatcher_card_21`~`25`

구현과 문서 종료 상태는 같은 기능 커밋으로 기록한다.
