using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Test & sửa đợt H8: docs/ban-giao/nang-cap-h8-test.md.</summary>
    public class NangCapH8TestSuaTests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        [Fact]
        public void H8T01_the_beam_section_is_drawn_at_its_own_scale()
        {
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "StructureTools.cs");
            Assert.Contains("double k = (double)scale / H8Numbers.SectionScale;", src);
            Assert.Contains("pen.Rect(sx0, sy0, S(b), S(h), pen.Conc);", src);
            Assert.Equal(20, H8Numbers.SectionScale);
            Assert.Equal(50, H8Numbers.ElevationScale);
        }

        [Fact]
        public void H8T02_the_stirrup_label_gives_both_spacings()
        {
            var w = new List<string>();
            var k = RebarLogic.BeamBars(220, 400, 5000, (2, 18), (2, 16), 6, 150, 200, null, w);
            Assert.Equal("Ø6a150/a200", k[2].Label);
            var same = RebarLogic.BeamBars(220, 400, 5000, (2, 18), (2, 16), 6, 150, 150, null, w);
            Assert.Equal("Ø6a150", same[2].Label);
        }

        [Fact]
        public void H8T03_column_bars_too_close_are_refused()
        {
            Assert.Throws<ToolException>(() => RebarLogic.ColumnBars(20, 300, 300, 25, 6, 25));
            Assert.Equal(8, RebarLogic.ColumnBars(8, 300, 400, 25, 6, 20).Count);
        }

        [Fact]
        public void H8T04_the_writer_fixes_stand()
        {
            // 2Ø18, span 5 m: 5000 + 2 × 30d (540) + 2 × L 10d (180) = 6440; the cover (25) is across the beam, not along it
            var w = new List<string>();
            var k = RebarLogic.BeamBars(220, 400, 5000, (2, 18), (2, 16), 6, 150, 200, null, w);
            Assert.Equal(6440, RebarLogic.RoundLength(k[0].LengthMm));
            Assert.Equal(5000 + 2 * 480 + 2 * 160, RebarLogic.RoundLength(k[1].LengthMm));
            Assert.Empty(w);
            // plain Ø10: hooks, no L
            var pk = RebarLogic.BeamBars(200, 300, 3000, (2, 10), (2, 10), 6, 150, 200, null, w);
            Assert.Equal(RebarLogic.RoundLength(3000 + 2 * 300 + 2 * RebarLogic.MainHook(10)), RebarLogic.RoundLength(pk[0].LengthMm));
            // a bar of 12.5 m: one lap of 40d added; 25 m: laps until they stay the same
            var one = RebarLogic.WithLaps(12500, 20, "dầm");
            Assert.Equal(1, one.splices); Assert.Equal(13300, one.lengthMm, 6);
            var col = RebarLogic.WithLaps(12500, 20, "cột");
            Assert.Equal(700, col.lapEach, 6);
            var two = RebarLogic.WithLaps(23000, 25, "dầm");
            Assert.Equal(2, two.splices); Assert.Equal(25000, two.lengthMm, 6);
            Assert.Equal(0, RebarLogic.WithLaps(11700, 20, "dầm").splices);
            // the same mark on two bar kinds: two rows and a warning; the same kind twice: one row, counts added
            var warn = new List<string>();
            var rows = RebarLogic.Schedule(new[]
            {
                new RebarMark { Mark = 1, Dia = 18, LengthMm = 6440, Count = 2, Shape = "s", Element = "dầm" },
                new RebarMark { Mark = 1, Dia = 16, LengthMm = 6280, Count = 2, Shape = "s", Element = "dầm" },
                new RebarMark { Mark = 2, Dia = 6, LengthMm = 1200, Count = 10, Shape = "đai", Element = "dầm" },
                new RebarMark { Mark = 2, Dia = 6, LengthMm = 1200, Count = 10, Shape = "đai", Element = "dầm" },
            }, warn);
            Assert.Equal(3, rows.Count);
            Assert.Single(warn);
            Assert.Equal(20, rows.Single(r => r.Mark == 2).Count);
            Assert.Equal(Math.Round(6.44 * 2 * 0.00617 * 18 * 18, 2), rows.First(r => r.Dia == 18).Kg);
        }

        [Fact]
        public void H8T05_standard_defaults_are_the_approved_ones()
        {
            Assert.Equal(25, RebarLogic.Cover("beam", 16));
            Assert.Equal(32, RebarLogic.Cover("beam", 32)); // never under the bar
            Assert.Equal(20, RebarLogic.Cover("slab", 8));
            Assert.Equal(75, RebarLogic.StirrupHook(6));
            Assert.Equal(75, RebarLogic.StirrupHook(10)); // 6d = 60 < 75
            Assert.Equal(84, RebarLogic.StirrupHook(14), 9);
            Assert.Equal("CB240-T", RebarLogic.Grade(10));
            Assert.Equal("CB400-V", RebarLogic.Grade(12));
            Assert.Equal(0.222, Math.Round(RebarLogic.KgPerM(6), 3));
            Assert.Equal(6.32, Math.Round(RebarLogic.KgPerM(32), 2));
            Assert.Null(RebarLogic.WasteShare(null));
            Assert.NotNull(RebarLogic.CheckLength("lap", 300, 16)); // 20d = 320 > 300
            Assert.Null(RebarLogic.CheckLength("anchor", 270, 16));
        }
    }
}
