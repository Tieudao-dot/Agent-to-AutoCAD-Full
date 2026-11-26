# Bản vá chưa lưu của thread con "Test & sửa F2" (lưu trữ)

*Luồng chính lưu 26/11/2026, trước khi đóng thread `session_01D56HmJDwuCnKRDvTAFHFXr`.*

## Đây là gì

- Thread con "Test & sửa F2" mở 25/11 16h36 để rà đợt F2, bị chế độ Auto từ chối `git commit`. Luồng chính cho dừng và
  giao việc rà F2 cho thread đợt F1; thread đó rà lại từ đầu trên `full/nang-cap-f2-test` (commit cuối `c9fdc08`).
- Trước khi dừng, thread con đã sửa 7 điểm và viết test, nhưng chỉ cất trong máy của nó (stash + file bản vá), chưa lưu lên
  GitHub và chưa chạy CI. Đóng thread thì phần này mất, nên luồng chính xin nguyên văn và lưu ở đây.
- Bản vá áp lên `bae8262` (= `full/nang-cap-f2`, bản F2 trước khi rà). **Không áp thẳng lên nhánh chốt được** vì nhánh chốt
  đã sửa cùng các chỗ đó theo cách của người rà.

## Đối chiếu với nhánh chốt `full/nang-cap-g-tong-the` @ `547d6af` (luồng chính, 26/11)

| # | Điểm thread con sửa | Trên nhánh chốt |
| --- | --- | --- |
| 1 | Liệt kê block thư viện báo lỗi khi UCS không phải World | Người rà sửa theo cách khác, ở bộ điều phối chung (lệnh gọi chỉ để đọc không đòi quyền ghi). Luồng chính dựa theo bàn giao `nang-cap-f2-test.md`, **chưa tự dò code** |
| 2 | Block thư viện, khung, khung tên, khung nhìn ăn theo màu / nét đang chọn | Có: hàm `ByLayer` trong `Drawing/Standards2DTools.cs` |
| 3 | So font Arial báo nhầm "khác font" | Có: `FontKey` trong `Core/Standards2D.cs` |
| 4 | Bản vẽ inch / dm bị gắn hậu tố `-CM` | Có: `UnitSuffix` trong `Core/Standards2D.cs` |
| 5 | Tỉ lệ trang in tự chọn sai khi UCS xoay | Có, làm kỹ hơn: `CreateSheet` tính khung bao trong UCS (`BoxFromWcs`) và căn lại cả tâm |
| 6 | "Liệt kê trạng thái layer" bị coi là lệnh ghi | Có: `LayerStateReads` dùng `NormalizeAction` |
| 7 | Tiền tố tên trang in `BV-` nằm rời trong plugin | Có: `F2Numbers.SheetNamePrefix` |

Kết luận: bản vá không thêm sửa lỗi nào so với nhánh chốt và bộ cài thử bản 5.

## Việc để lại

- **Test có thể lấy thêm:** bản vá có các test biên mà luồng chính chưa so với test trên nhánh chốt (15 block × 40 cỡ ngẫu
  nhiên; tham số plugin đọc khớp schema của trợ lý; cổng xác nhận trong batch trộn). Lần sửa lỗi kế tiếp (sau khi chủ dự
  án thử thật), thread giữ nhánh tích hợp xem và lấy test nào chưa có. Không gấp.
- **Điểm 1** cần thread đó dò lại code một lần cho chắc.
- Bản nháp bàn giao của thread con (99 dòng) và đoạn sửa `bo-cai/README.md` không lưu nguyên văn: nội dung trùng phần tóm
  tắt dưới đây.

## Nguyên văn 4 tin thread con gửi (26/11/2026 07h02–07h04)

Tin mở đầu là danh sách lỗi; ba tin sau là bản vá. Dòng "4.00" trong phần checklist của bản vá là cách hiểu cũ: kích thước
bản vẽ mét hiện "4" (đã sửa ở KTF-07).

[Cập nhật từ thread Test & sửa F2 (thread con)] Bản vá đợt F2 — phần mở đầu.

Commit gốc bản vá áp lên: `bae8262` (nhánh `full/nang-cap-f2-test`, = `full/nang-cap-f2`). Bản vá trong máy: `git stash` `stash@{0}` + scratchpad/f2-test.patch (63 934 byte). Không có key / secret / email trong bản vá (đã grep).

Lỗi đã tìm / sửa (đều đã có test; lõi 1021 → 1062, trợ lý 319 → 324, build 3 target 0 lỗi 0 cảnh báo; chưa CI):
1. `Core/Ucs.cs` `UcsLogic.ModeOf`: `insert_library_block` không `name` (liệt kê thư viện) là Create, lệnh đăng ký "đọc" → UCS khác World báo "cannot place new objects in the UCS without a transaction (plugin bug)". Sửa: không name → Wcs. Test `Listing_the_library_is_a_read_in_any_ucs`. Mức cao.
2. `Drawing/Standards2DTools.cs`: hình trong block thư viện / khung tên, khung bản vẽ, block chèn khung tên, khung nhìn lấy màu / nét / độ dày hiện hành (SetDatabaseDefaults). Sửa: hàm `ByLayer(e)` (ColorIndex 256, "ByLayer", LineWeight.ByLayer) sau mỗi SetDatabaseDefaults. Test `Block_definitions_and_the_sheet_do_not_take_the_current_color` (soát nguồn) + checklist B.9, C.12.
3. `Standards2DTools.cs` `ReadTextStyle` + `Core/Standards2D.cs` `Differences`: font đọc bằng FileName trước TypeFace → chạy lại có thể báo nhầm "khác font". Sửa: TypeFace trước, so bằng `StandardsLogic.FontKey`. Test `Font_names_compare_by_typeface`, `A_style_reported_by_its_font_file_is_unchanged_on_the_second_run` + checklist A.7.
4. `Core/Standards2D.cs` `DimStyleName`: inch / dm bị hậu tố `-CM`. Sửa: `UnitSuffix` (-CM/-DM/-M/-IN/-FT). Test `Dim_style_name_says_the_drawing_unit`, `Every_unit_of_the_drawing_gets_its_own_dim_style_name` + checklist A.8.
5. `CreateSheet`: tỉ lệ tự chọn theo khung bao WCS dù khung nhìn xoay theo UCS. Sửa: `SheetLogic.TurnedBox`, tính góc trước khi chọn tỉ lệ. Test `A_turned_viewport_fits_the_turned_drawing` + checklist B.8.
6. `layer_state` readsWhen chỉ nhận "list" đúng chữ. Sửa: `LayerStateLogic.IsList`. Test `Layer_state_list_is_a_read`.
7. Tiền tố layout "BV-" rời trong plugin → `F2Numbers.SheetNamePrefix`. Test `Default_layout_names_come_from_F2Numbers`.
Thêm test biên không kèm sửa (tỉ lệ, LTSCALE, khổ giấy, 15 block × 40 cỡ ngẫu nhiên, tên trạng thái layer…) và 5 test trợ lý `agent/test/nangcapf2test.test.ts`.

Toàn văn diff gửi tiếp 3 tin [1/3] code, [2/3] test lõi, [3/3] test trợ lý + tài liệu.

---

[1/3] Bản vá Test & sửa F2 — code (áp lên `bae8262`)

```diff
diff --git a/plugin/ClaudeCAD.Plugin/Core/Standards2D.cs b/plugin/ClaudeCAD.Plugin/Core/Standards2D.cs
index 3fb5e0e..20a4d9f 100644
--- a/plugin/ClaudeCAD.Plugin/Core/Standards2D.cs
+++ b/plugin/ClaudeCAD.Plugin/Core/Standards2D.cs
@@ -63,6 +63,8 @@ namespace ClaudeCAD.Plugin.Core
         public const int FrameLayerColor = 7, ViewportLayerColor = 8;
         /// <summary>Text style of the title block and the title block block name.</summary>
         public const string TitleTextStyle = "VN-KHUNG", TitleBlockName = "KHUNG-TEN-VN";
+        /// <summary>Default layout names: BV-01, BV-02… (test & sửa đợt F2: moved here from the plugin, the customer sees the tab name).</summary>
+        public const string SheetNamePrefix = "BV-";
         /// <summary>Paper sizes offered (ISO 216, mm, portrait).</summary>
         public static readonly (string name, double w, double h)[] Papers =
         {
@@ -113,9 +115,38 @@ namespace ClaudeCAD.Plugin.Core
 
         public static string TextStyleName(string prefix) => prefix + "-ARIAL";
 
-        /// <summary>KT-100; a drawing in metres gets its own name (KT-100-M) so a mm style of the same scale is not mixed up.</summary>
-        public static string DimStyleName(string prefix, int scale, double mmPerUnit) =>
-            $"{prefix}-{scale}" + (mmPerUnit >= 1000 ? "-M" : mmPerUnit >= 10 ? "-CM" : "");
+        /// <summary>
+        /// KT-100; a drawing in other units gets its own name (KT-100-M, -CM, -DM, -IN, -FT) so a mm style of the same scale
+        /// is not mixed up (test & sửa đợt F2: inches / decimetres were named -CM).
+        /// </summary>
+        public static string DimStyleName(string prefix, int scale, double mmPerUnit) => $"{prefix}-{scale}" + UnitSuffix(mmPerUnit);
+
+        /// <summary>"" for mm, else the unit of FloorPlanLogic.MmPerUnit: -CM, -DM, -M, -IN, -FT (-U&lt;mm&gt; for any other).</summary>
+        public static string UnitSuffix(double mmPerUnit)
+        {
+            bool Is(double v) => Math.Abs(mmPerUnit - v) < 1e-6 * v;
+            if (Is(1)) return "";
+            if (Is(10)) return "-CM";
+            if (Is(100)) return "-DM";
+            if (Is(1000)) return "-M";
+            if (Is(25.4)) return "-IN";
+            if (Is(304.8)) return "-FT";
+            return "-U" + Math.Round(mmPerUnit, 3).ToString(CultureInfo.InvariantCulture);
+        }
+
+        /// <summary>
+        /// A font name to compare: "Arial", "arial.ttf" and "C:\Windows\Fonts\ARIAL.TTF" are the same font (test & sửa
+        /// đợt F2: AutoCAD may report a TrueType style by its file name, and a second run must still find it unchanged).
+        /// </summary>
+        public static string FontKey(string font)
+        {
+            var f = (font ?? "").Trim();
+            int slash = Math.Max(f.LastIndexOf('\\'), f.LastIndexOf('/'));
+            if (slash >= 0) f = f.Substring(slash + 1);
+            foreach (var ext in new[] { ".ttf", ".ttc", ".otf", ".shx" })
+                if (f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) { f = f.Substring(0, f.Length - ext.Length); break; }
+            return f.ToLowerInvariant();
+        }
 
         /// <summary>Params of create_text_style.</summary>
         public static Dictionary<string, object> TextStyleParams(string prefix) => new Dictionary<string, object>
@@ -168,7 +199,7 @@ namespace ClaudeCAD.Plugin.Core
             foreach (var kv in wanted)
             {
                 if (kv.Key == "name" || existing == null || !existing.TryGetValue(kv.Key, out var have)) continue;
-                if (Same(kv.Value, have)) continue;
+                if (kv.Key == "font" ? FontKey(Convert.ToString(kv.Value, CultureInfo.InvariantCulture)) == FontKey(Convert.ToString(have, CultureInfo.InvariantCulture)) : Same(kv.Value, have)) continue;
                 out1.Add($"{kv.Key} {Show(have)} → {Show(kv.Value)}");
             }
             return out1;
@@ -265,6 +296,18 @@ namespace ClaudeCAD.Plugin.Core
         public static double[] ModelArea(Sheet s, int scale, double mmPerUnit) =>
             new[] { s.VpW / CustomScale(scale, mmPerUnit), s.VpH / CustomScale(scale, mmPerUnit) };
 
+        /// <summary>
+        /// The size on the sheet of a model box w × h when the viewport is turned by turnRad (a UCS turned in plan): the
+        /// box of the turned rectangle (test & sửa đợt F2: a UCS turned 90° swaps width and height for the fit scale).
+        /// </summary>
+        public static double[] TurnedBox(double w, double h, double turnRad)
+        {
+            double c = Math.Abs(Math.Cos(turnRad)), s = Math.Abs(Math.Sin(turnRad));
+            if (c < 1e-12) c = 0;
+            if (s < 1e-12) s = 0;
+            return new[] { w * c + h * s, w * s + h * c };
+        }
+
         /// <summary>Whether a model box w × h (drawing units) fits the viewport at this scale.</summary>
         public static bool Fits(Sheet s, int scale, double mmPerUnit, double w, double h)
         {
@@ -738,6 +781,13 @@ namespace ClaudeCAD.Plugin.Core
         /// <summary>What a state remembers of each layer (AutoCAD LayerStateMasks).</summary>
         public static readonly string[] Saved = { "on", "frozen", "locked", "plottable", "color", "linetype", "lineweight" };
 
+        /// <summary>Whether the call only lists (a read): "list" or its Vietnamese words; an unknown action is not a read.</summary>
+        public static bool IsList(string a)
+        {
+            try { return NormalizeAction(a) == "list"; }
+            catch (ToolException) { return false; }
+        }
+
         public static string NormalizeAction(string a)
         {
             var s = (a ?? "").Trim().ToLowerInvariant();
diff --git a/plugin/ClaudeCAD.Plugin/Core/Ucs.cs b/plugin/ClaudeCAD.Plugin/Core/Ucs.cs
index cfdd9c8..03eb0f9 100644
--- a/plugin/ClaudeCAD.Plugin/Core/Ucs.cs
+++ b/plugin/ClaudeCAD.Plugin/Core/Ucs.cs
@@ -352,6 +352,8 @@ namespace ClaudeCAD.Plugin.Core
                 case "build_beam": return Has("path_handles") ? UcsMode.Ref : UcsMode.Create;
                 case "build_railing": return Has("path_handle") ? UcsMode.Ref : UcsMode.Create;
                 case "add_dim_angular": return Has("line1_handle") || Has("line2_handle") ? UcsMode.Ref : UcsMode.Create;
+                // test & sửa đợt F2: without a name it only lists the library (a read, no coordinates)
+                case "insert_library_block": return Has("name") ? UcsMode.Create : UcsMode.Wcs;
             }
             return UcsMode.Create;
         }
diff --git a/plugin/ClaudeCAD.Plugin/Drawing/Standards2DTools.cs b/plugin/ClaudeCAD.Plugin/Drawing/Standards2DTools.cs
index 29a57ab..faad3f8 100644
--- a/plugin/ClaudeCAD.Plugin/Drawing/Standards2DTools.cs
+++ b/plugin/ClaudeCAD.Plugin/Drawing/Standards2DTools.cs
@@ -23,7 +23,19 @@ namespace ClaudeCAD.Plugin.Drawing
             d.Register("setup_standards", true, SetupStandards);
             d.Register(new ToolDef("create_sheet", true, CreateSheet, noTransaction: true));
             d.Register(new ToolDef("insert_library_block", true, InsertLibraryBlock, readsWhen: p => !p.Has("name")));
-            d.Register(new ToolDef("layer_state", true, LayerState, readsWhen: p => string.Equals(p.Str("action", ""), "list", StringComparison.OrdinalIgnoreCase)));
+            d.Register(new ToolDef("layer_state", true, LayerState, readsWhen: p => LayerStateLogic.IsList(p.Str("action", "list"))));
+        }
+
+        /// <summary>
+        /// Test & sửa đợt F2: what a new entity of a block definition / sheet gets after SetDatabaseDefaults, which copies
+        /// the current color, linetype and lineweight (CECOLOR, CELTYPE, CELWEIGHT): ByLayer, so a block on layer 0 takes
+        /// the layer of its reference and the frame takes the color of layer KHUNG, whatever the user's current color.
+        /// </summary>
+        private static void ByLayer(Entity e)
+        {
+            e.ColorIndex = 256;
+            e.Linetype = "ByLayer";
+            e.LineWeight = LineWeight.ByLayer;
         }
 
         // ------------------------------------------------------------------ shared helpers
@@ -82,7 +94,8 @@ namespace ClaudeCAD.Plugin.Drawing
             var r = (TextStyleTableRecord)ctx.Tr.GetObject(id, OpenMode.ForRead);
             return new Dictionary<string, object>
             {
-                ["font"] = string.IsNullOrEmpty(r.FileName) ? r.Font.TypeFace : r.FileName,
+                // the TrueType name first (a TrueType style may also report its file, arial.ttf); compared by StandardsLogic.FontKey
+                ["font"] = !string.IsNullOrEmpty(r.Font.TypeFace) ? r.Font.TypeFace : r.FileName,
                 ["height"] = r.TextSize,
                 ["width_factor"] = r.XScale,
             };
@@ -237,6 +250,7 @@ namespace ClaudeCAD.Plugin.Drawing
             void Put(Entity e)
             {
                 e.SetDatabaseDefaults(ctx.Db);
+                ByLayer(e);
                 e.Layer = "0";
                 btr.AppendEntity(e);
                 ctx.Tr.AddNewlyCreatedDBObject(e, true);
@@ -279,7 +293,7 @@ namespace ClaudeCAD.Plugin.Drawing
         {
             for (int i = 1; i < 1000; i++)
             {
-                var n = "BV-" + i.ToString("00", CultureInfo.InvariantCulture);
+                var n = F2Numbers.SheetNamePrefix + i.ToString("00", CultureInfo.InvariantCulture);
                 if (!lm.LayoutExists(n)) return n;
             }
             throw ToolException.Invalid("create_sheet: give a layout name.");
@@ -311,7 +325,12 @@ namespace ClaudeCAD.Plugin.Drawing
             bool extOk = db.Extmax.X > db.Extmin.X && db.Extmax.Y > db.Extmin.Y && db.Extmax.X < 1e20;
             double[] center = p.PointOpt("view_center");
             if (center == null) center = extOk ? new[] { (db.Extmin.X + db.Extmax.X) / 2, (db.Extmin.Y + db.Extmax.Y) / 2, 0 } : new[] { 0.0, 0, 0 };
-            double ew = extOk ? db.Extmax.X - db.Extmin.X : 0, eh = extOk ? db.Extmax.Y - db.Extmin.Y : 0;
+            // the current UCS turned in plan: the viewport turns with it (like PLAN), so the UCS x axis is horizontal on the sheet
+            var ucs = UcsScope.Input;
+            double turn = ucs != null && ucs.ZUp ? ucs.PlanAngleDeg * Math.PI / 180 : 0;
+            // the drawing's size as the turned viewport shows it (test & sửa đợt F2)
+            var onSheet = SheetLogic.TurnedBox(extOk ? db.Extmax.X - db.Extmin.X : 0, extOk ? db.Extmax.Y - db.Extmin.Y : 0, turn);
+            double ew = onSheet[0], eh = onSheet[1];
             bool scaleGiven = p.Has("scale");
             int n = scaleGiven ? StandardsLogic.ScaleDenominator(p.Get("scale"), "create_sheet")
                 : extOk ? SheetLogic.FitScale(sheet, f, ew, eh) : F2Numbers.DefaultScale;
@@ -377,6 +396,7 @@ namespace ClaudeCAD.Plugin.Drawing
                             new[] { sheet.X0, sheet.Y0 }, new[] { sheet.X1, sheet.Y0 }, new[] { sheet.X1, sheet.Y1 }, new[] { sheet.X0, sheet.Y1 },
                         }, true);
                         frame.SetDatabaseDefaults(db);
+                        ByLayer(frame);
                         frame.LayerId = frameLayer;
                         frame.LineWeight = (LineWeight)Geometry.LineWeightFrom(F2Numbers.FrameLineweight);
                         var frameId = space.AppendEntity(frame);
@@ -384,6 +404,7 @@ namespace ClaudeCAD.Plugin.Drawing
 
                         var br = new BlockReference(new Point3d(sheet.X1, sheet.Y0, 0), tbId);
                         br.SetDatabaseDefaults(db);
+                        ByLayer(br);
                         br.LayerId = frameLayer;
                         var brId = space.AppendEntity(br);
                         tr.AddNewlyCreatedDBObject(br, true);
@@ -393,6 +414,7 @@ namespace ClaudeCAD.Plugin.Drawing
 
                         var vp = new Viewport();
                         vp.SetDatabaseDefaults(db);
+                        ByLayer(vp);
                         vp.CenterPoint = new Point3d(sheet.VpCenter[0], sheet.VpCenter[1], 0);
                         vp.Width = sheet.VpW;
                         vp.Height = sheet.VpH;
@@ -401,9 +423,6 @@ namespace ClaudeCAD.Plugin.Drawing
                         tr.AddNewlyCreatedDBObject(vp, true);
                         vp.ViewDirection = Vector3d.ZAxis;
                         vp.ViewTarget = Point3d.Origin;
-                        // the current UCS turned in plan: the viewport turns with it (like PLAN), so the UCS x axis is horizontal on the sheet
-                        var ucs = UcsScope.Input;
-                        double turn = ucs != null && ucs.ZUp ? ucs.PlanAngleDeg * Math.PI / 180 : 0;
                         if (Math.Abs(turn) > 1e-12)
                         {
                             vp.TwistAngle = (2 * Math.PI - turn) % (2 * Math.PI);
@@ -430,7 +449,7 @@ namespace ClaudeCAD.Plugin.Drawing
                         result["view_center"] = Acad.J(new Point3d(center[0], center[1], 0));
                         result["model_area"] = new List<object> { Acad.R(area[0]), Acad.R(area[1]) };
                         result["units"] = unitName;
-                        if (turn != 0) result["viewport_twist_deg"] = Acad.R(-turn * 180 / Math.PI);
+                        if (Math.Abs(turn) > 1e-12) result["viewport_twist_deg"] = Acad.R(-turn * 180 / Math.PI);
                         if (extOk && !SheetLogic.Fits(sheet, n, f, ew, eh))
                             result["warning"] = $"The drawing ({Acad.R(ew)} × {Acad.R(eh)}) is bigger than the viewport at 1:{n} ({Acad.R(area[0])} × {Acad.R(area[1])}): " +
                                 $"part of it is outside. 1:{SheetLogic.FitScale(sheet, f, ew, eh)} fits this paper, or use a bigger paper.";
@@ -484,6 +503,7 @@ namespace ClaudeCAD.Plugin.Drawing
                         break;
                 }
                 e.SetDatabaseDefaults(ctx.Db);
+                ByLayer(e);
                 e.Layer = "0"; // takes the layer / color of the block reference
                 btr.AppendEntity(e);
                 ctx.Tr.AddNewlyCreatedDBObject(e, true);
```

---

[2/3] Bản vá Test & sửa F2 — test lõi (file mới)

```diff
diff --git a/plugin/ClaudeCAD.Core.Tests/NangCapF2TestSuaTests.cs b/plugin/ClaudeCAD.Core.Tests/NangCapF2TestSuaTests.cs
new file mode 100644
index 0000000..c2b3c90
--- /dev/null
+++ b/plugin/ClaudeCAD.Core.Tests/NangCapF2TestSuaTests.cs
@@ -0,0 +1,322 @@
+using System;
+using System.Collections.Generic;
+using System.IO;
+using System.Linq;
+using ClaudeCAD.Plugin.Core;
+using Xunit;
+
+namespace ClaudeCAD.Core.Tests
+{
+    /// <summary>
+    /// Test & sửa đợt F2 (25/11/2026, docs/ban-giao/nang-cap-f2-test.md): edge and cross cases of setup_standards,
+    /// create_sheet, insert_library_block and layer_state, and the bugs fixed in this round.
+    /// </summary>
+    public class NangCapF2TestSuaTests
+    {
+        private static string Src(params string[] parts) =>
+            File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", ".." }.Concat(parts).ToArray())));
+
+        private static string Tools => Src("plugin", "ClaudeCAD.Plugin", "Drawing", "Standards2DTools.cs");
+
+        // ------------------------------------------------------------ lỗi 1: listing the library in a UCS
+
+        [Fact]
+        public void Listing_the_library_is_a_read_in_any_ucs()
+        {
+            // without a name the call only lists (registered as a read): a Create mode would need a writable
+            // transaction and refused the listing in every UCS other than World
+            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("insert_library_block", new Dictionary<string, object>()));
+            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("insert_library_block", null));
+            Assert.Equal(UcsMode.Wcs, UcsLogic.ModeOf("insert_library_block", new Dictionary<string, object> { ["name"] = null }));
+            Assert.Equal(UcsMode.Create, UcsLogic.ModeOf("insert_library_block", new Dictionary<string, object> { ["name"] = "cua_so", ["position"] = new List<object> { 0.0, 0.0 } }));
+            Assert.Contains("readsWhen: p => !p.Has(\"name\")", Tools);
+        }
+
+        // ------------------------------------------------------------ lỗi 2: block / sheet entities ByLayer
+
+        [Fact]
+        public void Block_definitions_and_the_sheet_do_not_take_the_current_color()
+        {
+            var t = Tools;
+            Assert.Contains("e.ColorIndex = 256;", t);
+            Assert.Contains("e.Linetype = \"ByLayer\";", t);
+            Assert.Contains("e.LineWeight = LineWeight.ByLayer;", t);
+            // every SetDatabaseDefaults of a block entity, the frame, the title block reference and the viewport is followed by ByLayer
+            foreach (var who in new[] { "e", "frame", "br", "vp" })
+                Assert.Contains($"{who}.SetDatabaseDefaults(", t);
+            int defaults = CountOf(t, ".SetDatabaseDefaults(");
+            Assert.Equal(defaults, CountOf(t, "ByLayer(") - 1); // - the helper's own declaration
+            // the title block outline keeps its own lineweight (set after ByLayer)
+            Assert.True(t.IndexOf("Put(outline);", StringComparison.Ordinal) < t.IndexOf("outline.LineWeight =", StringComparison.Ordinal));
+        }
+
+        private static int CountOf(string s, string what)
+        {
+            int n = 0, i = 0;
+            while ((i = s.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; }
+            return n;
+        }
+
+        // ------------------------------------------------------------ lỗi 3: font of a TrueType style
+
+        [Theory]
+        [InlineData("Arial", "arial")]
+        [InlineData("arial.ttf", "arial")]
+        [InlineData("ARIAL.TTF", "arial")]
+        [InlineData(@"C:\Windows\Fonts\arial.ttf", "arial")]
+        [InlineData("romans.shx", "romans")]
+        [InlineData(" Times New Roman ", "times new roman")]
+        [InlineData(null, "")]
+        public void Font_names_compare_by_typeface(string font, string key) => Assert.Equal(key, StandardsLogic.FontKey(font));
+
+        [Fact]
+        public void A_style_reported_by_its_font_file_is_unchanged_on_the_second_run()
+        {
+            var wanted = StandardsLogic.TextStyleParams("VN");
+            var asFile = new Dictionary<string, object> { ["font"] = "arial.ttf", ["height"] = 0.0, ["width_factor"] = F2Numbers.WidthFactor };
+            Assert.Empty(StandardsLogic.Differences(wanted, asFile));
+            var other = new Dictionary<string, object> { ["font"] = "romans.shx", ["height"] = 0.0, ["width_factor"] = F2Numbers.WidthFactor };
+            Assert.Single(StandardsLogic.Differences(wanted, other));
+            Assert.Contains("r.Font.TypeFace) ? r.Font.TypeFace : r.FileName", Tools);
+        }
+
+        // ------------------------------------------------------------ lỗi 4: dimension style names per unit
+
+        [Theory]
+        [InlineData(1, "VN-100")]
+        [InlineData(10, "VN-100-CM")]
+        [InlineData(100, "VN-100-DM")]
+        [InlineData(1000, "VN-100-M")]
+        [InlineData(25.4, "VN-100-IN")]
+        [InlineData(304.8, "VN-100-FT")]
+        public void Dim_style_name_says_the_drawing_unit(double mmPerUnit, string name) =>
+            Assert.Equal(name, StandardsLogic.DimStyleName("VN", 100, mmPerUnit));
+
+        [Fact]
+        public void Every_unit_of_the_drawing_gets_its_own_dim_style_name()
+        {
+            var names = new[] { 0, 1, 2, 4, 5, 6, 14 }.Select(u => StandardsLogic.DimStyleName("KT", 50, FloorPlanLogic.MmPerUnit(u, out _))).ToList();
+            // 0 (unitless) is taken as mm like 4
+            Assert.Equal(names[0], names[3]);
+            Assert.Equal(6, names.Distinct().Count());
+            foreach (var n in names) Assert.StartsWith("KT-50", n);
+        }
+
+        // ------------------------------------------------------------ lỗi 5: fit scale with a turned UCS
+
+        [Fact]
+        public void A_turned_viewport_fits_the_turned_drawing()
+        {
+            var tb = SheetLogic.TurnedBox(100, 20, Math.PI / 2);
+            Assert.Equal(20, tb[0], 9);
+            Assert.Equal(100, tb[1], 9);
+            tb = SheetLogic.TurnedBox(100, 20, 0);
+            Assert.Equal(new[] { 100.0, 20.0 }, tb);
+            tb = SheetLogic.TurnedBox(10, 10, Math.PI / 4);
+            Assert.Equal(10 * Math.Sqrt(2), tb[0], 9);
+            // A3 landscape, a drawing 60 m wide × 10 m tall (mm) in a UCS turned 90°: on the sheet it is 10 × 60 m
+            var s = SheetLogic.Build("A3", null);
+            int flat = SheetLogic.FitScale(s, 1, 60000, 10000);
+            var turned = SheetLogic.TurnedBox(60000, 10000, Math.PI / 2);
+            int up = SheetLogic.FitScale(s, 1, turned[0], turned[1]);
+            Assert.True(up > flat, $"{up} vs {flat}");
+            Assert.True(SheetLogic.Fits(s, up, 1, turned[0], turned[1]));
+            Assert.False(SheetLogic.Fits(s, flat, 1, turned[0], turned[1]));
+            Assert.Contains("SheetLogic.TurnedBox(", Tools);
+        }
+
+        // ------------------------------------------------------------ lỗi 6: layer_state list in Vietnamese is a read
+
+        [Theory]
+        [InlineData("list", true)]
+        [InlineData("LIST", true)]
+        [InlineData("liệt kê", true)]
+        [InlineData("liet ke", true)]
+        [InlineData("save", false)]
+        [InlineData("xóa", false)]
+        [InlineData("bogus", false)]
+        [InlineData(null, false)]
+        public void Layer_state_list_is_a_read(string action, bool read) => Assert.Equal(read, LayerStateLogic.IsList(action));
+
+        [Fact]
+        public void Default_layout_names_come_from_F2Numbers()
+        {
+            Assert.Equal("BV-", F2Numbers.SheetNamePrefix);
+            Assert.Contains("F2Numbers.SheetNamePrefix + i.ToString(\"00\"", Tools);
+            Assert.DoesNotContain("\"BV-\"", Tools);
+        }
+
+        // ------------------------------------------------------------ edge cases without a fix
+
+        [Theory]
+        [InlineData("1:99.6", 100)] // rounded to a whole denominator
+        [InlineData("1:10000", 10000)]
+        [InlineData("1 / 75", 75)]
+        [InlineData("1", 1)]
+        public void Scale_edge_strings(string s, int n) => Assert.Equal(n, StandardsLogic.ScaleDenominator(s));
+
+        [Fact]
+        public void Scale_edge_errors_name_the_tool()
+        {
+            foreach (var bad in new object[] { "1:10001", "1:", ":100", "1:100:2", "NaN", 0.00001 })
+            {
+                var ex = Assert.Throws<ToolException>(() => StandardsLogic.ScaleDenominator(bad, "create_sheet"));
+                Assert.Contains("create_sheet", ex.Message);
+            }
+        }
+
+        [Fact]
+        public void Ltscale_is_only_set_while_it_is_the_default()
+        {
+            Assert.True(StandardsLogic.ShouldSetLtScale(1));
+            Assert.False(StandardsLogic.ShouldSetLtScale(50));
+            Assert.False(StandardsLogic.ShouldSetLtScale(1.0001));
+            Assert.Equal(0.05, StandardsLogic.LtScale(100, 1000), 12);
+            Assert.Equal(5, StandardsLogic.LtScale(100, 10), 12);
+        }
+
+        [Fact]
+        public void A_changed_dim_style_lists_each_difference_once()
+        {
+            var wanted = StandardsLogic.DimStyleParams("VN", 100, 1);
+            var have = new Dictionary<string, object>(wanted) { ["scale"] = 50.0, ["arrow"] = "closed_filled", ["text_style"] = "vn-arial" };
+            var d = StandardsLogic.Differences(wanted, have);
+            Assert.Equal(2, d.Count); // text style compares case-insensitively
+            Assert.Contains(d, x => x.StartsWith("scale 50 → 100"));
+            Assert.Contains(d, x => x.StartsWith("arrow closed_filled → archtick"));
+            // a value a style does not report is not a difference
+            Assert.Empty(StandardsLogic.Differences(wanted, new Dictionary<string, object>()));
+            Assert.Empty(StandardsLogic.Differences(wanted, null));
+        }
+
+        [Fact]
+        public void Fit_scale_falls_back_to_the_biggest_scale_and_is_never_finer_than_needed()
+        {
+            var s = SheetLogic.Build("A4", null);
+            Assert.Equal(F2Numbers.StandardScales.Last(), SheetLogic.FitScale(s, 1, 1e9, 1e9));
+            Assert.Equal(1, SheetLogic.FitScale(s, 1, 1, 1));
+            // metres: a 30 m × 20 m plan on A1 at 1:100
+            var a1 = SheetLogic.Build("A1", null);
+            int n = SheetLogic.FitScale(a1, 1000, 30, 20);
+            Assert.True(SheetLogic.Fits(a1, n, 1000, 30, 20));
+            int i = Array.IndexOf(F2Numbers.StandardScales, n);
+            Assert.True(i == 0 || !SheetLogic.Fits(a1, F2Numbers.StandardScales[i - 1], 1000, 30, 20));
+        }
+
+        [Fact]
+        public void Title_fields_given_override_the_defaults_and_long_values_are_reported()
+        {
+            var v = SheetLogic.FieldValues(new Dictionary<string, object> { ["scale"] = "1:50 / 1:20", ["DATE"] = "01/12/2026", ["title"] = null }, 100, "A3", new DateTime(2026, 11, 25));
+            Assert.Equal("1:50 / 1:20", v["TI_LE"]);
+            Assert.Equal("01/12/2026", v["NGAY"]);
+            Assert.Equal("", v["TEN_BAN_VE"]);
+            Assert.Equal("A3", v["KHO_GIAY"]);
+            var tb = SheetLogic.BuildTitleBlock();
+            Assert.Empty(SheetLogic.TooLong(tb, v));
+            v["SO_BAN_VE"] = new string('X', 40);
+            Assert.Equal(new[] { "sheet_no" }, SheetLogic.TooLong(tb, v));
+        }
+
+        [Fact]
+        public void Paper_names_and_orientation_edges()
+        {
+            Assert.Equal("A1", SheetLogic.NormalizePaper(" a1 "));
+            Assert.Equal("A3", SheetLogic.NormalizePaper(null));
+            Assert.Throws<ToolException>(() => SheetLogic.NormalizePaper("A5"));
+            Assert.Throws<ToolException>(() => SheetLogic.NormalizePaper("Letter"));
+            var p = SheetLogic.Build("A4", true);
+            Assert.True(p.Landscape);
+            Assert.Equal(297, p.W);
+            Assert.Equal("ISO_full_bleed_A4_(297.00_x_210.00_MM)", p.Media);
+            // the viewport never overlaps the title block or the frame
+            foreach (var paper in F2Numbers.Papers.Select(x => x.name))
+                foreach (var land in new bool?[] { true, false, null })
+                {
+                    var s = SheetLogic.Build(paper, land);
+                    Assert.True(s.VpY0 >= s.Y0 + s.TitleH + F2Numbers.ViewportGap - 1e-9);
+                    Assert.True(s.VpX0 >= s.X0 && s.VpX1 <= s.X1 && s.VpY1 <= s.Y1);
+                    Assert.True(s.VpW > 0 && s.VpH > 0);
+                }
+        }
+
+        [Fact]
+        public void Library_blocks_stay_inside_their_size_at_any_allowed_size()
+        {
+            var rnd = new Random(25112026);
+            foreach (var it in LibraryBlocks.Items)
+                for (int k = 0; k < 40; k++)
+                {
+                    double w = Math.Round(it.MinWidth + rnd.NextDouble() * (it.MaxWidth - it.MinWidth));
+                    double d = it.HasDepth ? Math.Round(it.MinDepth + rnd.NextDouble() * (it.MaxDepth - it.MinDepth)) : 0;
+                    var prims = LibraryBlocks.Build(it, w, d);
+                    Assert.NotEmpty(prims);
+                    var b = LibraryBlocks.Box(prims);
+                    double tol = 1e-6;
+                    switch (it.Key)
+                    {
+                        case "cua_di": Assert.Equal(new[] { 0, 0, w, w }, b.Select(x => Math.Round(x, 6)).ToArray()); break;
+                        case "cua_di_2_canh": Assert.Equal(new[] { 0, 0, w, w / 2 }, b.Select(x => Math.Round(x, 6)).ToArray()); break;
+                        case "cay": Assert.Equal(w / 2, b[2], 6); Assert.Equal(-w / 2, b[0], 6); break;
+                        case "bon_cau": case "lavabo":
+                            Assert.True(b[0] >= -w / 2 - tol && b[2] <= w / 2 + tol && b[1] >= -tol && b[3] <= d + tol, it.Key); break;
+                        default:
+                            Assert.True(b[0] >= -tol && b[1] >= -tol && b[2] <= w + tol && b[3] <= d + tol, $"{it.Key} {w}x{d}: {string.Join(",", b)}"); break;
+                    }
+                    foreach (var p in prims)
+                    {
+                        Assert.All(p.Pts, q => Assert.True(!double.IsNaN(q[0]) && !double.IsNaN(q[1])));
+                        if (p.Kind == "ellipse") Assert.True(p.Rx > 0 && p.Ry > 0, it.Key);
+                        if (p.Kind == "circle" || p.Kind == "arc") Assert.True(p.R > 0, it.Key);
+                        if (p.Kind == "pline") Assert.True(p.Pts.Count >= 2 && (p.Bulges == null || p.Bulges.Count == p.Pts.Count), it.Key);
+                    }
+                    // the block name is a valid AutoCAD symbol name and depends on the sizes only
+                    var name = LibraryBlocks.BlockName(it, w, d);
+                    Assert.DoesNotContain(name, c => "<>/\\\":;?*|,=`".IndexOf(c) >= 0);
+                    Assert.Equal(name, LibraryBlocks.BlockName(it, w, d));
+                }
+        }
+
+        [Fact]
+        public void Library_sizes_at_the_edges()
+        {
+            var door = LibraryBlocks.Require("Cửa đi");
+            Assert.Equal((500.0, 0.0), LibraryBlocks.Sizes(door, 500, null));
+            Assert.Equal((1500.0, 0.0), LibraryBlocks.Sizes(door, 1500.4, null)); // rounded first
+            Assert.Throws<ToolException>(() => LibraryBlocks.Sizes(door, 499, null));
+            Assert.Throws<ToolException>(() => LibraryBlocks.Sizes(door, 0.9, null)); // metres given as mm: refused, not a tiny door
+            Assert.Throws<ToolException>(() => LibraryBlocks.Sizes(door, null, 100)); // a door has no depth
+            var win = LibraryBlocks.Require("window");
+            Assert.Equal("CUA-SO-1200", LibraryBlocks.BlockName(win, 1200, 220));
+            Assert.Equal("CUA-SO-1200x110", LibraryBlocks.BlockName(win, 1200, 110));
+            Assert.Throws<ToolException>(() => LibraryBlocks.Require("thang may"));
+            Assert.Null(LibraryBlocks.Find(""));
+            Assert.Null(LibraryBlocks.Find(null));
+        }
+
+        [Fact]
+        public void Layer_state_names_at_the_edges()
+        {
+            Assert.Equal("Tầng 1 - in", LayerStateLogic.CheckName("  Tầng 1 - in "));
+            Assert.Equal(new string('a', 255), LayerStateLogic.CheckName(new string('a', 255)));
+            Assert.Throws<ToolException>(() => LayerStateLogic.CheckName(new string('a', 256)));
+            foreach (var bad in new[] { "", "   ", null, "a/b", "a:b", "a*", "a,b", "a=b" })
+                Assert.Throws<ToolException>(() => LayerStateLogic.CheckName(bad));
+            Assert.Equal("save", LayerStateLogic.NormalizeAction(" LƯU "));
+            Assert.Throws<ToolException>(() => LayerStateLogic.NormalizeAction(null));
+        }
+
+        [Fact]
+        public void Overwrite_refusals_never_change_anything_and_name_the_handles()
+        {
+            var m = F2Confirm.CardMessage("layer_state", "delete the layer state 'A'", new[] { "1F2", "1F3" });
+            Assert.Contains("Nothing was changed", m);
+            Assert.Contains("1F2, 1F3", m);
+            Assert.Contains("request_confirmation", m);
+            // the plugin refuses before deleting / saving over: the check comes first in the source
+            var t = Tools;
+            int del = t.IndexOf("lsm.DeleteLayerState(name);\n                    return", StringComparison.Ordinal);
+            int refuse = t.LastIndexOf("delete the layer state", del, StringComparison.Ordinal);
+            Assert.True(refuse > 0 && refuse < del);
+        }
+    }
+}
```

---

[3/3] Bản vá Test & sửa F2 — test trợ lý + tài liệu

Riêng `bo-cai/README.md` không chép nguyên hunk (2 dòng bảng dài ~2,5k ký tự mỗi dòng, kèm 5 dòng ngữ cảnh rất dài): thay đổi duy nhất là dòng bắt đầu `| Full · Nâng cấp đợt F2 *(nhánh `full/nang-cap-f2`, chưa phát hành)* |`, chèn trước ` |` cuối dòng (ngay sau `Thử tay: [`docs/checklist-dot-f2.md`](../docs/checklist-dot-f2.md).`) đoạn sau (có 1 dấu cách đầu):
` Test & sửa (nhánh `full/nang-cap-f2-test`): liệt kê thư viện block chạy được cả khi UCS không phải World; block thư viện, khung và khung tên theo màu / nét của layer (không lấy màu hiện hành); chạy lại "thiết lập style chuẩn" không báo nhầm khác font; bản vẽ inch / dm có tên kiểu kích thước riêng (`-IN`, `-DM`); tỉ lệ tự chọn của trang in tính theo khung nhìn đã xoay với UCS.`
Phần còn lại nguyên văn:

```diff
diff --git a/agent/test/nangcapf2test.test.ts b/agent/test/nangcapf2test.test.ts
new file mode 100644
index 0000000..a693222
--- /dev/null
+++ b/agent/test/nangcapf2test.test.ts
@@ -0,0 +1,92 @@
+/**
+ * Test & sửa đợt F2 (25/11/2026, docs/ban-giao/nang-cap-f2-test.md): cross checks of the F2 tools between the
+ * assistant and the plugin: the plugin's refusal stops the turn, the schema matches what the plugin reads, and the
+ * gate in a batch that mixes F2 overwrites with other calls.
+ */
+import assert from 'node:assert/strict';
+import fs from 'node:fs';
+import path from 'node:path';
+import { describe, it } from 'node:test';
+import { fileURLToPath } from 'node:url';
+import { z } from 'zod';
+import { confirmationError, refusedForConfirmation, type GateState } from '../src/agent/confirmation.js';
+import type { ToolBridge } from '../src/tools/common.js';
+import { standards2dTools } from '../src/tools/standards2d.js';
+
+const here = path.dirname(fileURLToPath(import.meta.url));
+const repo = path.resolve(here, '..', '..', '..');
+const read = (...p: string[]) => fs.readFileSync(path.join(repo, ...p), 'utf8');
+const bridge: ToolBridge = { call: async () => ({ id: 'x', ok: true, result: {} }), status: () => {} };
+const tools = standards2dTools(bridge);
+const schemaOf = (name: string) => z.object(tools.find((t) => t.name === name)!.inputSchema as z.ZodRawShape);
+
+describe('F2 test & sửa: the plugin and the assistant agree', () => {
+  it("the plugin's refusal of an overwrite (no card) stops the turn like the assistant's own", () => {
+    const core = read('plugin', 'ClaudeCAD.Plugin', 'Core', 'Standards2D.cs');
+    const m = core.match(/\$"(Confirmation required: \{tool\}[^"]*)"/);
+    assert.ok(m, 'F2Confirm.CardMessage starts with "Confirmation required"');
+    const msg = m![1]!.replace('{tool}', 'layer_state').replace('{what}', "delete the layer state 'IN'");
+    assert.ok(refusedForConfirmation({ ok: false, error: { message: msg } }));
+    // also inside a batch result
+    assert.ok(refusedForConfirmation({ ok: true, result: { results: [{ ok: true }, { ok: false, error: { message: msg } }] } }));
+  });
+
+  it('every parameter the plugin reads is in the schema (and nothing else)', () => {
+    const src = read('plugin', 'ClaudeCAD.Plugin', 'Drawing', 'Standards2DTools.cs');
+    const body = (fn: string) => {
+      const i = src.indexOf(`internal static object ${fn}(`);
+      const j = src.indexOf('internal static object ', i + 10);
+      return src.slice(i, j < 0 ? undefined : j);
+    };
+    const reads = (fn: string) =>
+      new Set([...body(fn).matchAll(/p\.(?:Str|Num|NumOpt|Bool|Get|Has|Point|PointOpt|Points|ObjOpt|Handles)\("([a-z_]+)"/g)].map((x) => x[1]!));
+    const pairs: Array<[string, string]> = [
+      ['setup_standards', 'SetupStandards'], ['create_sheet', 'CreateSheet'], ['insert_library_block', 'InsertLibraryBlock'], ['layer_state', 'LayerState'],
+    ];
+    for (const [tool, fn] of pairs) {
+      const keys = new Set(Object.keys(schemaOf(tool).shape));
+      for (const k of reads(fn)) assert.ok(keys.has(k), `${tool}: plugin reads '${k}' that the schema does not offer`);
+    }
+    // handles of an overwrite go through ApprovedOverwrites (p.Handles("handles")): offered on both overwrite tools
+    for (const t of ['setup_standards', 'layer_state']) assert.ok('handles' in schemaOf(t).shape, t);
+  });
+
+  it('the schemas accept the calls of the checklist and refuse a wrong paper / action', () => {
+    assert.ok(schemaOf('create_sheet').safeParse({ paper: 'A1', orientation: 'portrait', scale: '1:50', fields: { project: 'Nhà phố', sheet_no: 'KT-01' } }).success);
+    assert.ok(!schemaOf('create_sheet').safeParse({ paper: 'A5' }).success);
+    // an unknown title field is dropped by zod (the plugin also refuses one it receives)
+    assert.deepEqual((schemaOf('create_sheet').parse({ fields: { project: 'A', tenant: 'x' } }) as { fields: object }).fields, { project: 'A' });
+    assert.ok(schemaOf('insert_library_block').safeParse({ name: 'cua_di', positions: [[0, 0], [1000, 0]], width: 800, flip: true }).success);
+    assert.ok(schemaOf('insert_library_block').safeParse({}).success, 'no name = list the library');
+    assert.ok(schemaOf('layer_state').safeParse({ action: 'list' }).success);
+    assert.ok(!schemaOf('layer_state').safeParse({ action: 'rename', name: 'A' }).success);
+    assert.ok(schemaOf('setup_standards').safeParse({ scale: 100, standard: 've-kien-truc' }).success);
+    assert.ok(!schemaOf('setup_standards').safeParse({ scale: -100 }).success);
+  });
+});
+
+describe('F2 test & sửa: the gate in mixed batches', () => {
+  const noCard: GateState = { userText: 'đồng ý', answersQuestion: true };
+  const card: GateState = { userText: '', answersQuestion: true, confirmedByButton: true, cardConfirmed: new Set(['1A2B']) };
+
+  it('an approved overwrite next to an unapproved one is still refused', () => {
+    const calls = [
+      { tool: 'setup_standards', params: { scale: '1:100', handles: ['1a2b'] } },
+      { tool: 'layer_state', params: { action: 'delete', name: 'IN', handles: ['3C4D'] } },
+    ];
+    const err = confirmationError('batch_execute', { calls }, card);
+    assert.ok(err?.startsWith('Confirmation required'), err);
+    assert.match(err!, /3C4D/);
+    assert.doesNotMatch(err!, /1A2B/);
+  });
+
+  it('a batch that creates (no handles) is never gated', () => {
+    const calls = [
+      { tool: 'setup_standards', params: { scale: '1:100' } },
+      { tool: 'create_sheet', params: { paper: 'A3' } },
+      { tool: 'insert_library_block', params: { name: 'cua_so', position: [0, 0] } },
+      { tool: 'layer_state', params: { action: 'save', name: 'IN' } },
+    ];
+    assert.equal(confirmationError('batch_execute', { calls }, noCard), undefined);
+  });
+});
diff --git a/docs/checklist-dot-f2.md b/docs/checklist-dot-f2.md
index 9c954ba..0f53da3 100644
--- a/docs/checklist-dot-f2.md
+++ b/docs/checklist-dot-f2.md
@@ -31,6 +31,11 @@ Mở bản vẽ trống (đơn vị mm).
 5. Bản vẽ đơn vị **mét** (`INSUNITS` = 6): "thiết lập style chuẩn 1:100".
    - Đạt: tạo `VN-100-M`; ghi kích thước đoạn dài 4 (mét) hiện **4.00** và chữ cao khoảng 0,25.
 6. Đặt `LTSCALE` = 20 trước, rồi chạy lại: Claude báo giữ LTSCALE của cậu và nói số nên dùng (không tự đổi).
+7. *(Thêm ở Test & sửa F2)* Chạy A.1 hai lần trên bản vẽ mới, lần hai Claude phải báo "không thay đổi" cả **kiểu chữ**
+   (không báo khác font "arial.ttf → Arial"). Nếu lần hai vẫn hiện thẻ hỏi ghi đè `VN-ARIAL` thì báo lại tớ kèm câu Claude
+   trả lời.
+8. *(Thêm ở Test & sửa F2)* Bản vẽ đơn vị **inch** (`INSUNITS` = 1): kiểu kích thước tên `VN-100-IN` (trước đây nhầm là
+   `-CM`).
 
 ## B. Trang in có khung tên
 
@@ -50,6 +55,10 @@ Mở một mặt bằng có sẵn (khoảng 12 × 9 m, đơn vị mm).
 6. Tạo trùng tên layout có sẵn ("tạo trang tên BV-01"). Đạt: Claude báo tên đã có, **không** sửa layout cũ.
 7. Bản vẽ có UCS xoay 30° (lệnh `UCS` → Z → 30) rồi tạo trang: mặt bằng trong khung nhìn xoay theo UCS (trục X của
    UCS nằm ngang trên giấy). *(Cần thử thật: chưa kiểm được ngoài AutoCAD.)*
+8. *(Thêm ở Test & sửa F2)* Mặt bằng dài và hẹp (vd 60 × 10 m), UCS xoay **90°**, "tạo trang A3 cho bản vẽ này" (không
+   nói tỉ lệ): tỉ lệ tự chọn phải đủ để **cả** mặt bằng nằm trong khung nhìn đã xoay (không bị cắt mất hai đầu).
+9. *(Thêm ở Test & sửa F2)* Chọn màu hiện hành **đỏ** (ô Color trên thanh Properties) rồi tạo trang: khung và khung tên vẫn
+   màu của layer `KHUNG` (trắng / đen), không đỏ.
 
 ## C. Block thư viện
 
@@ -67,6 +76,10 @@ Mở một mặt bằng có sẵn (khoảng 12 × 9 m, đơn vị mm).
 9. Dựng 3D từ mặt bằng có cửa thư viện (máy 3D / Full: "dựng 3D mặt bằng này"): cửa đi / cửa sổ thư viện được nhận
    là lỗ cửa.
 10. UCS xoay 30°: "chèn cửa đi tại 0,0". Đạt: cửa nằm ở gốc UCS, xoay theo UCS.
+11. *(Thêm ở Test & sửa F2)* Vẫn UCS xoay 30°: "liệt kê thư viện block". Đạt: Claude kể được danh sách (trước đây lệnh báo
+    lỗi "plugin bug" khi UCS không phải World).
+12. *(Thêm ở Test & sửa F2)* Màu hiện hành **đỏ**, chèn "lavabo" trên layer A-FIXT (màu 6): lavabo màu tím của layer, không
+    đỏ; đổi màu layer A-FIXT thì lavabo đổi theo.
 
 ## D. Trạng thái layer
 
```

File mới `docs/ban-giao/nang-cap-f2-test.md` (bàn giao nháp, 99 dòng): nội dung là bảng 7 lỗi + test + bảng con số đã soát + quyết định tự chọn như tin tóm tắt và tin "[Cập nhật…]" trước; phần "Commit" ghi bị chặn. Thread rà nên viết lại bàn giao theo kết quả của nó; cần nguyên văn file này thì nhắn tớ gửi riêng.
