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
        public const int KnownKindCount = 6;

        public static BindingClass Of(MovementKind kind)
        {
            switch (kind)
            {
                case MovementKind.HomingToEntity:
                case MovementKind.BezierHomingToEntity:
                    return BindingClass.Entity;

                case MovementKind.BallisticArcToPoint:
                case MovementKind.SkyFall:
                case MovementKind.GrenadeToCell:
                    return BindingClass.Cell;

                case MovementKind.DirectionalLinear:
                    return BindingClass.Direction;

                default:
                    // 미분류 = 미개통으로 흐른다(emitter 가 loud warn 후 발사 소모).
                    // 조용한 오발사보다 눈에 보이는 경고가 낫다.
                    return BindingClass.Direction;
            }
        }
    }
}
