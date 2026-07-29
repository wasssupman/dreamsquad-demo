using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-emission-pattern unit 0 — 한 발의 발사 명령. 로직 계층이 만들고
    // 아키텍처가 소비한다(README 계약 2).
    //
    // **Entity 를 모른다**: 타겟을 "후보 배열의 index" 로 가리키고, 아키텍처가
    // 자기 배열에서 해석한다(ECS = NativeArray<Entity>, Mono = List<Transform>).
    // ThreatTable.Leader(entries, alive) 가 이미 쓰는 관용구 — aliveness 같은
    // 아키텍처 상태는 caller 가 parallel 배열로 넘기고 순수함수는 lookup 을
    // 만지지 않는다.
    public struct ShotOrder
    {
        public int shotIndex;            // 버스트 내 순번 (베지어 제어점 스윙 소스)
        public int targetCandidateIndex; // 후보 배열 index. < 0 = 후보 없음(발사 소모)
        public float damage;
        public int barrelDataIndex;
        public float telegraphSec;
        public float directionT;
    }

    // 명령 자료구조를 완성하는 유일한 지점. 아키텍처 계층은 이 order 를 받아
    // 자기 형태로 번역만 한다 — 스케줄/선택 판단을 되풀이하지 않는다.
    public static class PatternLogic
    {
        public static ShotOrder BuildOrder(in PatternSpec spec, ref EmitterRuntime rt,
                                          int selectedCandidateIndex)
        {
            var order = new ShotOrder
            {
                shotIndex = rt.shotIndex,
                targetCandidateIndex = selectedCandidateIndex,
                damage = spec.damage,
                barrelDataIndex = spec.barrelDataIndex,
                telegraphSec = spec.telegraphSec,
                directionT = spec.shots[rt.shotIndex].directionT,
            };
            rt.shotIndex++;
            rt.fireCount++;
            return order;
        }
    }
}
