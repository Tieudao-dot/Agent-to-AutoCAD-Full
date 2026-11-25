using System;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;
using ClaudeCAD.Plugin.UI;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>
    /// Sửa KFG phần G5, lượt 3 (KFG-39, KFG-40): the local copy of a network file the user attached, run on a real temporary
    /// folder (ChatAttachments is plain file code). Not parallel with other tests touching ChatAttachments.FolderForTests.
    /// </summary>
    [Collection("ChatAttachments")]
    public class SuaKfgB3Tests : IDisposable
    {
        private readonly string _root;
        private readonly string _chat;

        public SuaKfgB3Tests()
        {
            _root = Path.Combine(Path.GetTempPath(), "claudecad-kfg3-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            _chat = Path.Combine(_root, "anh-dinh-kem");
            Directory.CreateDirectory(_root);
            ChatAttachments.FolderForTests = _chat;
        }

        public void Dispose()
        {
            ChatAttachments.FolderForTests = null;
            try { Directory.Delete(_root, true); } catch (Exception) { }
        }

        private string Source(string name, int bytes, int ageDays)
        {
            var dir = Path.Combine(_root, "nas", Guid.NewGuid().ToString("N").Substring(0, 4));
            Directory.CreateDirectory(dir);
            var f = Path.Combine(dir, name);
            File.WriteAllBytes(f, Enumerable.Range(0, bytes).Select(i => (byte)(i * 7)).ToArray());
            File.SetLastWriteTimeUtc(f, DateTime.UtcNow.AddDays(-ageDays));
            return f;
        }

        // ------------------------------------------------------------ KFG-39: kept 7 days from the copy

        [Fact]
        public void Kfg39_two_old_network_files_in_one_message_both_stay()
        {
            var a = Source("scan.jpg", 3000, 30);
            var b = Source("scan2.jpg", 4000, 30);
            var ca = ChatAttachments.CopyForTrace(a);
            var cb = ChatAttachments.CopyForTrace(b); // its clean-up must not remove the first copy
            Assert.True(File.Exists(ca), "the first copy is still there");
            Assert.True(File.Exists(cb));
            Assert.True((DateTime.UtcNow - File.GetLastWriteTimeUtc(ca)).TotalMinutes < 5, "dated from the copy, not from the 30-day-old original");
            // then a pasted picture in the next message: the copy stays too
            Assert.NotNull(ChatAttachments.Save(Convert.ToBase64String(new byte[] { 1, 2, 3 }), "image/png"));
            Assert.True(File.Exists(ca));
        }

        [Fact]
        public void A_copy_older_than_seven_days_is_cleaned_and_a_used_one_is_kept_again()
        {
            var a = Source("a.png", 1000, 1);
            var ca = ChatAttachments.CopyForTrace(a);
            File.SetLastWriteTimeUtc(ca, DateTime.UtcNow.AddDays(-(G5Numbers.PastedKeepDays + 1)));
            var b = Source("b.png", 1000, 1);
            ChatAttachments.CopyForTrace(b);
            Assert.False(File.Exists(ca), "an unused copy older than 7 days is removed");
            // used again: the same version gets the same name and a new date
            var again = ChatAttachments.CopyForTrace(a);
            Assert.Equal(ca, again);
            var again2 = ChatAttachments.CopyForTrace(a);
            Assert.Equal(again, again2);
            Assert.True((DateTime.UtcNow - File.GetLastWriteTimeUtc(again2)).TotalMinutes < 5);
        }

        [Fact]
        public void Same_name_in_two_folders_and_a_changed_file_get_their_own_copies()
        {
            var a1 = Source("scan.jpg", 1000, 2);
            var a2 = Source("scan.jpg", 1000, 2); // same name, other folder
            Assert.NotEqual(ChatAttachments.CopyForTrace(a1), ChatAttachments.CopyForTrace(a2));
            var c1 = ChatAttachments.CopyForTrace(a1);
            File.WriteAllBytes(a1, new byte[1500]); // the user changed the file
            var c2 = ChatAttachments.CopyForTrace(a1);
            Assert.NotEqual(c1, c2);
            Assert.Equal(1500, new FileInfo(c2).Length);
        }

        // ------------------------------------------------------------ a copy cut by the network never becomes a picture

        [Fact]
        public void A_half_copy_is_never_used_and_is_cleaned_after_an_hour()
        {
            var a = Source("a.png", 2000, 1);
            var name = TraceLogic.NetworkCopyName(a, 2000, new FileInfo(a).LastWriteTimeUtc.Ticks);
            Directory.CreateDirectory(_chat);
            var half = Path.Combine(_chat, name + ".dang-chep-dead0001");
            File.WriteAllBytes(half, new byte[700]);
            File.SetLastWriteTimeUtc(half, DateTime.UtcNow.AddHours(-2));
            var c = ChatAttachments.CopyForTrace(a);
            Assert.Equal(Path.Combine(_chat, name), c);
            Assert.Equal(2000, new FileInfo(c).Length);
            Assert.False(File.Exists(half), "the old half copy is cleaned");
            Assert.Empty(Directory.GetFiles(_chat, "*.dang-chep-*"));
        }

        // ------------------------------------------------------------ KFG-40: reasons for Claude, nothing read at send

        [Fact]
        public void Kfg40_too_big_or_missing_files_say_why()
        {
            var gone = Path.Combine(_root, "nas", "khong-co.jpg");
            Assert.Contains("not there any more", Assert.Throws<IOException>(() => ChatAttachments.CopyForTrace(gone)).Message);
            var big = Path.Combine(_root, "big.png");
            using (var fs = File.Create(big)) fs.SetLength((long)(G5Numbers.MaxFileMb * 1024 * 1024) + 1);
            Assert.Contains("more than 50 MB", Assert.Throws<IOException>(() => ChatAttachments.CopyForTrace(big)).Message);
            Assert.False(Directory.Exists(_chat) && Directory.GetFiles(_chat).Length > 0, "nothing copied");
        }

        [Fact]
        public void Kfg40_sending_a_message_only_remembers_the_file()
        {
            var a = Source("a.png", 100, 1);
            Assert.False(ChatAttachments.IsUserFile(a));
            Assert.Equal(a, ChatAttachments.UserFile(a));
            Assert.True(ChatAttachments.IsUserFile(a));
            Assert.True(ChatAttachments.IsUserFile(a.ToUpperInvariant()), "case-free like Windows paths");
            Assert.False(Directory.Exists(_chat), "nothing is copied when the message is sent");
            Assert.Null(ChatAttachments.UserFile("  "));
            // only the palette adds to the list: Claude's own path is never a user file
            Assert.False(ChatAttachments.IsUserFile(Path.Combine(_root, "other.png")));
            var t = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ClaudeCAD.Plugin", "Drawing", "TraceTools.cs")));
            int copy = t.IndexOf("UI.ChatAttachments.CopyForTrace(path)", StringComparison.Ordinal);
            int rule = t.IndexOf("CheckPathAllowed(ctx, path, chatDir, tool); // the raw path", StringComparison.Ordinal);
            Assert.True(copy > 0 && rule > copy, "a user's network file is replaced by its local copy before the path rule runs");
            Assert.Contains("if (UI.ChatAttachments.IsUserFile(path) && TraceLogic.NeedsLocalCopy(path, OnNetworkDrive(path)))", t);
        }
    }
}
