namespace Wassup.Battle.Combat.Projectile.Emission
{
    // 발사 시점에 궤적이 요구하는 바인딩. 이동 수학이 몇 종이든 emitter 가 알아야
    // 하는 것은 이 셋뿐이다 — 엔티티를 겨누나, 셀을 겨누나, 방향으로 쏘나.
    public enum BindingClass : byte { Entity, Cell, Direction }

    // projectile-emission-pattern unit 0 — MovementKind → 바인딩 클래스 순수 분류.
    // emitter 의 분기 축(README 계약 11): 개별 MovementKind 로 분기하면 새 이동
    // 수학마다 emitter 가 자란다. 기존 바인딩으로 분류되는 새 궤적(나선 추적,
    // 사인 스트레이프, 오비트 등)은 emitter 변경 0 으로 발사된다.
    public static class MovementBinding
    {
        // C# 은 enum switch 의 전수성을 컴파일 시점에 강제하지 못한다. 그래서
        // 분류 누락은 EditMode 핀으로 잡는다 — MovementBindingTests 가 이 상수와
        // Enum.GetValues 길이를 대조하므로, 새 MovementKind 를 추가하면 테스트가
        // 실패하고 여기 분류를 갱신하게 된다.
        public const int KnownKindCount = 9;

        public static BindingClass Of(MovementKind kind)
        {
            switch (kind)
            {
                case MovementKind.HomingToEntity:
                case MovementKind.BezierHomingToEntity:
                // on-place-skill-rework unit 10 — 하늘낙하 × 적 조준. `SkyFall`(아래 Cell 목록)과
                // **그림은 같고 조준이 다르다** — 그 차이가 사는 곳이 바로 이 표다. 셀 낙하탄에
                // 임자를 실어 흉내내면 한 탄에 조준이 둘이 되어 예고 시간만큼 어긋난다(unit 8 결함).
                case MovementKind.SkyFallOnEntity:
                    return BindingClass.Entity;

                case MovementKind.BallisticArcToPoint:
                case MovementKind.SkyFall:
                case MovementKind.GrenadeToCell:
                // dreamcatcher-content-4 unit 0 — 궤도(화염구)는 **셀 바인딩**이다:
                // 궤도 중심이 발사 시점에 고정되고 타겟 엔티티를 잡지 않는다.
                // 위 주석이 예고한 "오비트 = emitter 변경 0" 이 실제로 성립하는 지점 —
                // 이 한 줄 말고 emitter 는 손대지 않는다.
                case MovementKind.OrbitAroundPoint:
                    return BindingClass.Cell;

                case MovementKind.DirectionalLinear:
                // dreamcatcher-content-5 unit 0 — 왕복(부메랑)은 **방향 바인딩**이다:
                // 타겟 엔티티도 착탄 셀도 잡지 않고 발사 축으로 나갔다 돌아온다.
                // 위 주석이 예고한 "기존 바인딩으로 분류되는 새 궤적은 emitter 변경 0"
                // 이 다시 성립하는 지점 — 이 한 줄 말고 emitter 는 손대지 않는다.
                case MovementKind.BoomerangReturn:
                    return BindingClass.Direction;

                default:
                    // 미분류 = 미개통으로 흐른다(emitter 가 loud warn 후 발사 소모).
                    // 조용한 오발사보다 눈에 보이는 경고가 낫다.
                    return BindingClass.Direction;
            }
        }
    }
}
