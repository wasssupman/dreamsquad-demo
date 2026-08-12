# unit 3 — Twin «쌍길 교차» (최대 변화)

## 목적 / 구현

마음 2 → **1**(계약 3) — 쌍둥이 정체성을 «골 2개»에서 **«회전 대칭 쌍길»**로 재해석했다.
SW 스폰(2,0)과 NE 스폰(12,11)에서 서로를 180° 회전한 갈고리 길이 광장(x5..9, y4..7)을 감아
북문(x6..7,y8)·남문(x7..8,y3)으로 각각 진입 — 광장 밖에서는 절대 만나지 않는다(계약 4).
마음 (7,5). 데코 대각 두 점(x8..10 y8..10 · x4..6 y1..3), Air 경로 `(9,9)` — NE 숲을 지나 마음 직행.

```
PPPPPPPPPPPWSPP
PPWWWWWWDDDWWPP
PPWWWWWWDDDWWPP
PPWWPPWWDDDWWPP   ← 북문
PPWWPWWWWWPWWPP
PPWWPWWWWWPWWPP
PPWWPWGWWWPWWPP   ← G(7,5)
PPWWPWWWWWPWWPP
PPWWDDDWWPPWWPP   ← 남문
PPWWDDDPWWWWWPP
PPWWDDDPWWWWWPP
PPSWPPPPPPPPPPP
```

## 완료 기준

- [x] 자가검사: 폭1 0 · 광장 존재 · 두 스폰 도달 · Walk 82칸 전체 연결
- [x] ReworkedPaths 이동 · EditMode 전량 그린 · 콘솔 에러 0
- [x] 라이브 스모크: 두 갈고리가 각자 문으로 진입 → 마음 공성 · 스크린샷
- [ ] 사용자 Play 체감
