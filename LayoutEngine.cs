using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(StructuralLayoutEngine.LayoutEngine))]

namespace StructuralLayoutEngine
{
    public class ViewBoundInfo
    {
        public ObjectId PolyId { get; set; }
        public string ViewName { get; set; } = "VIEW";
        public double SourceScale { get; set; } = 50.0;
        public string Priority { get; set; } = "A";
        public Extents3d OriginalExtents { get; set; }
        public Point3dCollection PolyPoints { get; set; } = new Point3dCollection();
        public double Width => OriginalExtents.MaxPoint.X - OriginalExtents.MinPoint.X;
        public double Height => OriginalExtents.MaxPoint.Y - OriginalExtents.MinPoint.Y;
        public bool IsVerticalMajor => Height > (1.2 * Width);
    }

    public class LayoutEngine
    {
        private static ObjectId _sampleFrameBlockId = ObjectId.Null;
        private static Point3d _sampleRecTopLeft;
        private static double _sampleRecW = 0.0;
        private static double _sampleRecH = 0.0;
        private static Point3d _sampleFrameInsPt;
        private static double _sampleOuterWidth = 0.0;
        private static double _detectedScale = 50.0;
        private static string _frameName = "AMC-khung ten doc";
        private static string _frameInfo = "Tỷ lệ gốc: 1/50";
        private static LayoutConfig _config = LayoutConfig.Load();
        private static int _sxbvUid = 0;

        private static List<string> GetBlockTags(Database db, string blockName)
        {
            var tags = new List<string>();
            if (string.IsNullOrEmpty(blockName) || blockName == "*" || blockName.StartsWith("TEXT:", StringComparison.OrdinalIgnoreCase))
            {
                return tags;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                if (bt.Has(blockName))
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                    foreach (ObjectId id in btr)
                    {
                        AttributeDefinition attDef = tr.GetObject(id, OpenMode.ForRead) as AttributeDefinition;
                        if (attDef != null)
                        {
                            tags.Add(attDef.Tag);
                        }
                    }
                }
                tr.Commit();
            }
            return tags;
        }

        [CommandMethod("KBSET")]
        public void RunKBSet()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            bool loop = true;
            while (loop)
            {
                List<string> frameTags = GetBlockTags(db, _config.FrameBlockName);
                List<string> beamTags = GetBlockTags(db, _config.BeamBlockName);
                List<string> colTags = GetBlockTags(db, _config.ColumnBlockName);
                string recStr = (_sampleRecW > 0) ? $"{Math.Round(_sampleRecW)} x {Math.Round(_sampleRecH)}" :
                                (_config.SampleRecW > 0) ? $"{Math.Round(_config.SampleRecW)} x {Math.Round(_config.SampleRecH)}" : "Chưa chọn";

                var win = new KBSetWindow(_frameName, recStr, frameTags, beamTags, colTags, _config);
                Application.ShowModalWindow(win);

                if (win.IsPickBeamRequested)
                {
                    var optEnt = new PromptEntityOptions("\n>> Chọn 1 Block thuộc tính HOẶC Text/MText tiêu đề DẦM mẫu: ");
                    var resEnt = ed.GetEntity(optEnt);
                    if (resEnt.Status == PromptStatus.OK)
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            Entity ent = tr.GetObject(resEnt.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent is BlockReference blk)
                            {
                                string bName = blk.IsDynamicBlock
                                    ? ((BlockTableRecord)tr.GetObject(blk.DynamicBlockTableRecord, OpenMode.ForRead)).Name
                                    : blk.Name;
                                _config.BeamBlockName = bName;
                                var tags = new List<string>();
                                foreach (ObjectId attId in blk.AttributeCollection)
                                {
                                    AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                                    if (att != null) tags.Add(att.Tag);
                                }
                                _config.BeamTagName = tags.Count > 0 ? tags[0] : "*";
                                ed.WriteMessage($"\n>> THÀNH CÔNG: Đã nhận diện Block Dầm [{_config.BeamBlockName}]");
                            }
                            else if (ent is DBText txt)
                            {
                                _config.BeamBlockName = $"TEXT:{txt.Layer}";
                                _config.BeamTagName = "*";
                                ed.WriteMessage($"\n>> THÀNH CÔNG: Đã nhận diện Text thường Dầm trên Layer [{txt.Layer}]");
                            }
                            else if (ent is MText mtx)
                            {
                                _config.BeamBlockName = $"TEXT:{mtx.Layer}";
                                _config.BeamTagName = "*";
                                ed.WriteMessage($"\n>> THÀNH CÔNG: Đã nhận diện MText Dầm trên Layer [{mtx.Layer}]");
                            }
                            _config.Save();
                            tr.Commit();
                        }
                    }
                }
                else if (win.IsPickColRequested)
                {
                    var optEnt = new PromptEntityOptions("\n>> Chọn 1 Block thuộc tính HOẶC Text/MText tiêu đề CỘT mẫu: ");
                    var resEnt = ed.GetEntity(optEnt);
                    if (resEnt.Status == PromptStatus.OK)
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            Entity ent = tr.GetObject(resEnt.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent is BlockReference blk)
                            {
                                string bName = blk.IsDynamicBlock
                                    ? ((BlockTableRecord)tr.GetObject(blk.DynamicBlockTableRecord, OpenMode.ForRead)).Name
                                    : blk.Name;
                                _config.ColumnBlockName = bName;
                                var tags = new List<string>();
                                foreach (ObjectId attId in blk.AttributeCollection)
                                {
                                    AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                                    if (att != null) tags.Add(att.Tag);
                                }
                                _config.ColumnTagName = tags.Count > 0 ? tags[0] : "*";
                                ed.WriteMessage($"\n>> THÀNH CÔNG: Đã nhận diện Block Cột [{_config.ColumnBlockName}]");
                            }
                            else if (ent is DBText txt)
                            {
                                _config.ColumnBlockName = $"TEXT:{txt.Layer}";
                                _config.ColumnTagName = "*";
                                ed.WriteMessage($"\n>> THÀNH CÔNG: Đã nhận diện Text thường Cột trên Layer [{txt.Layer}]");
                            }
                            else if (ent is MText mtx)
                            {
                                _config.ColumnBlockName = $"TEXT:{mtx.Layer}";
                                _config.ColumnTagName = "*";
                                ed.WriteMessage($"\n>> THÀNH CÔNG: Đã nhận diện MText Cột trên Layer [{mtx.Layer}]");
                            }
                            _config.Save();
                            tr.Commit();
                        }
                    }
                }
                else if (win.IsPickFrameRequested)
                {
                    if (PickFrameAndRec(ed, db))
                    {
                        _config.Save();
                    }
                }
                else
                {
                    loop = false;
                }
            }
        }

        private static bool PickFrameAndRec(Editor ed, Database db)
        {
            var optBlk = new PromptEntityOptions("\n>> Bước 1: Chọn Block Khung tên mẫu: ");
            optBlk.SetRejectMessage("\nChỉ chọn Block Khung tên!");
            optBlk.AddAllowedClass(typeof(BlockReference), exactMatch: true);
            var resBlk = ed.GetEntity(optBlk);
            if (resBlk.Status != PromptStatus.OK) return false;

            var optPl = new PromptEntityOptions("\n>> Bước 2: Chọn hình chữ nhật Polyline (Rec) khung làm việc bên trong: ");
            optPl.SetRejectMessage("\nChỉ chọn Polyline khung làm việc!");
            optPl.AddAllowedClass(typeof(Polyline), exactMatch: true);
            var resPl = ed.GetEntity(optPl);
            if (resPl.Status != PromptStatus.OK) return false;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockReference blk = tr.GetObject(resBlk.ObjectId, OpenMode.ForRead) as BlockReference;
                Polyline pl = tr.GetObject(resPl.ObjectId, OpenMode.ForRead) as Polyline;

                _sampleFrameBlockId = blk.ObjectId;
                _sampleFrameInsPt = blk.Position;
                _frameName = blk.Name;

                Extents3d rExt = pl.GeometricExtents;
                _sampleRecW = rExt.MaxPoint.X - rExt.MinPoint.X;
                _sampleRecH = rExt.MaxPoint.Y - rExt.MinPoint.Y;
                _sampleRecTopLeft = new Point3d(rExt.MinPoint.X, rExt.MaxPoint.Y, 0.0);

                _config.FrameBlockName = _frameName;
                _config.SampleRecW = _sampleRecW;
                _config.SampleRecH = _sampleRecH;
                _config.DeltaInsX = _sampleFrameInsPt.X - _sampleRecTopLeft.X;
                _config.DeltaInsY = _sampleFrameInsPt.Y - _sampleRecTopLeft.Y;

                Extents3d fExt = blk.GeometricExtents;
                _sampleOuterWidth = fExt.MaxPoint.X - fExt.MinPoint.X;
                double outerH = fExt.MaxPoint.Y - fExt.MinPoint.Y;
                _config.SampleOuterW = _sampleOuterWidth;

                double maxDim = Math.Max(_sampleOuterWidth, outerH);
                _detectedScale = Math.Round((maxDim / 420.0) / 5.0) * 5.0;
                if (_detectedScale <= 0) _detectedScale = 50.0;
                _config.DetectedScale = _detectedScale;
                _frameInfo = $"Tỷ lệ gốc: 1/{_detectedScale}";

                foreach (ObjectId attId in blk.AttributeCollection)
                {
                    AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (att != null)
                    {
                        string tagUpper = att.Tag.ToUpper();
                        if (tagUpper.Contains("DONG1") || tagUpper == "TIEUDE_1") _config.TagLine1 = att.Tag;
                        if (tagUpper.Contains("DONG2") || tagUpper == "TIEUDE_2") _config.TagLine2 = att.Tag;
                        if (tagUpper.Contains("KHBV") || tagUpper.Contains("SO_BV") || tagUpper.Contains("SHEET")) _config.TagSheetNo = att.Tag;
                    }
                }

                _config.Save();
                tr.Commit();
            }
            ed.WriteMessage($"\n>> THÀNH CÔNG: Đã nhận diện [{_frameName}] - {_frameInfo}");
            return true;
        }

        [CommandMethod("SXBV")]
        [CommandMethod("SX")]
        public void RunLayoutTool()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            bool keepRunning = true;
            while (keepRunning)
            {
                var win = new LayoutWindow(_frameName, _frameInfo, _config);
                Application.ShowModalWindow(win);
                _config = win.Config;

                if (win.IsPickRequested)
                {
                    PickFrameAndRec(ed, doc.Database);
                }
                else if (win.IsRunRequested)
                {
                    keepRunning = false;
                    ExecuteLayoutProcess(doc, _config);
                }
                else
                {
                    keepRunning = false;
                }
            }
        }

        private static void ExecuteLayoutProcess(Document doc, LayoutConfig config)
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;

            if (_sampleFrameBlockId == ObjectId.Null || _sampleRecW <= 0)
            {
                if (!PickFrameAndRec(ed, db)) return;
            }

            short oldOsm = (short)Application.GetSystemVariable("OSMODE");
            try
            {
                Application.SetSystemVariable("OSMODE", 0);
                Application.SetSystemVariable("CMDECHO", 0);
                Application.SetSystemVariable("XCLIPFRAME", 0);
            }
            catch { }

            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.LayerName, "Khungbao_NoPrint")
            };
            var selRes = ed.GetSelection(new SelectionFilter(filterList));
            if (selRes.Status != PromptStatus.OK)
            {
                try { Application.SetSystemVariable("OSMODE", oldOsm); } catch { }
                return;
            }

            var ptRes = ed.GetPoint("\n>> Pick chọn GÓC TRÊN - TRÁI KHUNG REC cho bản vẽ đầu tiên: ");
            if (ptRes.Status != PromptStatus.OK)
            {
                try { Application.SetSystemVariable("OSMODE", oldOsm); } catch { }
                return;
            }
            Point3d ptPlace = ptRes.Value;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayer(db, tr, "SXBV_PhanCach", 8, "HIDDEN", LineWeight.LineWeight018);

                List<ViewBoundInfo> rawViews = new List<ViewBoundInfo>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    Polyline pl = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
                    if (pl == null) continue;
                    rawViews.Add(ExtractViewInfo(pl));
                }

                if (rawViews.Count == 0)
                {
                    try { Application.SetSystemVariable("OSMODE", oldOsm); } catch { }
                    return;
                }

                var colPrefixes = config.ColumnPrefixes.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                bool isColumnSession = rawViews.Any(v => colPrefixes.Any(p => v.ViewName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                                       || rawViews.Any(v => v.IsVerticalMajor);

                double frameScaleFactor = config.TargetScale / _detectedScale;
                double scaledRecW = _sampleRecW * frameScaleFactor;
                double scaledRecH = _sampleRecH * frameScaleFactor;
                double scaledOuterW = _sampleOuterWidth * frameScaleFactor;
                double frameStepX = scaledOuterW + config.DFrame;
                double availW = scaledRecW - (2.0 * config.Dx);
                double availH = scaledRecH - (2.0 * config.Dy);

                int curFrameIdx = 0;
                List<string> currentFrameItems = new List<string>();
                ObjectId currentFrameBlkId = SpawnFrameNative(ed, curFrameIdx, ptPlace, frameStepX, frameScaleFactor);

                Point3d curRecTop = new Point3d(ptPlace.X + (curFrameIdx * frameStepX), ptPlace.Y, 0);
                double curX = curRecTop.X + config.Dx;
                double curY = curRecTop.Y - config.Dy;
                double rowTopY = curY;
                double maxRowH = 0.0;
                bool isFirstInFrame = true;

                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // ==================================================================
                // LUỒNG 1: XỬ LÝ CỘT (CẮT ĐỨNG THEO Y TỪ CHÂN LÊN & DỒN SANG PHẢI)
                // ==================================================================
                if (isColumnSession)
                {
                    var colClusters = rawViews
                        .GroupBy(v => v.ViewName, StringComparer.OrdinalIgnoreCase)
                        .Select(g => new
                        {
                            Name = g.Key,
                            Main = g.FirstOrDefault(v => v.Priority.Equals("A", StringComparison.OrdinalIgnoreCase)) ?? g.First(),
                            Subs = g.Where(v => v.Priority.Equals("B", StringComparison.OrdinalIgnoreCase))
                                    .OrderByDescending(v => v.OriginalExtents.MaxPoint.Y)
                                    .ToList()
                        })
                        .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var cluster in colClusters)
                    {
                        double colScale = config.TargetScale / cluster.Main.SourceScale;
                        double mainColW = cluster.Main.Width * colScale;
                        double mainColH = cluster.Main.Height * colScale;

                        int numSegments = (mainColH > availH) ? (int)Math.Ceiling(mainColH / availH) : 1;
                        double totalRequiredClusterW = (numSegments * (mainColW + config.Dx)) +
                            cluster.Subs.Sum(s => s.Width * (config.TargetScale / s.SourceScale) + config.Dx);

                        curRecTop = new Point3d(ptPlace.X + (curFrameIdx * frameStepX), ptPlace.Y, 0);
                        double frameLimitX = curRecTop.X + scaledRecW - config.Dx;

                        if (!isFirstInFrame && (curX + totalRequiredClusterW > frameLimitX + 1.0))
                        {
                            FinalizeTitleBlock(tr, currentFrameBlkId, currentFrameItems, curFrameIdx, config);
                            currentFrameItems.Clear();

                            curFrameIdx++;
                            currentFrameBlkId = SpawnFrameNative(ed, curFrameIdx, ptPlace, frameStepX, frameScaleFactor);
                            curRecTop = new Point3d(ptPlace.X + (curFrameIdx * frameStepX), ptPlace.Y, 0);

                            curX = curRecTop.X + config.Dx;
                            curY = curRecTop.Y - config.Dy;
                            rowTopY = curY;
                            isFirstInFrame = true;
                        }

                        if (!isFirstInFrame)
                        {
                            DrawVerticalSeparatorLine(modelSpace, curX - (config.Dx / 2.0), curRecTop.Y, curRecTop.Y - scaledRecH, config.TargetScale);
                        }

                        currentFrameItems.Add(cluster.Name);

                        _sxbvUid++;
                        string mainBlkName = $"VIEW_{cluster.Main.ViewName}_{DateTime.Now.Millisecond}_{_sxbvUid}";
                        Point3d mTopLeft = new Point3d(cluster.Main.OriginalExtents.MinPoint.X, cluster.Main.OriginalExtents.MaxPoint.Y, 0.0);
                        CreateBlockNative(ed, db, tr, mainBlkName, mTopLeft, cluster.Main);

                        if (mainColH > availH)
                        {
                            double remainH = mainColH;
                            double segOffsetH = 0.0;
                            int colSegIdx = 0;

                            while (remainH > 0.0)
                            {
                                double segClipH = (remainH > availH) ? availH : remainH;
                                Point3d targetPt = new Point3d(curX, rowTopY + (mainColH - segClipH - segOffsetH), 0.0);
                                ObjectId blockDefId = ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[mainBlkName];

                                BlockReference blkRef = new BlockReference(targetPt, blockDefId) { ScaleFactors = new Scale3d(colScale) };
                                modelSpace.AppendEntity(blkRef);
                                tr.AddNewlyCreatedDBObject(blkRef, true);

                                double hideMargin = 6.0;
                                double clipLeft = curX + hideMargin;
                                double clipRight = curX + mainColW - hideMargin;
                                double clipTop = (remainH > availH) ? rowTopY : (rowTopY - hideMargin);
                                double clipBottom = (colSegIdx == 0) ? (rowTopY - segClipH + hideMargin) : (rowTopY - segClipH);

                                Point3d clipPMin = new Point3d(clipLeft, clipBottom, 0.0);
                                Point3d clipPMax = new Point3d(clipRight, clipTop, 0.0);
                                ed.Command("._xclip", blkRef.ObjectId, "", "_N", "_R", clipPMin, clipPMax);

                                if (colSegIdx > 0)
                                {
                                    DrawHorizontalZigzag(modelSpace, curX, curX + mainColW, rowTopY - segClipH, config.TargetScale);
                                }
                                if (remainH > availH)
                                {
                                    DrawHorizontalZigzag(modelSpace, curX, curX + mainColW, rowTopY, config.TargetScale);
                                }

                                curX += mainColW + config.Dx;

                                if (remainH > availH)
                                {
                                    remainH -= (availH - config.Overlap);
                                    segOffsetH += (availH - config.Overlap);
                                }
                                else
                                {
                                    remainH = 0.0;
                                }
                                colSegIdx++;
                            }
                        }
                        else
                        {
                            Point3d targetPt = new Point3d(curX, rowTopY, 0.0);
                            ObjectId blockDefId = ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[mainBlkName];
                            BlockReference blkRef = new BlockReference(targetPt, blockDefId) { ScaleFactors = new Scale3d(colScale) };
                            modelSpace.AppendEntity(blkRef);
                            tr.AddNewlyCreatedDBObject(blkRef, true);

                            double hideMargin = 6.0;
                            ed.Command("._xclip", blkRef.ObjectId, "", "_N", "_R",
                                new Point3d(curX + hideMargin, rowTopY - mainColH + hideMargin, 0),
                                new Point3d(curX + mainColW - hideMargin, rowTopY - hideMargin, 0));

                            curX += mainColW + config.Dx;
                        }

                        foreach (var sub in cluster.Subs)
                        {
                            double sScale = config.TargetScale / sub.SourceScale;
                            double sW = sub.Width * sScale;
                            double sH = sub.Height * sScale;

                            _sxbvUid++;
                            string subBlkName = $"VIEW_{sub.ViewName}_{DateTime.Now.Millisecond}_{_sxbvUid}";
                            Point3d sTopLeft = new Point3d(sub.OriginalExtents.MinPoint.X, sub.OriginalExtents.MaxPoint.Y, 0.0);
                            CreateBlockNative(ed, db, tr, subBlkName, sTopLeft, sub);

                            Point3d sTargetPt = new Point3d(curX, rowTopY, 0.0);
                            ObjectId sBlockDefId = ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[subBlkName];
                            BlockReference sBlkRef = new BlockReference(sTargetPt, sBlockDefId) { ScaleFactors = new Scale3d(sScale) };
                            modelSpace.AppendEntity(sBlkRef);
                            tr.AddNewlyCreatedDBObject(sBlkRef, true);

                            double hideMargin = 6.0;
                            ed.Command("._xclip", sBlkRef.ObjectId, "", "_N", "_R",
                                new Point3d(curX + hideMargin, rowTopY - sH + hideMargin, 0),
                                new Point3d(curX + sW - hideMargin, rowTopY - hideMargin, 0));

                            curX += sW + config.Dx;
                        }

                        isFirstInFrame = false;
                    }
                }
                // ==================================================================
                // LUỒNG 2: XỬ LÝ CHO DẦM (DẦM CHÍNH + CHIA 5 Ô ĐỀU CHO DẦM MẶT CẮT)
                // ==================================================================
                else
                {
                    Func<string, bool> isSecondaryBeam = name =>
                    {
                        if (string.IsNullOrWhiteSpace(name)) return false;
                        string upper = name.Trim().ToUpper();
                        return Regex.IsMatch(upper, @"^[0-9]*D[A-Z0-9]*P[0-9]*([-_/].*)?$") || upper.StartsWith("DP");
                    };

                    Func<string, object> naturalSortKey = name =>
                    {
                        return Regex.Replace(name ?? "", @"\d+", m => m.Value.PadLeft(6, '0'));
                    };

                    // 1. Phân loại và gom nhóm
                    var allClusters = rawViews
                        .GroupBy(v => v.ViewName, StringComparer.OrdinalIgnoreCase)
                        .Select(g =>
                        {
                            var mainView = g.FirstOrDefault(v => v.Priority.Equals("A", StringComparison.OrdinalIgnoreCase)) ?? g.First();
                            var subViews = g.Where(v => v.Priority.Equals("B", StringComparison.OrdinalIgnoreCase))
                                            .OrderBy(v => v.OriginalExtents.MinPoint.X).ToList();
                            // Là dầm mặt cắt đơn nếu không có view phụ VÀ tỉ lệ rộng/cao hẹp (< 2.2)
                            bool isSingleSection = subViews.Count == 0 && (mainView.Width / Math.Max(1.0, mainView.Height) < 2.2);

                            return new
                            {
                                Name = g.Key,
                                IsSec = isSecondaryBeam(g.Key),
                                IsSingleSec = isSingleSection,
                                Main = mainView,
                                Subs = subViews
                            };
                        })
                        .OrderBy(c => c.IsSingleSec ? 1 : (c.IsSec ? 1 : 0)) // Dầm dọc chính xếp trước, Dầm mặt cắt đơn xếp sau
                        .ThenBy(c => naturalSortKey(c.Name))
                        .ToList();

                    var standardBeams = allClusters.Where(c => !c.IsSingleSec).ToList();
                    var singleSections = allClusters.Where(c => c.IsSingleSec).ToList();

                    Point3d originRecTop = new Point3d(ptPlace.X + (curFrameIdx * frameStepX), ptPlace.Y, 0.0);
                    double frameLeftX = originRecTop.X + config.Dx;
                    double frameRightX = originRecTop.X + scaledRecW - config.Dx;
                    double frameBottomY = originRecTop.Y - scaledRecH + config.Dy;

                    double rowTopY_Local = originRecTop.Y - config.Dy;
                    double rowMaxHeight = 0.0;
                    double currentX = frameLeftX;
                    isFirstInFrame = true;

                    // -------------------------------------------------------------
                    // PHẦN A: BỐ TRÍ DẦM CHÍNH (CÓ THÂN DẦM DỌC + CÁC MẶT CẮT CON)
                    // -------------------------------------------------------------
                    foreach (var cluster in standardBeams)
                    {
                        double mainScale = config.TargetScale / cluster.Main.SourceScale;
                        double mainW = cluster.Main.Width * mainScale;
                        double mainH = cluster.Main.Height * mainScale;

                        // Tính chiều rộng và chiều cao hàng mặt cắt con
                        double subRowTotalW = 0.0;
                        double maxSubH = 0.0;
                        foreach (var sub in cluster.Subs)
                        {
                            double sW = sub.Width * (config.TargetScale / sub.SourceScale);
                            double sH = sub.Height * (config.TargetScale / sub.SourceScale);
                            subRowTotalW += sW + config.Dx;
                            if (sH > maxSubH) maxSubH = sH;
                        }
                        if (subRowTotalW > 0) subRowTotalW -= config.Dx;

                        // Dầm chính dài: mặt cắt con luôn nằm dưới đáy thân dầm dọc
                        // Dầm chính ngắn: nếu tổng chiều dài (thân + mặt cắt) vừa hàng ngang thì mới xếp cùng hàng
                        bool canFitSameRow = (mainW + config.Dx + subRowTotalW <= (frameRightX - frameLeftX) + 1.0);                      

                        double clusterRequiredW = canFitSameRow ? (mainW + config.Dx + subRowTotalW) : Math.Max(mainW, subRowTotalW);
                        double clusterTotalH = canFitSameRow ? Math.Max(mainH, maxSubH) : (mainH + config.Dy + maxSubH);

                        // KIỂM TRA ĐIỀU KIỆN XUỐNG DÒNG THEO PHƯƠNG NGANG HOẶC ĐỨNG
                        bool needNewRow = false;
                        if (!isFirstInFrame)
                        {
                            // Nếu dầm trước đó đã chiếm cả hàng ngang (có mặt cắt bên dưới) 
                            // HOẶC dầm mới không vừa khoảng trống còn lại bên phải -> PHẢI XUỐNG HÀNG
                            if (currentX + clusterRequiredW > frameRightX + 1.0)
                            {
                                needNewRow = true;
                            }
                        }

                        if (needNewRow)
                        {
                            double nextRowY = rowTopY_Local - rowMaxHeight - 500.0;
                            if (nextRowY - clusterTotalH < frameBottomY)
                            {
                                // Tràn đáy khung -> Sang khung mới
                                FinalizeTitleBlock(tr, currentFrameBlkId, currentFrameItems, curFrameIdx, config);
                                currentFrameItems.Clear();

                                curFrameIdx++;
                                currentFrameBlkId = SpawnFrameNative(ed, curFrameIdx, ptPlace, frameStepX, frameScaleFactor);

                                originRecTop = new Point3d(ptPlace.X + (curFrameIdx * frameStepX), ptPlace.Y, 0.0);
                                frameLeftX = originRecTop.X + config.Dx;
                                frameRightX = originRecTop.X + scaledRecW - config.Dx;
                                frameBottomY = originRecTop.Y - scaledRecH + config.Dy;

                                rowTopY_Local = originRecTop.Y - config.Dy;
                                currentX = frameLeftX;
                                rowMaxHeight = 0.0;
                                isFirstInFrame = true;
                            }
                            else
                            {
                                // Còn chỗ trong khung -> Kẻ nét ngang và hạ xuống tầng dưới
                                DrawSeparatorLine(modelSpace, originRecTop.X, originRecTop.X + scaledRecW, rowTopY_Local - rowMaxHeight - 250.0, config.TargetScale);
                                rowTopY_Local = nextRowY;
                                currentX = frameLeftX;
                                rowMaxHeight = 0.0;
                            }
                        }

                        // Kẻ vách ngăn đứng nếu có 2 dầm chính xếp cạnh nhau trên cùng một hàng ngang
                        if (!isFirstInFrame && !needNewRow)
                        {
                            DrawVerticalSeparatorLine(modelSpace, currentX - (config.Dx / 2.0), rowTopY_Local, rowTopY_Local - clusterTotalH, config.TargetScale);
                        }

                        currentFrameItems.Add(cluster.Name);

                        // 1. VẼ THÂN DẦM DỌC CHÍNH
                        _sxbvUid++;
                        string mainBlkName = $"VIEW_{cluster.Main.ViewName}_{DateTime.Now.Millisecond}_{_sxbvUid}";
                        Point3d mTopLeft = new Point3d(cluster.Main.OriginalExtents.MinPoint.X, cluster.Main.OriginalExtents.MaxPoint.Y, 0.0);
                        CreateBlockNative(ed, db, tr, mainBlkName, mTopLeft, cluster.Main);

                        double remainW = mainW;
                        double segOffset = 0.0;
                        int segIdx = 0;
                        double placedBeamW = 0.0;

                        while (remainW > 0.0)
                        {
                            double segClipW = (remainW > availW) ? availW : remainW;
                            Point3d targetPt = new Point3d(currentX - segOffset, rowTopY_Local, 0.0);
                            ObjectId blockDefId = ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[mainBlkName];

                            BlockReference blkRef = new BlockReference(targetPt, blockDefId) { ScaleFactors = new Scale3d(mainScale) };
                            modelSpace.AppendEntity(blkRef);
                            tr.AddNewlyCreatedDBObject(blkRef, true);

                            double hideMargin = 6.0;
                            ed.Command("._xclip", blkRef.ObjectId, "", "_N", "_R",
                                new Point3d(currentX + (segIdx == 0 ? hideMargin : 0), rowTopY_Local - mainH + hideMargin, 0),
                                new Point3d(currentX + segClipW - (remainW > availW ? 0 : hideMargin), rowTopY_Local - hideMargin, 0));

                            if (segIdx > 0)
                                DrawZigzag(modelSpace, currentX, rowTopY_Local, rowTopY_Local - mainH, config.TargetScale);
                            if (remainW > availW)
                                DrawZigzag(modelSpace, currentX + segClipW, rowTopY_Local, rowTopY_Local - mainH, config.TargetScale);

                            placedBeamW += segClipW;

                            if (remainW > availW)
                            {
                                remainW -= (availW - config.Overlap);
                                segOffset += (availW - config.Overlap);
                                currentX = frameLeftX;
                                rowTopY_Local -= (mainH + config.Dy);
                            }
                            else
                            {
                                remainW = 0.0;
                            }
                            segIdx++;
                        }

                        // 2. VẼ CÁC MẶT CẮT CON
                        double subStartX;
                        double subStartY;

                        if (canFitSameRow)
                        {
                            // Xếp tiếp nối ngay bên phải thân dầm dọc
                            subStartX = currentX + placedBeamW + config.Dx;
                            // Căn tim tuyệt đối theo phương Y giữa thân dầm dọc và hàng mặt cắt
                            double beamCenterY = rowTopY_Local - (mainH / 2.0);
                            subStartY = beamCenterY + (maxSubH / 2.0);
                        }
                        else
                        {
                            // Dầm dài chuẩn: Bắt đầu từ lề trái của dầm này, nằm phía dưới đáy dầm dọc
                            subStartX = currentX;
                            subStartY = rowTopY_Local - mainH - config.Dy;
                        }

                        double actualSubH = 0.0;
                        double subEndX = subStartX;

                        foreach (var sub in cluster.Subs)
                        {
                            double sScale = config.TargetScale / sub.SourceScale;
                            double sW = sub.Width * sScale;
                            double sH = sub.Height * sScale;

                            // Nếu mặt cắt con tràn lề phải -> ngắt xuống hàng con tiếp theo
                            if (subStartX + sW > frameRightX + 1.0)
                            {
                                subStartX = currentX;
                                subStartY -= (actualSubH + config.Dy);
                                actualSubH = 0.0;
                            }

                            _sxbvUid++;
                            string subBlkName = $"VIEW_{sub.ViewName}_{DateTime.Now.Millisecond}_{_sxbvUid}";
                            Point3d sTopLeft = new Point3d(sub.OriginalExtents.MinPoint.X, sub.OriginalExtents.MaxPoint.Y, 0.0);
                            CreateBlockNative(ed, db, tr, subBlkName, sTopLeft, sub);

                            Point3d sTargetPt = new Point3d(subStartX, subStartY, 0.0);
                            ObjectId sDefId = ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[subBlkName];

                            BlockReference sBlkRef = new BlockReference(sTargetPt, sDefId) { ScaleFactors = new Scale3d(sScale) };
                            modelSpace.AppendEntity(sBlkRef);
                            tr.AddNewlyCreatedDBObject(sBlkRef, true);

                            double hideMargin = 6.0;
                            ed.Command("._xclip", sBlkRef.ObjectId, "", "_N", "_R",
                                new Point3d(subStartX + hideMargin, subStartY - sH + hideMargin, 0),
                                new Point3d(subStartX + sW - hideMargin, subStartY - hideMargin, 0));

                            subStartX += sW + config.Dx;
                            if (subStartX > subEndX) subEndX = subStartX;
                            if (sH > actualSubH) actualSubH = sH;
                        }

                        // 3. CẬP NHẬT TỔNG CHIỀU CAO VÀ VỊ TRÍ X CHO CẤU KIỆN TIẾP THEO
                        if (canFitSameRow)
                        {
                            currentX = subEndX + config.Dx;
                            double rowH = Math.Max(mainH, maxSubH);
                            if (rowH > rowMaxHeight) rowMaxHeight = rowH;
                        }
                        else
                        {
                            // Dầm dài có mặt cắt nằm dưới: Toàn bộ cụm này chiếm hết bề ngang tầng đó
                            double totalThisClusterH = (rowTopY_Local - (subStartY - actualSubH));
                            if (totalThisClusterH > rowMaxHeight) rowMaxHeight = totalThisClusterH;

                            // Ép cấu kiện dầm tiếp theo phải xuống hàng mới (bằng cách đặt currentX vượt frameRightX)
                            currentX = frameRightX + 100.0;
                        }

                        isFirstInFrame = false;
                    }

                    // -------------------------------------------------------------
                    // PHẦN B: BỐ TRÍ DẦM MẶT CẮT ĐƠN (KHÉP KÍN LƯỚI Ô 5 NGĂN VÀ CHỐT BIÊN)
                    // -------------------------------------------------------------
                    if (singleSections.Count > 0)
                    {
                        double totalWorkW = scaledRecW - (2.0 * config.Dx);
                        double cellW = totalWorkW / 5.0; // Bề rộng 1 ô

                        // Chiều cao đồng bộ của ô: lấy max chiều cao mặt cắt + đệm 200mm
                        double maxSecH = singleSections.Max(s => s.Main.Height * (config.TargetScale / s.Main.SourceScale));
                        double cellH = maxSecH + 200.0;
                        double separatorGap = 500.0;

                        // Nếu trước đó đã có dầm chính -> kẻ nét ngang đỉnh tầng ô
                        double currentCellTopY;
                        if (!isFirstInFrame)
                        {
                            currentCellTopY = rowTopY_Local - rowMaxHeight - 250.0;
                            DrawSeparatorLine(modelSpace, originRecTop.X, originRecTop.X + scaledRecW, currentCellTopY, config.TargetScale);
                        }
                        else
                        {
                            currentCellTopY = rowTopY_Local;
                        }

                        double currentCellBottomY = currentCellTopY - cellH;
                        int currentSlot = 0;
                        int countInCurrentRow = 0;

                        for (int idx = 0; idx < singleSections.Count; idx++)
                        {
                            var cluster = singleSections[idx];
                            double secScale = config.TargetScale / cluster.Main.SourceScale;
                            double secW = cluster.Main.Width * secScale;
                            double secH = cluster.Main.Height * secScale;

                            // Khi đã xếp đủ 5 ô trên 1 tầng -> Chốt đáy tầng cũ và chuyển tầng
                            if (currentSlot >= 5)
                            {
                                // 1. Chốt nét ngang chân của tầng 5 ô vừa xong
                                DrawSeparatorLine(modelSpace, originRecTop.X, originRecTop.X + scaledRecW, currentCellBottomY, config.TargetScale);

                                double nextCellTopY = currentCellBottomY - separatorGap;
                                double nextCellBottomY = nextCellTopY - cellH;

                                if (nextCellBottomY < frameBottomY)
                                {
                                    // Tràn khung -> Sang khung bản vẽ mới
                                    FinalizeTitleBlock(tr, currentFrameBlkId, currentFrameItems, curFrameIdx, config);
                                    currentFrameItems.Clear();

                                    curFrameIdx++;
                                    currentFrameBlkId = SpawnFrameNative(ed, curFrameIdx, ptPlace, frameStepX, frameScaleFactor);

                                    originRecTop = new Point3d(ptPlace.X + (curFrameIdx * frameStepX), ptPlace.Y, 0.0);
                                    frameLeftX = originRecTop.X + config.Dx;
                                    frameRightX = originRecTop.X + scaledRecW - config.Dx;
                                    frameBottomY = originRecTop.Y - scaledRecH + config.Dy;

                                    currentCellTopY = originRecTop.Y - config.Dy;
                                    currentCellBottomY = currentCellTopY - cellH;
                                    isFirstInFrame = true;
                                }
                                else
                                {
                                    // Cùng khung -> Xuống tầng tiếp theo, kẻ nét ngang đỉnh tầng mới
                                    DrawSeparatorLine(modelSpace, originRecTop.X, originRecTop.X + scaledRecW, nextCellTopY, config.TargetScale);
                                    currentCellTopY = nextCellTopY;
                                    currentCellBottomY = currentCellTopY - cellH;
                                }

                                currentSlot = 0;
                                countInCurrentRow = 0;
                            }

                            currentFrameItems.Add(cluster.Name);

                            // 1. TÍNH TOẠ ĐỘ ĐẶT CĂN GIỮA Ô THỨ currentSlot
                            double cellLeftX = frameLeftX + (currentSlot * cellW);
                            double cellRightX = cellLeftX + cellW;
                            double cellCenterX = cellLeftX + (cellW / 2.0);
                            double cellCenterY = currentCellTopY - (cellH / 2.0);

                            double insertX = cellCenterX - (secW / 2.0);
                            double insertY = cellCenterY + (secH / 2.0);

                            // 2. KẺ VÁCH NGĂN ĐỨNG BÊN TRÁI Ô (NẾU KHÔNG PHẢI Ô ĐẦU TIÊN)
                            if (currentSlot > 0)
                            {
                                DrawVerticalSeparatorLine(modelSpace, cellLeftX, currentCellTopY, currentCellBottomY, config.TargetScale);
                            }

                            // 3. ĐẶT BLOCK VÀ TẠO XCLIP
                            _sxbvUid++;
                            string blkName = $"VIEW_{cluster.Main.ViewName}_{DateTime.Now.Millisecond}_{_sxbvUid}";
                            Point3d mTopLeft = new Point3d(cluster.Main.OriginalExtents.MinPoint.X, cluster.Main.OriginalExtents.MaxPoint.Y, 0.0);
                            CreateBlockNative(ed, db, tr, blkName, mTopLeft, cluster.Main);

                            Point3d targetPt = new Point3d(insertX, insertY, 0.0);
                            ObjectId blockDefId = ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[blkName];

                            BlockReference blkRef = new BlockReference(targetPt, blockDefId) { ScaleFactors = new Scale3d(secScale) };
                            modelSpace.AppendEntity(blkRef);
                            tr.AddNewlyCreatedDBObject(blkRef, true);

                            double hideMargin = 6.0;
                            ed.Command("._xclip", blkRef.ObjectId, "", "_N", "_R",
                                new Point3d(insertX + hideMargin, insertY - secH + hideMargin, 0),
                                new Point3d(insertX + secW - hideMargin, insertY - hideMargin, 0));

                            countInCurrentRow++;

                            // 4. NẾU LÀ CẤU KIỆN CUỐI CÙNG CỦA TOÀN BỘ DANH SÁCH (HOẶC HẾT BẢN VẼ)
                            // Kẻ ngay nét đứng bên phải kết thúc ô và nét ngang chốt chân
                            bool isLastItem = (idx == singleSections.Count - 1);
                            if (isLastItem)
                            {
                                // Kẻ nét đứng bên phải chốt cấu kiện cuối cùng (như ở bản vẽ 2 có 2 dầm)
                                DrawVerticalSeparatorLine(modelSpace, cellRightX, currentCellTopY, currentCellBottomY, config.TargetScale);

                                // Kẻ nét ngang chốt chân đúng vị trí đáy của ô (chấm dứt nét lơ lửng)
                                DrawSeparatorLine(modelSpace, originRecTop.X, originRecTop.X + scaledRecW, currentCellBottomY, config.TargetScale);
                            }

                            currentSlot++;
                            isFirstInFrame = false;
                        }

                        rowTopY_Local = currentCellBottomY;
                        rowMaxHeight = 0.0;
                    }
                }

                FinalizeTitleBlock(tr, currentFrameBlkId, currentFrameItems, curFrameIdx, config);

                tr.Commit();
                try { Application.SetSystemVariable("OSMODE", oldOsm); } catch { }
                ed.WriteMessage($"\n>> HOÀN TẤT: Đã sắp xếp chuẩn xác và không tràn đáy vào {curFrameIdx + 1} khung bản vẽ!");
            }
        }

        private static void FinalizeTitleBlock(Transaction tr, ObjectId blkId, List<string> items, int frameIndex, LayoutConfig config)
        {
            if (blkId == ObjectId.Null) return;
            BlockReference blk = tr.GetObject(blkId, OpenMode.ForWrite) as BlockReference;
            if (blk == null) return;

            var distinctNames = items.Distinct().ToList();
            string line2Content = string.Join(config.SeparatorChar, distinctNames);

            string line1Content = "CHI TIẾT KẾT CẤU";
            var beamList = config.BeamPrefixes.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var colList = config.ColumnPrefixes.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            bool hasBeam = distinctNames.Any(n => beamList.Any(p => n.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
            bool hasCol = distinctNames.Any(n => colList.Any(p => n.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

            if (hasBeam && !hasCol) line1Content = "CHI TIẾT DẦM";
            else if (!hasBeam && hasCol) line1Content = "CHI TIẾT CỘT";
            else if (hasBeam && hasCol) line1Content = "CHI TIẾT DẦM - CỘT";

            string sheetNo = "";
            if (config.AutoNumbering && !string.IsNullOrEmpty(config.SheetStartIndex))
            {
                if (int.TryParse(config.SheetStartIndex, out int startVal))
                {
                    int curVal = startVal + frameIndex;
                    string formatStr = new string('0', config.SheetStartIndex.Length);
                    sheetNo = $"{config.SheetPrefix}{curVal.ToString(formatStr)}{config.SheetSuffix}";
                }
            }

            foreach (ObjectId attId in blk.AttributeCollection)
            {
                AttributeReference att = tr.GetObject(attId, OpenMode.ForWrite) as AttributeReference;
                if (att == null) continue;

                TextStyleTableRecord ts = tr.GetObject(att.TextStyleId, OpenMode.ForRead) as TextStyleTableRecord;
                int encoding = (config.FontMode == 0) ? EncodingConverter.DetectEncoding(ts, att.TextString) : config.FontMode;

                if (config.AutoFillTitle && att.Tag.Equals(config.TagLine1, StringComparison.OrdinalIgnoreCase))
                {
                    att.TextString = EncodingConverter.Convert(line1Content, encoding);
                }
                else if (config.AutoFillTitle && att.Tag.Equals(config.TagLine2, StringComparison.OrdinalIgnoreCase))
                {
                    att.TextString = EncodingConverter.Convert(line2Content, encoding);
                }
                else if (config.AutoNumbering && !string.IsNullOrEmpty(sheetNo) && att.Tag.Equals(config.TagSheetNo, StringComparison.OrdinalIgnoreCase))
                {
                    att.TextString = sheetNo;
                }
            }
        }

        private static ObjectId SpawnFrameNative(Editor ed, int frameIdx, Point3d startPt, double stepX, double scaleFactor)
        {
            Point3d destRecTop = new Point3d(startPt.X + (frameIdx * stepX), startPt.Y, 0.0);
            Vector3d delta = (_sampleFrameInsPt - _sampleRecTopLeft) * scaleFactor;
            Point3d targetFrameIns = destRecTop + delta;

            ed.Command("._copy", _sampleFrameBlockId, "", _sampleFrameInsPt, targetFrameIns);
            if (Math.Abs(scaleFactor - 1.0) > 0.001)
            {
                ed.Command("._scale", "_L", "", targetFrameIns, scaleFactor);
            }

            PromptSelectionResult ssLast = ed.SelectLast();
            return (ssLast.Status == PromptStatus.OK && ssLast.Value.Count > 0) ? ssLast.Value[0].ObjectId : ObjectId.Null;
        }

        private static bool CreateBlockNative(Editor ed, Database db, Transaction tr, string blkName, Point3d boundTopLeft, ViewBoundInfo view)
        {
            PromptSelectionResult pSel = ed.SelectCrossingPolygon(view.PolyPoints);
            if (pSel.Status != PromptStatus.OK) return false;

            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            BlockTableRecord btr = new BlockTableRecord { Name = blkName, Origin = Point3d.Origin };

            ObjectId btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);

            Matrix3d shiftToOrigin = Matrix3d.Displacement(Point3d.Origin - boundTopLeft);

            foreach (SelectedObject sObj in pSel.Value)
            {
                if (sObj.ObjectId == view.PolyId) continue;
                Entity ent = tr.GetObject(sObj.ObjectId, OpenMode.ForRead) as Entity;
                if (ent == null || ent.Layer.Equals("Khungbao_NoPrint", StringComparison.OrdinalIgnoreCase)) continue;

                Entity clone = (Entity)ent.Clone();
                clone.TransformBy(shiftToOrigin);
                btr.AppendEntity(clone);
                tr.AddNewlyCreatedDBObject(clone, true);
            }

            Entity boundClone = (Entity)(tr.GetObject(view.PolyId, OpenMode.ForRead)).Clone();
            boundClone.TransformBy(shiftToOrigin);
            btr.AppendEntity(boundClone);
            tr.AddNewlyCreatedDBObject(boundClone, true);

            return true;
        }

        private static void DrawZigzag(BlockTableRecord btr, double x, double yTop, double yBot, double scaleFactor)
        {
            double h = yTop - yBot;
            double midY = yBot + (h / 2.0);
            double zigW = 1.5 * scaleFactor;
            double zigH = 1.5 * scaleFactor;

            Polyline pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(x, yTop), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(x, midY + zigH), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(x + zigW, midY + zigH), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(x - zigW, midY - zigH), 0, 0, 0);
            pl.AddVertexAt(4, new Point2d(x, midY - zigH), 0, 0, 0);
            pl.AddVertexAt(5, new Point2d(x, yBot), 0, 0, 0);
            pl.Layer = "0"; pl.ColorIndex = 8;

            btr.AppendEntity(pl);
            btr.Database.TransactionManager.TopTransaction.AddNewlyCreatedDBObject(pl, true);
        }

        private static void DrawHorizontalZigzag(BlockTableRecord btr, double xLeft, double xRight, double y, double scaleFactor)
        {
            double w = xRight - xLeft;
            double midX = xLeft + (w / 2.0);
            double zigW = 1.5 * scaleFactor;
            double zigH = 1.5 * scaleFactor;

            Polyline pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(xLeft, y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(midX - zigW, y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(midX - zigW, y + zigH), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(midX + zigW, y - zigH), 0, 0, 0);
            pl.AddVertexAt(4, new Point2d(midX + zigW, y), 0, 0, 0);
            pl.AddVertexAt(5, new Point2d(xRight, y), 0, 0, 0);
            pl.Layer = "0"; pl.ColorIndex = 8;

            btr.AppendEntity(pl);
            btr.Database.TransactionManager.TopTransaction.AddNewlyCreatedDBObject(pl, true);
        }

        private static void DrawSeparatorLine(BlockTableRecord btr, double xMin, double xMax, double y, double targetScale)
        {
            Database db = btr.Database;
            // Quét LTS của bản vẽ hiện hành để tính tỷ lệ nét đứt chuẩn xác khi zoom xa
            double currentLts = db.Ltscale > 0 ? db.Ltscale : 1.0;
            double calculatedScale = (targetScale * 15.0) / currentLts;

            Line line = new Line(new Point3d(xMin, y, 0), new Point3d(xMax, y, 0))
            {
                Layer = "SXBV_PhanCach",
                Linetype = "HIDDEN",
                LinetypeScale = calculatedScale
            };
            btr.AppendEntity(line);
            btr.Database.TransactionManager.TopTransaction.AddNewlyCreatedDBObject(line, true);
        }

        private static void DrawVerticalSeparatorLine(BlockTableRecord btr, double x, double yTop, double yBot, double targetScale)
        {
            Database db = btr.Database;
            double currentLts = db.Ltscale > 0 ? db.Ltscale : 1.0;
            double calculatedScale = (targetScale * 15.0) / currentLts;

            double actualTop = Math.Max(yTop, yBot);
            double actualBot = Math.Min(yTop, yBot);

            Line line = new Line(new Point3d(x, actualTop, 0), new Point3d(x, actualBot, 0))
            {
                Layer = "SXBV_PhanCach",
                Linetype = "HIDDEN",
                LinetypeScale = calculatedScale
            };
            btr.AppendEntity(line);
            btr.Database.TransactionManager.TopTransaction.AddNewlyCreatedDBObject(line, true);
        }

        private static ViewBoundInfo ExtractViewInfo(Polyline pl)
        {
            var info = new ViewBoundInfo
            {
                PolyId = pl.ObjectId,
                OriginalExtents = pl.GeometricExtents
            };

            for (int i = 0; i < pl.NumberOfVertices; i++)
            {
                info.PolyPoints.Add(pl.GetPoint3dAt(i));
            }

            ResultBuffer xdata = pl.GetXDataForApplication("LAYOUT_TOOL_DATA");
            if (xdata != null)
            {
                var list = xdata.AsArray();
                var strings = list.Where(t => t.TypeCode == (short)DxfCode.ExtendedDataAsciiString).Select(t => (string)t.Value).ToList();
                var reals = list.Where(t => t.TypeCode == (short)DxfCode.ExtendedDataReal).Select(t => (double)t.Value).ToList();

                if (strings.Count > 0) info.ViewName = strings[0];
                if (reals.Count > 0) info.SourceScale = reals[0];
                if (strings.Count > 1) info.Priority = strings[1];
            }
            return info;
        }

        private static void EnsureLayer(Database db, Transaction tr, string layerName, short colorIndex, string lineType, LineWeight lw, bool isPlottable = true)
        {
            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

            // Tự động Load kiểu đường HIDDEN từ file acad.lin nếu bản vẽ chưa có
            if (!ltt.Has(lineType))
            {
                try
                {
                    db.LoadLineTypeFile(lineType, "acad.lin");
                }
                catch { }
            }

            ObjectId ltypeId = ltt.Has(lineType) ? ltt[lineType] : db.ContinuousLinetype;

            if (!lt.Has(layerName))
            {
                lt.UpgradeOpen();
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex),
                    LinetypeObjectId = ltypeId,
                    LineWeight = lw,
                    IsPlottable = isPlottable
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
            else
            {
                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
                ltr.LinetypeObjectId = ltypeId;
            }
        }
    }
}