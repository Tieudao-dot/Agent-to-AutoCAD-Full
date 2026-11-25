using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClaudeCAD.Plugin.Core;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// Nâng cấp đợt G3: cost_estimate — "lập dự toán". Runs one existing schedule (volume_schedule, block_schedule or
    /// area_schedule, under that schedule's own plan group) and prices its rows with the user's unit prices
    /// (Core/Estimate.cs). The result lists the rows without a price; table_position draws the table (text: 2D, like the
    /// schedules' table_position); excel writes an .xlsx with formulas (an existing file: the F5 card; a macro replay
    /// writes no file, like the F4 schedules).
    /// </summary>
    public static partial class DrawingService
    {
        private static void RegisterEstimate(ToolDispatcher d)
        {
            d.Register(new ToolDef("cost_estimate", true, CostEstimate, readsWhen: p => !p.Has("table_position")));
        }

        internal static object CostEstimate(ToolContext ctx, Params p)
        {
            const string tool = EstimateLogic.Tool;
            var source = (p.Str("quantities") ?? "").Trim().ToLowerInvariant(); // not "source": a macro would read it as a handle
            if (!EstimateLogic.Sources.TryGetValue(source, out var sourceTool))
                throw ToolException.Invalid($"{tool}: quantities must be volume (3D volumes by layer), blocks (block counts) or areas (room / layer areas).");
            // the estimate needs what its source schedule needs: the 3D volumes on a plan with 3D, blocks / areas with 2D
            string plan = ctx.Dispatcher?.Plan;
            if (plan != null && !PlanLogic.Allowed(plan, sourceTool))
                throw new ToolException(ErrorCodes.NotInPlan, ctx.Dispatcher.WorkMode != null
                    ? WorkModeLogic.BlockedMessage(sourceTool, ctx.Dispatcher.WorkMode)
                    : PlanLogic.BlockedMessage(sourceTool, plan, null));

            var sp = new Dictionary<string, object>(p.ObjOpt("quantities_params")?.Raw ?? new Dictionary<string, object>(), StringComparer.Ordinal);
            foreach (var k in new[] { "table_position", "excel", "overwrite", F5Confirm.Param, SolidLogic.UserConfirmedParam, "title", "text_height", "layer", "color" }) sp.Remove(k);
            var src = (Dictionary<string, object>)(source == "volume" ? VolumeSchedule(ctx, new Params(sourceTool, sp))
                : source == "blocks" ? BlockSchedule(ctx, new Params(sourceTool, sp)) : AreaSchedule(ctx, new Params(sourceTool, sp)));
            string quantity = p.Str("quantity", "net").Trim().ToLowerInvariant();
            if (quantity != "net" && quantity != "gross") throw ToolException.Invalid($"{tool}: quantity must be net or gross (volumes).");
            var rows = EstimateLogic.RowsOf(source, src, quantity);
            if (rows.Count == 0) throw ToolException.Invalid($"{tool}: the {sourceTool} found no rows (check quantities_params: layers, handles, names), so there is nothing to price.");

            var prices = new List<EstimateLogic.Price>();
            if (p.Has("prices"))
                foreach (var o in p.Array("prices"))
                {
                    if (!(o is Dictionary<string, object> d) || !d.TryGetValue("item", out var it) || !d.TryGetValue("unit_price", out var up) || !(up is double upd))
                        throw ToolException.Invalid($"{tool}: each price is {{\"item\": \"...\", \"unit_price\": number, \"unit\"?}} with the price the user gave.");
                    prices.Add(new EstimateLogic.Price { Item = Convert.ToString(it, CultureInfo.InvariantCulture), UnitPrice = upd, Unit = d.TryGetValue("unit", out var u) ? u as string : null });
                }
            var percents = new List<(string, double)>();
            if (p.Has("extra"))
                foreach (var o in p.Array("extra"))
                {
                    if (!(o is Dictionary<string, object> d) || !d.TryGetValue("name", out var n) || !d.TryGetValue("percent", out var pv) || !(pv is double pvd))
                        throw ToolException.Invalid($"{tool}: each extra line is {{\"name\": \"Thuế VAT\", \"percent\": 10}} (only what the user asked for).");
                    percents.Add((Convert.ToString(n, CultureInfo.InvariantCulture), pvd));
                }
            var est = EstimateLogic.Build(rows, prices, percents);
            string currency = p.Str("currency", G3Numbers.Currency);

            var res = new Dictionary<string, object>
            {
                ["source"] = sourceTool,
                ["currency"] = currency,
                ["lines"] = est.Lines.Take(200).Select(l =>
                {
                    var d = new Dictionary<string, object> { ["item"] = l.Item, ["unit"] = l.Unit, ["quantity"] = Math.Round(l.Quantity, G3Numbers.QuantityDecimals) };
                    if (l.UnitPrice.HasValue) { d["unit_price"] = l.UnitPrice.Value; d["amount"] = l.Amount.Value; d["priced_by"] = l.PricedBy; }
                    if (l.Note != null) d["note"] = l.Note;
                    return (object)d;
                }).ToList(),
                ["subtotal"] = est.Subtotal,
                ["total"] = est.Total,
                ["total_text"] = EstimateLogic.Vn(est.Total, G3Numbers.MoneyDecimals) + " " + currency,
            };
            if (est.Lines.Count > 200) res["lines_shown"] = $"200 of {est.Lines.Count} (all are in the table / Excel)";
            if (est.Percents.Count > 0)
                res["extra"] = est.Percents.Select(x => (object)new Dictionary<string, object> { ["name"] = x.Name, ["percent"] = x.Rate, ["amount"] = x.Amount }).ToList();
            if (est.Missing.Count > 0)
            {
                res["missing_prices"] = est.Missing.Cast<object>().ToList();
                res["warning"] = $"{est.Missing.Count} row(s) have no unit price from the user: they are empty and NOT in the total. Tell the user which, and ask for their prices; never fill them in yourself.";
            }
            if (est.Unused.Count > 0) res["unused_prices"] = est.Unused.Cast<object>().ToList();

            if (p.Has("table_position"))
            {
                var t = InsertSchedule(ctx, p, EstimateLogic.DrawingTable(est, currency), new[] { 5.0, 24, 8, 12, 14, 16 }, G3Numbers.Title);
                res["handles"] = t["handles"];
            }

            var requested = XlsxLogic.Requested(p.Get("excel"), tool);
            if (requested != null)
            {
                if (ctx.Dispatcher?.MacroRunning == true) res["excel_note"] = "A macro replay writes no Excel file (as the schedules).";
                else
                {
                    string docName = SafeString(() => ctx.Doc.Name);
                    string dir = SafeString(() => Path.GetDirectoryName(ctx.Doc.Name));
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    var full = XlsxLogic.Resolve(string.IsNullOrEmpty(requested) ? EstimateLogic.DefaultFile(docName) : requested, dir, docName, tool, Directory.Exists, Directory.Exists);
                    RequireOverwriteCard(ctx, p, tool, full, File.Exists(full), XlsxLogic.ExistsMessage(tool, full), "overwrite");
                    var data = EstimateLogic.ExcelRows(est, currency).Select(r => (IList<object>)r).ToList();
                    try { XlsxWriter.Write(full, data, G3Numbers.SheetName); }
                    catch (IOException ex) { throw ToolException.Invalid($"{tool}: could not write '{full}' ({ex.Message}). If it is open in Excel, ask the user to close it, then call again."); }
                    catch (UnauthorizedAccessException) { throw ToolException.Invalid($"{tool}: no permission to write '{full}'. Give another folder or file name in 'excel'."); }
                    res["excel"] = new Dictionary<string, object> { ["path"] = full, ["rows"] = est.Lines.Count, ["formulas"] = "Thành tiền = ROUND(Khối lượng × Đơn giá), Cộng = SUM: a price typed in Excel updates the amounts" };
                }
            }
            return res;
        }
    }
}
