using System;
using System.Collections.Generic;

namespace TowerLand.Tower
{
    public readonly struct CellCoord : IEquatable<CellCoord>
    {
        public readonly int Column;
        public readonly int Floor;

        public CellCoord(int column, int floor)
        {
            Column = column;
            Floor = floor;
        }

        public bool Equals(CellCoord other) => Column == other.Column && Floor == other.Floor;
        public override bool Equals(object obj) => obj is CellCoord other && Equals(other);
        public override int GetHashCode() => (Column * 397) ^ Floor;
        public override string ToString() => $"({Column}, {Floor})";
    }

    /// <summary>
    /// Cylindrical grid of cube colours. Columns wrap around the tower, floors do not.
    /// A cell holds a palette colour index, or <see cref="Empty"/>.
    /// </summary>
    public sealed class TowerGrid
    {
        public const int Empty = -1;

        private readonly int[] _cells;

        public int Columns { get; }
        public int Floors { get; }
        public int CellCount => _cells.Length;

        public TowerGrid(int columns, int floors)
        {
            if (columns < 3) throw new ArgumentOutOfRangeException(nameof(columns), "A tower needs at least 3 columns.");
            if (floors < 1) throw new ArgumentOutOfRangeException(nameof(floors), "A tower needs at least 1 floor.");

            Columns = columns;
            Floors = floors;
            _cells = new int[columns * floors];
            Array.Fill(_cells, Empty);
        }

        public int this[int column, int floor]
        {
            get => _cells[IndexOf(column, floor)];
            set => _cells[IndexOf(column, floor)] = value;
        }

        public int this[CellCoord cell]
        {
            get => this[cell.Column, cell.Floor];
            set => this[cell.Column, cell.Floor] = value;
        }

        public int WrapColumn(int column) => ((column % Columns) + Columns) % Columns;

        public int IndexOf(int column, int floor)
        {
            if (floor < 0 || floor >= Floors) throw new ArgumentOutOfRangeException(nameof(floor));
            return floor * Columns + WrapColumn(column);
        }

        public int IndexOf(CellCoord cell) => IndexOf(cell.Column, cell.Floor);

        public CellCoord CoordOf(int index) => new CellCoord(index % Columns, index / Columns);

        public bool IsEmpty(CellCoord cell) => this[cell] == Empty;

        /// <summary>Appends the 4-connected neighbours of <paramref name="cell"/> (left/right wrap, up/down clamp).</summary>
        public void GetNeighbours(CellCoord cell, List<CellCoord> results)
        {
            results.Add(new CellCoord(WrapColumn(cell.Column - 1), cell.Floor));
            results.Add(new CellCoord(WrapColumn(cell.Column + 1), cell.Floor));
            if (cell.Floor > 0) results.Add(new CellCoord(cell.Column, cell.Floor - 1));
            if (cell.Floor < Floors - 1) results.Add(new CellCoord(cell.Column, cell.Floor + 1));
        }

        public int CountFilled()
        {
            int count = 0;
            foreach (int colour in _cells)
                if (colour != Empty) count++;
            return count;
        }
    }
}
