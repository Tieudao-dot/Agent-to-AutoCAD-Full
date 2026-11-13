using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Sửa sau kiểm tra độc lập nâng cấp (docs/ban-giao/kiem-tra-doc-lap-nang-cap.md), phần đợt C: KT2-02 (tìm &amp; thay với chữ
    /// trên layer khóa), KT2-04 (tiện ích 2D trong bản vẽ đơn vị mét), KT2-16 (PDF cũ báo là đã in), KT2-17 (khổ giấy từng
    /// layout), KT2-28 (mô tả spacing).
    /// </summary>
    public class NangCapCKt2Tests
    {
        private static string Src(params string[] parts) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));

        // ------------------------------------------------------------ KT2-02

        [Fact]
        public void Kt2_02_locked_texts_are_named_and_checked_before_any_change()
        {
            var msg = Util2DLogic.LockedTextMessage(new List<(string, string)> { ("2F4", "KHUNG-TEN"), ("2F5", "KHUNG-TEN") });
            Assert.Contains("nothing was replaced", msg);
            Assert.Contains("2F4 on KHUNG-TEN", msg);
            Assert.Contains("2 of the approved", msg);
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs");
            // every approved text is read first; a locked one stops the call before the first write
            int check = src.IndexOf("if (locked.Count > 0) throw new ToolException(ErrorCodes.Locked, Util2DLogic.LockedTextMessage(locked));", StringComparison.Ordinal);
            int write = src.IndexOf("WriteText(w, hit);", StringComparison.Ordinal);
            Assert.True(check > 0 && write > check);
            // opened for read, upgraded only after the check (ForWrite first made AutoCAD throw eOnLockedLayer)
            Assert.Contains("if (!w.IsWriteEnabled) w.UpgradeOpen();", src);
            Assert.DoesNotContain("GetObject(hit.Ent.ObjectId, OpenMode.ForWrite)", src);
            // the preview lists them apart and keeps them off the card
            Assert.Contains("res[\"locked\"]", src);
            Assert.Contains("all = all.Where(h => !lockedSet.Contains(", src);
            // an attribute is also blocked by its block's locked layer
            Assert.Contains("e is AttributeReference && ctx.Tr.GetObject(e.OwnerId, OpenMode.ForRead) is Entity owner && Locked(owner)", src);
        }

        [Fact]
        public void Kt2_32_every_attribute_of_a_block_is_checked()
        {
            // a block whose first attribute is fine and whose second one sits on a locked layer
            var hits = new List<(string, string, string, string)>
            {
                ("2A1", "MA", "2A0", null),
                ("2A2", "TEN", "2A0", "KHUNG-TEN"),
                ("2A2", "TEN", "2A0", "KHUNG-TEN"),
                ("2A3", "GHI_CHU", "2a0", "KHOA"),
            };
            var locked = Util2DLogic.LockedHits(hits);
            Assert.Equal(2, locked.Count);
            Assert.Equal(("2A2 (attribute TEN of block 2A0)", "KHUNG-TEN"), locked[0]);
            Assert.Equal(("2A3 (attribute GHI_CHU of block 2A0)", "KHOA"), locked[1]);
            Assert.Empty(Util2DLogic.LockedHits(new[] { ("1F", (string)null, (string)null, (string)null) }));
            var msg = Util2DLogic.LockedTextMessage(locked);
            Assert.Contains("2A2 (attribute TEN of block 2A0) on KHUNG-TEN", msg);
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs");
            Assert.DoesNotContain("LockedLayerOf(ctx, hits[0].Ent)", src);
            Assert.Contains("Util2DLogic.LockedHits(hits.Select(", src);
        }

        // ------------------------------------------------------------ KT2-04

        [Fact]
        public void Kt2_04_dimension_distance_follows_the_drawing_unit()
        {
            // mm drawing, DIMSCALE 100, house 10 m: unchanged (800 mm)
            Assert.Equal(800, Util2DLogic.DefaultDimOffset(100, 10000), 6);
            Assert.Equal(800, Util2DLogic.DefaultDimOffset(100, 10000, 1), 6);
            // metre drawing, same house (10 units): 0,8 m, not 800 m
            Assert.Equal(0.8, Util2DLogic.DefaultDimOffset(100, 10, 1000), 9);
            // metre drawing with DIMSCALE 1: 5 % of the plan
            Assert.Equal(0.5, Util2DLogic.DefaultDimOffset(1, 10, 1000), 9);
            Assert.Equal(80, Util2DLogic.DefaultDimOffset(100, 1000, 10), 9); // cm
        }

        [Fact]
        public void Kt2_04_table_text_and_areas_follow_the_drawing_unit()
        {
            Assert.Equal(250, Util2DLogic.DefaultTableTextHeight(100), 9);
            Assert.Equal(0.25, Util2DLogic.DefaultTableTextHeight(100, 1000), 9);
            Assert.Equal(2.5, Util2DLogic.DefaultTableTextHeight(0), 9);
            // a 4 × 3 m room: 12e6 mm² or 12 m² (metre drawing)
            Assert.Equal(12, Util2DLogic.ToM2(12e6), 9);
            Assert.Equal(12, Util2DLogic.ToM2(12, 1000), 9);
            Assert.Equal(1e6, Util2DLogic.FromM2(1), 9);
            Assert.Equal(1, Util2DLogic.FromM2(1, 1000), 9); // min_area 1 m² no longer drops every room
            Assert.Equal("12,00", Util2DLogic.M2(12, 2, 1000));
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs");
            Assert.Contains("Util2DLogic.FromM2(p.Num(\"min_area\", 0), mmPerUnit)", src);
            Assert.Contains("Util2DLogic.ToM2(total, mmPerUnit)", src);
            Assert.Contains("Util2DLogic.ToM2(r.Area, mmPerUnit)", src);
            Assert.Contains("Util2DLogic.AreaTable(rows, groupBy, decimals, mmPerUnit)", src);
            Assert.Contains("Util2DLogic.DefaultDimOffset(ctx.Db.Dimscale, size, mmPerUnit)", src);
            Assert.Contains("Util2DLogic.DefaultTableTextHeight(ctx.Db.Dimscale, FloorPlanLogic.MmPerUnit(", src);
            Assert.DoesNotContain("1e6", src.Substring(src.IndexOf("static object AreaSchedule", StringComparison.Ordinal)));
        }

        // ------------------------------------------------------------ KT2-16

        [Fact]
        public void Kt2_16_an_old_pdf_is_not_a_new_plot()
        {
            var t0 = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
            Assert.True(Util2DLogic.PdfWritten(false, null, true, t0));
            Assert.False(Util2DLogic.PdfWritten(false, null, false, null));
            Assert.False(Util2DLogic.PdfWritten(true, t0, true, t0)); // same date: the old file
            Assert.True(Util2DLogic.PdfWritten(true, t0, true, t0.AddSeconds(3)));
            Assert.Contains("open in another program", Util2DLogic.PdfLockedMessage("C:\\a.pdf"));
            Assert.Contains("old date", Util2DLogic.PdfNotWrittenMessage(true));
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "LayoutTools.cs");
            Assert.Contains("[\"exists\"] = written,", src);
            Assert.Contains("FileShare.None", src);
            // the lock check runs before anything is plotted
            Assert.True(src.IndexOf("PdfLockedMessage", StringComparison.Ordinal) < src.IndexOf("pe.BeginPlot(", StringComparison.Ordinal));
        }

        // ------------------------------------------------------------ KT2-17

        [Fact]
        public void Kt2_17_each_page_keeps_its_layout_paper()
        {
            var dev = new[] { "ISO_full_bleed_A1_(841.00_x_594.00_MM)", "ISO_full_bleed_A3_(420.00_x_297.00_MM)" };
            const string a3 = "ISO_full_bleed_A3_(420.00_x_297.00_MM)";
            Assert.Equal(dev[0], Util2DLogic.PickMedia(null, "iso_full_bleed_a1_(841.00_x_594.00_mm)", dev, a3));
            Assert.Equal(a3, Util2DLogic.PickMedia(null, "Letter", dev, a3)); // the PDF device lacks it
            Assert.Equal(a3, Util2DLogic.PickMedia(null, "", dev, a3));
            Assert.Equal("X", Util2DLogic.PickMedia("X", dev[0], dev, a3)); // asked for: as asked
            var src = Src("plugin", "ClaudeCAD.Plugin", "Drawing", "LayoutTools.cs");
            Assert.Contains("string media = p.Str(\"media\", null);", src);
            Assert.Contains("Util2DLogic.PickMedia(null, lo.CanonicalMediaName, deviceMedia, DefaultPdfMedia)", src);
            Assert.Contains("pageMedia.Add(ConfigurePlot(", src);
        }

        // ------------------------------------------------------------ KT2-28

        [Fact]
        public void Kt2_28_spacing_described_as_it_works()
        {
            var ts = Src("agent", "src", "tools", "utility2d.ts");
            Assert.Contains("spacing between rows (default = offset)", ts);
            Assert.DoesNotContain("spacing between rows (default 8 mm", ts);
            Assert.Contains("double spacing = p.Positive(\"spacing\", offset);", Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Util2DTools.cs"));
        }
    }
}
