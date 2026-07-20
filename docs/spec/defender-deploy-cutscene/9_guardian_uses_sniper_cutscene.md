# 9 — Guardian이 Sniper 컷씬 사용

## 목적

Guardian 전용 컷씬 대신 기존 Sniper 컷씬을 임시 공용한다. 전투 캐릭터·스탯·배치 규칙은
그대로 두고 `DefenderUnitData`의 컷씬 프레젠테이션 참조만 교체한다.

## 변경 대상

- `Assets/_Project/Data/Defenders/Defender_Guardian.asset`

## 구현

- `deployCutsceneFrames`: Sniper의 49개 Sprite 참조와 동일하게 변경한다.
- `deployCutsceneDepth`: Sniper의 정적 뎁스 텍스처 참조와 동일하게 변경한다.
- `deployCutsceneScale`: `2.6`으로 변경한다.
- `deployCutsceneOffset`: `(0, 0)`으로 변경한다.
- 이미 같은 `deployCutsceneFps=24`, `deployCutsceneTiltGain=1`은 유지한다.
- Sniper PNG·뎁스 파일은 복제하지 않는다. 두 DefenderUnitData가 같은 immutable 자산을 참조한다.

## 완료 기준

- Guardian 드래그 시 Sniper와 동일한 49프레임·뎁스 컷씬이 재생된다.
- Sniper와 같은 크기·도착 위치를 사용한다.
- Guardian의 전투 모델·스탯·배치/공격 애니메이션에는 변경이 없다.
- Unity 컴파일 및 Console error 0.

_구현 확인 2026-07-18 (`e3632167`): Guardian/Sniper 프레임 49개·뎁스·scale·offset
동일성 확인, 열린 Unity 6000.4.3f1 Editor 스크립트 리컴파일 완료, Console error 0.
실제 Guardian 배치 Play smoke는 후속 확인._
