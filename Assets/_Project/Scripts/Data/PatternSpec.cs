using Unity.Collections;

namespace Wassup.Data
{
    // projectile-emission-pattern unit 0 — 타겟 선택 규칙. index 기반 결정론만
    // (seeded RNG 금지, README 계약 6). v1 어휘 2종: RoundRobin(융단폭격 순회),
    // DeterministicShuffle(미사일 랜덤 저격). append-only.
    public enum PatternSelectionRule : byte
    {
        RoundRobin = 0,
        DeterministicShuffle = 1,
        // projectile-shot-sequence unit 1 — 타겟 선택을 하지 않는 방향 발사.
        None = 2,
    }

    // projectile-shot-sequence unit 0 — 한 발의 architecture-neutral 명세.
    // FixedList128Bytes 에 15개까지 들어가도록 8-byte plain 값만 둔다.
    public struct PatternShotSpec
    {
        // 패턴 min/max 각도 안의 정규화 위치. 실제 방향 회전은 PatternDirection 몫.
        public float directionT;
        // 이 탄과 직전 탄 사이의 간격. 첫 탄은 trigger 프레임에 나가므로 index 0 값은 무시.
        public float intervalAfterPreviousSec;
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
        public float minAngleDeg;
        public float maxAngleDeg;
        // shot 목록이 트리거당 발수의 단일 source of truth다.
        public FixedList128Bytes<PatternShotSpec> shots;
        // trigger마다 directionT와 첫 탄 이후 interval을 다시 스냅샷한다.
        // 값 범위는 SO가 소유하고, seed는 trigger producer가 공급한다.
        public bool randomizeShotsPerTrigger;
        public float randomIntervalMinSec;
        public float randomIntervalMaxSec;
        // 발마다 타겟을 다시 뽑는가(산개) / 첫 타겟에 집중하는가. 잠금 신원(Entity)
        // 자체는 아키텍처 바인딩이라 여기 없다 — 순수 계층은 "재추첨하는가" 만 답한다.
        public bool reselectPerShot;
        // SkyFall 낙하 예고 초. ProjectileData 에 대응 필드가 없는 유일한 값이라
        // 패턴이 소유한다(그 외 탄 파라미터는 barrel 소유 — README 계약 3).
        public float telegraphSec;
    }
}
