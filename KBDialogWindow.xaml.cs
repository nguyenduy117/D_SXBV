using System.Collections.Generic;
using System.Windows;

namespace StructuralLayoutEngine
{
    public partial class KBDialogWindow : Window
    {
        public string ResultName { get; private set; } = "DX1";
        public double ResultScale { get; private set; } = 50.0;
        public string ResultPrio { get; private set; } = "A";
        public bool IsSaved { get; private set; } = false;

        public KBDialogWindow(string curName, double curScale, string curPrio)
        {
            InitializeComponent();
            txtKbName.Text = curName;
            txtKbScale.Text = curScale.ToString();

            List<string> listPrio = new List<string> { "A", "B", "C", "D", "E", "F" };
            cboPrio.ItemsSource = listPrio;
            cboPrio.SelectedItem = listPrio.Contains(curPrio) ? curPrio : "A";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ResultName = string.IsNullOrWhiteSpace(txtKbName.Text) ? "VIEW" : txtKbName.Text.Trim();
            if (double.TryParse(txtKbScale.Text, out double sc)) ResultScale = sc;
            ResultPrio = cboPrio.SelectedItem?.ToString() ?? "A";
            IsSaved = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsSaved = false;
            this.Close();
        }
    }
}