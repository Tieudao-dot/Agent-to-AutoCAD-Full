using System;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    public class SkillStoreTests
    {
        private static string Temp()
        {
            var d = Path.Combine(Path.GetTempPath(), "ccskills-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        private const string Good = "---\nname: ve-ket-cau\ndescription: Chuẩn vẽ kết cấu - layer S-*, dầm, cột, móng. Dùng khi vẽ kết cấu.\n---\n\n# Vẽ kết cấu\n\nNội dung.";

        private static string Write(string dir, string file, string text)
        {
            var p = Path.Combine(dir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, text, new UTF8Encoding(false));
            return p;
        }

        [Fact]
        public void ImportsAMarkdownFileAndRegistersIt()
        {
            var root = Temp();
            var src = Write(Temp(), "ket-cau.md", Good);
            var store = new SkillStore(root);
            var r = store.Import(src, false);
            Assert.Equal(SkillImportStatus.Added, r.Status);
            Assert.Equal("ve-ket-cau", r.Name);
            Assert.True(File.Exists(Path.Combine(root, "ve-ket-cau", "SKILL.md")));
            var list = store.Scan();
            var s = Assert.Single(list);
            Assert.True(s.Enabled);
            Assert.Null(s.Error);
            Assert.Equal("Vẽ kết cấu", s.Title);
            Assert.Equal(Path.GetFullPath(src), s.Source);
            Assert.Contains("\"ve-ket-cau\"", File.ReadAllText(Path.Combine(root, "skills.json")));
        }

        [Fact]
        public void NameComesFromFileWhenFrontmatterHasNone()
        {
            var root = Temp();
            var src = Write(Temp(), "Vẽ Cấp Thoát Nước.md", "---\ndescription: Chuẩn cấp thoát nước của tớ, dùng khi vẽ ống cấp nước, thoát nước.\n---\nx");
            var r = new SkillStore(root).Import(src, false);
            Assert.Equal("ve-cap-thoat-nuoc", r.Name);
            var md = File.ReadAllText(Path.Combine(root, "ve-cap-thoat-nuoc", "SKILL.md"));
            Assert.StartsWith("---\nname: ve-cap-thoat-nuoc\n", md);
            Assert.Null(new SkillStore(root).Scan().Single().Error);
        }

        [Theory]
        [InlineData("chỉ có nội dung, không có phần đầu", "frontmatter")]
        [InlineData("---\nname: x-y\n---\nbody", "description")]
        [InlineData("---\nname: x-y\ndescription: ngắn\n---\nbody", "quá ngắn")]
        public void RejectsInvalidFilesWithAReason(string text, string expected)
        {
            var r = new SkillStore(Temp()).Import(Write(Temp(), "a.md", text), false);
            Assert.Equal(SkillImportStatus.Invalid, r.Status);
            Assert.Contains(expected, r.Message);
        }

        [Fact]
        public void ConflictThenOverwrite()
        {
            var root = Temp();
            var store = new SkillStore(root);
            store.Import(Write(Temp(), "a.md", Good), false);
            var second = Write(Temp(), "b.md", Good.Replace("Nội dung.", "Bản mới."));
            Assert.Equal(SkillImportStatus.Conflict, store.Import(second, false).Status);
            Assert.Equal(SkillImportStatus.Updated, store.Import(second, true).Status);
            Assert.Contains("Bản mới.", File.ReadAllText(store.SkillFile("ve-ket-cau")));
        }

        [Fact]
        public void PickingAFileAlreadyInThePluginJustEnablesIt()
        {
            var root = Temp();
            var store = new SkillStore(root);
            store.Import(Write(Temp(), "a.md", Good), false);
            store.SetEnabled("ve-ket-cau", false);
            var r = store.Import(store.SkillFile("ve-ket-cau"), false);
            Assert.Equal(SkillImportStatus.AlreadyAdded, r.Status);
            Assert.True(store.Scan().Single().Enabled);
        }

        [Fact]
        public void SkillMdBringsItsReferenceFiles()
        {
            var src = Temp();
            Write(src, "ve-mong/SKILL.md", Good.Replace("ve-ket-cau", "ve-mong"));
            Write(src, "ve-mong/bang-thep.md", "| D | kg/m |");
            var root = Temp();
            var r = new SkillStore(root).Import(Path.Combine(src, "ve-mong", "SKILL.md"), false);
            Assert.Equal("ve-mong", r.Name);
            Assert.True(File.Exists(Path.Combine(root, "ve-mong", "bang-thep.md")));
        }

        [Fact]
        public void ToggleDeleteAndManualFolders()
        {
            var root = Temp();
            Write(root, "ve-kien-truc/SKILL.md", Good.Replace("ve-ket-cau", "ve-kien-truc"));
            Write(root, "_tam-tat/SKILL.md", Good);                    // ignored: starts with _
            Write(root, "sai-ten/SKILL.md", Good);                     // name mismatch → error shown
            var store = new SkillStore(root);
            var list = store.Scan();
            Assert.Equal(new[] { "sai-ten", "ve-kien-truc" }, list.Select(s => s.Name).ToArray());
            Assert.True(list.Single(s => s.Name == "ve-kien-truc").Builtin);
            Assert.Contains("phải giống nhau", list.Single(s => s.Name == "sai-ten").Error);

            store.SetEnabled("ve-kien-truc", false);
            Assert.False(store.Scan().Single(s => s.Name == "ve-kien-truc").Enabled);
            store.Delete("sai-ten");
            Assert.False(Directory.Exists(Path.Combine(root, "sai-ten")));
            Assert.Single(store.Scan());
            Assert.DoesNotContain("sai-ten", File.ReadAllText(Path.Combine(root, "skills.json")));
        }

        [Theory]
        [InlineData("Vẽ Kết Cấu (v2)", "ve-ket-cau-v2")]
        [InlineData("Điện  nhẹ", "dien-nhe")]
        [InlineData("__x__", "x")]
        public void Slugify(string input, string expected) => Assert.Equal(expected, SkillStore.Slugify(input));
    }
}
