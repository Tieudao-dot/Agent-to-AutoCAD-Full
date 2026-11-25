using System;
using System.IO;
using System.Linq;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>
    /// Nâng cấp đợt G5: every picture of a message has a file on disk, so Claude can attach it under the drawing
    /// (trace_image action attach). A picture chosen / dropped keeps its own file; a pasted one (clipboard, screenshot) is
    /// saved here once (named by its content). trace_image copies a file from here next to the drawing before attaching.
    /// </summary>
    public static class ChatAttachments
    {
        public static string Folder => FolderForTests ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD", "anh-dinh-kem");

        /// <summary>Tests only: another folder (the core tests run the copy / clean-up on a temporary folder).</summary>
        public static string FolderForTests;

        /// <summary>
        /// Sửa KFG-13: pasted pictures older than G5Numbers.PastedKeepDays are removed (a traced one was copied next to its
        /// drawing at attach).
        /// </summary>
        private static void CleanOld()
        {
            try
            {
                foreach (var f in Directory.GetFiles(Folder, "anh-*.*").Concat(Directory.GetFiles(Folder, "tep-*.*"))) // pasted, network copies
                    try
                    {
                        // KFG-39: the date of the copy (set to the time it was made / used again); a copy cut half-way: after an hour
                        var w = File.GetLastWriteTimeUtc(f);
                        if (Core.TraceLogic.IsOldPasted(w, DateTime.UtcNow) || (f.Contains(".dang-chep-") && (DateTime.UtcNow - w).TotalHours >= 1)) File.Delete(f);
                    }
                    catch (Exception) { }
            }
            catch (Exception) { }
        }

        // KFG-40: the files the user dragged / chose in this AutoCAD session (only the palette adds to it, never Claude)
        private static readonly System.Collections.Generic.HashSet<string> UserFiles = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string Key(string path) => (path ?? "").Trim().Replace('/', '\\');

        /// <summary>
        /// KFG-29 / KFG-40: the file the user dragged / chose, given to Claude as it is, and remembered as the user's own choice.
        /// Nothing is read here (a network file is only copied when trace_image attach needs it, see CopyForTrace), so sending a
        /// message never waits for the network.
        /// </summary>
        public static string UserFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            lock (UserFiles) UserFiles.Add(Key(path));
            return path;
        }

        /// <summary>The user attached this very file in the chat (a network file is then allowed, through its local copy).</summary>
        public static bool IsUserFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            lock (UserFiles) return UserFiles.Contains(Key(path));
        }

        /// <summary>
        /// KFG-29 / 39 / 40: a network file the user attached, copied here when trace_image attach needs it ("tep-…"). Copied to a
        /// temporary name and renamed when complete (a copy cut by the network never stays as a picture), its date set to now
        /// (kept PastedKeepDays from the copy, not from the original's date). Throws with the reason Claude tells the user.
        /// </summary>
        public static string CopyForTrace(string path)
        {
            FileInfo info;
            try { info = new FileInfo(path); }
            catch (Exception ex) { throw new IOException($"the path cannot be read ({ex.Message})"); }
            if (!info.Exists) throw new IOException("the file is not there any more (moved, renamed, or the network folder is not reachable)");
            if (info.Length > Core.G5Numbers.MaxFileMb * 1024 * 1024)
                throw new IOException($"the file is {info.Length / 1048576.0:0.#} MB, more than {Core.G5Numbers.MaxFileMb:0} MB (ask for a smaller scan)");
            Directory.CreateDirectory(Folder);
            CleanOld();
            var file = Path.Combine(Folder, Core.TraceLogic.NetworkCopyName(path, info.Length, info.LastWriteTimeUtc.Ticks));
            if (File.Exists(file))
            {
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
                return file;
            }
            var temp = file + ".dang-chep-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                File.Copy(path, temp);
                if (new FileInfo(temp).Length != info.Length) throw new IOException("the copy is incomplete (the network was cut)");
                File.Move(temp, file);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { }
                if (File.Exists(file) && !(ex is IOException && ex.Message.StartsWith("the copy"))) { File.SetLastWriteTimeUtc(file, DateTime.UtcNow); return file; } // another copy won the rename
                throw new IOException("the file could not be copied from the network (" + ex.Message + ")");
            }
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
            File.SetCreationTimeUtc(file, DateTime.UtcNow);
            return file;
        }

        /// <summary>The file of a pasted picture (null when it cannot be written: the message goes without a path).</summary>
        public static string Save(string base64, string mediaType)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64 ?? "");
                if (bytes.Length == 0) return null;
                string ext = mediaType == "image/jpeg" ? ".jpg" : mediaType == "image/gif" ? ".gif" : ".png";
                Directory.CreateDirectory(Folder);
                CleanOld();
                var file = Path.Combine(Folder, "anh-" + Core.F5Confirm.FileToken(base64).Substring(0, 12) + ext);
                if (!File.Exists(file)) File.WriteAllBytes(file, bytes);
                else File.SetLastWriteTimeUtc(file, DateTime.UtcNow); // used again: kept again
                return file;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
