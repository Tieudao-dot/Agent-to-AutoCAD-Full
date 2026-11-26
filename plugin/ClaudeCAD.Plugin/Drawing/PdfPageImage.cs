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
                string outPng = Path.Combine(Folder, PdfRenderLogic.CacheName(info.FullName, info.Length, info.LastWriteTimeUtc, page, width));
                if (File.Exists(outPng) && new FileInfo(outPng).Length > 0) return outPng;
                string script = Path.Combine(Folder, "pdf-trang.ps1");
                if (!File.Exists(script) || File.ReadAllText(script) != PdfRenderLogic.Script)
                    File.WriteAllText(script, PdfRenderLogic.Script, new UTF8Encoding(true)); // a BOM: Windows PowerShell reads it as UTF-8
                string ps = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
                if (!File.Exists(ps)) { why = "Windows PowerShell is not on this computer"; return null; }
                var psi = new ProcessStartInfo(ps, PdfRenderLogic.Arguments(script, info.FullName, page, width, outPng))
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
                        try { proc.Kill(); } catch (Exception) { }
                        why = $"Windows did not render the page within {PdfRenderLogic.TimeoutSeconds} s";
                        return null;
                    }
                    if (proc.ExitCode != 0 || !File.Exists(outPng) || new FileInfo(outPng).Length == 0)
                    {
                        string e = err.Wait(1000) ? err.Result : "";
                        why = "Windows could not render the page" + (string.IsNullOrWhiteSpace(e) ? "" : " (" + Params.Truncate(e.Trim(), 200) + ")");
                        try { if (File.Exists(outPng)) File.Delete(outPng); } catch (Exception) { }
                        return null;
                    }
                }
                return outPng;
            }
            catch (Exception ex)
            {
                why = "rendering the page failed (" + ex.Message + ")";
                return null;
            }
        }
    }
}
