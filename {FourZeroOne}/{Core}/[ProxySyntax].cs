using System.Collections.Generic;
using Perfection;

namespace FourZeroOne.Core.ProxySyntax
{
    using IToken = Token.Unsafe.IToken;
    using r = Resolutions;
    using ResObj = Resolution.IResolution;
    using Proxies;
    using Proxy;
    public interface IOriginalHint<TOrig, out TOrig_> where TOrig : IToken where TOrig_ : IToken { }
    public sealed record RHint<R> where R : class, ResObj
    {
        public static RHint<R> Hint() => new();
    }
    public sealed record OriginalHint<TOrig> : IOriginalHint<TOrig, TOrig> where TOrig : IToken { }
    namespace ProxyStructure
    {
        public sealed record IfElse<TOrig, R> where TOrig : IToken where R : class, ResObj
        {
            public IProxy<TOrig, r.Action<R>> Then { get; init; }
            public IProxy<TOrig, r.Action<R>> Else { get; init; }
        }
        public sealed record RecursiveCall<RArg1, RArg2, RArg3, ROut>
            where RArg1 : class, ResObj
            where RArg2 : class, ResObj
            where RArg3 : class, ResObj
            where ROut : class, ResObj
        {
            public IProxy<Tokens.Recursive<RArg1, RArg2, RArg3, ROut>, RArg1> A { get; init; }
            public IProxy<Tokens.Recursive<RArg1, RArg2, RArg3, ROut>, RArg2> B { get; init; }
            public IProxy<Tokens.Recursive<RArg1, RArg2, RArg3, ROut>, RArg3> C { get; init; }
        }
        public sealed record RecursiveCall<RArg1, RArg2, ROut>
            where RArg1 : class, ResObj
            where RArg2 : class, ResObj
            where ROut : class, ResObj
        {
            public IProxy<Tokens.Recursive<RArg1, RArg2, ROut>, RArg1> A { get; init; }
            public IProxy<Tokens.Recursive<RArg1, RArg2, ROut>, RArg2> B { get; init; }
        }
        public sealed record RecursiveCall<RArg1, ROut>
            where RArg1 : class, ResObj
            where ROut : class, ResObj
        {
            public IProxy<Tokens.Recursive<RArg1, ROut>, RArg1> A { get; init; }
        }
        public sealed record SubEnvironment<TOrig, R> where TOrig : IToken where R : class, ResObj
        {
            public Proxy.IProxy<TOrig, Resolution.IMulti<ResObj>> EnvironmentProxy { get; init; }
            public IProxy<TOrig, R> SubProxy { get; init; }
        }
    }
    public static class MakeProxy
    {
        public static IProxy<TOrig, ROut> Statement<TOrig, ROut>(System.Func<OriginalHint<TOrig>, IProxy<TOrig, ROut>> statement) where TOrig : Token.IToken<ROut> where ROut : class, ResObj
        { return statement(new()); }
        public static Rule.Rule<TOrig, ROut> AsRuleFor<TOrig, ROut>(System.Func<OriginalHint<TOrig>, IProxy<TOrig, ROut>> statement) where TOrig : Token.IToken<ROut> where ROut : class, ResObj
        { return new(statement(new())); }
    }
    public static class _Extensions
    {
        public static Direct<TOrig, R> pDirect<TOrig, R>(this Token.IToken<R> token, OriginalHint<TOrig> _) where TOrig : IToken where R : class, ResObj
        { return new(token); }
        public static OriginalArg1<TOrig, R> pOriginalA<TOrig, R>(this IOriginalHint<TOrig, Token.Unsafe.IHasArg1<R>> _) where TOrig : Token.Unsafe.IHasArg1<R> where R : class, ResObj
        { return new(); }
        public static OriginalArg2<TOrig, R> pOriginalB<TOrig, R>(this IOriginalHint<TOrig, Token.Unsafe.IHasArg2<R>> _) where TOrig : Token.Unsafe.IHasArg2<R> where R : class, ResObj
        { return new(); }
        public static OriginalArg3<TOrig, R> pOriginalC<TOrig, R>(this IOriginalHint<TOrig, Token.Unsafe.IHasArg3<R>> _) where TOrig : Token.Unsafe.IHasArg3<R> where R : class, ResObj
        { return new(); }

        public static SubEnvironment<TOrig, R> pSubEnvironment<TOrig, R>(this OriginalHint<TOrig> _, RHint<R> __, ProxyStructure.SubEnvironment<TOrig, R> block) where TOrig : IToken where R : class, ResObj
        {
            return new(block.EnvironmentProxy)
            {
                SubTokenProxy = block.SubProxy
            };
        }
        public static Combiner<Tokens.Multi.Union<R>, TOrig, Resolution.IMulti<R>, r.Multi<R>> pArrayOf<TOrig, R>(this OriginalHint<TOrig> _, RHint<R> __, List<IProxy<TOrig, R>> array) where TOrig : IToken where R : class, ResObj
        {
            return array.pToMulti();
        }

        public static RecursiveCall<RArg1, ROut> pRecurseWith<RArg1, ROut>(this OriginalHint<Tokens.Recursive<RArg1, ROut>> _, ProxyStructure.RecursiveCall<RArg1, ROut> block)
             where RArg1 : class, ResObj
            where ROut : class, ResObj
        { return new(block.A); }
        public static RecursiveCall<RArg1, RArg2, ROut> pRecurseWith<RArg1, RArg2, ROut>(this OriginalHint<Tokens.Recursive<RArg1, RArg2, ROut>> _, ProxyStructure.RecursiveCall<RArg1, RArg2, ROut> block)
             where RArg1 : class, ResObj
            where RArg2 : class, ResObj
            where ROut : class, ResObj
        { return new(block.A, block.B); }
        public static RecursiveCall<RArg1, RArg2, RArg3, ROut> pRecurseWith<RArg1, RArg2, RArg3, ROut>(this OriginalHint<Tokens.Recursive<RArg1, RArg2, RArg3, ROut>> _, ProxyStructure.RecursiveCall<RArg1, RArg2, RArg3, ROut> block)
             where RArg1 : class, ResObj
            where RArg2 : class, ResObj
            where RArg3 : class, ResObj
            where ROut : class, ResObj
        { return new(block.A, block.B, block.C); }

        public static Variable<TOrig, R> pAsVariable<TOrig, R>(this IProxy<TOrig, R> value, out Token.VariableIdentifier<R> identifier) where TOrig : IToken where R : class, ResObj
        {
            identifier = new();
            return new(identifier, value);
        }
        public static ToAction<TOrig, R> pAsAction<TOrig, R>(this IProxy<TOrig, R> proxy) where TOrig : IToken where R : class, ResObj
        {
            return new(proxy);
        }
        public static IfElse<TOrig, R> pIfTrue<TOrig, R>(this IProxy<TOrig, r.Bool> condition, RHint<R> _, ProxyStructure.IfElse<TOrig, R> block) where TOrig : IToken where R : class, ResObj
        {
            return new(condition)
            {
                PassProxy = block.Then,
                FailProxy = block.Else
            };
        }

        public static Function<Tokens.Multi.Exclusion<R>, TOrig, Resolution.IMulti<R>, Resolution.IMulti<R>, r.Multi<R>> pWithout<TOrig, R>(this IProxy<TOrig, Resolution.IMulti<R>> source, IProxy<TOrig, Resolution.IMulti<R>> values) where TOrig : IToken where R : class, ResObj
        { return new(source, values); }
        public static Function<Tokens.Multi.Count, TOrig, Resolution.IMulti<ResObj>, r.Number> pCount<TOrig>(this IProxy<TOrig, Resolution.IMulti<ResObj>> source) where TOrig : IToken
        { return new(source); }
        public static Function<Tokens.Multi.Yield<R>, TOrig, R, r.Multi<R>> pYield<TOrig, R>(this IProxy<TOrig, R> source) where TOrig : IToken where R : class, ResObj
        { return new(source); }
        public static Combiner<Tokens.Multi.Union<R>, TOrig, Resolution.IMulti<R>, r.Multi<R>> pToMulti<TOrig, R>(this IEnumerable<IProxy<TOrig, R>> values) where TOrig : IToken where R : class, ResObj
        { return new(values.Map(x => x.pYield())); }
        public static Combiner<Tokens.Multi.Union<R>, TOrig, Resolution.IMulti<R>, r.Multi<R>> pUnioned<TOrig, R>(this IEnumerable<IProxy<TOrig, Resolution.IMulti<R>>> values) where TOrig : IToken where R : class, ResObj
        { return new(values); }

        public static Function<Tokens.Number.Add, TOrig, r.Number, r.Number, r.Number> pAdd<TOrig>(this IProxy<TOrig, r.Number> a, IProxy<TOrig, r.Number> b) where TOrig : IToken
        { return new(a, b); }
        public static Function<Tokens.Number.Subtract, TOrig, r.Number, r.Number, r.Number> pSubtract<TOrig>(this IProxy<TOrig, r.Number> a, IProxy<TOrig, r.Number> b) where TOrig : IToken
        { return new(a, b); }
        public static Function<Tokens.Number.Multiply, TOrig, r.Number, r.Number, r.Number> pMultiply<TOrig>(this IProxy<TOrig, r.Number> a, IProxy<TOrig, r.Number> b) where TOrig : IToken
        { return new(a, b); }
        public static Function<Tokens.Number.Negate, TOrig, r.Number, r.Number> pNegative<TOrig>(this IProxy<TOrig, r.Number> a) where TOrig : IToken
        { return new(a); }
        public static Function<Tokens.Number.Compare.GreaterThan, TOrig, r.Number, r.Number, r.Bool> pIsGreaterThan<TOrig>(this IProxy<TOrig, r.Number> a, IProxy<TOrig, r.Number> b) where TOrig : IToken
        { return new(a, b); }

        public static Function<Tokens.IO.Select.One<R>, TOrig, Resolution.IMulti<R>, R> pIO_SelectOne<TOrig, R>(this IProxy<TOrig, Resolution.IMulti<R>> source) where TOrig : IToken where R : class, ResObj
        { return new(source); }
        public static Function<Tokens.IO.Select.Multiple<R>, TOrig, Resolution.IMulti<R>, r.Number, r.Multi<R>> pIO_SelectMany<TOrig, R>(this IProxy<TOrig, Resolution.IMulti<R>> source, IProxy<TOrig, r.Number> count) where TOrig : IToken where R : class, ResObj
        { return new(source, count); }

        public static Function<Tokens.PerformAction<R>, TOrig, r.Action<R>, R> pPerform<TOrig, R>(this IProxy<TOrig, r.Action<R>> action) where TOrig : IToken where R : class, ResObj
        { return new(action); }
    }
}