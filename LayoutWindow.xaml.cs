using System;
using System.Windows;

namespace StructuralLayoutEngine
{
	public partial class LayoutWindow : Window
	{
		public bool IsPickRequested { get; private set; }
		public bool IsRunRequested { get; private set; }
		public LayoutConfig Config { get; private set; }

		public LayoutWindow(string frameName, string frameInfo, LayoutConfig config)
		{
			InitializeComponent();
			Config = config;

			// 1. Khoảng cách & Căn giữa
			txtDx.Text = config.Dx.ToString();
			txtDy.Text = config.Dy.ToString();
			txtOverlap.Text = config.Overlap.ToString();
			txtDFrame.Text = config.DFrame.ToString();
			txtTargetScale.Text = config.TargetScale.ToString();
			chkCenterX.IsChecked = config.CenterX;
			chkCenterY.IsChecked = config.CenterY;

			// 2. Thuật toán thông minh
			chkOptimized.IsChecked = config.IsOptimized;
			chkGroupCluster.IsChecked = config.GroupCluster;
			chkAutoFillTitle.IsChecked = config.AutoFillTitle;

			// 3. Đánh số hiệu bản vẽ
			chkAutoNumbering.IsChecked = config.AutoNumbering;
			txtPrefix.Text = config.SheetPrefix;
			txtStartIndex.Text = config.SheetStartIndex;
			txtSuffix.Text = config.SheetSuffix;

			// 4. Khung tên
			txtFrameName.Text = $"Tên khung: {frameName}";
			txtFrameInfo.Text = $"Trạng thái: {frameInfo}";
		}

		private void BtnPick_Click(object sender, RoutedEventArgs e)
		{
			SaveInputs();
			IsPickRequested = true;
			this.Close();
		}

		private void BtnRun_Click(object sender, RoutedEventArgs e)
		{
			SaveInputs();
			IsRunRequested = true;
			this.Close();
		}

		private void SaveInputs()
		{
			if (double.TryParse(txtDx.Text, out double dx)) Config.Dx = dx;
			if (double.TryParse(txtDy.Text, out double dy)) Config.Dy = dy;
			if (double.TryParse(txtOverlap.Text, out double ol)) Config.Overlap = ol;
			if (double.TryParse(txtDFrame.Text, out double df)) Config.DFrame = df;
			if (double.TryParse(txtTargetScale.Text, out double sc)) Config.TargetScale = sc;

			Config.CenterX = chkCenterX.IsChecked ?? false;
			Config.CenterY = chkCenterY.IsChecked ?? false;

			Config.IsOptimized = chkOptimized.IsChecked ?? true;
			Config.GroupCluster = chkGroupCluster.IsChecked ?? true;
			Config.AutoFillTitle = chkAutoFillTitle.IsChecked ?? true;

			Config.AutoNumbering = chkAutoNumbering.IsChecked ?? true;
			Config.SheetPrefix = txtPrefix.Text;
			Config.SheetStartIndex = txtStartIndex.Text;
			Config.SheetSuffix = txtSuffix.Text;

			Config.Save();
		}
	}
}