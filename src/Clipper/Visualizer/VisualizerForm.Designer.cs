namespace Visualizer
{
    partial class VisualizerForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VisualizerForm));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.ViewFilterControl = new Visualizer.ViewFilterControl();
            this.ClipperViewControl = new Visualizer.ClipperViewControl();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.ViewFilterControl);
            this.splitContainer1.Panel1MinSize = 53;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.ClipperViewControl);
            this.splitContainer1.Size = new System.Drawing.Size(1331, 438);
            this.splitContainer1.SplitterDistance = 56;
            this.splitContainer1.TabIndex = 0;
            // 
            // ViewFilterControl
            // 
            this.ViewFilterControl.BackColor = System.Drawing.Color.Black;
            this.ViewFilterControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ViewFilterControl.Font = new System.Drawing.Font("Consolas", 9.75F);
            this.ViewFilterControl.Location = new System.Drawing.Point(0, 0);
            this.ViewFilterControl.Name = "ViewFilterControl";
            this.ViewFilterControl.Size = new System.Drawing.Size(1331, 56);
            this.ViewFilterControl.TabIndex = 0;
            // 
            // ClipperViewControl
            // 
            this.ClipperViewControl.BackColor = System.Drawing.Color.Black;
            this.ClipperViewControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ClipperViewControl.Font = new System.Drawing.Font("Consolas", 9.75F);
            this.ClipperViewControl.Location = new System.Drawing.Point(0, 0);
            this.ClipperViewControl.Name = "ClipperViewControl";
            this.ClipperViewControl.Size = new System.Drawing.Size(1331, 378);
            this.ClipperViewControl.TabIndex = 0;
            // 
            // VisualizerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(1331, 438);
            this.Controls.Add(this.splitContainer1);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "VisualizerForm";
            this.Text = "Visualizer";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private ViewFilterControl ViewFilterControl;
        internal ClipperViewControl ClipperViewControl;
    }
}

