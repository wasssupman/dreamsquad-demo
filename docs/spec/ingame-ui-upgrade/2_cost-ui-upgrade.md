# 2 — Cost UI 업그레이드 (컴팩트 에너지 배지)

## 목적

좌하단 `CostDisplay` 를 화면 폭을 다 먹던 단색/젬 세그먼트에서, 사용자 레퍼런스
(⚡ 아이콘 + 큰 현재값 + 작은 `/max` + 짧은 바 게이지) 기준의 **컴팩트 에너지 배지**
로 재설계한다. Codex 아트 키트(패널/볼트/바)를 쓰고, 각 슬롯은 절차 폴백을 갖는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/CostDisplay.cs` — 전면 재작성. 게임 로직(코스트 계산) 무변경.

## 구현

1. 아트 슬롯 SerializeField: `costPanelSprite`, `costEnergyIcon`, `costBarFilled`,
   `costBarEmpty`(Sprite) + `numberFont`(TMP). 각 미할당 시 절차/기본 폴백.
2. 레이아웃(컴팩트 배지, 좌하단 `(40,150)`, ~200×218):
   - 패널: `costPanelSprite`(Simple) 또는 절차 라운드 플레이트.
   - 좌상단 에너지 볼트 아이콘(`preserveAspect`).
   - 큰 현재값 숫자(`numberFont`=Jua, Bold) + 그 아래 작은 `/max`.
   - 하단 바 게이지: 정수 1당 바 1개. 각 바 = 빈 바 스프라이트 배경 + 채움 바 오버레이
     (`Image.Type.Filled` Vertical Bottom, `preserveAspect`). 선두 바가 리젠에 따라
     아래→위로 차오른다.
3. `Update`: `fillAmount = clamp(current - i)`, 숫자 = `CurrentInt`, max = `/RoundToInt(Max)`.

## 완료 기준

- 컴파일/콘솔 클린. ✅
- (육안) 좌하단에 컴팩트 에너지 배지: ⚡ + 큰 숫자 + `/max` + 짧은 바 게이지.
  리젠 시 선두 바가 차오르고 정수 도달 시 다음 칸으로. 화면 폭을 먹지 않는다.
- 코스트 계산/페이즈 게이팅 로직 무변경. 슬롯 미할당 시 폴백 안전.

---
완료 확인: 2026-07-09 · 커밋 e25fb553
