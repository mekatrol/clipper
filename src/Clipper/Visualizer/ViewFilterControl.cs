using System.Windows.Forms;

namespace Visualizer
{
    public partial class ViewFilterControl : UserControl
    {
        public ViewFilterControl()
        {
            InitializeComponent();
        }

        private void ViewSubjectsCheckBox_CheckedChanged(object sender, System.EventArgs e)
        {
            Program.VisualizerForm.ClipperViewControl.ViewSubjects = viewSubjectsCheckBox.Checked;
        }

        private void ViewClipsCheckBox_CheckedChanged(object sender, System.EventArgs e)
        {
            Program.VisualizerForm.ClipperViewControl.ViewClips = viewClipsCheckBox.Checked;
        }

        private void ViewBoundaryCheckBox_CheckedChanged(object sender, System.EventArgs e)
        {
            Program.VisualizerForm.ClipperViewControl.ViewBoundaries = viewBoundaryCheckBox.Checked;
        }

        private void ViewFillCheckBox_CheckedChanged(object sender, System.EventArgs e)
        {
            Program.VisualizerForm.ClipperViewControl.ViewFill = viewFillCheckBox.Checked;
        }
    }
}
