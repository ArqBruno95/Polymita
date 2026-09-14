using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
namespace WireShelf
{
    public sealed class GridCell { public ShelfItem Item; public Rectangle Bounds; }
    public sealed class GridHeading { public string Title; public Rectangle Bounds; }
    public sealed class ShelfGridLayout
    {
        public readonly List<GridCell> Cells = new List<GridCell>();
        public readonly List<GridHeading> Headings = new List<GridHeading>();
        public Size Size;
        public const int Cell = 36;
        public ShelfGridLayout(IList<ShelfSection> sections, int columns, int banks)
        {
            columns = Math.Max(1, columns);
            var heights = sections.Select(s => 26 + Math.Max(1, (s.Items.Count + columns - 1) / columns) * Cell).ToArray();
            var split = sections.Count;
            if (banks == 2) {
                var best = int.MaxValue;
                for (var i = 1; i < sections.Count; i++) {
                    var h = Math.Max(heights.Take(i).Sum(), heights.Skip(i).Sum());
                    if (h < best) { best = h; split = i; }
                }
            }
            var x = 0; var y = 0; var height = 0;
            for (var s = 0; s < sections.Count; s++) {
                if (s == split) { x = columns * Cell + 16; y = 0; }
                Headings.Add(new GridHeading { Title = sections[s].Title, Bounds = new Rectangle(x, y, columns * Cell, 22) });
                y += 22;
                for (var i = 0; i < sections[s].Items.Count; i++)
                    Cells.Add(new GridCell { Item = sections[s].Items[i], Bounds = new Rectangle(x + i % columns * Cell, y + i / columns * Cell, Cell, Cell) });
                y += heights[s] - 22; height = Math.Max(height, y);
            }
            Size = new Size(columns * Cell * banks + (banks - 1) * 16, height);
        }
    }
}

