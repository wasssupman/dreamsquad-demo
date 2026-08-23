# 10 — 리뷰 반영: 폭발 반경 · 노후화 재저작 · 바위

## 목적

units 7~9 에 대한 **투트랙 리뷰**(ECS 정합성 / 설계 비판, 2026-08-23) 결과를 반영한다.
코드 트랙은 CRITICAL 0 · HIGH 0 으로 통과했고, 실질 지적은 전부 **설계·저작 층**에서 나왔다.

## 변경 대상

- `Assets/_Project/Data/Hazards/Blocker_BombBarrel.asset` — `explodeTileRange` · `healthDecayPerSec`
- `Assets/_Project/Data/Hazards/Hazard_Rock_1x1.asset` — `healthDecayPerSec`
- `Assets/_Project/Tests/EditMode/BarrelExplosionTests.cs` — 시한 회귀 테스트 재작성
- 문서 정정: `7_`·`8_`·`9_`·`README`

## ① 폭발 반경 1 → 2 (사용자 결정)

**리뷰가 찾은 구멍**: `explodeTileRange: 1` 은 인접 8칸이다. 적 23종 중 **12종이 사거리
2 이상**(킨들러 4 · 스나이퍼/루트캐스터 3 · 탱커·뱅가드·니들러·드래곤·디버퍼·소용돌이 2 ·
보스 3종 2). 이들은 2칸 밖에 서서 배럴을 부수고 **한 명도 안 맞는다.**

그러면 이 spec 의 검증 질문(「때린 놈들이 다친다」)이 **로스터 절반에게 구조적으로 성립하지
않는다.** 초반 웨이브가 전부 사거리 1 이라 Play 검증에 안 잡혔다 — 「초반에서 잘 읽힌다」가
「성립한다」의 증거가 아니었다.

반경 2 는 사거리 2 를 정확히 덮는다. 사거리 3·4(스나이퍼·루트캐스터·킨들러)는 **여전히
안 맞는다** — 그건 원거리 적의 설계상 우위로 남긴다(후속 후보 참조).

⚠ `explodeTargetCap` 은 **0(무제한) 유지**다. 상한을 두면 「몰려온 놈들이 다 같이 다친다」에서
누가 빠지는지가 저작 순서 문제가 된다.

## ② 노후화 20초 → 12초 (사용자 결정)

근거는 `9_health_decay.md` 저작 절의 DPS 표로 옮겼다. 요지: 이 knob 의 교전 감도는 5% 미만이고
**실효는 「방치 배럴이 판에 남는 시간」 하나**다. 그 축에서는 짧을수록 좋다.

## ③ 바위 길막 노후화 켬 (사용자 결정)

`Hazard_Rock_1x1` `healthDecayPerSec: 6.67` = **15초**(캐스터 쿨 5초 × 3 → 동시 최대 3개).

배경: `Ability_Hazard_BlockingCaster`(쿨 5초 · 사거리 3 · 1x1 바위 100HP)가 **시한도 노후화도
없이 무한 생성**했다. 아군은 자기 설치물을 못 부수므로 아무것도 치우지 않는다 — 180초면
최대 ~36개가 영구히 쌓인다. 「맵에 지속 생성돼 안 사라지는 오브젝트」의 정의 그 자체였다.

**단 `Defender_BlockingCaster` 는 현 시점 legacy 유닛**(사용자 확인)이라 실害는 낮았다.
`DefenderCatalog`·`BattleScene` 에는 살아 있으므로 위생 차원에서 저작한다.
`Hazard_Rock_3x3` 은 **참조 0(죽은 에셋)** 이라 손대지 않는다.

⚠ 바위에는 체력 바를 달지 않는다(`overheadHeight` 0 유지). legacy 유닛이라 판독 투자를
하지 않는 선택이며, 「닳는데 안 보인다」는 배럴에서 문제였던 그 모양이 여기선 수용된다.

## ④ 오버헤드 바 유지 (사용자 결정 — 리뷰 권고와 반대)

리뷰는 타일 게이지 전환을 권고했다. 근거와 반대 결정 기록은 `8_barrel_health_bar.md`
「형태 선택」 절에 있다.

## ⑤ 시한 회귀 테스트 재작성

초판 `Barrel_NeverExpires_...` 는 `healthDecayPerSec = 0` 을 깔고 돌아 **실게임이 쓰지 않는
설정만** 방어했다. 노후화를 켠 채로 물어 라이브 구성에서 계약이 지켜지는지 보게 고쳤다
(`ObstacleLifetimeSystem_NeverKillsAHazardItself_EvenWithDecayAuthored`). 값 자체는 단언하지
않는다 — 밸런스 수치를 리터럴로 못박지 않는 규율.

## 반영하지 않은 지적 (근거와 함께 보류)

- **`HealthDeathSystem` 순서 미선언**(ECS 트랙 MEDIUM). 현재 발현 경로 0 — 배럴 체력을
  `IncomingDamage` 우회로 쓰는 코드가 없다. 핀 추가는 M0 가 얼린 총순서 재확인이 필요하므로
  별도 작업으로 둔다.
- **노후화와 폭발의 자격 조건 비대칭**: 노후화 루프는 `BlockingHazard` 만, 폭발·체력바는
  `Obstacle` 도 요구한다. `Obstacle` 없는 설치물에 노후화를 저작하면 **바 없이 조용히 닳다가
  폭발 없이 죽는다.** 현재 그런 엔티티는 테스트 픽스처에만 있다 — 후속 후보가 이 지뢰를 밟는다.
- **`Obstacle` 없는 방벽 주석이 stale**(`ObstacleLifetimeSystem.cs`). 프로덕션 경로엔 없고
  `StructureSpawnAndBreachTests` 픽스처에만 있다.

## 완료 기준

- [x] compile 0 에러.
- [x] EditMode 2585건 중 실패 2건 — 둘 다 **이 변경과 무관**하다.
      ① `UnitKitCatalogTests` 말파이트 desc 30자(사전 실패, M0 착수 전부터 빨감)
      ② `DreamcatcherCardAssetTextTests` 부메랑 — 디스크 에셋 무변경 + 관련 커밋 0건이므로
      **시트 임포트가 메모리의 카드 문안을 덮은 드리프트**다(이 레포의 알려진 함정).
- [x] **(Play) 사용자 확인 완료 2026-08-23.**

확인 2026-08-23 · 사용자 Play 확인 + EditMode.
