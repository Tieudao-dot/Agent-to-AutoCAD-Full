using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt H1: renders one page of a PDF to a PNG with the PDF reader of Windows (Core/PdfRender.cs), in the user's
    /// temp folder, reusing an earlier render of the same file / page / width. Returns null (and why) when it cannot, so the
    /// caller keeps the old way (AutoCAD's view).
    /// </summary>
    public static class PdfPageImage
    {
        private static string Folder => Path.Combine(Path.GetTempPath(), "ClaudeCAD", "pdf-trang");

        public static string Render(string pdfPath, int page, int width, out string why)
        {
            why = null;
            try
            {
                if (string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath)) { why = "the PDF file is not there"; return null; }
                var info = new FileInfo(pdfPath);
                Directory.CreateDirectory(Folder);
                PdfRenderLogic.CleanCache(Folder, DateTime.UtcNow); // Test & sửa H1: the temp folder does not grow for ever
                string outPng = Path.Combine(Folder, PdfRenderLogic.CacheName(info.FullName, info.Length, info.LastWriteTimeUtc, page, width));
                if (File.Exists(outPng) && new FileInfo(outPng).Length > 0)
                {
                    try { File.SetLastWriteTimeUtc(outPng, DateTime.UtcNow); } catch (Exception) { } // used: kept again
                    return outPng;
                }
                // Test & sửa H1: written under a temporary name, renamed once whole (a render stopped at the time limit is never reused)
                string part = PdfRenderLogic.PartialName(outPng, Guid.NewGuid().ToString("N").Substring(0, 8));
                string script = Path.Combine(Folder, "pdf-trang.ps1");
                if (!File.Exists(script) || File.ReadAllText(script) != PdfRenderLogic.Script)
                    File.WriteAllText(script, PdfRenderLogic.Script, new UTF8Encoding(true)); // a BOM: Windows PowerShell reads it as UTF-8
                string ps = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
                if (!File.Exists(ps)) { why = "Windows PowerShell is not on this computer"; return null; }
                var psi = new ProcessStartInfo(ps, PdfRenderLogic.Arguments(script, info.FullName, page, width, part))
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using (var proc = Process.Start(psi))
                {
                    var err = proc.StandardError.ReadToEndAsync();
                    proc.StandardOutput.ReadToEndAsync();
                    if (!proc.WaitForExit(PdfRenderLogic.TimeoutSeconds * 1000))
                    {
                        try { proc.Kill(); proc.WaitForExit(2000); } catch (Exception) { }
                        TryDelete(part);
                        why = $"Windows did not render the page within {PdfRenderLogic.TimeoutSeconds} s";
                        return null;
                    }
                    if (proc.ExitCode != 0 || !File.Exists(part) || new FileInfo(part).Length == 0)
                    {
                        string e = err.Wait(1000) ? err.Result : "";
                        why = "Windows could not render the page" + (string.IsNullOrWhiteSpace(e) ? "" : " (" + Params.Truncate(e.Trim(), 200) + ")");
                        TryDelete(part);
                        return null;
                    }
                }
                TryDelete(outPng);
                File.Move(part, outPng);
                return outPng;
            }
            catch (Exception ex)
            {
                why = "rendering the page failed (" + ex.Message + ")";
                return null;
            }
        }

        private static void TryDelete(string f)
        {
            try { if (f != null && File.Exists(f)) File.Delete(f); } catch (Exception) { }
        }
    }
}
