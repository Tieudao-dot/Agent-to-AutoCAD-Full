using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>v0.6: macros, change summary, @ names, project notes, saved templates, new settings.</summary>
    public class ChatExtrasTests
    {
        private static string Temp()
        {
            var d = Path.Combine(Path.GetTempPath(), "ccextras-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        private static Dictionary<string, object> P(params (string k, object v)[] kv) => kv.ToDictionary(x => x.k, x => x.v);
        private static Dictionary<string, object> R(params string[] handles) => new Dictionary<string, object> { ["handles"] = handles.Cast<object>().ToList() };

        // ------------------------------------------------------------------ macros

        [Fact]
        public void Recorder_copies_params_and_keeps_result_handles()
        {
            var rec = new MacroRecorder();
            var pts = new List<object> { new List<object> { 0.0, 0.0 }, new List<object> { 4000.0, 0.0 } };
            var p = P(("vertices", pts), ("layer", "A-WALL"));
            rec.Record("draw_polyline", p, R("2F4A"));
            pts.Add("changed later");
            var calls = rec.Take();
            Assert.Single(calls);
            Assert.Equal(2, ((List<object>)calls[0].Params["vertices"]).Count);
            Assert.Equal(new[] { "2F4A" }, calls[0].ResultHandles);
            Assert.Null(rec.Take()); // emptied
        }

        [Fact]
        public void Recorder_gives_up_on_too_long_turns()
        {
            var rec = new MacroRecorder();
            for (int i = 0; i <= MacroLogic.MaxCalls; i++) rec.Record("draw_line", P(), R());
            Assert.Null(rec.Take());
        }

        [Fact]
        public void Only_drawing_writes_are_recorded()
        {
            Assert.True(MacroLogic.ShouldRecord("draw_line", true));
            Assert.False(MacroLogic.ShouldRecord("list_layers", false));
            Assert.False(MacroLogic.ShouldRecord("zoom_extents", true));
            Assert.False(MacroLogic.ShouldRecord("purge_unused", true));
            Assert.False(MacroLogic.ShouldRecord("batch_execute", true));
        }

        [Fact]
        public void Replay_remaps_handles_created_by_earlier_calls()
        {
            var m = new Macro { Name = "Khung tên A3" };
            m.Calls.Add(new MacroCall { Tool = "draw_rectangle", Params = P(("corner1", new List<object> { 0.0, 0.0 })), ResultHandles = new List<string> { "A1" } });
            m.Calls.Add(new MacroCall { Tool = "offset_entity", Params = P(("handle", "A1"), ("distance", 10.0)), ResultHandles = new List<string> { "A2" } });
            m.Calls.Add(new MacroCall { Tool = "move_entities", Params = P(("handles", new List<object> { "A1", "A2", "FF" }), ("displacement", new List<object> { 5.0, 0.0 })), ResultHandles = new List<string> { "A1", "A2", "FF" } });

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            MacroLogic.Learn(map, m.Calls[0].ResultHandles, new List<string> { "B7" });
            var p1 = (Dictionary<string, object>)MacroLogic.Remap(m.Calls[1].Params, map);
            Assert.Equal("B7", p1["handle"]);
            Assert.Equal(10.0, p1["distance"]);
            MacroLogic.Learn(map, m.Calls[1].ResultHandles, new List<string> { "B8" });
            var p2 = (Dictionary<string, object>)MacroLogic.Remap(m.Calls[2].Params, map);
            Assert.Equal(new object[] { "B7", "B8", "FF" }, ((List<object>)p2["handles"]).ToArray());
            Assert.Equal("A1", m.Calls[1].Params["handle"]); // the macro itself is untouched

            Assert.Equal(new[] { "FF" }, MacroLogic.ExternalHandles(m));
            Assert.Equal(new[] { "A1", "A2" }, MacroLogic.CreatedHandles(m));
            Assert.StartsWith("3 lệnh:", MacroLogic.Describe(m));
        }

        [Fact]
        public void Macro_calls_are_grouped_into_independent_batches()
        {
            MacroCall C(string tool, string created, params string[] uses) => new MacroCall
            {
                Tool = tool,
                Params = uses.Length == 0 ? P(("layer", "A-WALL")) : P(("handles", uses.Cast<object>().ToList())),
                ResultHandles = created == null ? new List<string>() : new List<string> { created },
            };
            var calls = new List<MacroCall>
            {
                C("create_layer", "L1"),
                C("draw_line", "A1"),
                C("draw_line", "A2"),
                C("move_entities", "A1", "A1"),      // uses A1 created in the current batch → new batch
                C("draw_circle", "A3"),
                C("purge_unused", null),              // not batchable → alone
                C("draw_line", "A4"),
            };
            var groups = MacroLogic.PlanBatches(calls, t => t != "purge_unused");
            Assert.Equal(new[] { "0,1,2", "3,4", "5", "6" }, groups.Select(g => string.Join(",", g)).ToArray());
            var capped = MacroLogic.PlanBatches(Enumerable.Range(0, 5).Select(i => C("draw_line", "X" + i)).ToList(), t => true, 2);
            Assert.Equal(new[] { 2, 2, 1 }, capped.Select(g => g.Count).ToArray());
            Assert.True(MacroLogic.References(P(("nested", P(("h", "A1")))), new HashSet<string> { "A1" }));
            Assert.False(MacroLogic.References(P(("h", "A9")), new HashSet<string> { "A1" }));
        }

        [Fact]
        public void Learn_keeps_the_first_mapping()
        {
            var map = new Dictionary<string, string>();
            MacroLogic.Learn(map, new List<string> { "A1" }, new List<string> { "B1" });
            MacroLogic.Learn(map, new List<string> { "A1", "A2" }, new List<string> { "B1" });
            Assert.Equal("B1", map["A1"]);
            Assert.False(map.ContainsKey("A2"));
        }

        [Fact]
        public void Macro_store_round_trips_and_slugifies_names()
        {
            var store = new MacroStore(Temp());
            var m = new Macro { Name = "Ký hiệu trục đứng", Description = "vẽ ký hiệu trục", BasePoint = new[] { 100.0, 200.5 } };
            m.Calls.Add(new MacroCall { Tool = "draw_circle", Params = P(("center", new List<object> { 0.0, 0.0 }), ("radius", 400.0)), ResultHandles = new List<string> { "1A" } });
            store.Save(m);
            Assert.True(File.Exists(Path.Combine(store.Root, "ky-hieu-truc-dung.json")));
            Assert.True(store.Exists("Ký hiệu trục đứng"));
            var back = store.Load("Ký hiệu trục đứng");
            Assert.Equal("Ký hiệu trục đứng", back.Name);
            Assert.Equal(new[] { 100.0, 200.5 }, back.BasePoint);
            Assert.Equal(400.0, back.Calls[0].Params["radius"]);
            Assert.Equal(new[] { "1A" }, back.Calls[0].ResultHandles);
            Assert.Single(store.List());
            Assert.True(store.Delete("Ký hiệu trục đứng"));
            Assert.Empty(store.List());
            Assert.Equal("macro.json", MacroStore.FileName("!!!"));
        }

        // ------------------------------------------------------------------ change summary

        [Fact]
        public void Tracker_nets_out_temporary_objects()
        {
            var t = new ChangeTracker();
            t.Appended("1");          // new
            t.Modified("1");          // still "added"
            t.Appended("2");
            t.Erased("2", true);      // temporary → nothing
            t.Modified("3");          // existing object changed
            t.Erased("4", true);      // existing object erased
            t.Modified("4");          // ignored once erased
            t.Erased("5", true);
            t.Erased("5", false);     // erased and restored → changed
            var snap = t.Snapshot().OrderBy(x => x.handle).ToList();
            Assert.Equal(new[] { ("added", "1"), ("modified", "3"), ("erased", "4"), ("modified", "5") }, snap.ToArray());
            t.Clear();
            Assert.True(t.IsEmpty);
        }

        [Fact]
        public void Summary_groups_by_kind_and_layer_and_round_trips()
        {
            var s = ChangeSummary.Build(new[]
            {
                ("added", "A-WALL", "1"), ("added", "A-WALL", "2"), ("added", "A-DOOR", "3"),
                ("modified", "A-TEXT", "4"), ("erased", "", "5"),
            });
            Assert.Equal("Thêm 3 · Sửa 1 · Xóa 1", s.Headline);
            Assert.Equal(new[] { "added A-WALL 2", "added A-DOOR 1", "modified A-TEXT 1", "erased 0 1" },
                s.Rows.Select(r => $"{r.Kind} {r.Layer} {r.Count}").ToArray());
            var back = ChangeSummary.FromDict((Dictionary<string, object>)JsonCodec.Parse(JsonCodec.Serialize(s.ToDict())));
            Assert.Equal(s.Headline, back.Headline);
            Assert.Equal(new[] { "1", "2" }, back.Rows[0].Handles);
        }

        [Fact]
        public void Transcript_keeps_read_only_notes_and_changes()
        {
            var t = new ChatTranscript { DrawingKey = "k", ReadOnly = true, Notes = "tường bao 220" };
            t.Entries.Add(new ChatEntry { Role = "assistant", Text = "xong", Changes = ChangeSummary.Build(new[] { ("added", "A-WALL", "1") }) });
            var back = ChatTranscript.FromJson(t.ToJson());
            Assert.True(back.ReadOnly);
            Assert.Equal("tường bao 220", back.Notes);
            Assert.Equal(1, back.Entries[0].Changes.Added);
            Assert.Contains("Thêm 1 trên A-WALL", back.ToMarkdown());
            var old = ChatTranscript.FromJson("{\"drawing_key\":\"k\",\"entries\":[{\"role\":\"user\",\"text\":\"x\"}]}");
            Assert.False(old.ReadOnly);
            Assert.Null(old.Entries[0].Changes);
        }

        // ------------------------------------------------------------------ @ names

        [Theory]
        [InlineData("chuyển sang @A-W", 16, true, 12, "A-W")]
        [InlineData("@", 1, true, 0, "")]
        [InlineData("(@blo", 5, true, 1, "blo")]
        [InlineData("mail a@b.com", 12, false, -1, null)]
        [InlineData("@A-WALL xong", 12, false, -1, null)]
        [InlineData("không có", 8, false, -1, null)]
        public void Mention_token_at_caret(string text, int caret, bool ok, int start, string query)
        {
            Assert.Equal(ok, Mentions.TryGetToken(text, caret, out var s, out var q));
            Assert.Equal(start, s);
            Assert.Equal(query, q);
        }

        [Fact]
        public void Mention_filter_and_insert()
        {
            var items = new List<MentionItem>
            {
                new MentionItem { Name = "A-WALL", Kind = "layer" },
                new MentionItem { Name = "X-A-WIN", Kind = "layer" },
                new MentionItem { Name = "A-WIN", Kind = "block" },
                new MentionItem { Name = "Standard", Kind = "text_style" },
            };
            Assert.Equal(new[] { "A-WALL", "A-WIN", "X-A-WIN" }, Mentions.Filter(items, "a-w").Select(m => m.Name).ToArray());
            Assert.Equal(4, Mentions.Filter(items, "").Count);

            var text = Mentions.Insert("đổi sang @a-wa rồi", 9, 14, "A-WALL", out var caret);
            Assert.Equal("đổi sang A-WALL rồi", text);
            Assert.Equal(15, caret);
            var end = Mentions.Insert("block @cua", 6, 10, "Cửa đi 1 cánh", out var c2);
            Assert.Equal("block \"Cửa đi 1 cánh\" ", end);
            Assert.Equal(end.Length, c2);
        }

        // ------------------------------------------------------------------ layer scope

        [Fact]
        public void Layer_counts_filter_and_labels()
        {
            var layers = new List<string> { "A-WALL", "A-DOOR", "A-WALL", "", "a-wall" };
            var counts = LayerSelection.CountByLayer(layers);
            Assert.Equal(new[] { "A-WALL", "0", "A-DOOR" }, counts.Keys.ToArray());
            Assert.Equal(3, counts["a-wall"]);

            var handles = new List<string> { "1", "2", "3", "4", "5" };
            Assert.Equal(new[] { "2", "4" }, LayerSelection.Filter(handles, layers, new[] { "a-wall" }));
            Assert.Equal(handles, LayerSelection.Filter(handles, layers, new string[0]));
            Assert.Equal(handles, LayerSelection.Filter(handles, new List<string>(), new[] { "A-WALL" })); // layers unknown → keep all

            Assert.Equal("A, B, C +2", LayerSelection.Label(new[] { "A", "B", "C", "D", "E" }));
            var rows = new[] { new LayerRow { Name = "A-WALL" }, new LayerRow { Name = "A-WALL-EX" }, new LayerRow { Name = "M-DUCT" } };
            Assert.Equal(new[] { "A-WALL", "A-WALL-EX" }, LayerSelection.Search(rows, "wall").Select(r => r.Name).ToArray());
            Assert.Equal(new[] { "A-WALL-EX" }, LayerSelection.Search(rows, "wall ex").Select(r => r.Name).ToArray());
            Assert.Equal(3, LayerSelection.Search(rows, "").Count);
            Assert.Equal("hiện hành, khóa", new LayerRow { Current = true, Locked = true }.State);
        }

        [Fact]
        public void User_entry_keeps_chosen_layers()
        {
            var t = new ChatTranscript { DrawingKey = "k" };
            t.Entries.Add(new ChatEntry { Role = "user", Text = "đổi màu sang đỏ", Layers = { "A-WALL", "A-DOOR" } });
            var back = ChatTranscript.FromJson(t.ToJson());
            Assert.Equal(new[] { "A-WALL", "A-DOOR" }, back.Entries[0].Layers);
            Assert.Contains("Layer đã chọn: A-WALL, A-DOOR", back.ToMarkdown());
        }

        // ------------------------------------------------------------------ project notes

        [Fact]
        public void Project_notes_live_next_to_the_drawing()
        {
            var dir = Temp();
            var dwg = Path.Combine(dir, "MB-tang1.dwg");
            Assert.Equal("", ProjectNotes.LoadFolder(dwg));
            ProjectNotes.SaveFolder(dwg, "  cao độ ±0.000 = +3.200  ");
            Assert.Equal("cao độ ±0.000 = +3.200\n", File.ReadAllText(Path.Combine(dir, ProjectNotes.FolderFileName)));
            Assert.Equal("cao độ ±0.000 = +3.200\n", ProjectNotes.LoadFolder(Path.Combine(dir, "MB-tang2.dwg")));
            ProjectNotes.SaveFolder(dwg, "");
            Assert.False(File.Exists(Path.Combine(dir, ProjectNotes.FolderFileName)));
            Assert.Null(ProjectNotes.FolderFile(null));
            Assert.Throws<InvalidOperationException>(() => ProjectNotes.SaveFolder(null, "x"));
        }

        [Fact]
        public void Project_notes_combine_and_clip()
        {
            Assert.Null(ProjectNotes.Combine("", "  "));
            var c = ProjectNotes.Combine("tường bao 220", "bản vẽ tầng 1");
            Assert.StartsWith("Ghi chú dự án (cả thư mục):\ntường bao 220", c);
            Assert.EndsWith("Ghi chú riêng bản vẽ này:\nbản vẽ tầng 1", c);
            Assert.Equal(ProjectNotes.MaxChars, ProjectNotes.Clip(new string('x', 9000)).Length);
        }

        // ------------------------------------------------------------------ templates + settings

        [Fact]
        public void Save_user_template_slugifies_and_keeps_names_unique()
        {
            var file = Path.Combine(Temp(), "templates.json");
            var a = QuickTemplates.SaveUser(file, "Lưới trục 5x4", "vẽ lưới trục {{số trục=5x4}}");
            var b = QuickTemplates.SaveUser(file, "Lưới trục 5x4", "vẽ lưới trục khác");
            Assert.Equal("luoi-truc-5x4", a.Name);
            Assert.Equal("luoi-truc-5x4-2", b.Name);
            var all = QuickTemplates.LoadAll(null, file);
            Assert.Equal(2, all.Count);
            Assert.Equal("vẽ lưới trục {{số trục=5x4}}", all[0].Text);
            Assert.True(QuickTemplates.DeleteUser(file, "luoi-truc-5x4"));
            Assert.Single(QuickTemplates.LoadAll(null, file));
            var c = QuickTemplates.SaveUser(file, "", "vẽ cửa đi rộng 900 tại điểm đã chọn\nrồi ghi kích thước");
            Assert.Equal("ve-cua-di-rong-900-tai-diem-da", c.Name);
            Assert.DoesNotContain("\n", c.Title);
        }

        [Fact]
        public void New_settings_default_on_and_round_trip()
        {
            var s = PaletteSettings.FromJson("{}");
            Assert.True(s.VisualCheck && s.PlanFirst && s.Suggestions && s.Hotkey && s.Notify);
            s.VisualCheck = false;
            s.Hotkey = false;
            var back = PaletteSettings.FromJson(s.ToJson());
            Assert.False(back.VisualCheck);
            Assert.False(back.Hotkey);
            Assert.True(back.PlanFirst);
        }
    }
}
