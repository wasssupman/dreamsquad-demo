using System;

namespace Wassup.Data.MapGrid
{
    // map-pipeline-cleanup unit 4 — 절차 생성기 은퇴 후 hard-fail 신호로 재사용.
    // usable 하지 않은 authored 문서로 맵 빌드를 시도하면 조용한 폴백 대신 이 예외로 실패한다.
    public sealed class MapGenerationFailedException : Exception
    {
        public MapGenerationFailedException(string message) : base(message) { }
    }
}
