using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Marshals work from pipe/thread-pool threads onto AutoCAD's main thread.
    ///
    /// A hidden WinForms control created on the main thread gives us a reliable
    /// BeginInvoke target (AutoCAD's main thread runs a Win32 message loop). Once on
    /// the main thread we enter the application context with
    /// DocumentManager.ExecuteInApplicationContext (spec §4) so that document locks
    /// behave like they do for modeless UI.
    /// </summary>
    public static class MainThread
    {
        private static Control _marshal;

        /// <summary>Must be called on the AutoCAD main thread (IExtensionApplication.Initialize).</summary>
        public static void Initialize()
        {
            if (_marshal != null) return;
            _marshal = new Control();
            _marshal.CreateControl();
            var _ = _marshal.Handle; // force handle creation on this thread
        }

        public static bool IsMainThread => _marshal != null && !_marshal.InvokeRequired;

        public static Task<T> InvokeAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_marshal == null)
            {
                tcs.SetException(new ToolException(ErrorCodes.AcadError, "Plugin not initialized (main-thread marshal missing)."));
                return tcs.Task;
            }

            void Run()
            {
                try { tcs.TrySetResult(func()); }
                catch (Exception ex) { tcs.TrySetException(ex); }
            }

            void OnMain()
            {
                try
                {
                    var dm = AcApp.DocumentManager;
                    if (dm.IsApplicationContext) Run();
                    else dm.ExecuteInApplicationContext(_ => Run(), null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            try
            {
                _marshal.BeginInvoke((MethodInvoker)OnMain);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return tcs.Task;
        }

        public static Task InvokeAsync(Action action) =>
            InvokeAsync<bool>(() => { action(); return true; });

        /// <summary>Fire-and-forget on main thread (UI-ish work, never throws).</summary>
        public static void Post(Action action)
        {
            InvokeAsync(action).ContinueWith(t =>
            {
                if (t.Exception != null) Logger.Error("MainThread.Post failed", t.Exception.GetBaseException());
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
