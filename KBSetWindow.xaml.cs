using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace StructuralLayoutEngine
{
    public partial class KBSetWindow : Window
    {
        public bool IsPickFrameRequested { get; private set; }
        public bool IsPickBeamRequested { get; private set; }
        public bool IsPickColRequested { get; private set; }
        public LayoutConfig Config { get; private set; }

        public KBSetWindow(string frameName, string recSize, List<string> frameTags, List<string> beamTags, List<string> colTags, LayoutConfig config)
        {
            InitializeComponent();
            Config = config;

            // 1. Tiêu đề Dầm
            txtBeamBlockName.Text = config.BeamBlockName;
            cmbBeamTag.ItemsSource = beamTags;
            cmbBeamTag.SelectedItem = beamTags.Contains(config.BeamTagName) ? config.BeamTagName : beamTags.FirstOrDefault();
            bool isBeamTextMode = (config.BeamBlockName ?? "").StartsWith("TEXT:", StringComparison.OrdinalIgnoreCase) || beamTags.Count == 0;
            cmbBeamTag.IsEnabled = !isBeamTextMode;
            if (isBeamTextMode) cmbBeamTag.SelectedItem = null;

            // 2. Tiêu đề Cột
            txtColBlockName.Text = config.ColumnBlockName;
            cmbColTag.ItemsSource = colTags;
            cmbColTag.SelectedItem = colTags.Contains(config.ColumnTagName) ? config.ColumnTagName : colTags.FirstOrDefault();
            bool isColTextMode = (config.ColumnBlockName ?? "").StartsWith("TEXT:", StringComparison.OrdinalIgnoreCase) || colTags.Count == 0;
            cmbColTag.IsEnabled = !isColTextMode;
            if (isColTextMode) cmbColTag.SelectedItem = null;

            // 3. Khung tên & Rec
            txtFrameName.Text = frameName;
            txtRecSize.Text = recSize;

            cmbTagLine1.ItemsSource = frameTags;
            cmbTagLine1.SelectedItem = frameTags.Contains(config.TagLine1) ? config.TagLine1 : frameTags.FirstOrDefault();

            cmbTagLine2.ItemsSource = frameTags;
            cmbTagLine2.SelectedItem = frameTags.Contains(config.TagLine2) ? config.TagLine2 : frameTags.FirstOrDefault();

            cmbTagSheetNo.ItemsSource = frameTags;
            cmbTagSheetNo.SelectedItem = frameTags.Contains(config.TagSheetNo) ? config.TagSheetNo : frameTags.FirstOrDefault();

            cmbFontMode.SelectedIndex = config.FontMode;
        }

        private void BtnPickBeam_Click(object sender, RoutedEventArgs e)
        {
            IsPickBeamRequested = true;
            this.Close();
        }

        private void BtnPickCol_Click(object sender, RoutedEventArgs e)
        {
            IsPickColRequested = true;
            this.Close();
        }

        private void BtnPickFrame_Click(object sender, RoutedEventArgs e)
        {
            IsPickFrameRequested = true;
            this.Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Config.BeamBlockName = txtBeamBlockName.Text;
            Config.BeamTagName = cmbBeamTag.IsEnabled ? (cmbBeamTag.SelectedItem?.ToString() ?? "*") : "*";

            Config.ColumnBlockName = txtColBlockName.Text;
            Config.ColumnTagName = cmbColTag.IsEnabled ? (cmbColTag.SelectedItem?.ToString() ?? "*") : "*";

            Config.TagLine1 = cmbTagLine1.SelectedItem?.ToString() ?? "TENBANVEDONG1";
            Config.TagLine2 = cmbTagLine2.SelectedItem?.ToString() ?? "TENBANVEDONG2";
            Config.TagSheetNo = cmbTagSheetNo.SelectedItem?.ToString() ?? "KHBV";
            Config.FontMode = cmbFontMode.SelectedIndex;

            Config.Save();
            this.Close();
        }
    }
}