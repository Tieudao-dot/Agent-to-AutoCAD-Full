using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.Drawing;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin
{
    /// <summary>
    /// CLAUDECHATSELFTEST: exercises, inside the real AutoCAD, the parts that cannot be tested on CI
    /// (off-screen picture, undo group, database events, snapshot, version copy, read-only tab,
    /// layer / name lists, assistant connection) on a NEW temporary drawing — the user's drawings are
    /// not touched. Writes a ✓/✗ report to the log folder and opens it.
    /// </summary>
    public static class SelfTest
    {
        private sealed class Step
        {
            public string Name;
            public bool Ok;
            public string Detail;
            public long Ms;
        }

        public static string Run()
        {
            var steps = new List<Step>();
            var dm = AcApp.DocumentManager;
            var original = dm.MdiActiveDocument;
            CleanOldRuns();
            var dir = Path.Combine(Path.GetTempPath(), "ClaudeCAD-selftest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(dir);
            var dwg = Path.Combine(dir, "selftest.dwg");
            Document doc = null;
            Document versionDoc = null;
            var handles = new List<string>();

            void Do(string name, Func<string> action)
            {
                var sw = Stopwatch.StartNew();
                var st = new Step { Name = name };
                try
                {
                    st.Detail = action() ?? "";
                    st.Ok = true;
                }
                catch (Exception ex)
                {
                    st.Ok = false;
                    st.Detail = ex.GetType().Name + ": " + ex.Message;
                    Logger.Error("selftest: " + name, ex);
                }
                st.Ms = sw.ElapsedMilliseconds;
                steps.Add(st);
            }

            Do("Phiên bản AutoCAD / plugin", () => DrawingService.BuildInfo);

            Do("Tạo bản vẽ thử (không đụng bản vẽ của cậu)", () =>
            {
                try { doc = DocumentCollectionExtension.Add(dm, "acadiso.dwt"); }
                catch { doc = DocumentCollectionExtension.Add(dm, "acad.dwt"); }
                dm.MdiActiveDocument = doc;
                return Path.GetFileName(doc.Name);
            });
            if (doc == null) return Report(steps, dir);

            Do("Vẽ bằng API (khóa bản vẽ + transaction)", () =>
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var db = doc.Database;
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    var layer = new LayerTableRecord { Name = "CC-SELFTEST" };
                    lt.Add(layer);
                    tr.AddNewlyCreatedDBObject(layer, true);
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                    var ents = new Entity[]
                    {
                        new Line(new Point3d(0, 0, 0), new Point3d(4000, 0, 0)) { Layer = "CC-SELFTEST" },
                        new Circle(new Point3d(2000, 1500, 0), Vector3d.ZAxis, 500) { Layer = "CC-SELFTEST" },
                        new DBText { Position = new Point3d(0, 2500, 0), Height = 250, TextString = "ClaudeCAD self-test", Layer = "CC-SELFTEST" },
                    };
                    foreach (var e in ents)
                    {
                        ms.AppendEntity(e);
                        tr.AddNewlyCreatedDBObject(e, true);
                        handles.Add(e.Handle.ToString());
                    }
                    tr.Commit();
                }
                return $"3 đối tượng: {string.Join(", ", handles)}";
            });

            Do("Lưu bản vẽ thử", () =>
            {
                using (doc.LockDocument()) doc.Database.SaveAs(dwg, DwgVersion.Current);
                return dwg;
            });

            Do("Nhóm hoàn tác theo lượt (StartUndoMark / EndUndoMark)", () =>
            {
                var undo = new UndoScope();
                undo.EnsureStarted(doc);
                bool opened = undo.IsOpen;
                undo.End();
                if (!opened) throw new InvalidOperationException("không mở được nhóm hoàn tác (lượt chat sẽ cần nhiều lệnh U)");
                return "mở và đóng được";
            });

            Do("Sự kiện bản vẽ (ghi nhận sửa tay)", () =>
            {
                var key = DrawingHistory.KeyOf(doc) ?? ChatTranscript.KeyFor(dwg);
                DrawingHistory.Instance.Watch(doc);
                DrawingHistory.Instance.TakeManualChanges(doc, key, dwg, false, out _); // reset
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var id = Acad.IdFromHandle(doc.Database, handles[0]);
                    var line = (Line)tr.GetObject(id, OpenMode.ForWrite);
                    line.EndPoint = new Point3d(5000, 0, 0);
                    tr.Commit();
                }
                var report = DrawingHistory.Instance.TakeManualChanges(doc, key, dwg, true, out var shortText);
                if (report == null) throw new InvalidOperationException("sửa một đường thẳng nhưng không ghi nhận được");
                return shortText + " — " + report["text"];
            });

            Do("Biên bản (snapshot) sau lượt", () =>
            {
                var key = DrawingHistory.KeyOf(doc) ?? ChatTranscript.KeyFor(dwg);
                var sw = Stopwatch.StartNew();
                DrawingHistory.Instance.AfterTurn(doc, key, dwg);
                var file = DrawingHistory.LocalFile(key);
                if (!File.Exists(file)) throw new FileNotFoundException("không thấy file biên bản", file);
                var snap = DrawingSnapshot.FromBytes(File.ReadAllBytes(file));
                DrawingHistory.Instance.Reset(doc, key, dwg); // do not leave the test snapshot behind
                return $"{snap.Entries.Count} đối tượng, {new FileInfo(file).Length / 1024.0:0.0} KB trước khi xóa, {sw.ElapsedMilliseconds} ms";
            });

            Do("Chụp ảnh bản vẽ ẩn (capture_view)", () =>
            {
                var r = Capture.Render(doc, new Point3d(-500, -500, 0), new Point3d(5500, 3500, 0), 800, "window");
                if (r.Png == null || r.Png.Length < 500) throw new InvalidOperationException("ảnh rỗng");
                File.WriteAllBytes(Path.Combine(dir, "capture.png"), r.Png);
                return $"{r.Width}×{r.Height}, {r.Png.Length / 1024.0:0.0} KB (ảnh: dòng «Ảnh chụp thử» bên dưới, hoặc capture.png trong thư mục thử)";
            });

            Do("Danh sách layer / tên cho @", () =>
            {
                var rows = Interaction.LayerRows(doc);
                var names = Interaction.DrawingNames(doc);
                var test = rows.FirstOrDefault(r => r.Name == "CC-SELFTEST");
                if (test == null || test.Count != 3) throw new InvalidOperationException($"layer CC-SELFTEST đếm {(test == null ? "không thấy" : test.Count.ToString())}, cần 3");
                return $"{rows.Count} layer, {names.Count} tên";
            });

            string versionFile = Path.Combine(dir, "version-copy.dwg");
            Do("Bản sao phiên bản (Wblock + SaveAs)", () =>
            {
                using (doc.LockDocument())
                using (var copy = doc.Database.Wblock())
                    copy.SaveAs(versionFile, DwgVersion.Current);
                return $"{new FileInfo(versionFile).Length / 1024.0:0.0} KB";
            });

            Do("Mở phiên bản trong tab chỉ xem rồi đóng", () =>
            {
                versionDoc = DocumentCollectionExtension.Open(dm, versionFile, true);
                bool readOnly = versionDoc.IsReadOnly;
                DocumentExtension.CloseAndDiscard(versionDoc);
                versionDoc = null;
                dm.MdiActiveDocument = doc;
                return readOnly ? "mở chỉ xem, đóng được" : "mở được nhưng không ở chế độ chỉ xem";
            });

            Do("Phím tắt Ctrl+Shift+Space", () => PluginApp.HotkeyEnabled ? "đã bật (thử bấm sau khi test xong)" : "đang tắt trong ⚙");

            foreach (var (name, ok, detail) in PluginApp.Controller.Diagnostics())
                steps.Add(new Step { Name = name, Ok = ok, Detail = detail });

            // clean up: close the test drawing, back to the user's drawing
            try { if (versionDoc != null && !versionDoc.IsDisposed) DocumentExtension.CloseAndDiscard(versionDoc); } catch { }
            try { if (doc != null && !doc.IsDisposed) DocumentExtension.CloseAndDiscard(doc); } catch (Exception ex) { Logger.Warn("selftest: close failed: " + ex.Message); }
            try { if (original != null && !original.IsDisposed) dm.MdiActiveDocument = original; } catch { }
            return Report(steps, dir);
        }

        /// <summary>Test folders of earlier runs (kept when something failed) older than a week.</summary>
        private static void CleanOldRuns()
        {
            try
            {
                foreach (var d in Directory.GetDirectories(Path.GetTempPath(), "ClaudeCAD-selftest-*"))
                    if (DateTime.Now - Directory.GetLastWriteTime(d) > TimeSpan.FromDays(7))
                        try { Directory.Delete(d, true); } catch { }
            }
            catch { }
        }

        private static string Report(List<Step> steps, string dir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ClaudeCAD — tự kiểm tra " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            sb.AppendLine(new string('=', 60));
            foreach (var s in steps)
                sb.AppendLine($"{(s.Ok ? "✓" : "✗")} {s.Name}{(s.Ms > 0 ? $" ({s.Ms} ms)" : "")}: {s.Detail}");
            int bad = steps.Count(s => !s.Ok);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            sb.AppendLine(new string('=', 60));
            sb.AppendLine(bad == 0 ? "Tất cả đều đạt." : $"{bad} mục chưa đạt — gửi file này (và log CLAUDECHATLOG) cho người phát triển.");
            if (bad == 0)
            {
                // nothing to investigate: keep only the picture (next to the report), drop the test drawings
                try
                {
                    var png = Path.Combine(dir, "capture.png");
                    if (File.Exists(png))
                    {
                        Directory.CreateDirectory(Logger.LogDirectory);
                        var keep = Path.Combine(Logger.LogDirectory, "selftest-" + stamp + ".png");
                        File.Copy(png, keep, true);
                        sb.AppendLine("Ảnh chụp thử: " + keep);
                    }
                    Directory.Delete(dir, true);
                }
                catch (Exception ex) { Logger.Warn("selftest folder not removed: " + ex.Message); sb.AppendLine("Thư mục thử: " + dir); }
            }
            else sb.AppendLine("Thư mục thử (giữ lại để kiểm tra): " + dir);
            var file = Path.Combine(Logger.LogDirectory, "selftest-" + stamp + ".txt");
            try
            {
                Directory.CreateDirectory(Logger.LogDirectory);
                File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
                Process.Start("notepad.exe", "\"" + file + "\"");
            }
            catch (Exception ex) { Logger.Warn("selftest report not written: " + ex.Message); }
            return $"{steps.Count - bad}/{steps.Count} mục đạt. Báo cáo: {file}";
        }
    }
}
