using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using ClaudeCAD.Plugin.Core;
using AcException = Autodesk.AutoCAD.Runtime.Exception;
using AcErrorStatus = Autodesk.AutoCAD.Runtime.ErrorStatus;

namespace ClaudeCAD.Plugin.Drawing
{
    public delegate object ToolHandler(ToolContext ctx, Params p);

    public sealed class ToolDef
    {
        public string Name { get; }
        /// <summary>Writes need DocumentLock + undo group; reads run lock-free.</summary>
        public bool Writes { get; }
        public ToolHandler Handler { get; }
        /// <summary>Handlers that drive AutoCAD outside a transaction (zoom, save, undo...).</summary>
        public bool NoTransaction { get; }
        /// <summary>Not allowed inside batch_execute.</summary>
        public bool BatchForbidden { get; }

        public ToolDef(string name, bool writes, ToolHandler handler, bool noTransaction = false, bool batchForbidden = false)
        {
            Name = name;
            Writes = writes;
            Handler = handler;
            NoTransaction = noTransaction;
            BatchForbidden = batchForbidden || noTransaction;
        }
    }

    /// <summary>Supplied by the chat controller: which drawing the current turn works on.</summary>
    public interface ITurnHost
    {
        /// <summary>Returns the drawing of the current turn or throws ToolException (ACAD_ERROR).</summary>
        Document ResolveDocument();
        UndoScope Undo { get; }
    }

    /// <summary>
    /// Maps tool names to handlers (spec §4). Validates, marshals to the main
    /// thread, wraps each request in exactly one transaction, converts every
    /// exception into an error envelope — nothing may escape into AutoCAD.
    /// </summary>
    public sealed class ToolDispatcher
    {
        public const int MaxBatchCalls = 500;

        private readonly Dictionary<string, ToolDef> _tools = new Dictionary<string, ToolDef>(StringComparer.Ordinal);
        private readonly ITurnHost _host;

        public ToolDispatcher(ITurnHost host)
        {
            _host = host;
        }

        public void Register(string name, bool writes, ToolHandler handler) =>
            _tools[name] = new ToolDef(name, writes, handler);

        public void Register(ToolDef def) => _tools[def.Name] = def;

        public bool TryGet(string name, out ToolDef def) => _tools.TryGetValue(name, out def);

        public IReadOnlyCollection<string> ToolNames => _tools.Keys.ToList();

        // -------------------------------------------------------------- entry point

        /// <summary>Handles one tool_call message and returns the response envelope. Never throws.</summary>
        public async Task<Dictionary<string, object>> HandleAsync(Dictionary<string, object> msg)
        {
            string id = msg.TryGetValue("id", out var idv) ? idv as string : null;
            string tool = msg.TryGetValue("tool", out var tv) ? tv as string : null;
            var sw = Stopwatch.StartNew();
            try
            {
                if (string.IsNullOrEmpty(id)) throw ToolException.Invalid("tool_call without 'id'.");
                if (string.IsNullOrEmpty(tool)) throw ToolException.Invalid("tool_call without 'tool' name.");
                if (!_tools.TryGetValue(tool, out var def))
                    throw ToolException.Invalid($"Unknown tool '{tool}'. Available tools: {string.Join(", ", _tools.Keys.OrderBy(k => k))}.");

                var paramsObj = msg.TryGetValue("params", out var pv) ? pv : null;
                if (paramsObj != null && !(paramsObj is Dictionary<string, object>))
                    throw ToolException.Invalid($"{tool}: 'params' must be a JSON object.");
                var p = new Params(tool, paramsObj as Dictionary<string, object>);

                var result = await MainThread.InvokeAsync(() => ExecuteOnMain(def, p)).ConfigureAwait(false);
                Logger.Info($"tool {tool} ok in {sw.ElapsedMilliseconds} ms");
                return Ok(id, result);
            }
            catch (Exception ex)
            {
                var env = ErrorEnvelope(id, tool, ex);
                Logger.Info($"tool {tool} failed in {sw.ElapsedMilliseconds} ms: {JsonCodec.Serialize(env["error"])}");
                return env;
            }
        }

        public static Dictionary<string, object> Ok(string id, object result) => new Dictionary<string, object>
        {
            ["id"] = id,
            ["ok"] = true,
            ["result"] = result ?? new Dictionary<string, object>(),
        };

        public static Dictionary<string, object> Error(string id, string code, string message) => new Dictionary<string, object>
        {
            ["id"] = id,
            ["ok"] = false,
            ["error"] = new Dictionary<string, object> { ["code"] = code, ["message"] = message },
        };

        public static Dictionary<string, object> ErrorEnvelope(string id, string tool, Exception ex)
        {
            var (code, message) = Classify(tool, ex);
            return Error(id, code, message);
        }

        /// <summary>Exception -> (code, message Claude can act on).</summary>
        public static (string, string) Classify(string tool, Exception ex)
        {
            ex = Unwrap(ex);
            switch (ex)
            {
                case ToolException te:
                    return (te.Code, te.Message);
                case AcException ae:
                    switch (ae.ErrorStatus)
                    {
                        case AcErrorStatus.OnLockedLayer:
                            return (ErrorCodes.Locked, $"{tool}: the entity is on a locked layer. Unlock it with set_layer_props {{\"name\": ..., \"locked\": false}} (ask the user first) or skip it.");
                        case AcErrorStatus.WasErased:
                        case AcErrorStatus.NullObjectId:
                        case AcErrorStatus.InvalidKey:
                        case AcErrorStatus.UnknownHandle:
                            return (ErrorCodes.EntityNotFound, $"{tool}: entity not found or already erased. Call list_entities to get current handles.");
                        case AcErrorStatus.LockViolation:
                            return (ErrorCodes.Locked, $"{tool}: the drawing is locked by another operation. Ask the user to finish/cancel the running AutoCAD command (Esc) and retry.");
                        case AcErrorStatus.NotApplicable:
                        case AcErrorStatus.InvalidInput:
                        case AcErrorStatus.DegenerateGeometry:
                            return (ErrorCodes.InvalidParams, $"{tool}: AutoCAD rejected the geometry ({ae.ErrorStatus}). Check the points/dimensions (degenerate or out of range) and retry.");
                        default:
                            Logger.Error($"{tool}: AutoCAD exception", ae);
                            return (ErrorCodes.AcadError, $"{tool}: AutoCAD error {ae.ErrorStatus}: {ae.Message}");
                    }
                case ArgumentException ae2:
                    return (ErrorCodes.InvalidParams, $"{tool}: {ae2.Message}");
                default:
                    Logger.Error($"{tool}: unexpected exception", ex);
                    return (ErrorCodes.AcadError, $"{tool}: internal plugin error {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static Exception Unwrap(Exception ex)
        {
            while ((ex is AggregateException || ex is System.Reflection.TargetInvocationException) && ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }

        // -------------------------------------------------------------- main thread

        private object ExecuteOnMain(ToolDef def, Params p)
        {
            var doc = _host.ResolveDocument();
            bool writes = def.Writes || (def.Name == "batch_execute" && BatchWrites(p));

            if (def.NoTransaction)
            {
                if (writes) _host.Undo.EnsureStarted(doc);
                var ctx0 = new ToolContext(doc, null, writes, this);
                return def.Handler(ctx0, p);
            }

            if (!writes) return RunInTransaction(doc, def, p, false);

            _host.Undo.EnsureStarted(doc);
            using (doc.LockDocument())
            {
                return RunInTransaction(doc, def, p, true);
            }
        }

        private object RunInTransaction(Document doc, ToolDef def, Params p, bool writable)
        {
            var tm = doc.TransactionManager;
            using (var tr = tm.StartTransaction())
            {
                var ctx = new ToolContext(doc, tr, writable, this);
                var result = def.Handler(ctx, p);
                if (writable)
                {
                    tm.QueueForGraphicsFlush();
                    tr.Commit();
                }
                else
                {
                    tr.Abort();
                }
                if (writable)
                {
                    try
                    {
                        if (ctx.NeedsRegen) doc.Editor.Regen();
                        doc.Editor.UpdateScreen();
                    }
                    catch (Exception ex) { Logger.Warn("screen update failed: " + ex.Message); }
                }
                return result;
            }
            // Dispose without Commit == rollback when the handler threw.
        }

        private bool BatchWrites(Params p)
        {
            if (!(p.Get("calls") is List<object> calls)) return false;
            foreach (var c in calls)
            {
                if (c is Dictionary<string, object> d && d.TryGetValue("tool", out var t) && t is string name
                    && _tools.TryGetValue(name, out var def) && def.Writes)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Runs batch_execute calls sequentially inside the caller's (single) top
        /// transaction. Each call gets a nested transaction so a failing call is
        /// rolled back on its own and the following calls still run (spec §7.8).
        /// </summary>
        internal List<object> ExecuteBatch(ToolContext ctx, List<object> calls)
        {
            var results = new List<object>(calls.Count);
            ctx.InBatch = true;
            try
            {
                for (int i = 0; i < calls.Count; i++)
                {
                    string tool = null;
                    var outer = ctx.Tr;
                    try
                    {
                        if (!(calls[i] is Dictionary<string, object> call))
                            throw ToolException.Invalid($"batch_execute: calls[{i}] must be an object {{\"tool\": ..., \"params\": {{...}}}}.");
                        tool = call.TryGetValue("tool", out var tv) ? tv as string : null;
                        if (string.IsNullOrEmpty(tool)) throw ToolException.Invalid($"batch_execute: calls[{i}] has no 'tool'.");
                        if (tool == "batch_execute") throw ToolException.Invalid("batch_execute cannot be nested.");
                        if (!_tools.TryGetValue(tool, out var def))
                            throw ToolException.Invalid($"batch_execute: calls[{i}] unknown tool '{tool}'.");
                        if (def.BatchForbidden)
                            throw ToolException.Invalid($"batch_execute: '{tool}' cannot run inside a batch; call it on its own.");
                        if (def.Writes && !ctx.Writable) ctx.RequireWritable(tool);

                        var pobj = call.TryGetValue("params", out var pv) ? pv as Dictionary<string, object> : null;
                        var p = new Params(tool, pobj);

                        using (var nested = ctx.Doc.TransactionManager.StartTransaction())
                        {
                            ctx.Tr = nested;
                            ctx.ResetCache();
                            object r;
                            try
                            {
                                r = def.Handler(ctx, p);
                            }
                            finally
                            {
                                ctx.Tr = outer;
                                ctx.ResetCache();
                            }
                            nested.Commit();
                            results.Add(new Dictionary<string, object> { ["index"] = i, ["tool"] = tool, ["ok"] = true, ["result"] = r });
                        }
                    }
                    catch (Exception ex)
                    {
                        ctx.Tr = outer;
                        ctx.ResetCache();
                        var (code, message) = Classify(tool ?? "batch_execute", ex);
                        results.Add(new Dictionary<string, object>
                        {
                            ["index"] = i,
                            ["tool"] = tool,
                            ["ok"] = false,
                            ["error"] = new Dictionary<string, object> { ["code"] = code, ["message"] = message },
                        });
                        // "system" errors (lost document, out of memory) abort the whole batch
                        if (ex is FatalToolException || ex is OutOfMemoryException) throw;
                    }
                }
            }
            finally
            {
                ctx.InBatch = false;
            }
            return results;
        }
    }
}
