using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>Service plans per machine (2D / 3D / Full): signed plan line, effective plan, tool labels.</summary>
    public class PlanTests
    {
        private const string Machine = "CC-1A2B-3C4D-5E6F";

        // a key pair made for the tests only (the plugin's real public key is never used to sign here)
        private static readonly RSA Key = RSA.Create(2048);
        private static string Modulus => Convert.ToBase64String(Key.ExportParameters(false).Modulus);
        private static string Exponent => Convert.ToBase64String(Key.ExportParameters(false).Exponent);

        private static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        /// <summary>"CCF1|machine|plan|date|sig", signed like the Apps Script does (RSA SHA-256 PKCS#1 v1.5).</summary>
        private static string Sign(string machine, string plan, string date)
        {
            var body = $"CCF1|{machine}|{plan}|{date}";
            var sig = Key.SignData(Encoding.UTF8.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return body + "|" + B64Url(sig);
        }

        private static PlanCheck Verify(string line, string machine = Machine) => LicenseLogic.VerifyPlan(line, machine, Modulus, Exponent);

        /// <summary>"prefix|…|sig" signed with the test key (CCS1 status lines, CCF1 plan lines).</summary>
        private static string SignBody(string body)
        {
            var sig = Key.SignData(Encoding.UTF8.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return body + "|" + B64Url(sig);
        }

        /// <summary>
        /// KT-01 (Kiểm tra độc lập, 12/11/2026; test copied from full/kiem-tra-doc-lap): the installer before the fix saved
        /// everything from "CCS1|" to the end of a 3-line offline paste (CCL1 / CCS1 / CCF1) as status.txt. The plugin must
        /// still read the CCS1 line, and the CCF1 line from the same text, so an offline machine is not stuck at 🔒.
        /// </summary>
        [Fact]
        public void KT01_status_file_written_by_installer_from_three_line_offline_paste_is_accepted()
        {
            var status = SignBody($"CCS1|{Machine}|offline|2026-11-12");
            var plan = SignBody($"CCF1|{Machine}|3d|2026-11-12");
            var statusTxt = status + "\r\n" + plan; // what the installer saved for a 3-line paste
            var r = LicenseLogic.VerifyStatus(statusTxt, Machine, new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc), Modulus, Exponent);
            Assert.True(r.Ok, "status.txt from a 3-line offline paste is rejected: " + r.Reason);
            Assert.True(r.Fresh);
            // the plan line is read from the same text too (and from a plan.txt that got the status line first)
            Assert.Equal("3d", LicenseLogic.VerifyPlan(statusTxt, Machine, Modulus, Exponent).Plan);
            Assert.True(LicenseLogic.VerifyPlan(" \n" + status + " " + plan + "\n", Machine, Modulus, Exponent).Ok);
            // a text without the prefix is still refused, as before
            Assert.False(LicenseLogic.VerifyStatus(plan, Machine, DateTime.UtcNow, Modulus, Exponent).Ok);
            Assert.Equal("x", LicenseLogic.PickLine("x", "CCS1"));
            Assert.Equal("CCS1|a", LicenseLogic.PickLine("CCL1|b\tCCS1|a\r\nCCS1|c", "CCS1"));
        }

        // ------------------------------------------------------------ VerifyPlan

        [Theory]
        [InlineData("2d")]
        [InlineData("3d")]
        [InlineData("2d3d")]
        public void Verify_plan_accepts_each_plan_for_this_machine(string plan)
        {
            var r = Verify(Sign(Machine, plan, "2026-11-12"));
            Assert.True(r.Ok, r.Reason);
            Assert.Equal(plan, r.Plan);
            Assert.Equal("2026-11-12", r.Date);
        }

        [Fact]
        public void Verify_plan_ignores_case_of_machine_and_surrounding_spaces()
        {
            Assert.True(Verify("  " + Sign(Machine, "3d", "2026-11-12") + "\r\n", Machine.ToLowerInvariant()).Ok);
        }

        [Fact]
        public void Verify_plan_refuses_another_machine()
        {
            var r = Verify(Sign("CC-9999-9999-9999", "2d3d", "2026-11-12"));
            Assert.False(r.Ok);
            Assert.Contains("máy khác", r.Reason);
        }

        [Fact]
        public void Verify_plan_refuses_an_edited_plan()
        {
            var line = Sign(Machine, "2d", "2026-11-12").Replace("|2d|", "|2d3d|");
            var r = Verify(line);
            Assert.False(r.Ok);
            Assert.Contains("không hợp lệ", r.Reason);
            // and a newer date on an old signature
            Assert.False(Verify(Sign(Machine, "2d3d", "2026-11-01").Replace("2026-11-01", "2026-12-01")).Ok);
        }

        [Fact]
        public void Verify_plan_refuses_a_broken_or_foreign_signature()
        {
            var line = Sign(Machine, "3d", "2026-11-12");
            var body = line.Substring(0, line.LastIndexOf('|'));
            Assert.False(Verify(body + "|%%%").Ok);
            Assert.False(Verify(body + "|AAAA").Ok);
            using (var other = RSA.Create(2048))
            {
                var sig = other.SignData(Encoding.UTF8.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                Assert.False(Verify(body + "|" + B64Url(sig)).Ok);
            }
            // the build's real key does not accept a line signed with the test key
            Assert.False(LicenseLogic.VerifyPlan(line, Machine).Ok);
        }

        [Fact]
        public void Verify_plan_refuses_an_unknown_plan_and_bad_format()
        {
            var r = Verify(Sign(Machine, "4d", "2026-11-12"));
            Assert.False(r.Ok);
            Assert.Contains("chưa biết", r.Reason);
            Assert.False(Verify(Sign(Machine, "2d", "12/11/2026")).Ok);
            Assert.False(Verify("").Ok);
            Assert.False(Verify(null).Ok);
            Assert.False(Verify("CCS1|" + Machine + "|active|2026-11-12|AAAA").Ok); // a status line is not a plan
            Assert.False(Verify("CCF1|" + Machine + "|2d|2026-11-12").Ok);
        }

        [Fact]
        public void Plan_text_is_recognised_alone_and_pasted_with_the_licence()
        {
            Assert.True(LicenseLogic.IsPlanText("CCF1|CC-1|2d|2026-11-12|x"));
            Assert.True(LicenseLogic.IsPlanText("CCL1|a|b|c\nCCF1|CC-1|3d|2026-11-12|x"));
            Assert.False(LicenseLogic.IsPlanText("CCS1|CC-1|active|2026-11-12|x"));
            Assert.False(LicenseLogic.IsPlanText(null));
        }

        [Fact]
        public void Parse_response_returns_the_plan_line()
        {
            var r = LicenseLogic.ParseResponse("{\"ok\":true,\"status\":\"CCS1|x\",\"plan\":\"CCF1|y\"}");
            Assert.True(r.ok);
            Assert.Equal("CCS1|x", r.status);
            Assert.Equal("CCF1|y", r.plan);
            Assert.Null(LicenseLogic.ParseResponse("{\"ok\":true,\"status\":\"CCS1|x\"}").plan); // an older service
            Assert.Null(LicenseLogic.ParseResponse("<html>").plan);
        }

        // ------------------------------------------------------------ EffectivePlan

        private static StatusCheck Status(string date, string status = "active") => new StatusCheck { Ok = true, Status = status, Date = date, Machine = Machine };

        [Fact]
        public void Effective_plan_uses_a_plan_line_as_new_as_the_status()
        {
            var plan = Verify(Sign(Machine, "2d3d", "2026-11-12"));
            Assert.Equal("2d3d", LicenseLogic.EffectivePlan(plan, Status("2026-11-12")));
            Assert.Equal("2d3d", LicenseLogic.EffectivePlan(plan, Status("2026-11-10"))); // newer than the status
            Assert.Equal("3d", LicenseLogic.EffectivePlan(Verify(Sign(Machine, "3d", "2026-11-12")), Status("2026-11-12")));
        }

        [Fact]
        public void Effective_plan_falls_back_when_the_plan_line_is_older_than_the_status()
        {
            // a kept old "Full" line (plan.txt made read-only) while status.txt keeps refreshing
            var old = Verify(Sign(Machine, "2d3d", "2026-11-01"));
            Assert.Equal(PlanLogic.DefaultPlan, LicenseLogic.EffectivePlan(old, Status("2026-11-12")));
        }

        [Fact]
        public void Effective_plan_keeps_the_plan_of_a_no_network_pass()
        {
            var old = Verify(Sign(Machine, "2d3d", "2026-01-01"));
            Assert.Equal("2d3d", LicenseLogic.EffectivePlan(old, Status("2026-11-12", "offline")));
        }

        [Fact]
        public void Effective_plan_without_a_valid_plan_line_is_the_default()
        {
            Assert.Equal(PlanLogic.DefaultPlan, LicenseLogic.EffectivePlan(null, Status("2026-11-12")));
            Assert.Equal(PlanLogic.DefaultPlan, LicenseLogic.EffectivePlan(new PlanCheck(), Status("2026-11-12")));
            Assert.Equal(PlanLogic.DefaultPlan, LicenseLogic.EffectivePlan(Verify(Sign("CC-9999-9999-9999", "2d3d", "2026-11-12")), Status("2026-11-12")));
            // no status line yet: the signed plan still counts
            Assert.Equal("3d", LicenseLogic.EffectivePlan(Verify(Sign(Machine, "3d", "2026-11-12")), null));
        }

        [Fact]
        public void Default_plan_is_2d()
        {
            // decided by the owner on 12/11/2026; a change goes here, in Code.gs.template and in the docs together
            Assert.Equal("2d", PlanLogic.DefaultPlan);
        }

        // ------------------------------------------------------------ PlanLogic

        [Fact]
        public void Normalize_and_labels()
        {
            Assert.Equal("2d3d", PlanLogic.Normalize(" 2D3D "));
            Assert.Equal(PlanLogic.DefaultPlan, PlanLogic.Normalize("full"));
            Assert.Equal(PlanLogic.DefaultPlan, PlanLogic.Normalize(null));
            Assert.Equal("2D", PlanLogic.Label("2d"));
            Assert.Equal("3D", PlanLogic.Label("3d"));
            Assert.Equal("Full", PlanLogic.Label("2d3d"));
        }

        [Fact]
        public void Allowed_follows_option_A()
        {
            // common: reading, layers, view, transforms, blocks and basic curves
            foreach (var plan in new[] { "2d", "3d", "2d3d" })
                foreach (var t in new[] { "ping", "get_drawing_info", "batch_execute", "capture_view", "draw_line", "draw_polyline", "draw_circle", "move_entities", "insert_block", "create_layer", "zoom_extents" })
                    Assert.True(PlanLogic.Allowed(plan, t), plan + " " + t);
            // 2D only: text, dimensions, hatch, layouts, PDF, 2D editing
            foreach (var t in new[] { "add_text", "add_mtext", "add_dim_linear", "draw_hatch", "create_layout", "plot_pdf", "offset_entity", "trim_entities", "create_dim_style" })
            {
                Assert.True(PlanLogic.Allowed("2d", t), t);
                Assert.False(PlanLogic.Allowed("3d", t), t);
                Assert.True(PlanLogic.Allowed("2d3d", t), t);
            }
        }

        [Fact]
        public void A_tool_nobody_labelled_is_only_open_to_the_full_plan()
        {
            Assert.Null(PlanLogic.GroupOf("extrude_solid_not_written_yet"));
            Assert.False(PlanLogic.Allowed("2d", "extrude_solid_not_written_yet"));
            Assert.False(PlanLogic.Allowed("3d", "extrude_solid_not_written_yet"));
            Assert.True(PlanLogic.Allowed("2d3d", "extrude_solid_not_written_yet"));
            Assert.False(PlanLogic.Allowed("2d", null));
        }

        [Fact]
        public void Allowed_tools_and_blocked_message()
        {
            var t2 = PlanLogic.AllowedTools("2d");
            var t3 = PlanLogic.AllowedTools("3d");
            Assert.Contains("add_text", t2);
            Assert.DoesNotContain("add_text", t3);
            Assert.Contains("draw_line", t3);
            Assert.Equal(PlanLogic.Groups.Count, PlanLogic.AllowedTools("2d3d").Count);
            var msg = PlanLogic.BlockedMessage("add_text", "3d", "Zalo 0");
            Assert.Contains("\"3D\"", msg);
            Assert.Contains("2D group", msg);
            Assert.Contains("(Zalo 0)", msg);
            Assert.EndsWith(".", PlanLogic.BlockedMessage("add_text", "3d", ""));
        }

        // ------------------------------------------------------------ every registered tool has a label

        private static string DrawingDir =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ClaudeCAD.Plugin", "Drawing"));

        private static string[] RegisteredTools()
        {
            var rx = new Regex("(?:Register\\(\\s*|new\\s+ToolDef\\(\\s*)\"([a-z0-9_]+)\"");
            return Directory.GetFiles(DrawingDir, "*.cs")
                .SelectMany(f => rx.Matches(File.ReadAllText(f)).Cast<Match>().Select(m => m.Groups[1].Value))
                .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        [Fact]
        public void Every_registered_tool_has_a_plan_label()
        {
            var tools = RegisteredTools();
            Assert.True(tools.Length >= 90, $"only {tools.Length} tools found in {DrawingDir}");
            var missing = tools.Where(t => PlanLogic.GroupOf(t) == null).ToList();
            Assert.True(missing.Count == 0, "label these tools in Core/Plans.cs (PlanLogic): " + string.Join(", ", missing));
        }

        [Fact]
        public void No_label_for_a_tool_that_does_not_exist()
        {
            var tools = RegisteredTools();
            var extra = PlanLogic.Groups.Keys.Where(t => !tools.Contains(t)).ToList();
            Assert.True(extra.Count == 0, "labels without a registered tool: " + string.Join(", ", extra));
        }
    }
}
