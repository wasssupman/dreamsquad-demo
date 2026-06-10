# 0 — MatchSeed 정적 유틸 + EditMode 테스트

## 목적

단일 matchSeed 에서 맵/웨이브/비주얼 시드를 **결정론적·decorrelated** 하게 파생하는 순수 함수를 한곳에 모은다. BattleBridge(Bridge)와 GameManager(Core)가 모두 호출하므로 공용 정적 유틸로 둔다.

## 변경 대상

- `Assets/_Project/Scripts/Core/MatchSeed.cs` (신규)
- `Assets/_Project/Tests/EditMode/MatchSeedTests.cs` (신규)

## 구현

```csharp
namespace Wassup.Core
{
    public static class MatchSeed
    {
        // salt 는 임의 고정 상수. 같은 matchSeed 라도 계열을 분리해 상관 제거.
        const uint MapSalt    = 0x9E3779B1u; // map stream
        const uint WaveSalt   = 0x85EBCA77u; // wave stream
        const uint VisualSalt = 0xC2B2AE3Du; // projectile jitter stream

        // 미지정(0) 시 매 판 새 시드. 시간 + Unity RNG 혼합으로 같은 tick 충돌 회피.
        // (DraftController.GenerateSeed 와 동일 관용구. 결정론 함수 아님 — 진입점 전용.)
        public static int GenerateRandom() => unchecked(
            System.Environment.TickCount ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue));

        public static int DeriveMapSeed(int matchSeed)    => Mix((uint)matchSeed, MapSalt);
        public static int DeriveWaveSeed(int matchSeed)   => Mix((uint)matchSeed, WaveSalt);
        public static int DeriveVisualSeed(int matchSeed) => Mix((uint)matchSeed, VisualSalt);

        // 결정론적 32-bit 믹스(FNV/xorshift 류). 0 입력도 0 아닌 출력 보장.
        static int Mix(uint seed, uint salt)
        {
            uint h = seed ^ salt;
            h ^= h >> 16; h *= 0x7FEB352Du;
            h ^= h >> 15; h *= 0x846CA68Bu;
            h ^= h >> 16;
            int v = (int)h;
            return v != 0 ? v : 1; // 다운스트림 생성기들이 0 을 별도 폴백 처리하므로 0 회피
        }
    }
}
```

- **결정론**: `Derive*` 는 같은 matchSeed → 항상 같은 출력. `Math.Random`/시간 비의존.
- **decorrelation**: 같은 matchSeed 라도 map/wave/visual 출력이 salt 로 분리.
- `GenerateRandom()` 만 비결정론(진입점에서 1회 호출). 결정론 함수와 명확히 구분.

## 완료 기준

> ✅ 검증 2026-06-10 (Unity MCP, force refresh 후 EditMode) — `Core/MatchSeed.cs` + `MatchSeedTests.cs` 6개
> 작성. 전체 EditMode **315 total / 313 passed / 0 failed / 2 skipped**(skip 2개는 기존 Ignored). 신규 6개
> (결정론 3 + decorrelation + 충돌회피 + 0회피) 전부 통과. 컴파일 green, 콘솔 에러 0. 커밋: (다음 줄)

- [ ] compile green (EditMode 포함).
- [ ] `MatchSeedTests`: 동일 matchSeed 반복 호출 시 `DeriveMapSeed`/`DeriveWaveSeed`/`DeriveVisualSeed` 각각 동일값.
- [ ] 동일 matchSeed 에서 map/wave/visual 세 출력이 **서로 다름**(decorrelation).
- [ ] 서로 다른 matchSeed 두 개가 같은 derive 함수에서 다른 출력(충돌 회피 — 적어도 샘플 몇 개).
- [ ] 모든 derive 출력이 0 이 아님(경계: matchSeed=0 포함).
- [ ] 기존 EditMode 전부 통과(회귀 0).
