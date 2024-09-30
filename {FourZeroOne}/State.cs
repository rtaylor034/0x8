
using Perfection;
using System;


#nullable enable
namespace FourZeroOne
{
    using Token.Unsafe;
    using ResObj = Resolution.IResolution;
    using r = Core.Resolutions;
    using rb = Core.Resolutions.Objects.Board;
    public record State
    {
        public required PMap<VariableIdentifier, IOption<ResObj>> Variables { get; init; }
        public Updater<PMap<VariableIdentifier, IOption<ResObj>>> dVariables { init => Variables = value(Variables); }
        public required PList<Rule.IRule> Rules { get; init; }
        public Updater<PList<Rule.IRule>> dRules { init => Rules = value(Rules); }
        public required BoardState Board { get; init; }
        public Updater<BoardState> dBoard { init => Board = value(Board); }
        public State WithResolution(ResObj resolution) { return resolution.ChangeState(this); }
    }
    public record BoardState
    {
        public readonly PIndexedSet<int, rb.Unit> Units;
        public readonly PIndexedSet<int, rb.Hex> Hexes;
        public readonly PIndexedSet<int, rb.Player> Players;
        public Updater<PIndexedSet<int, rb.Unit>> dUnits { init => Units = value(Units); }
        public Updater<PIndexedSet<int, rb.Hex>> dHexes { init => Hexes = value(Hexes); }
        public Updater<PIndexedSet<int, rb.Player>> dPlayers { init => Players = value(Players); }
        public BoardState()
        {
            Units = new(unit => unit.UUID, 13);
            Hexes = new(hex => hex.UUID, 367);
            Players = new(player => player.UUID, 7);
        }
    }
}