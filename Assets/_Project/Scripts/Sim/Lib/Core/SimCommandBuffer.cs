using System;
using System.Collections.Generic;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-A — "루프 중 기록, 루프 후 적용"(청사진 ③ §5).
    ///
    /// 구 sim 실측: ECB 를 쓰는 시스템이 **28개**이고 전부 `Allocator.Temp` + 같은 `OnUpdate` 내
    /// Playback 이다(시스템 ECB·지연 재생 0). 즉 지연 범위는 **한 phase 안**이고 틱을 넘지 않는다.
    ///
    /// **"루프 중 즉시 적용" 으로 바꾸면 안 되는 이유**는 성능이 아니다 — 같은 엔티티에 2연산이
    /// 걸리는 함정(`ModifierApplySystem` 선례)과 순회 중 컬렉션 변경 계열 버그가 **재현된다**.
    /// 그 재현이 계약이다. 신 sim 이 "더 낫게" 고치면 골든이 갈린다.
    ///
    /// 재사용 규약: phase 마다 새로 만들거나 <see cref="Clear"/> 후 재사용한다. Playback 은
    /// **기록 순서대로** 적용한다 — 같은 엔티티에 add→remove 가 쌓이면 마지막이 이긴다.
    ///
    /// ## 왜 클로저가 아니라 값 기록인가 (18-N2, 3렌즈 리뷰 F4)
    ///
    /// 초판은 `_ops.Add(w => w.Set(e, value))` 였다. 그러면 **op 하나당 힙 객체 2개**(캡처
    /// display class + 델리게이트)가 생긴다. 구 sim 의 `EntityConstraintCommandBuffer(Allocator.Temp)`
    /// 는 **GC 할당 0** 이었고, 호출처가 전부 hot path 다 — 착탄마다·투사체 만료마다·발사 1발마다·
    /// 파괴 6지점. 모바일/IL2CPP 기준 이식으로 생긴 가장 큰 거동 회귀였다.
    ///
    /// ⇒ 연산을 **struct 로 기록**하고 페이로드는 타입별 재사용 리스트에 넣는다.
    /// `_ops` 가 전역 순서를 들고 있으므로 **기록 순서 계약은 그대로**다(타입별로 나눠 재생하면
    /// 그 계약이 깨진다 — 그래서 타입별 버킷만 두는 설계는 쓰지 않았다).
    ///
    /// ⚠ <see cref="Defer"/> 만은 여전히 델리게이트다. 위 3종으로 표현 안 되는 구조 변경
    /// (엔티티 생성·버퍼 제거)에 쓰고, 호출처가 2곳뿐이라 남겨 뒀다. **hot path 에 새로 쓰지 말 것** —
    /// 쓸 일이 생기면 그 연산을 여기에 정식 op 로 추가하는 쪽이 맞다.
    /// </summary>
    public sealed class SimCommandBuffer
    {
        private enum OpKind : byte { Set, RemoveComponent, Destroy, Defer }

        private readonly struct Op
        {
            public readonly OpKind Kind;
            public readonly SimEntityId Entity;
            /// <see cref="_stores"/> 의 index (Set/RemoveComponent) 또는 <see cref="_deferred"/> 의 index(Defer).
            public readonly int Store;
            /// 타입별 페이로드 리스트 안의 index. `RemoveComponent`·`Destroy` 는 -1.
            public readonly int Item;

            public Op(OpKind kind, SimEntityId entity, int store, int item)
            {
                Kind = kind; Entity = entity; Store = store; Item = item;
            }
        }

        /// 타입 소거된 페이로드 저장소. 구현체는 <see cref="PayloadStore{T}"/> 하나지만,
        /// `_ops` 가 타입을 모른 채 순서를 들고 있어야 해서 이 간접이 필요하다.
        private interface IPayloadStore
        {
            void ApplySet(SimWorld world, SimEntityId entity, int item);
            void ApplyRemove(SimWorld world, SimEntityId entity);
            void Clear();
        }

        private sealed class PayloadStore<T> : IPayloadStore where T : struct
        {
            /// 재사용된다 — `Clear` 는 Count 만 0 으로 만들고 용량은 유지한다.
            public readonly List<T> Items = new List<T>();
            public void ApplySet(SimWorld world, SimEntityId entity, int item) => world.Set(entity, Items[item]);
            public void ApplyRemove(SimWorld world, SimEntityId entity) => world.RemoveComponent<T>(entity);
            public void Clear() => Items.Clear();
        }

        private readonly List<Op> _ops = new List<Op>();
        private readonly List<IPayloadStore> _stores = new List<IPayloadStore>();
        private readonly Dictionary<Type, int> _storeIndex = new Dictionary<Type, int>();
        private readonly List<Action<SimWorld>> _deferred = new List<Action<SimWorld>>();

        public int Count => _ops.Count;

        private PayloadStore<T> Store<T>(out int index) where T : struct
        {
            if (!_storeIndex.TryGetValue(typeof(T), out index))
            {
                index = _stores.Count;
                _storeIndex[typeof(T)] = index;
                _stores.Add(new PayloadStore<T>());
            }
            return (PayloadStore<T>)_stores[index];
        }

        public void Set<T>(SimEntityId e, T value) where T : struct
        {
            var store = Store<T>(out int si);
            store.Items.Add(value);
            _ops.Add(new Op(OpKind.Set, e, si, store.Items.Count - 1));
        }

        public void RemoveComponent<T>(SimEntityId e) where T : struct
        {
            Store<T>(out int si);
            _ops.Add(new Op(OpKind.RemoveComponent, e, si, -1));
        }

        /// <summary>
        /// ⚠ **P12(UnitLifecycle)의 파괴 루프에서만 기록한다** — 다만 그 계약의 적용 범위는
        /// `DeadTag` 를 가진 **유닛**이다. 수명 만료 계열(해저드·장애물)과 투사체는 자기 phase 에서
        /// 즉시 파괴하며, 그건 구 sim 의 동작이다(`SimWorld.Destroy` 주석).
        /// 여기서 막지 않는 이유는 버퍼가 호출 지점을 알 수 없어서이고, 대신 phase 배치가 규율을 진다.
        /// </summary>
        public void Destroy(SimEntityId e) => _ops.Add(new Op(OpKind.Destroy, e, -1, -1));

        /// <summary>
        /// 임의 지연 연산. 위 3종으로 표현 안 되는 구조 변경(엔티티 생성·버퍼 제거)에.
        /// ⚠ **델리게이트 할당이 생긴다** — 클래스 주석의 마지막 문단 참조.
        /// </summary>
        public void Defer(Action<SimWorld> op)
        {
            _deferred.Add(op);
            _ops.Add(new Op(OpKind.Defer, default, _deferred.Count - 1, -1));
        }

        public void Playback(SimWorld world)
        {
            try
            {
                for (int i = 0; i < _ops.Count; i++)
                {
                    var op = _ops[i];
                    switch (op.Kind)
                    {
                        case OpKind.Set: _stores[op.Store].ApplySet(world, op.Entity, op.Item); break;
                        case OpKind.RemoveComponent: _stores[op.Store].ApplyRemove(world, op.Entity); break;
                        case OpKind.Destroy: world.Destroy(op.Entity); break;
                        case OpKind.Defer: _deferred[op.Store](world); break;
                    }
                }
            }
            finally
            {
                // ⚠ `finally` 다 — 중간에 던져도 기록이 **다음 틱으로 이월되지 않는다**.
                //   구 sim 의 ECB 는 프레임 로컬이라 그 사고가 구조적으로 불가능했다.
                Clear();
            }
        }

        public void Clear()
        {
            _ops.Clear();
            _deferred.Clear();
            for (int i = 0; i < _stores.Count; i++) _stores[i].Clear();
        }
    }
}
