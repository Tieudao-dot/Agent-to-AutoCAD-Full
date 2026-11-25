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
        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD", "anh-dinh-kem");

        /// <summary>
        /// Sửa KFG-13: pasted pictures older than G5Numbers.PastedKeepDays are removed (a traced one was copied next to its
        /// drawing at attach).
        /// </summary>
        private static void CleanOld()
        {
            try
            {
                foreach (var f in Directory.GetFiles(Folder, "anh-*.*").Concat(Directory.GetFiles(Folder, "tep-*.*"))) // pasted, network copies
                    try { if (Core.TraceLogic.IsOldPasted(File.GetLastWriteTimeUtc(f), DateTime.UtcNow)) File.Delete(f); } catch (Exception) { }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// KFG-29: the file to give Claude for a picture the user dragged / chose. A file of the network (\\server\share, a mapped
        /// network drive, a device path) is copied here first, so trace_image only ever opens a file of this computer: the user
        /// chose that file themselves, and Claude never reaches the network computer. Null when the copy fails (no path sent).
        /// </summary>
        public static string LocalFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            bool network = false;
            try
            {
                var plain = Core.TraceLogic.PlainPath(path);
                if (plain != null && Core.TraceLogic.IsAbsoluteDrivePath(plain))
                    network = new DriveInfo(plain.Substring(0, 1)).DriveType == DriveType.Network;
            }
            catch (Exception) { }
            if (!Core.TraceLogic.NeedsLocalCopy(path, network)) return path;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > Core.G5Numbers.MaxFileMb * 1024 * 1024) return null;
                Directory.CreateDirectory(Folder);
                CleanOld();
                var file = Path.Combine(Folder, "tep-" + Core.F5Confirm.FileToken(path + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks).Substring(0, 12) + Path.GetExtension(path).ToLowerInvariant());
                if (!File.Exists(file)) File.Copy(path, file);
                else File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
                return file;
            }
            catch (Exception)
            {
                return null;
            }
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
