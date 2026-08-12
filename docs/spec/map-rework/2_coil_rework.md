# unit 2 — Coil «로터리»

## 목적 / 구현

고리 정체성 = 폭2 순환로. 중앙 광장(5×4, x5..9 y4..7)에 마음(7,5), 입구는 **북문(x6..7,y8)·남문(x7..8,y3) 둘뿐** — NW 스폰(0,10)은 북문, SE 스폰(14,1)은 남문으로 들어와 레인이 곧 갈래다(계약 4). 서쪽 숲 기둥(x3..4, y3..8) = 공중 대륙, Air 경로 `(3,7)→(4,4)` 로 숲을 종단해 광장 남서로 진입. 동쪽 x10..11 열은 광장을 내려다보는 배치 포켓(D1). 스폰 3→2(계약 4 — 웨이브는 lane modulo 라 무회귀).

```
PPPPPPPPPPPPPPP      P=배치 W=길 D=숲 S=스폰 G=마음
SWWWWWWWWWWWWWP
WWWWWWWWWWWWWWP
PWWDDPWWPPPPWWP      ← 북문
PWWDDWWWWWPPWWP
PWWDDWWWWWPPWWP
PWWDDWWGWWPPWWP      ← G(7,5)
PWWDDWWWWWPPWWP
PWWDDPPWWPPPWWP      ← 남문
PWWWWWWWWWWWWWW
PWWWWWWWWWWWWWS
PPPPPPPPPPPPPPP
```

## 완료 기준

- [x] 자가검사: 폭1 0칸 · 광장 존재 · 두 스폰 골 도달 · Walk 104칸 전체 연결
- [x] `MultiGoalPoolSeparationTests` ReworkedPaths 이동(새 계약: 골1·폭≥2·광장)
- [x] EditMode 전량 그린 · 콘솔 에러 0
- [x] 라이브 스모크: 두 레인이 각자 다른 문으로 광장 진입 → 마음 공성 · 스크린샷
- [ ] 사용자 Play 체감
