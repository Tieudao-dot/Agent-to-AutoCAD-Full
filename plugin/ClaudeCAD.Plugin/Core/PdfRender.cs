using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H1: a page of a PDF made into a PNG on the user's computer, for the look of trace_image (AutoCAD may leave
    /// a PDF underlay out of its off-screen view). It uses the PDF reader built into Windows 10 / 11 (Windows.Data.Pdf),
    /// called from the Windows PowerShell that comes with Windows: no library added to the plugin, the same for net48 / net8 /
    /// net10, nothing sent anywhere. When it fails (older Windows, a locked PowerShell, a password) the look falls back to
    /// AutoCAD's view as before. The process is run by Drawing/PdfPageImage.cs; this part is AutoCAD-free and tested.
    /// </summary>
    public static class PdfRenderLogic
    {
        /// <summary>Internal: the longest render (seconds) before the fallback; the widest page render (px).</summary>
        public const int TimeoutSeconds = 30;
        public const int MinWidthPx = 1024, MaxWidthPx = 6000;

        /// <summary>
        /// Test & sửa H1 (internal): a page render not used for this many days is removed from the temp folder; a render cut
        /// off half way (its temporary name) after this many hours.
        /// </summary>
        public const int CacheKeepDays = 7, PartialKeepHours = 1;
        /// <summary>The temporary name of a render being written (renamed to the cache name only once it is complete).</summary>
        public const string PartialMark = ".dang-ve-";

        public static string PartialName(string outPng, string tag) => outPng + PartialMark + tag + ".png";

        /// <summary>
        /// Test & sửa H1: removes old renders from the cache folder (pdf-*.png unused for CacheKeepDays, half renders older than
        /// PartialKeepHours). Only files this code names are touched; errors (a file in use) are skipped.
        /// </summary>
        public static int CleanCache(string folder, DateTime nowUtc)
        {
            int removed = 0;
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return 0;
                foreach (var f in Directory.GetFiles(folder, "pdf-*.png"))
                {
                    try
                    {
                        bool partial = Path.GetFileName(f).IndexOf(PartialMark, StringComparison.Ordinal) >= 0;
                        var age = nowUtc - File.GetLastWriteTimeUtc(f);
                        if (partial ? age.TotalHours > PartialKeepHours : age.TotalDays > CacheKeepDays) { File.Delete(f); removed++; }
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            return removed;
        }

        /// <summary>The script given to powershell.exe -File (Windows PowerShell 5.1, .NET Framework: WinRT types load there).</summary>
        public const string Script =
            "param([string]$Pdf, [int]$Page, [int]$Width, [string]$Out)\r\n" +
            "$ErrorActionPreference = 'Stop'\r\n" +
            "Add-Type -AssemblyName System.Runtime.WindowsRuntime\r\n" +
            "$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]\r\n" +
            "$null = [Windows.Storage.StorageFolder, Windows.Storage, ContentType = WindowsRuntime]\r\n" +
            "$null = [Windows.Data.Pdf.PdfDocument, Windows.Data.Pdf, ContentType = WindowsRuntime]\r\n" +
            "$null = [Windows.Data.Pdf.PdfPageRenderOptions, Windows.Data.Pdf, ContentType = WindowsRuntime]\r\n" +
            "$ext = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 }\r\n" +
            "$asOp = $ext | Where-Object { $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1\r\n" +
            "$asAct = $ext | Where-Object { $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' } | Select-Object -First 1\r\n" +
            "function Wait-Op($op, [Type]$t) { $task = $asOp.MakeGenericMethod($t).Invoke($null, @($op)); $task.Wait(-1) | Out-Null; $task.Result }\r\n" +
            "function Wait-Act($op) { $task = $asAct.Invoke($null, @($op)); $task.Wait(-1) | Out-Null }\r\n" +
            "$file = Wait-Op ([Windows.Storage.StorageFile]::GetFileFromPathAsync($Pdf)) ([Windows.Storage.StorageFile])\r\n" +
            "$doc = Wait-Op ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)) ([Windows.Data.Pdf.PdfDocument])\r\n" +
            "if ($Page -lt 1 -or $Page -gt $doc.PageCount) { exit 3 }\r\n" +
            "$pg = $doc.GetPage([uint32]($Page - 1))\r\n" +
            "$opt = New-Object Windows.Data.Pdf.PdfPageRenderOptions\r\n" +
            "$opt.DestinationWidth = [uint32]$Width\r\n" +
            "$folder = Wait-Op ([Windows.Storage.StorageFolder]::GetFolderFromPathAsync([IO.Path]::GetDirectoryName($Out))) ([Windows.Storage.StorageFolder])\r\n" +
            "$outFile = Wait-Op ($folder.CreateFileAsync([IO.Path]::GetFileName($Out), [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])\r\n" +
            "$stream = Wait-Op ($outFile.OpenAsync([Windows.Storage.FileAccessMode]::ReadWrite)) ([Windows.Storage.Streams.IRandomAccessStream])\r\n" +
            "try { Wait-Act ($pg.RenderToStreamAsync($stream, $opt)) } finally { $stream.Dispose(); $pg.Dispose() }\r\n" +
            "Write-Output $doc.PageCount\r\n";

        /// <summary>A path as one argument of the command line (Windows paths have no quote; a trailing backslash is doubled).</summary>
        public static string Quote(string arg)
        {
            var s = arg ?? "";
            if (s.EndsWith("\\", StringComparison.Ordinal)) s += "\\";
            return "\"" + s + "\"";
        }

        public static string Arguments(string scriptPath, string pdf, int page, int width, string outPng) =>
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(scriptPath) +
            " -Pdf " + Quote(pdf) + " -Page " + page.ToString(CultureInfo.InvariantCulture) +
            " -Width " + width.ToString(CultureInfo.InvariantCulture) + " -Out " + Quote(outPng);

        /// <summary>The width to render: enough for the part shown (like the raster look), between MinWidthPx and MaxWidthPx.</summary>
        public static int RenderWidth(double shownShareOfWidth, int lookW)
        {
            double share = Math.Max(1e-6, Math.Min(1, shownShareOfWidth));
            double want = Math.Ceiling(lookW / share * 1.25);
            int w = (int)Math.Max(MinWidthPx, Math.Min(MaxWidthPx, want));
            return Math.Min(MaxWidthPx, (w + 255) / 256 * 256); // steps of 256: renders are reused
        }

        /// <summary>The cached PNG of a page: one per file (path, size, date), page and width.</summary>
        public static string CacheName(string pdfPath, long size, DateTime written, int page, int width)
        {
            var key = (pdfPath ?? "").Trim().ToUpperInvariant() + "|" + size.ToString(CultureInfo.InvariantCulture) + "|" +
                      written.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
            ulong h = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(key)) { h ^= b; h *= 1099511628211UL; }
            return "pdf-" + h.ToString("x16", CultureInfo.InvariantCulture) + "-p" + page.ToString(CultureInfo.InvariantCulture) +
                   "-w" + width.ToString(CultureInfo.InvariantCulture) + ".png";
        }

        /// <summary>
        /// A one-page PDF (A4, a thick black frame on white) for the self-test, with a correct cross-reference table so any
        /// reader opens it.
        /// </summary>
        public static byte[] SamplePdf()
        {
            const string content = "0 0 0 RG 12 w 60 60 475 722 re S\n";
            var objs = new[]
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << >> >>",
                "<< /Length " + content.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n" + content + "endstream",
            };
            var sb = new StringBuilder("%PDF-1.4\n");
            var offsets = new int[objs.Length];
            for (int i = 0; i < objs.Length; i++)
            {
                offsets[i] = sb.Length;
                sb.Append(i + 1).Append(" 0 obj\n").Append(objs[i]).Append("\nendobj\n");
            }
            int xref = sb.Length;
            sb.Append("xref\n0 ").Append(objs.Length + 1).Append("\n0000000000 65535 f \n");
            foreach (var o in offsets) sb.Append(o.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            sb.Append("trailer\n<< /Size ").Append(objs.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        /// <summary>Width and height of a PNG from its header (null when it is not a PNG).</summary>
        public static (int w, int h)? PngSize(byte[] head)
        {
            if (head == null || head.Length < 24) return null;
            byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            if (!sig.SequenceEqual(head.Take(8))) return null;
            int Be(int i) => (head[i] << 24) | (head[i + 1] << 16) | (head[i + 2] << 8) | head[i + 3];
            int w = Be(16), h = Be(20);
            return w > 0 && h > 0 ? (w, h) : ((int, int)?)null;
        }
    }
}
