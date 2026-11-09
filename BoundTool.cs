using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

// Đăng ký toàn bộ lệnh trong BoundTool với AutoCAD Runtime
[assembly: CommandClass(typeof(StructuralLayoutEngine.BoundTool))]

namespace StructuralLayoutEngine
{
    public class BoundTool
    {
        private static string _globalBeamName = "D1";
        private static string _globalColName = "C1";
        private static double _globalColScale = 50.0;

        // ======================================================================
        // 1. LỆNH KBD: VẼ KHUNG BAO DẦM (TỰ ĐỘNG GOM MẶT CẮT B)
        // ======================================================================
        [CommandMethod("KBD")]
        public void RunCreateBeamBounding()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            LayoutConfig config = LayoutConfig.Load();

            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = $"\n>> Quét chọn cụm dầm (Mặt cắt dọc hoặc ngang) [Tên hiện tại: {_globalBeamName}]: "
            };
            var selRes = ed.GetSelection(pso);
            if (selRes.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayer(db, tr, "Khungbao_NoPrint", 6, isPlottable: false);
                EnsureRegApp(db, tr, "LAYOUT_TOOL_DATA");

                double finalScale = GetDimStyleScale(tr, selRes.Value);
                string detectedName = FindNameInSelection(tr, selRes.Value, config.BeamBlockName, config.BeamTagName, isBeam: true);

                string finalPrio;
                if (!string.IsNullOrEmpty(detectedName))
                {
                    _globalBeamName = detectedName;
                    finalPrio = "A";
                }
                else
                {
                    finalPrio = "B"; // Mặt cắt ngang đi kèm dầm trước đó
                }

                Extents3d bBox = GetSelectionBoundingBox(tr, selRes.Value);
                if (bBox.MinPoint.X < bBox.MaxPoint.X)
                {
                    CreateBoundingPolyline(db, tr, bBox, _globalBeamName, finalScale, finalPrio, 50.0);
                    ed.WriteMessage($"\n>> [TẠO THÀNH CÔNG DẦM] Khung: [{_globalBeamName} | 1/{finalScale} | Nhóm: {finalPrio}]");
                }

                tr.Commit();
            }
        }

        // ======================================================================
        // 2. LỆNH KBC: VẼ KHUNG BAO CỘT
        // ======================================================================
        [CommandMethod("KBC")]
        public void RunCreateColumnBounding()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            LayoutConfig config = LayoutConfig.Load();

            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = $"\n>> Quét chọn chi tiết cột (Mặt đứng hoặc Mặt cắt) [Tên hiện tại: {_globalColName}]: "
            };
            var selRes = ed.GetSelection(pso);
            if (selRes.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayer(db, tr, "Khungbao_NoPrint", 6, isPlottable: false);
                EnsureRegApp(db, tr, "LAYOUT_TOOL_DATA");

                double finalScale = GetDimStyleScale(tr, selRes.Value);
                string detectedName = FindNameInSelection(tr, selRes.Value, config.ColumnBlockName, config.ColumnTagName, isBeam: false);

                string finalPrio;
                if (!string.IsNullOrEmpty(detectedName))
                {
                    if (!detectedName.Equals(_globalColName, StringComparison.OrdinalIgnoreCase))
                    {
                        _globalColName = detectedName;
                        _globalColScale = finalScale;
                        finalPrio = "A";
                    }
                    else if (finalScale >= _globalColScale)
                    {
                        _globalColScale = finalScale;
                        finalPrio = "A";
                    }
                    else
                    {
                        finalPrio = "B";
                    }
                }
                else
                {
                    finalPrio = "B";
                }

                Extents3d bBox = GetSelectionBoundingBox(tr, selRes.Value);
                if (bBox.MinPoint.X < bBox.MaxPoint.X)
                {
                    CreateBoundingPolyline(db, tr, bBox, _globalColName, finalScale, finalPrio, 50.0);
                    ed.WriteMessage($"\n>> [TẠO THÀNH CÔNG CỘT] Khung: [{_globalColName} | 1/{finalScale} | Nhóm: {finalPrio}]");
                }

                tr.Commit();
            }
        }

        // ======================================================================
        // 3. LỆNH KB & SOI_KB: SOI VÀ SỬA THÔNG TIN KHUNG BAO TRỰC TIẾP QUA UI
        // ======================================================================
        [CommandMethod("KB")]
        [CommandMethod("SOI_KB")]
        [CommandMethod("SOIXDATA")]
        public void RunSoiVaSuaKhungBao()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = "\n>> Chọn 1 khung bao Polyline để Soi/Sửa (hoặc quét cụm đối tượng để tạo khung mới): "
            };
            var selRes = ed.GetSelection(pso);
            if (selRes.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayer(db, tr, "Khungbao_NoPrint", 6, isPlottable: false);
                EnsureRegApp(db, tr, "LAYOUT_TOOL_DATA");

                if (selRes.Value.Count == 1)
                {
                    ObjectId entId = selRes.Value[0].ObjectId;
                    Polyline pl = tr.GetObject(entId, OpenMode.ForRead) as Polyline;
                    if (pl != null)
                    {
                        var (curName, curScale, curPrio) = GetXData(pl);
                        double w = Math.Round(pl.GeometricExtents.MaxPoint.X - pl.GeometricExtents.MinPoint.X);
                        double h = Math.Round(pl.GeometricExtents.MaxPoint.Y - pl.GeometricExtents.MinPoint.Y);

                        ed.WriteMessage($"\n>> ĐANG SOI KHUNG: [{curName} | 1/{curScale} | Nhóm: {curPrio} | Kích thước: {w}x{h} mm]");

                        var win = new KBDialogWindow(curName, curScale, curPrio);
                        Application.ShowModalWindow(win);

                        if (win.IsSaved)
                        {
                            pl.UpgradeOpen();
                            AttachXData(pl, win.ResultName, win.ResultScale, win.ResultPrio);
                            ed.WriteMessage($"\n>> THÀNH CÔNG: Đã lưu thông tin mới -> [{win.ResultName} | 1/{win.ResultScale} | Nhóm: {win.ResultPrio}]");
                        }
                        tr.Commit();
                        return;
                    }
                }

                double maxDimScale = GetDimStyleScale(tr, selRes.Value);
                var createWin = new KBDialogWindow("DX1", maxDimScale, "A");
                Application.ShowModalWindow(createWin);

                if (createWin.IsSaved)
                {
                    Extents3d bBox = GetSelectionBoundingBox(tr, selRes.Value);
                    if (bBox.MinPoint.X < bBox.MaxPoint.X)
                    {
                        CreateBoundingPolyline(db, tr, bBox, createWin.ResultName, createWin.ResultScale, createWin.ResultPrio, 25.0);
                        ed.WriteMessage($"\n>> ĐÃ TẠO KHUNG BAO: [{createWin.ResultName} | 1/{createWin.ResultScale} | Nhóm: {createWin.ResultPrio}]");
                    }
                }

                tr.Commit();
            }
        }

        // ======================================================================
        // 4. LỆNH CKB / CHUYEN_KB: CHUYỂN KHUNG LAYER KHÁC VỀ Khungbao_NoPrint
        // ======================================================================
        [CommandMethod("CKB")]
        [CommandMethod("CHUYEN_KB")]
        public void RunChuyenKhungBao()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            var optEnt = new PromptEntityOptions("\n>> Chọn 1 khung bao mẫu cần chuyển đổi (Layer cũ): ");
            optEnt.SetRejectMessage("\nChỉ chọn Polyline khung bao!");
            optEnt.AddAllowedClass(typeof(Polyline), exactMatch: true);
            var resEnt = ed.GetEntity(optEnt);
            if (resEnt.Status != PromptStatus.OK) return;

            string targetLayer = "";
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity sampleEnt = tr.GetObject(resEnt.ObjectId, OpenMode.ForRead) as Entity;
                if (sampleEnt != null) targetLayer = sampleEnt.Layer;
                tr.Commit();
            }

            if (string.IsNullOrEmpty(targetLayer)) return;
            ed.WriteMessage($"\n>> Đã nhận diện Layer cần chuyển: [{targetLayer}]");

            var pso = new PromptSelectionOptions { MessageForAdding = "\n>> Quét chọn vùng bản vẽ cần chuyển sang Khungbao_NoPrint: " };
            TypedValue[] filters = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.LayerName, targetLayer)
            };
            var selRes = ed.GetSelection(pso, new SelectionFilter(filters));
            if (selRes.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayer(db, tr, "Khungbao_NoPrint", 6, isPlottable: false);
                EnsureRegApp(db, tr, "LAYOUT_TOOL_DATA");

                int count = 0;
                foreach (SelectedObject so in selRes.Value)
                {
                    if (so == null) continue;
                    Polyline pl = tr.GetObject(so.ObjectId, OpenMode.ForWrite) as Polyline;
                    if (pl == null) continue;

                    pl.Layer = "Khungbao_NoPrint";
                    pl.ColorIndex = 6;

                    var (vName, vScale, vPrio) = GetXData(pl);
                    AttachXData(pl, vName, vScale, vPrio);
                    count++;
                }

                tr.Commit();
                ed.WriteMessage($"\n>> HOÀN TẤT: Đã chuyển đổi thành công {count} khung bao sang Layer 'Khungbao_NoPrint'!");
            }
        }

        // ======================================================================
        // 5. LỆNH GAN_KB: COPY GÁN THUỘC TÍNH XDATA TỪ MẪU SANG HÀNG LOẠT
        // ======================================================================
        [CommandMethod("GAN_KB")]
        public void RunGanKhungBao()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            var optSrc = new PromptEntityOptions("\n>> Chọn Khung bao nguồn (mẫu) có sẵn để lấy thông tin: ");
            optSrc.SetRejectMessage("\nChỉ chọn Polyline khung bao!");
            optSrc.AddAllowedClass(typeof(Polyline), exactMatch: true);
            var resSrc = ed.GetEntity(optSrc);
            if (resSrc.Status != PromptStatus.OK) return;

            string srcName = "VIEW";
            double srcScale = 50.0;
            string srcPrio = "A";

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Polyline srcPl = tr.GetObject(resSrc.ObjectId, OpenMode.ForRead) as Polyline;
                if (srcPl != null)
                {
                    var data = GetXData(srcPl);
                    srcName = data.Item1;
                    srcScale = data.Item2;
                    srcPrio = data.Item3;
                    ed.WriteMessage($"\n>> ĐÃ LẤY MẪU: [Tên: {srcName} | Tỷ lệ: 1/{srcScale} | Nhóm: {srcPrio}]");
                }
                tr.Commit();
            }

            var pso = new PromptSelectionOptions { MessageForAdding = "\n>> Quét chọn các Khung bao đích cần gán thông tin này: " };
            TypedValue[] filters = new TypedValue[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") };
            var selRes = ed.GetSelection(pso, new SelectionFilter(filters));
            if (selRes.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayer(db, tr, "Khungbao_NoPrint", 6, isPlottable: false);
                EnsureRegApp(db, tr, "LAYOUT_TOOL_DATA");

                int count = 0;
                foreach (SelectedObject so in selRes.Value)
                {
                    if (so == null || so.ObjectId == resSrc.ObjectId) continue;
                    Polyline destPl = tr.GetObject(so.ObjectId, OpenMode.ForWrite) as Polyline;
                    if (destPl == null) continue;

                    destPl.Layer = "Khungbao_NoPrint";
                    destPl.ColorIndex = 6;
                    AttachXData(destPl, srcName, srcScale, srcPrio);
                    count++;
                }

                tr.Commit();
                ed.WriteMessage($"\n>> HOÀN TẤT: Đã gán thông tin sao chép cho {count} khung bao!");
            }
        }

        // ======================================================================
        // HÀM HÌNH HỌC, XDATA & NHẬN DIỆN TÊN (HỖ TRỢ CẢ TEXT/MTEXT)
        // ======================================================================
        public static (string, double, string) GetXData(Polyline pl)
        {
            string name = "VIEW";
            double scale = 50.0;
            string prio = "A";

            ResultBuffer rb = pl.GetXDataForApplication("LAYOUT_TOOL_DATA");
            if (rb != null)
            {
                var arr = rb.AsArray();
                var strList = arr.Where(t => t.TypeCode == (short)DxfCode.ExtendedDataAsciiString).Select(t => (string)t.Value).ToList();
                var realList = arr.Where(t => t.TypeCode == (short)DxfCode.ExtendedDataReal).Select(t => (double)t.Value).ToList();

                if (strList.Count > 0) name = strList[0];
                if (realList.Count > 0) scale = realList[0];
                if (strList.Count > 1) prio = strList[1];
            }
            return (name, scale, prio);
        }

        public static void AttachXData(Polyline pl, string vName, double vScale, string vPrio)
        {
            pl.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, "LAYOUT_TOOL_DATA"),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, vName),
                new TypedValue((int)DxfCode.ExtendedDataReal, vScale),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, vPrio)
            );
        }

        private static void CreateBoundingPolyline(Database db, Transaction tr, Extents3d ext, string vName, double vScale, string vPrio, double offset)
        {
            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            Point2d p1 = new Point2d(ext.MinPoint.X - offset, ext.MinPoint.Y - offset);
            Point2d p2 = new Point2d(ext.MaxPoint.X + offset, ext.MinPoint.Y - offset);
            Point2d p3 = new Point2d(ext.MaxPoint.X + offset, ext.MaxPoint.Y + offset);
            Point2d p4 = new Point2d(ext.MinPoint.X - offset, ext.MaxPoint.Y + offset);

            Polyline pl = new Polyline();
            pl.AddVertexAt(0, p1, 0, 0, 0);
            pl.AddVertexAt(1, p2, 0, 0, 0);
            pl.AddVertexAt(2, p3, 0, 0, 0);
            pl.AddVertexAt(3, p4, 0, 0, 0);
            pl.Closed = true;
            pl.Layer = "Khungbao_NoPrint";
            pl.ColorIndex = 6;

            AttachXData(pl, vName, vScale, vPrio);

            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
        }

        private static string FindNameInSelection(Transaction tr, SelectionSet ss, string sampleBlk, string sampleTag, bool isBeam)
        {
            string pattern = isBeam
                ? @"(?i)\b(?:DẦM\s+|DAM\s+)?([0-9]*[D][A-Z0-9]*(?:[-_xX/][0-9]+)*)\b"
                : @"(?i)\b(?:CỘT\s+|COT\s+)?([0-9]*[C][A-Z0-9]*(?:[-_xX/][0-9]+)*|[Kk][Cc][0-9]+(?:[-_xX/][0-9]+)*)\b";

            bool isTextLayerMode = (sampleBlk ?? "").StartsWith("TEXT:", StringComparison.OrdinalIgnoreCase);
            string targetTextLayer = isTextLayerMode ? sampleBlk.Substring(5) : "";

            foreach (SelectedObject so in ss)
            {
                if (so == null) continue;

                // Chế độ nhận diện Text thường hoặc MText
                if (isTextLayerMode)
                {
                    string rawText = null;
                    if (tr.GetObject(so.ObjectId, OpenMode.ForRead) is DBText dbTxt)
                    {
                        if (string.IsNullOrEmpty(targetTextLayer) || dbTxt.Layer.Equals(targetTextLayer, StringComparison.OrdinalIgnoreCase))
                            rawText = dbTxt.TextString;
                    }
                    else if (tr.GetObject(so.ObjectId, OpenMode.ForRead) is MText mTxt)
                    {
                        if (string.IsNullOrEmpty(targetTextLayer) || mTxt.Layer.Equals(targetTextLayer, StringComparison.OrdinalIgnoreCase))
                            rawText = mTxt.Text;
                    }

                    if (!string.IsNullOrEmpty(rawText))
                    {
                        string clean = CleanText(rawText);
                        if (!Regex.IsMatch(clean, @"^\s*([0-9]+|[A-Z])\s*[-_]\s*([0-9]+|[A-Z])\s*$", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(clean, pattern);
                            if (match.Success) return match.Groups[1].Value.ToUpper();
                        }
                    }
                    continue;
                }

                // Chế độ nhận diện Block thuộc tính
                BlockReference blk = tr.GetObject(so.ObjectId, OpenMode.ForRead) as BlockReference;
                if (blk == null) continue;

                string effectiveName = blk.Name;
                if (blk.IsDynamicBlock)
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blk.DynamicBlockTableRecord, OpenMode.ForRead);
                    effectiveName = btr.Name;
                }

                if (sampleBlk == "*" || effectiveName.Equals(sampleBlk, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (ObjectId attId in blk.AttributeCollection)
                    {
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att == null) continue;

                        if (sampleTag == "*" || att.Tag.Equals(sampleTag, StringComparison.OrdinalIgnoreCase))
                        {
                            string val = CleanText(att.TextString);
                            if (Regex.IsMatch(val, @"^\s*([0-9]+|[A-Z])\s*[-_]\s*([0-9]+|[A-Z])\s*$", RegexOptions.IgnoreCase))
                                continue;

                            var match = Regex.Match(val, pattern);
                            if (match.Success)
                            {
                                return match.Groups[1].Value.ToUpper();
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static double GetDimStyleScale(Transaction tr, SelectionSet ss)
        {
            foreach (SelectedObject so in ss)
            {
                if (so == null) continue;
                Dimension dim = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Dimension;
                if (dim != null && dim.Dimscale > 0) return Math.Round(dim.Dimscale);
            }
            return 50.0;
        }

        private static Extents3d GetSelectionBoundingBox(Transaction tr, SelectionSet ss)
        {
            Extents3d totalExt = new Extents3d();
            bool hasFirst = false;

            foreach (SelectedObject so in ss)
            {
                if (so == null) continue;
                Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                if (ent == null || ent.Layer.Equals("Khungbao_NoPrint", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    if (!hasFirst)
                    {
                        totalExt = ent.GeometricExtents;
                        hasFirst = true;
                    }
                    else
                    {
                        totalExt.AddExtents(ent.GeometricExtents);
                    }
                }
                catch { }
            }
            return totalExt;
        }

        private static string CleanText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string[] controlCodes = { "%%u", "%%U", "%%o", "%%O", "%%c", "%%C", "%%d", "%%D", "%%p", "%%P", "\\P" };
            foreach (var code in controlCodes) raw = raw.Replace(code, "");
            return raw.Trim();
        }

        private static void EnsureLayer(Database db, Transaction tr, string layerName, short colorIndex, bool isPlottable)
        {
            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerName))
            {
                lt.UpgradeOpen();
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex),
                    IsPlottable = isPlottable
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }

        private static void EnsureRegApp(Database db, Transaction tr, string appName)
        {
            RegAppTable rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (!rat.Has(appName))
            {
                rat.UpgradeOpen();
                RegAppTableRecord ratr = new RegAppTableRecord { Name = appName };
                rat.Add(ratr);
                tr.AddNewlyCreatedDBObject(ratr, true);
            }
        }
    }
}