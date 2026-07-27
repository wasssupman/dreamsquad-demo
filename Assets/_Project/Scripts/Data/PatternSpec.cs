namespace Wassup.Data
{
    // projectile-emission-pattern unit 0 — 타겟 선택 규칙. index 기반 결정론만
    // (seeded RNG 금지, README 계약 6). v1 어휘 2종: RoundRobin(융단폭격 순회),
    // DeterministicShuffle(미사일 랜덤 저격). append-only.
    public enum PatternSelectionRule : byte
    {
        RoundRobin = 0,
        DeterministicShuffle = 1,
    }

    // projectile-emission-pattern unit 0 — ProjectilePatternData 의 unmanaged 미러.
    // 정의 계층 계약: UnityEngine/Entities/Battle 타입 무참조(DcMechanic.cs 선례).
    // asset 참조는 정수 핸들(barrelDataIndex)로 치환되며 핸들 해석은 아키텍처
    // 몫이다 — ECS 는 BattleBridge 의 ProjectileData 레지스트리, Mono 라면 자기
    // 테이블. 이 struct 자체는 어느 아키텍처도 모른다.
    public struct PatternSpec
    {
        public int barrelDataIndex;
        public float damage;
        public PatternSelectionRule selection;
        public int shotCount;
        public float shotIntervalSec;
        // 발마다 타겟을 다시 뽑는가(산개) / 첫 타겟에 집중하는가. 잠금 신원(Entity)
        // 자체는 아키텍처 바인딩이라 여기 없다 — 순수 계층은 "재추첨하는가" 만 답한다.
        public bool reselectPerShot;
        // SkyFall 낙하 예고 초. ProjectileData 에 대응 필드가 없는 유일한 값이라
        // 패턴이 소유한다(그 외 탄 파라미터는 barrel 소유 — README 계약 3).
        public float telegraphSec;
    }
}
