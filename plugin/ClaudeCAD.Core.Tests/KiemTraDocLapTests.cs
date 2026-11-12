using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Thread "Kiểm tra độc lập" (12/11/2026): tests that PROVE bugs found in the independent review.
    /// They are expected to FAIL until the owning thread fixes the bug; each test names its KT-xx code
    /// (see docs/ban-giao/kiem-tra-doc-lap.md). Do not delete or skip them to get green: fix the code.
    /// </summary>
    public class KiemTraDocLapTests
    {
        private const string Machine = "CC-1A2B-3C4D-5E6F";
        private static readonly RSA Key = RSA.Create(2048);
        private static string Modulus => Convert.ToBase64String(Key.ExportParameters(false).Modulus);
        private static string Exponent => Convert.ToBase64String(Key.ExportParameters(false).Exponent);
        private static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string Sign(string body)
        {
            var sig = Key.SignData(Encoding.UTF8.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return body + "|" + B64Url(sig);
        }

        /// <summary>
        /// KT-01: the installer's offline paste (installer/ClaudeCAD.iss, Activate) keeps everything from "CCS1|" to the
        /// end as status.txt. The no-network email of taoKeyThuCong now has 3 lines (CCL1, CCS1, CCF1), so status.txt
        /// gets "CCS1…\r\nCCF1…". The plugin reads status.txt as one line and must still accept the CCS1 part.
        /// </summary>
        [Fact]
        public void KT01_status_file_written_by_installer_from_three_line_offline_paste_is_accepted()
        {
            var status = Sign($"CCS1|{Machine}|offline|2026-11-12");
            var plan = Sign($"CCF1|{Machine}|3d|2026-11-12");
            var statusTxt = status + "\r\n" + plan; // what the installer saves for a 3-line paste
            var r = LicenseLogic.VerifyStatus(statusTxt, Machine, new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc), Modulus, Exponent);
            Assert.True(r.Ok, "status.txt from a 3-line offline paste is rejected: " + r.Reason);
        }

        /// <summary>
        /// KT-10: the dispatcher records p.Raw, which carries the user_confirmed: true the assistant added after the
        /// user's yes. A saved macro must not keep it, or replaying it explodes older solids without asking.
        /// </summary>
        [Fact]
        public void KT10_macro_recorder_drops_user_confirmed()
        {
            var rec = new MacroRecorder();
            rec.Record("explode_entities", new Dictionary<string, object>
            {
                ["handles"] = new List<object> { "2F4A" },
                [SolidLogic.UserConfirmedParam] = true,
            }, null);
            var calls = rec.Take();
            Assert.False(calls[0].Params.ContainsKey(SolidLogic.UserConfirmedParam), "the macro keeps user_confirmed: replay skips the confirmation");
        }

        private static IEnumerable<PlanSeg> Poly(string h, params double[] xy)
        {
            for (int i = 0; i + 3 < xy.Length; i += 2) yield return new PlanSeg(xy[i], xy[i + 1], xy[i + 2], xy[i + 3], h);
        }

        /// <summary>
        /// KT-11: room 4×6 m, walls 220 on the axis (skill ve-kien-truc). Door gap in the RIGHT wall right at the corner
        /// (y 110..1010, its jamb on the inner face of the bottom wall: "bản lề đặt sát góc phòng"). The door must be
        /// cut into the right wall, never into the bottom wall.
        /// </summary>
        [Fact]
        public void KT11_door_at_room_corner_goes_into_its_own_wall()
        {
            double g0 = 110, g1 = g0 + 900;
            var s = new List<PlanSeg>();
            s.AddRange(Poly("A1", 4110, g1, 4110, 6110, -110, 6110, -110, -110, 4110, -110, 4110, g0));
            s.AddRange(Poly("A2", 3890, g1, 3890, 5890, 110, 5890, 110, 110, 3890, 110));
            s.Add(new PlanSeg(3890, g1, 4110, g1, "J1"));
            s.Add(new PlanSeg(3890, g0, 4110, g0, "J2"));
            var sym = new List<PlanSymbol>
            {
                new PlanSymbol { Kind = "door", Min = new double[] { 2990, g0 }, Max = new double[] { 3890, g0 }, Sources = { "D1" } },
                new PlanSymbol { Kind = "door", Min = new double[] { 2990, g0 }, Max = new double[] { 3890, g1 }, Sources = { "D2" } },
            };
            var r = FloorPlanLogic.Recognize(s, null, sym, new PlanOptions());
            foreach (var op in r.Openings)
            {
                var w = r.Walls[op.Wall];
                Assert.True(Math.Abs(w.Dir[1]) > 0.9, $"door cut into a horizontal wall (s {op.S0:0}–{op.S1:0}), not into the right wall where the gap is");
            }
            Assert.True(r.Openings.Count == 1 || r.OpenGaps.Count > 0 || r.Skipped.Count > 0, "the corner door is neither built nor reported");
        }

        /// <summary>KT-13 (đợt 2): an edge with no start point (closed edge whose curve could not be read) must not crash a side filter.</summary>
        [Fact]
        public void KT13_edge_side_filter_does_not_crash_on_edge_without_points()
        {
            var edges = new List<EdgeDesc> { new EdgeDesc { Index = 0, Straight = false } };
            var ex = Record.Exception(() => Solid2Logic.FilterEdges(edges, new[] { "top" }, new double[] { 0, 0, 0 }, new double[] { 100, 100, 100 }, "fillet_edges"));
            Assert.True(ex == null || ex is ToolException, "FilterEdges threw " + ex?.GetType().Name + " (internal plugin error) instead of skipping the edge");
        }

        /// <summary>KT-13 (đợt 2): a planar face with no bounds and no centre must not crash a face filter.</summary>
        [Fact]
        public void KT13_face_side_filter_does_not_crash_on_face_without_points()
        {
            var faces = new List<FaceDesc> { new FaceDesc { Index = 0, Planar = true, Normal = new double[] { 0, 0, 1 } } };
            var ex = Record.Exception(() => Solid2Logic.FilterFaces(faces, new[] { "top" }, new double[] { 0, 0, 0 }, new double[] { 100, 100, 100 }, "shell_solid"));
            Assert.True(ex == null || ex is ToolException, "FilterFaces threw " + ex?.GetType().Name + " (internal plugin error) instead of skipping the face");
        }
    }
}
