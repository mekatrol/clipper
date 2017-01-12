namespace Visualizer
{
    partial class ViewFilterControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.viewSubjectsCheckBox = new System.Windows.Forms.CheckBox();
            this.viewClipsCheckBox = new System.Windows.Forms.CheckBox();
            this.viewTestBoundaryCheckBox = new System.Windows.Forms.CheckBox();
            this.viewTestFillCheckBox = new System.Windows.Forms.CheckBox();
            this.viewClipperFillCheckBox = new System.Windows.Forms.CheckBox();
            this.viewClipperBoundaryCheckBox = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // viewSubjectsCheckBox
            // 
            this.viewSubjectsCheckBox.AutoSize = true;
            this.viewSubjectsCheckBox.Checked = true;
            this.viewSubjectsCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.viewSubjectsCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.viewSubjectsCheckBox.Location = new System.Drawing.Point(9, 12);
            this.viewSubjectsCheckBox.Name = "viewSubjectsCheckBox";
            this.viewSubjectsCheckBox.Size = new System.Drawing.Size(82, 19);
            this.viewSubjectsCheckBox.TabIndex = 0;
            this.viewSubjectsCheckBox.Text = "Subjects";
            this.viewSubjectsCheckBox.UseVisualStyleBackColor = true;
            this.viewSubjectsCheckBox.CheckedChanged += new System.EventHandler(this.ViewSubjectsCheckBox_CheckedChanged);
            // 
            // viewClipsCheckBox
            // 
            this.viewClipsCheckBox.AutoSize = true;
            this.viewClipsCheckBox.Checked = true;
            this.viewClipsCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.viewClipsCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.viewClipsCheckBox.Location = new System.Drawing.Point(113, 12);
            this.viewClipsCheckBox.Name = "viewClipsCheckBox";
            this.viewClipsCheckBox.Size = new System.Drawing.Size(61, 19);
            this.viewClipsCheckBox.TabIndex = 1;
            this.viewClipsCheckBox.Text = "Clips";
            this.viewClipsCheckBox.UseVisualStyleBackColor = true;
            this.viewClipsCheckBox.CheckedChanged += new System.EventHandler(this.ViewClipsCheckBox_CheckedChanged);
            // 
            // viewTestBoundaryCheckBox
            // 
            this.viewTestBoundaryCheckBox.AutoSize = true;
            this.viewTestBoundaryCheckBox.Checked = true;
            this.viewTestBoundaryCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.viewTestBoundaryCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.viewTestBoundaryCheckBox.Location = new System.Drawing.Point(281, 12);
            this.viewTestBoundaryCheckBox.Name = "viewTestBoundaryCheckBox";
            this.viewTestBoundaryCheckBox.Size = new System.Drawing.Size(82, 19);
            this.viewTestBoundaryCheckBox.TabIndex = 2;
            this.viewTestBoundaryCheckBox.Text = "Boundary";
            this.viewTestBoundaryCheckBox.UseVisualStyleBackColor = true;
            this.viewTestBoundaryCheckBox.CheckedChanged += new System.EventHandler(this.ViewTestBoundaryCheckBox_CheckedChanged);
            // 
            // viewTestFillCheckBox
            // 
            this.viewTestFillCheckBox.AutoSize = true;
            this.viewTestFillCheckBox.Checked = true;
            this.viewTestFillCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.viewTestFillCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.viewTestFillCheckBox.Location = new System.Drawing.Point(389, 12);
            this.viewTestFillCheckBox.Name = "viewTestFillCheckBox";
            this.viewTestFillCheckBox.Size = new System.Drawing.Size(54, 19);
            this.viewTestFillCheckBox.TabIndex = 3;
            this.viewTestFillCheckBox.Text = "Fill";
            this.viewTestFillCheckBox.UseVisualStyleBackColor = true;
            this.viewTestFillCheckBox.CheckedChanged += new System.EventHandler(this.ViewTestFillCheckBox_CheckedChanged);
            // 
            // viewClipperFillCheckBox
            // 
            this.viewClipperFillCheckBox.AutoSize = true;
            this.viewClipperFillCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.viewClipperFillCheckBox.Location = new System.Drawing.Point(677, 12);
            this.viewClipperFillCheckBox.Name = "viewClipperFillCheckBox";
            this.viewClipperFillCheckBox.Size = new System.Drawing.Size(54, 19);
            this.viewClipperFillCheckBox.TabIndex = 5;
            this.viewClipperFillCheckBox.Text = "Fill";
            this.viewClipperFillCheckBox.UseVisualStyleBackColor = true;
            this.viewClipperFillCheckBox.CheckedChanged += new System.EventHandler(this.ViewClipperFillCheckBox_CheckedChanged);
            // 
            // viewClipperBoundaryCheckBox
            // 
            this.viewClipperBoundaryCheckBox.AutoSize = true;
            this.viewClipperBoundaryCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.viewClipperBoundaryCheckBox.Location = new System.Drawing.Point(569, 12);
            this.viewClipperBoundaryCheckBox.Name = "viewClipperBoundaryCheckBox";
            this.viewClipperBoundaryCheckBox.Size = new System.Drawing.Size(82, 19);
            this.viewClipperBoundaryCheckBox.TabIndex = 4;
            this.viewClipperBoundaryCheckBox.Text = "Boundary";
            this.viewClipperBoundaryCheckBox.UseVisualStyleBackColor = true;
            this.viewClipperBoundaryCheckBox.CheckedChanged += new System.EventHandler(this.ViewClipperBoundaryCheckBox_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(128)))), ((int)(((byte)(255)))));
            this.label1.Location = new System.Drawing.Point(463, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(98, 15);
            this.label1.TabIndex = 6;
            this.label1.Text = "Clipper Data:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(128)))), ((int)(((byte)(255)))));
            this.label2.Location = new System.Drawing.Point(198, 14);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(77, 15);
            this.label2.TabIndex = 7;
            this.label2.Text = "Test Data:";
            // 
            // ViewFilterControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.viewClipperFillCheckBox);
            this.Controls.Add(this.viewClipperBoundaryCheckBox);
            this.Controls.Add(this.viewTestFillCheckBox);
            this.Controls.Add(this.viewTestBoundaryCheckBox);
            this.Controls.Add(this.viewClipsCheckBox);
            this.Controls.Add(this.viewSubjectsCheckBox);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Consolas", 9.75F);
            this.Name = "ViewFilterControl";
            this.Size = new System.Drawing.Size(1320, 40);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox viewSubjectsCheckBox;
        private System.Windows.Forms.CheckBox viewClipsCheckBox;
        private System.Windows.Forms.CheckBox viewTestBoundaryCheckBox;
        private System.Windows.Forms.CheckBox viewTestFillCheckBox;
        private System.Windows.Forms.CheckBox viewClipperFillCheckBox;
        private System.Windows.Forms.CheckBox viewClipperBoundaryCheckBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}
