# unit 4 — Spiral «나선 광장»

## 목적 / 구현

폭2 나선 한 궤도가 중앙 광장(x6..9, y4..7)으로 감겨 들어간다 — 마음 (8,5). 상단 밴드는
우측 열로만 이어져 **바깥 스폰(0,10)은 나선을 완주**해야 하고(자가검사로 강제 확인),
SE 스폰(14,2)은 하단 밴드로 **중간 진입** — 계약 4의 «자연 분기»를 갈래 대신 **진입 깊이 차**로
해석한 의도적 편차(나선은 단일 궤도가 정체성이다). 서쪽 숲 종단 스트립(x1..2, y3..8)이 공중
대륙 — Air 경로 `(2,7)→(5,5)`: **지상은 나선 완주, 비행은 서쪽 숲을 질러 중앙 직행.**
공중 대비가 가장 강한 맵. 스폰 3→2.

```
PPPPPPPPPPPPPPP
SWWWWWWWWWWWWWP
WWWWWWWWWWWWWWP
PDDPPPPPPPPPWWP
PDDWWWWWWWPPWWP
PDDWWWWWWWPPWWP
PDDWWPWWGWPPWWP   ← G(8,5)
PDDWWPWWWWPPWWP
PDDWWPPPPPPPWWP
PPPWWWWWWWWWWWW
PPPWWWWWWWWWWWS
PPPPPPPPPPPPPPP
```

## 완료 기준

- [x] 자가검사: 폭1 0 · 광장 존재 · 두 스폰 도달 · Walk 92칸 전체 연결 · 상단 밴드→우측 열 단일 연결(나선 완주 강제)
- [x] ReworkedPaths 이동 · EditMode 전량 그린 · 콘솔 에러 0
- [x] 라이브 스모크: 바깥 스폰 완주 + 중간 진입 공존 → 마음 공성 · 스크린샷
- [ ] 사용자 Play 체감
