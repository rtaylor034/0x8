
using System.Collections.Generic;
using Perfection;
using ControlledFlows;
using System.Threading.Tasks;
using System.Diagnostics;
using MorseCode.ITask;
#nullable enable
namespace FourZeroOne.Runtime
{
    using ResObj = Resolution.IResolution;
    using Resolved = IOption<Resolution.IResolution>;
    using IToken = Token.Unsafe.IToken;
    using Token;
    public interface IRuntime
    {
        public IState GetState();
        public Task<Resolved> Run(IState startingState, IToken program);
        public ITask<IOption<R>> MetaExecute<R>(IToken<R> token, IEnumerable<(Resolution.Unsafe.IStateAddress, Resolved)> args) where R : class, ResObj;
        public ITask<IOption<IEnumerable<R>>> ReadSelection<R>(IEnumerable<R> from, int count) where R : class, ResObj;
    }

    public abstract class FrameSaving : IRuntime
    {
        
        public FrameSaving()
        {
            _stateStack = new None<LinkedStack<IState>>();
            _operationStack = new None<LinkedStack<IToken>>();
            _resolutionStack = new None<LinkedStack<Resolved>>();
            _runThread = ControlledFlow.Resolved((Resolved)(new None<ResObj>()));
            _frameStack = new None<LinkedStack<Frame>>();
            _appliedRuleStack = new None<LinkedStack<PList<RuleContainer>>>();
            _discontinueEval = false;
        }
        public async Task<Resolved> Run(IState startingState, IToken program)
        {
            if (!_runThread.Awaiter.IsCompleted) throw MakeInternalError("Run() was called while a previous run operation was still in progress on this instance. (FrameSaving instances can only run 1 evaluation at a time.)");

            
            _stateStack = new LinkedStack<IState>(startingState).AsSome();
            _operationStack = new LinkedStack<IToken>(program).AsSome();
            _runThread = new ControlledFlow<Resolved>();
            /* assert that these are already true.
            _resolutionStack = new None<LinkedStack<Resolved>>();
            _appliedRuleStack = new None<LinkedStack<PList<Rule.IRule>>>();
            _discontinueEval = false;
            */
            _frameStack = new None<LinkedStack<Frame>>();
            StoreFrame(program, new None<Resolved>());
            OnRunCall(startingState, program);
            StartEval();
            return await _runThread;
        }

        private void ResolveRun(Resolved resolution)
        {
            _runThread.Resolve(resolution);
        }
        public IState GetState() => _stateStack.Check(out var state) ? state.Value : throw MakeInternalError("No state exists on state stack?");
        public ITask<IOption<R>> MetaExecute<R>(IToken<R> token, IEnumerable<(Resolution.Unsafe.IStateAddress, Resolved)> metaPointers) where R : class, ResObj
        {
            var node = _operationStack.Unwrap();
            // directly replaces operation stack and state stack with token and arg variables.
            // is probably impure and may have to change implementation.
            _operationStack = (node with
            {
                Value = token
            }).AsSome();
            var currentState = _stateStack.Unwrap();
            _stateStack = (currentState with
            {
                dValue = Q => Q.WithObjectsUnsafe(metaPointers.FilterMap(x => x.Item2.RemapAs(confirmedData => (x.Item1, confirmedData))))
            }).AsSome();
            _discontinueEval = true;
            return new None<R>().ToCompletedITask();

        }
        public ITask<IOption<IEnumerable<R>>> ReadSelection<R>(IEnumerable<R> from, int count) where R : class, ResObj
        {
            var o = SelectionImplementation(from, count, out var frameShiftOpt);
            if (frameShiftOpt.Check(out var targetFrame)) GoToFrame(targetFrame);
            return o;
        }

        protected const int MAX_MACRO_EXPANSION_DEPTH = 24;

        protected abstract void OnRunCall(IState startingState, IToken program);
        protected abstract void RecieveToken(IToken token, int depth);
        protected abstract void RecieveResolution(IOption<ResObj> resolution, int depth);
        protected abstract void RecieveFrame(LinkedStack<Frame> frameStackNode);
        protected abstract void RecieveMacroExpansion(IToken macro, IToken expanded, int depth);
        protected abstract void RecieveRuleSteps(IEnumerable<(IToken token, Rule.IRule appliedRule)> steps);
        protected abstract ITask<IOption<IEnumerable<R>>> SelectionImplementation<R>(IEnumerable<R> from, int count, out IOption<LinkedStack<Frame>> orFrameShift) where R : class, ResObj;

        protected void GoToFrame(LinkedStack<Frame> frameStack)
        {
            var frame = frameStack.Value;
            _operationStack = frame.OperationStack;
            _resolutionStack = frame.ResolutionStack;
            _stateStack = frame.StateStack;
            _frameStack = frameStack.AsSome();
            _discontinueEval = true;
        }

        protected record Frame
        {
            public required IToken Token { get; init; }
            public required IOption<Resolved> Resolution { get; init; }
            public required IOption<LinkedStack<IState>> StateStack { get; init; }
            public required IOption<LinkedStack<IToken>> OperationStack { get; init; }
            public required IOption<LinkedStack<Resolved>> ResolutionStack { get; init; }
        }
        protected record LinkedStack<T>
        {
            public readonly IOption<LinkedStack<T>> Link;
            public readonly int Depth;
            public T Value { get; init; } 
            public Updater<T> dValue { init => Value = value(Value); }
            public LinkedStack(T value)
            {
                Value = value;
                Link = this.None();
                Depth = 0;
            }
            public IEnumerable<LinkedStack<T>> ThroughStack()
            {
                return (this.AsSome() as IOption<LinkedStack<T>>)
                    .Sequence(x => x.RemapAs(y => y.Link).Press())
                    .TakeWhile(x => x.IsSome())
                    .Map(x => x.Unwrap());
            }
            public static IOption<LinkedStack<T>> Linked(IOption<LinkedStack<T>> parent, int depth, IEnumerable<T> values)
            {
                return values.AccumulateInto(parent, (stack, x) => new LinkedStack<T>(stack, x, depth).AsSome());
            }
            public static IOption<LinkedStack<T>> Linked(IOption<LinkedStack<T>> parent, int depth, params T[] values) { return Linked(parent, depth, values.IEnumerable()); }
            private LinkedStack(IOption<LinkedStack<T>> link, T value, int depth)
            {
                Link = link;
                Value = value;
                Depth = depth;
            }
        }

        private readonly struct RuleContainer(int index, Rule.IRule rule)
        {
            public readonly int Index = index;
            public readonly Rule.IRule Rule = rule;
        }
        private async void StartEval()
        {
            while (_operationStack.Check(out var operationNode))
            {
                // _stateStack should never be empty, depth 0 is the starting state.
                var currentStateNode = _stateStack.Unwrap();
                // DEV: RuleContainer exists so 'Except' only excludes the exact rule that was applied if there exists duplicates.
                var rulesToApply = currentStateNode.Value.Rules.Enumerate().Map(x => new RuleContainer(x.index, x.value));

                if (_appliedRuleStack.Check(out var appliedRuleNode))
                {
                    for (int t = appliedRuleNode.Depth - currentStateNode.Depth; t > 0; t--)
                    {
                        _ = PopFromStack(ref _appliedRuleStack);
                    }
                    if ( _appliedRuleStack.Check(out appliedRuleNode) && appliedRuleNode is not null)
                    {
                        rulesToApply = rulesToApply.Except(appliedRuleNode.Value.Elements);
                    }
                }
                
                var ruledToken = ApplyRules(operationNode.Value, rulesToApply, out var appliedRules);

                rulesToApply = rulesToApply.Except(appliedRules.Elements.Map(x => x.container));

                for (int macroExpansions = 0; ruledToken is Macro.Unsafe.IMacro macro; macroExpansions++)
                {
                    if (macroExpansions > MAX_MACRO_EXPANSION_DEPTH) throw MakeInternalError("Max macro expansion depth exceeded (usually the result of a macro expansion loop).");
                    var expanded = macro.ExpandUnsafe();
                    RecieveMacroExpansion(macro, expanded, operationNode.Depth);
                    ruledToken = ApplyRules(expanded, rulesToApply, out var appliedPostMacro);
                    // Assert(appliedRules.Count = 0 || appliedPostMacro.Count = 0); logically right?
                    appliedRules = appliedRules with { dElements = Q => Q.Also(appliedPostMacro.Elements) };
                }
                RecieveRuleSteps(appliedRules.Elements.Map(x => (x.fromToken, x.container.Rule)));
                RecieveToken(ruledToken, operationNode.Depth);
                operationNode = operationNode with { Value = ruledToken };
                _operationStack = operationNode.AsSome();

                int argAmount = operationNode.Value.ArgTokens.Length;

                if (argAmount == 0 || (_resolutionStack.Check(out var resolutionNode) && resolutionNode.Depth == operationNode.Depth + 1))
                {
                    var argPass = new Resolved[argAmount];
                    for (int i = argAmount - 1; i >= 0; i--)
                    {
                        argPass[i] = PopFromStack(ref _resolutionStack).Value;
                    }
                    var resolution = await operationNode.Value.UnsafeResolve(this, argPass);
                    if (_discontinueEval)
                    {
                        _discontinueEval = false;
                        continue;
                    }
                    RecieveResolution(resolution, operationNode.Depth);

                    var poppedStateNode = PopFromStack(ref _stateStack);
                    if (_stateStack.Check(out var linkedStateNode) && linkedStateNode.Depth == poppedStateNode.Depth)
                    {
                        var newState = resolution.Check(out var notNolla)
                            ? poppedStateNode.Value.WithResolution(notNolla)
                            : poppedStateNode.Value;
                        _stateStack = (linkedStateNode with { Value = newState }).AsSome();
                    }
                    PushToStack(ref _resolutionStack, operationNode.Depth, resolution);
                    _ = PopFromStack(ref _operationStack);
                    StoreFrame(operationNode.Value, resolution.AsSome());
                }
                else
                {
                    PushToStack(ref _operationStack, operationNode.Depth + 1, operationNode.Value.ArgTokens.AsMutList().Reversed());
                    PushToStack(ref _stateStack, currentStateNode.Depth + 1, currentStateNode.Value.Yield(argAmount));
                    var previousRules = _appliedRuleStack.Check(out var ruleStack) ? ruleStack.Value.Elements : [];
                    PushToStack(ref _appliedRuleStack, operationNode.Depth + 1, new PList<RuleContainer>() { Elements = previousRules.Also(appliedRules.Elements.Map(x => x.container))});
                }
            }

            Debug.Assert(_resolutionStack.Check(out var finalNode) && !finalNode.Link.IsSome());
            ResolveRun(PopFromStack(ref _resolutionStack).Value);
        }
        private void StoreFrame(IToken token, IOption<Resolved> resolution)
        {
            var newFrame = new Frame()
            {
                Resolution = resolution,
                Token = token,
                StateStack = _stateStack,
                OperationStack = _operationStack,
                ResolutionStack = _resolutionStack,
            };
            PushToStack(ref _frameStack, 0, newFrame);
            RecieveFrame(_frameStack.Unwrap());
        }
        private static void PushToStack<T>(ref IOption<LinkedStack<T>> stack, int depth, IEnumerable<T> values)
        {
            stack = LinkedStack<T>.Linked(stack, depth, values);
        }
        private static LinkedStack<T> PopFromStack<T>(ref IOption<LinkedStack<T>> stack)
        {
            var o = stack.Check(out var popped) ? popped : throw MakeInternalError("Tried to pop from empty LinkedStack.");
            if (stack.Check(out var node)) stack = node.Link;
            return o;
        }
        private static void PushToStack<T>(ref IOption<LinkedStack<T>> stack, int depth, params T[] values) { PushToStack(ref stack, depth, values.IEnumerable()); }
        private static IToken ApplyRules(IToken token, IEnumerable<RuleContainer> rules, out PList<(IToken fromToken, RuleContainer container)> appliedRules)
        {
            var o = token;
            var appliedRulesList = new List<(IToken fromToken, RuleContainer container)>();
            foreach (var cont in rules)
            {
                if (cont.Rule.TryApply(o).Check(out var newToken))
                {
                    appliedRulesList.Add((o, cont));
                    o = newToken;
                }
            }
            appliedRules = new() { Elements = appliedRulesList };
            return o;
        }
        private static Exception MakeInternalError(string msg)
        {
            return new Exception($"[FrameSaving Runtime] {msg}");
        }
        private ControlledFlow<Resolved> _runThread;
        // I guess _appliedRuleStack could be a stack of normal IEnumerables, but PList has P in it
        private IOption<LinkedStack<PList<RuleContainer>>> _appliedRuleStack;
        private IOption<LinkedStack<IState>> _stateStack;
        private IOption<LinkedStack<Frame>> _frameStack;
        private IOption<LinkedStack<IToken>> _operationStack;
        private IOption<LinkedStack<Resolved>> _resolutionStack;
        private bool _discontinueEval;
    }
}