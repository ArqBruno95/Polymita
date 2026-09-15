using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
namespace Polymita
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
            var heights = sections.Select(s => 26 + Rows(s.Items, columns) * Cell).ToArray();
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
                int row=0, column=0;
                foreach (var item in sections[s].Items)
                {
                    if (item.IsAction)
                    {
                        if (column>0) { row++; column=0; }
                        Cells.Add(new GridCell { Item=item, Bounds=new Rectangle(x,y+row*Cell,columns*Cell,Cell) });
                        row++;
                    }
                    else
                    {
                        Cells.Add(new GridCell { Item=item, Bounds=new Rectangle(x+column*Cell,y+row*Cell,Cell,Cell) });
                        if (++column==columns) { row++; column=0; }
                    }
                }
                y += heights[s] - 22; height = Math.Max(height, y);
            }
            Size = new Size(columns * Cell * banks + (banks - 1) * 16, height);
        }
        private static int Rows(IList<ShelfItem> items, int columns)
        {
            int rows=0, column=0;
            foreach (var item in items)
                if (item.IsAction) { if (column>0) { rows++; column=0; } rows++; }
                else if (++column==columns) { rows++; column=0; }
            return Math.Max(1, rows+(column>0 ? 1 : 0));
        }
    }
}

