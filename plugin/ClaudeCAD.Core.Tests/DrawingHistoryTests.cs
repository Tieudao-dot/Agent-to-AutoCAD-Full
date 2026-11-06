using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>v0.7: snapshot ("biên bản"), diff, manual change report, version history.</summary>
    public class DrawingHistoryTests
    {
        private static string Temp()
        {
            var d = Path.Combine(Path.GetTempPath(), "cchist-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        private static DrawingSnapshot Snap(params (string h, string layer, string sig)[] rows)
        {
            var s = new DrawingSnapshot { DrawingKey = "d:\\a.dwg" };
            foreach (var r in rows)
            {
                s.Entries[r.h] = new SnapshotEntry { Type = "Line", Layer = r.layer, Sig = r.sig };
                s.LayerCounts[r.layer] = s.LayerCounts.TryGetValue(r.layer, out var n) ? n + 1 : 1;
            }
            s.EntityCount = rows.Length;
            return s;
        }

        [Fact]
        public void Snapshot_round_trips_compressed()
        {
            var s = Snap(("1A", "A-WALL", "abc"), ("1B", "A-DOOR", "def"));
            s.Full = true;
            var bytes = s.ToBytes();
            var back = DrawingSnapshot.FromBytes(bytes);
            Assert.Equal("d:\\a.dwg", back.DrawingKey);
            Assert.True(back.Full);
            Assert.Equal(2, back.EntityCount);
            Assert.Equal("A-DOOR", back.Entries["1b"].Layer); // case-insensitive handles
            Assert.Equal(1, back.LayerCounts["A-WALL"]);
            Assert.Equal(bytes[0], (byte)0x1f); // gzip
        }

        [Fact]
        public void Signature_is_stable_and_ignores_float_noise()
        {
            var a = DrawingSnapshot.Sig(new[] { "Line", "A-WALL", DrawingSnapshot.N(100.000001), DrawingSnapshot.N(0) });
            var b = DrawingSnapshot.Sig(new[] { "Line", "A-WALL", DrawingSnapshot.N(99.999999), DrawingSnapshot.N(-0.0000001) });
            var c = DrawingSnapshot.Sig(new[] { "Line", "A-WALL", DrawingSnapshot.N(100.5), DrawingSnapshot.N(0) });
            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
            Assert.Equal(16, a.Length);
            Assert.NotEqual(DrawingSnapshot.Sig(new[] { "ab", "c" }), DrawingSnapshot.Sig(new[] { "a", "bc" }));
        }

        [Fact]
        public void Full_diff_finds_added_modified_erased()
        {
            var before = Snap(("1", "A-WALL", "s1"), ("2", "A-WALL", "s2"), ("3", "A-DOOR", "s3"));
            var now = Snap(("1", "A-WALL", "s1"), ("2", "A-WALL", "s2-moved"), ("4", "A-TEXT", "s4"));
            var d = SnapshotDiff.Compare(before, now);
            Assert.Equal(new[] { ("added", "A-TEXT", "4"), ("erased", "A-DOOR", "3"), ("modified", "A-WALL", "2") },
                d.Items.OrderBy(i => i.kind).ToArray());
            Assert.Empty(d.LayerCountChanges);
            Assert.True(SnapshotDiff.Compare(before, before).IsEmpty);
        }

        [Fact]
        public void Layer_change_counts_as_modified()
        {
            var d = SnapshotDiff.Compare(Snap(("1", "A-WALL", "s")), Snap(("1", "A-WALL-EX", "s")));
            Assert.Equal(("modified", "A-WALL-EX", "1"), d.Items.Single());
        }

        [Fact]
        public void Large_drawing_diff_uses_tracked_objects_and_layer_counts()
        {
            var before = Snap(("1", "A-WALL", "s1"), ("2", "A-WALL", "s2"));
            before.Full = false;
            before.LayerCounts["A-WALL"] = 30000;
            before.LayerCounts["X-OLD"] = 50;
            var now = Snap(("1", "A-WALL", "s1-changed"));
            now.Full = false;
            now.LayerCounts["A-WALL"] = 29990;
            now.LayerCounts["X-NEW"] = 7;
            var d = SnapshotDiff.Compare(before, now);
            Assert.Equal(new[] { ("erased", "A-WALL", "2"), ("modified", "A-WALL", "1") }, d.Items.OrderBy(i => i.kind).ToArray());
            Assert.Equal(new[] { ("A-WALL", 30000, 29990), ("X-NEW", 0, 7), ("X-OLD", 50, 0) }, d.LayerCountChanges.ToArray());
        }

        [Fact]
        public void Report_summarises_by_kind_and_layer_and_caps_handles()
        {
            var items = new List<(string, string, string)> { ("modified", "A-WALL", "1"), ("modified", "A-WALL", "2"), ("modified", "A-TEXT", "3"), ("erased", "A-DOOR", "4"), ("added", "", "5") };
            var r = ManualChangeReport.Build(items, null, "session");
            Assert.Equal("sửa 3 (A-WALL 2, A-TEXT 1); xóa 1 (A-DOOR 1); thêm 1 (0 1)", r["text"]);
            Assert.Equal("session", r["since"]);
            Assert.Equal(new object[] { "1", "2", "3" }, ((List<object>)r["modified"]).ToArray());
            Assert.False(r.ContainsKey("more"));
            Assert.Equal("sửa 3, xóa 1, thêm 1", ManualChangeReport.Short(items, 0));
            Assert.Null(ManualChangeReport.Build(new List<(string, string, string)>(), null, "session"));

            var many = Enumerable.Range(0, 450).Select(i => ("modified", "A-WALL", i.ToString("X"))).ToList();
            var big = ManualChangeReport.Build(many, new[] { ("A-DIM", 10, 12) }, "previous");
            Assert.Equal(ManualChangeReport.MaxHandles, ((List<object>)big["modified"]).Count);
            Assert.Equal(150, big["more"]);
            Assert.Contains("số đối tượng theo layer: A-DIM 10→12", (string)big["text"]);
            Assert.Equal("sửa 450, 1 layer đổi số lượng", ManualChangeReport.Short(many, 1));
        }

        [Fact]
        public void Versions_are_due_every_30_minutes()
        {
            var t = new DateTime(2026, 11, 6, 14, 0, 0);
            Assert.True(VersionStore.IsDue(null, t));
            Assert.False(VersionStore.IsDue(t, t.AddMinutes(29)));
            Assert.True(VersionStore.IsDue(t, t.AddMinutes(30)));
        }

        [Fact]
        public void Version_store_keeps_the_10_newest()
        {
            var store = new VersionStore(Temp(), "d:\\du an\\mb-tang1.dwg");
            var t0 = new DateTime(2026, 11, 6, 8, 0, 0);
            for (int i = 0; i < 12; i++)
            {
                var when = t0.AddMinutes(30 * i);
                var file = store.NewFile(when, out var id);
                File.WriteAllText(file, "dwg " + i);
                store.Register(id, when, "lượt " + i + "\nvẽ mặt bằng");
            }
            var list = store.List();
            Assert.Equal(VersionStore.Keep, list.Count);
            Assert.Equal("lượt 11 vẽ mặt bằng", list[0].Label);           // newest first, one line
            Assert.Equal("lượt 2 vẽ mặt bằng", list.Last().Label);
            Assert.Equal(VersionStore.Keep, Directory.GetFiles(store.Dir, "*.dwg").Length); // old copies deleted
            Assert.False(store.IsDue(t0.AddMinutes(30 * 11 + 10)));
            Assert.True(store.Delete(list[0].Id));
            Assert.Equal(VersionStore.Keep - 1, store.List().Count);

            var same = store.NewFile(t0, out var id1);
            File.WriteAllText(same, "x");
            store.NewFile(t0, out var id2);
            Assert.NotEqual(id1, id2); // two copies in the same second get distinct names
        }
    }
}
