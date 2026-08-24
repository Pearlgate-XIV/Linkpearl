using Linkpearl.Geometry;

namespace Linkpearl.Canvas.Layout;

public readonly struct TileGrid
{
    public readonly int Columns;
    public readonly int Rows;
    public readonly Vector2 CellSize;
    public readonly float Gap;
    public readonly Rect Area;

    public TileGrid(Rect area, int columns, int rows, float gap)
    {
        Area = area;
        Columns = Math.Max(columns, 1);
        Rows = Math.Max(rows, 1);
        Gap = gap;
        var cellWidth = (area.Width - gap * (Columns - 1)) / Columns;
        var cellHeight = (area.Height - gap * (Rows - 1)) / Rows;
        CellSize = new Vector2(cellWidth, cellHeight);
    }

    public Rect Cell(int column, int row)
    {
        var origin = Area.Min + new Vector2(column * (CellSize.X + Gap), row * (CellSize.Y + Gap));
        return Rect.FromSize(origin, CellSize);
    }

    public Rect CellAt(int index) => Cell(index % Columns, index / Columns);

    public int CapacityPerPage => Columns * Rows;
}
