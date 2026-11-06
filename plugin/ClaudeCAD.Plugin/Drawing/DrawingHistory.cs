using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using ClaudeCAD.Plugin.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ClaudeCAD.Plugin.Drawing
{
    /// <summary>
    /// What happened to each drawing outside the chat:
    ///   * manual edits (tracked from the moment AutoCAD loads the plugin, palette open or not),
    ///   * the "biên bản" (snapshot) written after every chat turn and compared with the
    ///     drawing the next time, even in another AutoCAD session or on another computer,
    ///   * the version history (a copy of the DWG before chat changes it, every 30 min max).
    /// All members run on the AutoCAD main thread.
    /// </summary>
    public sealed class DrawingHistory
    {
        public static DrawingHistory Instance { get; } = new DrawingHistory();

        private const int MaxDirty = 200000;
        private static readonly TimeSpan SnapshotMaxAge = TimeSpan.FromDays(90);

        private sealed class State
        {
            public readonly ChangeTracker Manual = new ChangeTracker();
            public readonly HashSet<string> Dirty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public DrawingSnapshot Snapshot;
            public bool Compared;
            public SnapshotDiff Carried;
            /// <summary>Layer of entities just before they were opened for write (large drawings: keeps layer counts exact without rescanning).</summary>
            public readonly Dictionary<string, string> PreLayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            /// <summary>Model space + paper space block ids (read when the snapshot is built; no transaction inside events).</summary>
            public HashSet<ObjectId> LayoutIds = new HashSet<ObjectId>();
        }

        /// <summary>
        /// Every object change of every open drawing (viewports / pan / zoom excluded), raised once for all
        /// listeners — the chat controller uses it for the turn's change card and the "Hoàn tác lượt này"
        /// safety, instead of subscribing to the database events itself. kind = added | modified | erased.
        /// </summary>
        public event Action<Database, DBObject, string, bool> ObjectChanged;

        private readonly Dictionary<Database, State> _states = new Dictionary<Database, State>();
        private bool _started;

        /// <summary>Database of the running chat turn: its changes are Claude's, not manual.</summary>
        public Database TurnDb { get; set; }

        /// <summary>Also write the snapshot next to the DWG (.claudecad folder) so other computers can compare.</summary>
        public bool NextToDrawing { get; set; }

        public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeCAD");
        public static string SnapshotsDir => Path.Combine(Root, "snapshots");
        public static string VersionsRoot => Path.Combine(Root, "versions");

        // ------------------------------------------------------------------ tracking

        /// <summary>Called from PluginApp.Initialize: watch every open and future drawing.</summary>
        public void Start()
        {
            if (_started) return;
            _started = true;
            var dm = AcApp.DocumentManager;
            dm.DocumentCreated += (s, e) => { Watch(e.Document); ScheduleCompare(e.Document); };
            dm.DocumentToBeDestroyed += (s, e) => Forget(e.Document);
            foreach (Document d in dm) { Watch(d); ScheduleCompare(d); }
            CleanOldSnapshots();
            CleanOldVersions();
        }

        public void Watch(Document doc)
        {
            Database db;
            try { db = doc?.Database; } catch { return; }
            if (db == null || _states.ContainsKey(db)) return;
            _states[db] = new State();
            db.ObjectAppended += (s, e) => On(db, e.DBObject, "added", true);
            db.ObjectModified += (s, e) => On(db, e.DBObject, "modified", true);
            db.ObjectErased += (s, e) => On(db, e.DBObject, "erased", e.Erased);
            db.ObjectOpenedForModify += (s, e) => OnOpenedForModify(db, e.DBObject);
        }

        /// <summary>Large drawings only: remember the layer before a modification so layer counts stay exact.</summary>
        private void OnOpenedForModify(Database db, DBObject obj)
        {
            try
            {
                if (!(obj is Entity e) || obj is Viewport) return;
                if (!_states.TryGetValue(db, out var st) || st.Snapshot == null || st.Snapshot.Full) return;
                var h = e.Handle.ToString();
                if (!st.PreLayer.ContainsKey(h) && st.PreLayer.Count < MaxDirty) st.PreLayer[h] = e.Layer;
            }
            catch { /* never throw into AutoCAD */ }
        }

        private void Forget(Document doc)
        {
            try { if (doc?.Database != null) _states.Remove(doc.Database); } catch { }
        }

        private void On(Database db, DBObject obj, string kind, bool erased)
        {
            try
            {
                if (obj is ViewportTableRecord || obj is Viewport) return; // pan / zoom
                var listeners = ObjectChanged;
                if (listeners != null)
                    foreach (Action<Database, DBObject, string, bool> l in listeners.GetInvocationList())
                    {
                        try { l(db, obj, kind, erased); }
                        catch (Exception ex) { Logger.Warn("object change listener failed: " + ex.Message); }
                    }
                if (!(obj is Entity ent)) return;
                if (!_states.TryGetValue(db, out var st)) return;
                string h = obj.Handle.ToString();
                if (st.Dirty.Count < MaxDirty) st.Dirty.Add(h);
                TrackLayerCount(st, ent, h, kind, erased);
                if (ReferenceEquals(db, TurnDb)) return; // Claude's own change
                if (kind == "added") st.Manual.Appended(h);
                else if (kind == "modified") st.Manual.Modified(h);
                else st.Manual.Erased(h, erased);
            }
            catch (Exception ex)
            {
                Logger.Warn("drawing change tracking failed: " + ex.Message); // never throw into AutoCAD
            }
        }

        /// <summary>Large drawings: keep the per-layer counts of the snapshot exact from the events (no rescans).</summary>
        private static void TrackLayerCount(State st, Entity e, string h, string kind, bool erased)
        {
            var snap = st.Snapshot;
            if (snap == null || snap.Full) return;
            void Add(string layer, int d)
            {
                if (string.IsNullOrEmpty(layer)) return;
                snap.LayerCounts[layer] = Math.Max(0, (snap.LayerCounts.TryGetValue(layer, out var n) ? n : 0) + d);
            }
            if (!st.LayoutIds.Contains(e.OwnerId)) return;
            var layer = e.Layer;
            switch (kind)
            {
                case "added": Add(layer, +1); break;
                case "erased": Add(layer, erased ? -1 : +1); break;
                default:
                    if (st.PreLayer.TryGetValue(h, out var before))
                    {
                        st.PreLayer.Remove(h);
                        if (!string.Equals(before, layer, StringComparison.OrdinalIgnoreCase)) { Add(before, -1); Add(layer, +1); }
                    }
                    break;
            }
        }

        private static HashSet<ObjectId> LayoutIds(Database db)
        {
            var ids = new HashSet<ObjectId>();
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId id in bt)
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockTableRecord btr && btr.IsLayout) ids.Add(id);
                tr.Commit();
            }
            return ids;
        }

        private State Get(Document doc)
        {
            Watch(doc);
            return _states[doc.Database];
        }

        // ------------------------------------------------------------------ manual changes for the next message

        /// <summary>
        /// Changes made outside the chat since the last chat turn (or since an earlier session, from the
        /// snapshot). Clears them: they are reported once. Null when nothing changed. hasConversation =
        /// false → discarded (Claude has never seen this drawing, nothing can be stale).
        /// </summary>
        public Dictionary<string, object> TakeManualChanges(Document doc, string key, string path, bool hasConversation, out string shortText)
        {
            shortText = null;
            if (doc == null) return null;
            var st = Get(doc);
            EnsureCompared(doc, st, key, path);
            var carried = st.Carried;
            st.Carried = null;
            var manual = st.Manual.Snapshot();
            st.Manual.Clear();
            if (!hasConversation) return null;

            var items = new List<(string kind, string layer, string handle)>();
            if (carried != null) items.AddRange(carried.Items);
            var seen = new HashSet<string>(items.Select(i => i.handle), StringComparer.OrdinalIgnoreCase);
            foreach (var r in Resolve(doc.Database, manual))
                if (seen.Add(r.handle)) items.Add(r);
            var counts = carried?.LayerCountChanges ?? new List<(string, int, int)>();
            var report = ManualChangeReport.Build(items, counts, carried != null ? "previous" : "session");
            if (report != null) shortText = ManualChangeReport.Short(items, counts.Count);
            return report;
        }

        /// <summary>
        /// First use of a drawing in this session: compare the snapshot of the last chat turn with the
        /// drawing now. The difference covers every manual change made since, so the session tracker restarts.
        /// </summary>
        private void EnsureCompared(Document doc, State st, string key, string path)
        {
            if (st.Compared) return;
            st.Compared = true;
            DrawingSnapshot baseline = null;
            try { baseline = LoadBaseline(key, path); }
            catch (Exception ex) { Logger.Warn("snapshot unreadable: " + ex.Message); }
            if (baseline == null) return;
            var now = Build(doc.Database, key, baseline.Entries.Keys);
            st.LayoutIds = LayoutIds(doc.Database);
            var diff = SnapshotDiff.Compare(baseline, now);
            st.Snapshot = now;
            st.Dirty.Clear();
            st.Manual.Clear();
            if (!diff.IsEmpty) st.Carried = diff;
            Logger.Info($"snapshot compare {Path.GetFileName(path ?? key)}: {diff.Items.Count} changed objects, {diff.LayerCountChanges.Count} layer count changes");
        }

        /// <summary>Changed handles → (kind, layer, handle) for objects in model/paper space; attributes count as their block.</summary>
        private static List<(string kind, string layer, string handle)> Resolve(Database db, List<(string kind, string handle)> changes)
        {
            var list = new List<(string, string, string)>();
            if (changes.Count == 0) return list;
            var layoutOwner = new Dictionary<ObjectId, bool>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var (kind0, h0) in changes)
                {
                    try
                    {
                        string kind = kind0, h = h0;
                        var id = Acad.IdFromHandle(db, h, true);
                        if (!(tr.GetObject(id, OpenMode.ForRead, true) is Entity ent)) continue;
                        if (ent is AttributeReference && tr.GetObject(ent.OwnerId, OpenMode.ForRead, true) is BlockReference owner)
                        {
                            ent = owner;
                            h = owner.Handle.ToString();
                            kind = owner.IsErased ? "erased" : "modified";
                        }
                        if (!layoutOwner.TryGetValue(ent.OwnerId, out var isLayout))
                        {
                            isLayout = tr.GetObject(ent.OwnerId, OpenMode.ForRead, true) is BlockTableRecord btr && btr.IsLayout;
                            layoutOwner[ent.OwnerId] = isLayout;
                        }
                        if (isLayout && seen.Add(h)) list.Add((kind, ent.Layer, h));
                    }
                    catch { /* never committed / purged */ }
                }
                tr.Commit();
            }
            return list;
        }

        // ------------------------------------------------------------------ snapshot after a chat turn

        /// <summary>End of a chat turn on this drawing: bring the snapshot up to date and write it.</summary>
        public void AfterTurn(Document doc, string key, string path)
        {
            if (doc == null || doc.IsDisposed) return;
            var st = Get(doc);
            EnsureCompared(doc, st, key, path); // a macro run must not overwrite an uncompared snapshot
            if (st.Snapshot == null || (st.Snapshot.Full && st.Snapshot.Entries.Count > DrawingSnapshot.FullLimit * 1.2))
            {
                var tracked = new HashSet<string>(st.Dirty, StringComparer.OrdinalIgnoreCase);
                if (st.Snapshot != null) foreach (var h in st.Snapshot.Entries.Keys) if (tracked.Count < DrawingSnapshot.MaxTracked) tracked.Add(h);
                st.Snapshot = Build(doc.Database, key, tracked);
                st.LayoutIds = LayoutIds(doc.Database);
            }
            else if (st.Snapshot.Full) Refresh(doc.Database, st.Snapshot, st.Dirty);
            else RefreshLarge(doc.Database, st.Snapshot, st.Dirty); // only the changed objects; counts kept by the events
            st.PreLayer.Clear();
            st.Dirty.Clear();
            st.Snapshot.Time = DateTime.Now;
            st.Snapshot.DrawingKey = key;
            SaveBaseline(st.Snapshot, key, path);
        }

        /// <summary>New chat: forget the snapshot of the old conversation.</summary>
        public void Reset(Document doc, string key, string path)
        {
            if (doc != null && !doc.IsDisposed)
            {
                var st = Get(doc);
                st.Snapshot = null;
                st.Carried = null;
                st.Compared = true;
                st.Manual.Clear();
            }
            try { var f = LocalFile(key); if (File.Exists(f)) File.Delete(f); } catch { }
            try { var f = SideFile(path); if (f != null && File.Exists(f)) File.Delete(f); } catch { }
        }

        public static DrawingSnapshot Build(Database db, string key, IEnumerable<string> tracked)
        {
            var s = new DrawingSnapshot { DrawingKey = key, Time = DateTime.Now };
            var trackedSet = new HashSet<string>(tracked ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var ids = new List<ObjectId>();
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId btrId in bt)
                {
                    if (!(tr.GetObject(btrId, OpenMode.ForRead) is BlockTableRecord btr) || !btr.IsLayout) continue;
                    foreach (ObjectId id in btr) ids.Add(id);
                }
                s.EntityCount = ids.Count;
                s.Full = ids.Count <= DrawingSnapshot.FullLimit;
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e) || e is Viewport) continue;
                    var layer = e.Layer;
                    s.LayerCounts[layer] = s.LayerCounts.TryGetValue(layer, out var n) ? n + 1 : 1;
                    var h = e.Handle.ToString();
                    if (s.Full || trackedSet.Contains(h)) s.Entries[h] = Entry(tr, e);
                }
                tr.Commit();
            }
            return s;
        }

        /// <summary>Full snapshot: recompute only the objects that changed since it was written.</summary>
        private static void Refresh(Database db, DrawingSnapshot s, IEnumerable<string> handles)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var layoutOwner = new Dictionary<ObjectId, bool>();
                foreach (var h in handles.ToList())
                {
                    try
                    {
                        if (!db.TryGetObjectId(new Handle(Convert.ToInt64(h, 16)), out var id) || id.IsNull || id.IsErased) { s.Entries.Remove(h); continue; }
                        if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e) || e is Viewport) continue;
                        if (!layoutOwner.TryGetValue(e.OwnerId, out var isLayout))
                        {
                            isLayout = tr.GetObject(e.OwnerId, OpenMode.ForRead) is BlockTableRecord btr && btr.IsLayout;
                            layoutOwner[e.OwnerId] = isLayout;
                        }
                        if (isLayout) s.Entries[h] = Entry(tr, e);
                        else if (e is AttributeReference && tr.GetObject(e.OwnerId, OpenMode.ForRead) is BlockReference br && s.Entries.ContainsKey(br.Handle.ToString()))
                            s.Entries[br.Handle.ToString()] = Entry(tr, br);
                    }
                    catch { s.Entries.Remove(h); }
                }
                tr.Commit();
            }
            s.LayerCounts.Clear();
            foreach (var e in s.Entries.Values) s.LayerCounts[e.Layer] = s.LayerCounts.TryGetValue(e.Layer, out var n) ? n + 1 : 1;
            s.EntityCount = s.Entries.Count;
        }

        /// <summary>Large drawing: update / add entries of the changed objects (up to MaxTracked), drop erased ones.</summary>
        private static void RefreshLarge(Database db, DrawingSnapshot s, IEnumerable<string> handles)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var layoutOwner = new Dictionary<ObjectId, bool>();
                foreach (var h in handles.ToList())
                {
                    try
                    {
                        if (!db.TryGetObjectId(new Handle(Convert.ToInt64(h, 16)), out var id) || id.IsNull || id.IsErased) { s.Entries.Remove(h); continue; }
                        if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e) || e is Viewport) continue;
                        if (!layoutOwner.TryGetValue(e.OwnerId, out var isLayout))
                        {
                            isLayout = tr.GetObject(e.OwnerId, OpenMode.ForRead) is BlockTableRecord btr && btr.IsLayout;
                            layoutOwner[e.OwnerId] = isLayout;
                        }
                        if (!isLayout) continue;
                        if (s.Entries.ContainsKey(h) || s.Entries.Count < DrawingSnapshot.MaxTracked) s.Entries[h] = Entry(tr, e);
                    }
                    catch { s.Entries.Remove(h); }
                }
                tr.Commit();
            }
            s.EntityCount = s.LayerCounts.Values.Sum();
        }

        private static SnapshotEntry Entry(Transaction tr, Entity e) => new SnapshotEntry
        {
            Type = e.GetType().Name,
            Layer = e.Layer,
            Sig = DrawingSnapshot.Sig(Parts(tr, e)),
        };

        /// <summary>Properties whose change counts as "modified": geometry (extents, length), style, text, block data.</summary>
        private static IEnumerable<string> Parts(Transaction tr, Entity e)
        {
            var parts = new List<string> { e.GetType().Name, e.Layer, e.ColorIndex.ToString(), e.Linetype, ((int)e.LineWeight).ToString(), e.Visible ? "1" : "0" };
            try
            {
                var x = EntityInfo.Extents(e);
                if (x.HasValue)
                {
                    var a = x.Value.MinPoint;
                    var b = x.Value.MaxPoint;
                    parts.AddRange(new[] { DrawingSnapshot.N(a.X), DrawingSnapshot.N(a.Y), DrawingSnapshot.N(a.Z), DrawingSnapshot.N(b.X), DrawingSnapshot.N(b.Y), DrawingSnapshot.N(b.Z) });
                }
            }
            catch { }
            try
            {
                switch (e)
                {
                    case DBText t:
                        parts.Add(t.TextString); parts.Add(DrawingSnapshot.N(t.Height)); parts.Add(DrawingSnapshot.N(t.Rotation)); parts.Add(t.TextStyleName);
                        break;
                    case MText m:
                        parts.Add(m.Contents); parts.Add(DrawingSnapshot.N(m.TextHeight)); parts.Add(DrawingSnapshot.N(m.Width));
                        break;
                    case BlockReference br:
                        parts.Add(br.Name); parts.Add(DrawingSnapshot.N(br.Rotation));
                        parts.Add(DrawingSnapshot.N(br.ScaleFactors.X)); parts.Add(DrawingSnapshot.N(br.ScaleFactors.Y));
                        parts.Add(DrawingSnapshot.N(br.Position.X)); parts.Add(DrawingSnapshot.N(br.Position.Y));
                        foreach (ObjectId aid in br.AttributeCollection)
                            if (tr.GetObject(aid, OpenMode.ForRead) is AttributeReference ar) { parts.Add(ar.Tag); parts.Add(ar.TextString); }
                        if (br.IsDynamicBlock)
                            foreach (DynamicBlockReferenceProperty dp in br.DynamicBlockReferencePropertyCollection) { parts.Add(dp.PropertyName); parts.Add(Convert.ToString(dp.Value, System.Globalization.CultureInfo.InvariantCulture)); }
                        break;
                    case Dimension d:
                        parts.Add(d.DimensionText); parts.Add(DrawingSnapshot.N(d.Measurement)); parts.Add(d.DimensionStyleName);
                        break;
                    case Hatch hatch:
                        parts.Add(hatch.PatternName); parts.Add(DrawingSnapshot.N(hatch.PatternScale));
                        break;
                    case Curve c:
                        parts.Add(DrawingSnapshot.N(c.GetDistanceAtParameter(c.EndParam)));
                        parts.Add(c.Closed ? "c" : "o");
                        break;
                }
            }
            catch { /* some objects cannot report a property: extents still count */ }
            return parts;
        }

        // ------------------------------------------------------------------ compare while AutoCAD is idle

        private readonly Queue<Document> _toCompare = new Queue<Document>();
        private bool _idleHooked;

        /// <summary>Key of a saved drawing as the chat history uses it (null for unsaved drawings).</summary>
        public static string KeyOf(Document doc)
        {
            try { return doc != null && doc.IsNamedDrawing ? ChatTranscript.KeyFor(doc.Name) : null; } catch { return null; }
        }

        /// <summary>
        /// A drawing that was chatted about before was opened: compare it with its snapshot as soon as AutoCAD
        /// is idle, so the first chat message does not wait for the comparison.
        /// </summary>
        public void ScheduleCompare(Document doc)
        {
            var key = KeyOf(doc);
            if (key == null) return;
            bool hasBaseline;
            try { hasBaseline = File.Exists(LocalFile(key)) || (SideFile(doc.Name) is string sf && File.Exists(sf)); } catch { hasBaseline = false; }
            if (!hasBaseline) return;
            _toCompare.Enqueue(doc);
            if (_idleHooked) return;
            _idleHooked = true;
            AcApp.Idle += OnIdle;
        }

        private void OnIdle(object sender, EventArgs e)
        {
            try
            {
                if (_toCompare.Count == 0) { AcApp.Idle -= OnIdle; _idleHooked = false; return; }
                var doc = _toCompare.Peek();
                if (doc == null || doc.IsDisposed) { _toCompare.Dequeue(); return; }
                // wait until no command is running in that drawing
                if (!string.IsNullOrEmpty(doc.CommandInProgress) || !doc.Editor.IsQuiescent) return;
                _toCompare.Dequeue();
                var st = Get(doc);
                if (!st.Compared && !ReferenceEquals(doc.Database, TurnDb)) EnsureCompared(doc, st, KeyOf(doc), doc.Name);
            }
            catch (Exception ex)
            {
                Logger.Warn("idle snapshot compare failed: " + ex.Message);
                if (_toCompare.Count > 0) _toCompare.Dequeue();
            }
        }

        // ------------------------------------------------------------------ snapshot files

        public static string LocalFile(string key) =>
            Path.Combine(SnapshotsDir, ChatTranscript.FileNameFor(key).Replace("chat-", "snap-").Replace(".json", ".gz"));

        /// <summary>&lt;drawing folder&gt;\.claudecad\&lt;name&gt;.dwg.snap.gz (null for unsaved drawings).</summary>
        public static string SideFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                var dir = Path.GetDirectoryName(path);
                return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, ".claudecad", Path.GetFileName(path) + ".snap.gz");
            }
            catch { return null; }
        }

        private DrawingSnapshot LoadBaseline(string key, string path)
        {
            DrawingSnapshot local = null, side = null;
            var lf = LocalFile(key);
            if (File.Exists(lf))
            {
                local = DrawingSnapshot.FromBytes(File.ReadAllBytes(lf));
                if (!string.Equals(local.DrawingKey, key, StringComparison.OrdinalIgnoreCase)) local = null; // hash collision
            }
            var sf = SideFile(path);
            if (sf != null && File.Exists(sf))
            {
                try { side = DrawingSnapshot.FromBytes(File.ReadAllBytes(sf)); }
                catch (Exception ex) { Logger.Warn("shared snapshot unreadable: " + ex.Message); }
            }
            if (local == null) return side;
            if (side == null) return local;
            return side.Time > local.Time ? side : local; // the newest chat turn wins (another computer may be newer)
        }

        private void SaveBaseline(DrawingSnapshot s, string key, string path)
        {
            if (key.StartsWith("unsaved:", StringComparison.Ordinal) || key == "(none)") return; // nothing to compare against later
            byte[] data;
            try { data = s.ToBytes(); }
            catch (Exception ex) { Logger.Warn("snapshot not serialised: " + ex.Message); return; }
            try
            {
                Directory.CreateDirectory(SnapshotsDir);
                WriteAtomic(LocalFile(key), data);
            }
            catch (Exception ex) { Logger.Warn("snapshot not saved: " + ex.Message); }
            if (!NextToDrawing) return;
            var sf = SideFile(path);
            if (sf == null) return;
            try
            {
                var dir = Path.GetDirectoryName(sf);
                var di = Directory.CreateDirectory(dir);
                try { di.Attributes |= FileAttributes.Hidden; } catch { }
                WriteAtomic(sf, data);
            }
            catch (Exception ex) { Logger.Warn("shared snapshot not saved next to the drawing: " + ex.Message); }
        }

        private static void WriteAtomic(string file, byte[] data)
        {
            var tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(tmp, data);
            if (File.Exists(file)) File.Replace(tmp, file, null, true);
            else File.Move(tmp, file);
        }

        private static void CleanOldVersions()
        {
            try
            {
                if (!Directory.Exists(VersionsRoot)) return;
                foreach (var d in Directory.GetDirectories(VersionsRoot, "v-*"))
                {
                    var index = Path.Combine(d, "versions.json");
                    var last = File.Exists(index) ? File.GetLastWriteTime(index) : Directory.GetLastWriteTime(d);
                    if (DateTime.Now - last > SnapshotMaxAge) Directory.Delete(d, true);
                }
            }
            catch (Exception ex) { Logger.Warn("old versions not cleaned: " + ex.Message); }
        }

        private static void CleanOldSnapshots()
        {
            try
            {
                if (!Directory.Exists(SnapshotsDir)) return;
                foreach (var f in Directory.GetFiles(SnapshotsDir, "snap-*.gz"))
                    if (DateTime.Now - File.GetLastWriteTime(f) > SnapshotMaxAge) File.Delete(f);
            }
            catch (Exception ex) { Logger.Warn("old snapshots not cleaned: " + ex.Message); }
        }

        // ------------------------------------------------------------------ version history

        public VersionStore Versions(string key) => new VersionStore(VersionsRoot, key);

        /// <summary>
        /// No unsaved changes (DBMOD = 0): the DWG on disk is exactly what is open, so copying the file is
        /// enough and takes milliseconds instead of a full database copy.
        /// </summary>
        private static bool TryCopySavedFile(Document doc, string target)
        {
            try
            {
                if (doc != AcApp.DocumentManager.MdiActiveDocument || !doc.IsNamedDrawing || !File.Exists(doc.Name)) return false;
                if (Convert.ToInt32(AcApp.GetSystemVariable("DBMOD")) != 0) return false;
                File.Copy(doc.Name, target, false);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Info("version: file copy not possible, copying the database instead (" + ex.Message + ")");
                try { if (File.Exists(target)) File.Delete(target); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Before the first change of a chat turn: copy the whole drawing when the newest copy is at least
        /// 30 minutes old. The copy is a separate DWG (Wblock of the database); the open drawing is untouched.
        /// </summary>
        public VersionInfo MaybeSaveVersion(Document doc, string key, string label)
        {
            // unsaved drawings ("Drawing1") get a new key every session: no history to keep
            if (doc == null || key == "(none)" || key.StartsWith("unsaved:", StringComparison.Ordinal)) return null;
            var store = Versions(key);
            var now = DateTime.Now;
            if (!store.IsDue(now)) return null;
            var file = store.NewFile(now, out var id);
            if (!TryCopySavedFile(doc, file))
            {
                using (doc.LockDocument())
                using (var copy = doc.Database.Wblock())
                {
                    copy.SaveAs(file, DwgVersion.Current);
                }
            }
            store.Register(id, now, label);
            Logger.Info($"version saved: {file}");
            return store.List().FirstOrDefault(v => v.Id == id);
        }
    }
}
