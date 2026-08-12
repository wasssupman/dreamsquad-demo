# unit 5 — Zig «산맥 고개»

## 목적 / 구현

중앙 산맥(데코 능선 y3·y8, 어깨 x3..5/x10..11)을 **폭2 고개(y5..6)가 관통**하고, 마음(7,5)은
고개 한복판 광장(x6..9, y4..7)에 선다. NW 스폰(0,10)은 좌측 열→**서쪽 고개 입구**,
SE 스폰(14,1)은 우측 열→**동쪽 고개 입구** — 반대편 입구가 곧 갈래(계약 4). 외곽 순환로는
우회 대안으로 남는다. Air 경로 `(4,8)→(10,4)`: 능선을 대각으로 넘는 공중 침투.
(기존 «사선(지그재그)» 정체성은 고개 접근각으로 계승 — 이름은 유지한다.)

```
PPPPPPPPPPPPPPP
SWWWWWWWWWWWWWP
WWWWWWWWWWWWWWP
PWWDDDDDDDDDWWP   ← 북쪽 능선
PWWDDDWWWWDDWWP
PWWWWWWWWWWWWWP   ← 고개(폭2)
PWWWWWWGWWWWWWP   ← G(7,5)
PWWDDDWWWWDDWWP
PWWDDDDDDDDDWWP   ← 남쪽 능선
PWWWWWWWWWWWWWW
PWWWWWWWWWWWWWS
PPPPPPPPPPPPPPP
```

## 완료 기준

- [x] 자가검사: 폭1 0 · 광장 존재 · 두 스폰 도달 · Walk 106칸 전체 연결
- [x] ReworkedPaths 이동 · EditMode 전량 그린 · 콘솔 에러 0
- [x] 라이브 스모크: 두 레인이 반대편 고개 입구로 진입 → 마음 공성 · Skimmer 능선 越 경로 통과 · 스크린샷
- [ ] 사용자 Play 체감
