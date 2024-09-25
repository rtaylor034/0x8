
using Perfection;
using System.Collections.Generic;

#nullable enable
namespace FourZeroOne.Core.Resolutions
{
    using ResObj = Resolution.IResolution;
    using Token;
    using Resolution;

    namespace Objects
    {
        namespace Board
        {
            using Resolution.Board;
            using Objects;
            public sealed record Coordinates : NoOp
            {
                public required int R { get; init; }
                public required int U { get; init; }
                public required int D { get; init; }
                public int this[int i] => i switch
                {
                    0 => R,
                    1 => U,
                    2 => D,
                    _ => throw new System.IndexOutOfRangeException("Attempted to index Coordinates out of 0..2 range.")
                };
                public override string ToString() => $"({R}.{U}.{D})";
                public Coordinates Add(Coordinates b) => new()
                {
                    R = R + b.R,
                    U = U + b.U,
                    D = D + b.D,
                };
                public Coordinates ScalarManipulate(System.Func<int, int> scalarFunction) => new()
                {
                    R = scalarFunction(R),
                    U = scalarFunction(U),
                    D = scalarFunction(D)
                };
            }
            public abstract record Hex : NoOp, IPositioned, IStateTracked
            {
                public int UUID => _uuid;
                public required Coordinates Position { get; init; }
                public Updater<Coordinates> dPosition { init => Position = value(Position); }
                public Hex(int id)
                {
                    _uuid = id;
                }
                public override bool ResEqual(IResolution? other)
                {
                    return (other is Hex h && Position.ResEqual(h.Position));
                }
                private readonly int _uuid;
            }
            public sealed record Unit : NoOp, IPositioned, IStateTracked
            {
                public int UUID => _uuid;
                public required Number HP { get; init; }
                public Updater<Number> dHP { init => HP = value(HP); }
                public required Coordinates Position { get; init; }
                public Updater<Coordinates> dPosition { init => Position = value(Position); }
                public required Multi<Effect> Effects { get; init; }
                public Updater<Multi<Effect>> dEffects { init => Effects = value(Effects); }
                public required Player Owner { get; init; }
                public Updater<Player> dOwner { init => Owner = value(Owner); }
                public Unit(int id)
                {
                    _uuid = id;
                }
                public override bool ResEqual(IResolution? other)
                {
                    return (other is Unit u && UUID == u.UUID);
                }
                public sealed record Effect : NoOp
                {
                    public readonly string Identity;
                    public Effect(string identity)
                    {
                        Identity = identity;
                    }
                }
                private readonly int _uuid;
            }
            public sealed record Player : NoOp, IStateTracked
            {
                public int UUID => _uuid;
                public Player(int id)
                {
                    _uuid = id;
                }
                private readonly int _uuid;
            }
        }
        public sealed record BoxedToken<R> : NoOp where R : class, ResObj
        {
            public required IToken<R> Token { get; init; }
            public override string ToString() => $"{Token}!";
        }
        public sealed record Number : NoOp
        {
            public required int Value { get; init; }
            public Updater<int> dValue { init => Value = value(Value); }
            public static implicit operator Number(int value) => new() { Value = value };
            public override string ToString() => $"{Value}";
        }
        public sealed record Bool : NoOp
        {
            public required bool IsTrue { get; init; }
            public Updater<bool> dIsTrue { init => IsTrue = value(IsTrue); }
            public static implicit operator Bool(bool value) => new() { IsTrue = value };
            public override string ToString() => $"{IsTrue}";
        }
    }
    namespace Actions
    {
        namespace Board
        {
            namespace Unit
            {
                //HPChange...
            }
        }
        public sealed record VariableAssign<R> : Operation where R : class, ResObj
        {
            public readonly VariableIdentifier<R> Identifier;
            public required IOption<R> Object { get; init; }
            public Updater<IOption<R>> dObject { init => Object = value(Object); }
            public VariableAssign(VariableIdentifier<R> identifier)
            {
                Identifier = identifier;
            }
            protected override State UpdateState(State state) => state with
            {
                dVariables = Q => Q with
                {
                    dElements = Q => Q.Also(((Token.Unsafe.VariableIdentifier)Identifier, (IOption<ResObj>)Object).Yield())
                }
            };
            public override string ToString() => $"{Identifier}<-{Object}";
        }
        public sealed record RuleAdd : Operation
        {
            public required Rule.IRule Rule { get; init; }
            public Updater<Rule.IRule> dRule { init => Rule = value(Rule); }

            protected override State UpdateState(State state) => state with
            {
                dRules = Q => Q with { dElements = Q => Q.Also(Rule.Yield()) }
            };
        }
    }
    public sealed record Multi<R> : Operation, IMulti<R> where R : class, ResObj
    {
        public int Count => _list.Count;
        public required IEnumerable<R> Values { get => _list.Elements; init => _list = new() { Elements = value }; }
        public Updater<IEnumerable<R>> dValues { init => Values = value(Values); }
        public override bool ResEqual(ResObj? other)
        {
            if (other is not IMulti<R> othermulti) return false;
            foreach (var (a, b) in Values.ZipLong(othermulti.Values)) if (a is null || (a is not null && !a.ResEqual(b))) return false;
            return true;
        }
        protected override State UpdateState(State state)
        {
            return Values.AccumulateInto(state, (p, x) => p.WithResolution(x));
        }

        private readonly PList<R> _list;
        public override string ToString()
        {
            List<R> argList = [.. _list.Elements];
            return $"[{argList[0]}{argList[1..].AccumulateInto("", (msg, v) => $"{msg}, {v}")}]";
        }
    }
}