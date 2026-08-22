# 6 — 수명 경과 틴트 (언제 터지나를 색으로)

## 목적

배럴이 **수명이 다해 갈수록 빨갛게 물들게** 한다. 「언제 터지나」를 숫자나 게이지가 아니라
물건 자체의 색으로 말한다(이 프로젝트의 게이지 금지 규율과 같은 결).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/BlockingHazardSO.cs` — `fuseTintColor` · `fuseTintExponent`
- `Assets/_Project/Scripts/Battle/Effects/BlockingHazardPresenter.cs` — `SetFuseTint`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SyncBlockingHazardFuseTint()`
- `Assets/_Project/Data/Hazards/Blocker_BombBarrel.asset`

## 구현

- **저작**: `fuseTintColor`(기본 **흰색 = 물들지 않음** → 기존 설치물 무회귀) +
  `fuseTintExponent`(1=선형, 클수록 막판에 몰아서). 배럴은 붉은색 · 지수 2.5.
- **sim→뷰 배선**: 남은 수명은 ECS(`Obstacle.remainingLife`)에 있고 색은 순수 프레젠테이션이라
  브리지가 매 프레임 흘린다 — `SyncGoalStability` 와 **같은 자리·같은 성격**(게임 상태 갱신 없음).
  `t = 1 - remaining/lifetime` 후 `t^exponent`.
  ⚠ **역수(`1/exponent`)를 쓰면 정반대**가 된다(초반에 확 빨개지고 막판엔 변화 없음).
- **⚠ 머티리얼을 직접 만지지 않는다.** 벤더 메시(KayKit)는 프랍 **500여 개가 머티리얼 하나를
  공유**하므로 `renderer.material` 을 건드리면 맵의 모든 프랍이 같이 물들고 인스턴스 머티리얼도
  샌다. `MaterialPropertyBlock` 으로 렌더러 단위 오버라이드한다(`_BaseColor` + `_Color` 둘 다 —
  그 머티리얼이 두 이름을 다 갖고 있다).
- **틴트 대상은 `MeshRenderer` 만** — 파티클(스폰 VFX)은 제외한다. 틴트는 «물건» 의 색이지
  연출의 색이 아니다. 그래서 `Bind` 에서 스폰 VFX 가 자식으로 붙기 **전**에 한 번 모은다.
- 변화가 0.01 미만이면 다시 쓰지 않는다(매 프레임 `SetPropertyBlock` 회피).

## 완료 기준

- [x] compile 0 에러.
- [x] (Play) 배럴을 세우고 방치하면 `_BaseColor` 의 g채널이 `1.00 → 0.00` 으로 내려간다.
      초반엔 거의 안 변하고(지수 2.5) 막판에 몰아서 빨개지는 것을 실측·육안 확인.
- [x] 전체 EditMode 회귀 없음.
- [ ] 색·지수 체감 튜닝(저작값).

확인 2026-08-22 · Play 실측 g채널 추이 + 막판 스크린샷.
