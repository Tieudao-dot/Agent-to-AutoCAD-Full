using System;
using System.IO;

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

        /// <summary>The file of a pasted picture (null when it cannot be written: the message goes without a path).</summary>
        public static string Save(string base64, string mediaType)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64 ?? "");
                if (bytes.Length == 0) return null;
                string ext = mediaType == "image/jpeg" ? ".jpg" : mediaType == "image/gif" ? ".gif" : ".png";
                Directory.CreateDirectory(Folder);
                var file = Path.Combine(Folder, "anh-" + Core.F5Confirm.FileToken(base64).Substring(0, 12) + ext);
                if (!File.Exists(file)) File.WriteAllBytes(file, bytes);
                return file;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
