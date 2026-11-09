using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace StructuralLayoutEngine
{
	public static class EncodingConverter
	{
		public static int DetectEncoding(TextStyleTableRecord style, string sampleText)
		{
			if (style == null) return 1;

			string fontName = (style.FileName + " " + style.Font.TypeFace).ToLower();

			if (fontName.Contains(".vn") || fontName.Contains("vntime") || fontName.Contains("vnarial") || fontName.Contains("tc_") || fontName.Contains("vns.shx"))
				return 2; // TCVN3

			if (fontName.Contains("vni") || fontName.Contains("v_"))
				return 3; // VNI-Windows

			if (!string.IsNullOrEmpty(sampleText))
			{
				foreach (char c in sampleText)
				{
					if ((c >= 180 && c <= 255) || c == '\u00a1' || c == '\u00a2' || c == '\u00a3' || c == '\u00a7' || c == '\u00b5' || c == '\u00b8' || c == '\u00d5' || c == '\u00c7')
						return 2;
				}
			}
			return 1; // Unicode
		}

		public static string Convert(string text, int targetEncoding)
		{
			if (string.IsNullOrEmpty(text)) return text;

			// 1. Chuyển đổi sang TCVN3
			if (targetEncoding == 2)
			{
				return text.Replace("CHI TIẾT DẦM - CỘT", "CHI TI\u00d5T D\u00c7M - C\u00e9T")
						   .Replace("CHI TIẾT DẦM", "CHI TI\u00d5T D\u00c7M")
						   .Replace("CHI TIẾT CỘT", "CHI TI\u00d5T C\u00e9T")
						   .Replace("CHI TIẾT VÁCH", "CHI TI\u00d5T V\u00b8CH")
						   .Replace("CHI TIẾT SÀN", "CHI TI\u00d5T S\u00b5N")
						   .Replace("CHI TIẾT MÓNG", "CHI TI\u00d5T M\u00e3NG")
						   .Replace("CHI TIẾT KẾT CẤU", "CHI TI\u00d5T K\u00d5T C\u00caU");
			}

			// 2. Chuyển đổi sang VNI-Windows
			if (targetEncoding == 3)
			{
				return text.Replace("CHI TIẾT DẦM - CỘT", "CHI TIEÁT DAÀM - COÄT")
						   .Replace("CHI TIẾT DẦM", "CHI TIEÁT DAÀM")
						   .Replace("CHI TIẾT CỘT", "CHI TIEÁT COÄT")
						   .Replace("CHI TIẾT VÁCH", "CHI TIEÁT VAÙCH")
						   .Replace("CHI TIẾT SÀN", "CHI TIEÁT SAØN")
						   .Replace("CHI TIẾT MÓNG", "CHI TIEÁT MOÙNG")
						   .Replace("CHI TIẾT KẾT CẤU", "CHI TIEÁT KEÁT CAÁU");
			}

			return text; // Unicode
		}
	}
}