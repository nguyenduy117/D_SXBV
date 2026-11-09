using System;
using System.IO;

namespace StructuralLayoutEngine
{
	public class LayoutConfig
	{
		// 1. Cấu hình Tiêu đề Dầm (KBD)
		public string BeamBlockName { get; set; } = "*";
		public string BeamTagName { get; set; } = "*";

		// 2. Cấu hình Tiêu đề Cột (KBC)
		public string ColumnBlockName { get; set; } = "*";
		public string ColumnTagName { get; set; } = "*";

		// 3. Cấu hình Khung tên & Rec bản vẽ (SXBV)
		public string FrameBlockName { get; set; } = "AMC-khung ten doc";
		public double SampleRecW { get; set; } = 0.0;
		public double SampleRecH { get; set; } = 0.0;
		public double SampleOuterW { get; set; } = 0.0;
		public double DetectedScale { get; set; } = 50.0;
		public double DeltaInsX { get; set; } = 0.0;
		public double DeltaInsY { get; set; } = 0.0;

		// 4. Thông số sắp xếp & Tối ưu hóa
		public double Dx { get; set; } = 200.0;
		public double Dy { get; set; } = 400.0;
		public double Overlap { get; set; } = 1200.0;
		public double DFrame { get; set; } = 4000.0;
		public double TargetScale { get; set; } = 50.0;
		public bool IsOptimized { get; set; } = true;
		public bool GroupCluster { get; set; } = true;
		public bool CenterX { get; set; } = false;
		public bool CenterY { get; set; } = false;

		// 5. Tag điền thuộc tính Khung tên
		public bool AutoFillTitle { get; set; } = true;
		public string TagLine1 { get; set; } = "TENBANVEDONG1";
		public string TagLine2 { get; set; } = "TENBANVEDONG2";
		public string SeparatorChar { get; set; } = ",";
		public string BeamPrefixes { get; set; } = "D, DP, DX, DY, DC, DS, B, BX, BY, BC";
		public string ColumnPrefixes { get; set; } = "C, KC, CC, P, W, SW, V";

		// 6. Đánh số hiệu bản vẽ
		public bool AutoNumbering { get; set; } = true;
		public string TagSheetNo { get; set; } = "KHBV";
		public string SheetPrefix { get; set; } = "KC.D-";
		public string SheetStartIndex { get; set; } = "01";
		public string SheetSuffix { get; set; } = "";

		// 7. Bảng mã Font
		public int FontMode { get; set; } = 0;

		private static string GetConfigPath()
		{
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string dir = Path.Combine(appData, "StructuralLayoutEngine");
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			return Path.Combine(dir, "layout_config.ini");
		}

		public void Save()
		{
			try
			{
				using (var sw = new StreamWriter(GetConfigPath()))
				{
					sw.WriteLine($"BeamBlockName={BeamBlockName}");
					sw.WriteLine($"BeamTagName={BeamTagName}");
					sw.WriteLine($"ColumnBlockName={ColumnBlockName}");
					sw.WriteLine($"ColumnTagName={ColumnTagName}");
					sw.WriteLine($"FrameBlockName={FrameBlockName}");
					sw.WriteLine($"SampleRecW={SampleRecW}");
					sw.WriteLine($"SampleRecH={SampleRecH}");
					sw.WriteLine($"SampleOuterW={SampleOuterW}");
					sw.WriteLine($"DetectedScale={DetectedScale}");
					sw.WriteLine($"DeltaInsX={DeltaInsX}");
					sw.WriteLine($"DeltaInsY={DeltaInsY}");
					sw.WriteLine($"Dx={Dx}");
					sw.WriteLine($"Dy={Dy}");
					sw.WriteLine($"Overlap={Overlap}");
					sw.WriteLine($"DFrame={DFrame}");
					sw.WriteLine($"TargetScale={TargetScale}");
					sw.WriteLine($"IsOptimized={IsOptimized}");
					sw.WriteLine($"GroupCluster={GroupCluster}");
					sw.WriteLine($"CenterX={CenterX}");
					sw.WriteLine($"CenterY={CenterY}");
					sw.WriteLine($"AutoFillTitle={AutoFillTitle}");
					sw.WriteLine($"TagLine1={TagLine1}");
					sw.WriteLine($"TagLine2={TagLine2}");
					sw.WriteLine($"SeparatorChar={SeparatorChar}");
					sw.WriteLine($"BeamPrefixes={BeamPrefixes}");
					sw.WriteLine($"ColumnPrefixes={ColumnPrefixes}");
					sw.WriteLine($"AutoNumbering={AutoNumbering}");
					sw.WriteLine($"TagSheetNo={TagSheetNo}");
					sw.WriteLine($"SheetPrefix={SheetPrefix}");
					sw.WriteLine($"SheetStartIndex={SheetStartIndex}");
					sw.WriteLine($"SheetSuffix={SheetSuffix}");
					sw.WriteLine($"FontMode={FontMode}");
				}
			}
			catch { }
		}

		public static LayoutConfig Load()
		{
			var cfg = new LayoutConfig();
			try
			{
				string path = GetConfigPath();
				if (File.Exists(path))
				{
					foreach (var line in File.ReadAllLines(path))
					{
						var parts = line.Split(new[] { '=' }, 2);
						if (parts.Length != 2) continue;
						string key = parts[0].Trim();
						string val = parts[1].Trim();

						if (key == "BeamBlockName") cfg.BeamBlockName = val;
						else if (key == "BeamTagName") cfg.BeamTagName = val;
						else if (key == "ColumnBlockName") cfg.ColumnBlockName = val;
						else if (key == "ColumnTagName") cfg.ColumnTagName = val;
						else if (key == "FrameBlockName") cfg.FrameBlockName = val;
						else if (key == "SampleRecW" && double.TryParse(val, out double rw)) cfg.SampleRecW = rw;
						else if (key == "SampleRecH" && double.TryParse(val, out double rh)) cfg.SampleRecH = rh;
						else if (key == "SampleOuterW" && double.TryParse(val, out double ow)) cfg.SampleOuterW = ow;
						else if (key == "DetectedScale" && double.TryParse(val, out double ds)) cfg.DetectedScale = ds;
						else if (key == "DeltaInsX" && double.TryParse(val, out double dix)) cfg.DeltaInsX = dix;
						else if (key == "DeltaInsY" && double.TryParse(val, out double diy)) cfg.DeltaInsY = diy;
						else if (key == "Dx" && double.TryParse(val, out double dx)) cfg.Dx = dx;
						else if (key == "Dy" && double.TryParse(val, out double dy)) cfg.Dy = dy;
						else if (key == "Overlap" && double.TryParse(val, out double ol)) cfg.Overlap = ol;
						else if (key == "DFrame" && double.TryParse(val, out double df)) cfg.DFrame = df;
						else if (key == "TargetScale" && double.TryParse(val, out double sc)) cfg.TargetScale = sc;
						else if (key == "IsOptimized" && bool.TryParse(val, out bool opt)) cfg.IsOptimized = opt;
						else if (key == "GroupCluster" && bool.TryParse(val, out bool grp)) cfg.GroupCluster = grp;
						else if (key == "CenterX" && bool.TryParse(val, out bool cx)) cfg.CenterX = cx;
						else if (key == "CenterY" && bool.TryParse(val, out bool cy)) cfg.CenterY = cy;
						else if (key == "AutoFillTitle" && bool.TryParse(val, out bool aft)) cfg.AutoFillTitle = aft;
						else if (key == "TagLine1") cfg.TagLine1 = val;
						else if (key == "TagLine2") cfg.TagLine2 = val;
						else if (key == "SeparatorChar") cfg.SeparatorChar = val;
						else if (key == "BeamPrefixes") cfg.BeamPrefixes = val;
						else if (key == "ColumnPrefixes") cfg.ColumnPrefixes = val;
						else if (key == "AutoNumbering" && bool.TryParse(val, out bool an)) cfg.AutoNumbering = an;
						else if (key == "TagSheetNo") cfg.TagSheetNo = val;
						else if (key == "SheetPrefix") cfg.SheetPrefix = val;
						else if (key == "SheetStartIndex") cfg.SheetStartIndex = val;
						else if (key == "SheetSuffix") cfg.SheetSuffix = val;
						else if (key == "FontMode" && int.TryParse(val, out int fm)) cfg.FontMode = fm;
					}
				}
			}
			catch { }
			return cfg;
		}
	}
}