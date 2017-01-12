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
            this.viewBoundaryCheckBox = new System.Windows.Forms.CheckBox();
            this.viewFillCheckBox = new System.Windows.Forms.CheckBox();
            this.solutionComboBox = new System.Windows.Forms.ComboBox();
            this.testListComboBox = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // viewSubjectsCheckBox
            // 
            this.viewSubjectsCheckBox.AutoSize = true;
            this.viewSubjectsCheckBox.Checked = true;
            this.viewSubjectsCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.viewSubjectsCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.viewSubjectsCheckBox.Location = new System.Drawing.Point(139, 10);
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
            this.viewClipsCheckBox.Location = new System.Drawing.Point(250, 10);
            this.viewClipsCheckBox.Name = "viewClipsCheckBox";
            this.viewClipsCheckBox.Size = new System.Drawing.Size(61, 19);
            this.viewClipsCheckBox.TabIndex = 1;
            this.viewClipsCheckBox.Text = "Clips";
            this.viewClipsCheckBox.UseVisualStyleBackColor = true;
            this.viewClipsCheckBox.CheckedChanged += new System.EventHandler(this.ViewClipsCheckBox_CheckedChanged);
            // 
            // viewBoundaryCheckBox
            // 
            this.viewBoundaryCheckBox.AutoSize = true;
            this.viewBoundaryCheckBox.Checked = true;
            this.viewBoundaryCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.viewBoundaryCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.viewBoundaryCheckBox.Location = new System.Drawing.Point(514, 10);
            this.viewBoundaryCheckBox.Name = "viewBoundaryCheckBox";
            this.viewBoundaryCheckBox.Size = new System.Drawing.Size(82, 19);
            this.viewBoundaryCheckBox.TabIndex = 2;
            this.viewBoundaryCheckBox.Text = "Boundary";
            this.viewBoundaryCheckBox.UseVisualStyleBackColor = true;
            this.viewBoundaryCheckBox.CheckedChanged += new System.EventHandler(this.ViewBoundaryCheckBox_CheckedChanged);
            // 
            // viewFillCheckBox
            // 
            this.viewFillCheckBox.AutoSize = true;
            this.viewFillCheckBox.Checked = true;
            this.viewFillCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.viewFillCheckBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.viewFillCheckBox.Location = new System.Drawing.Point(625, 10);
            this.viewFillCheckBox.Name = "viewFillCheckBox";
            this.viewFillCheckBox.Size = new System.Drawing.Size(54, 19);
            this.viewFillCheckBox.TabIndex = 3;
            this.viewFillCheckBox.Text = "Fill";
            this.viewFillCheckBox.UseVisualStyleBackColor = true;
            this.viewFillCheckBox.CheckedChanged += new System.EventHandler(this.ViewFillCheckBox_CheckedChanged);
            // 
            // solutionComboBox
            // 
            this.solutionComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.solutionComboBox.FormattingEnabled = true;
            this.solutionComboBox.Items.AddRange(new object[] {
            "Test",
            "New Clipper",
            "Original Clipper"});
            this.solutionComboBox.Location = new System.Drawing.Point(340, 8);
            this.solutionComboBox.Name = "solutionComboBox";
            this.solutionComboBox.Size = new System.Drawing.Size(145, 23);
            this.solutionComboBox.TabIndex = 4;
            this.solutionComboBox.SelectedIndexChanged += new System.EventHandler(this.SolutionComboBox_SelectedIndexChanged);
            // 
            // testListComboBox
            // 
            this.testListComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.testListComboBox.FormattingEnabled = true;
            this.testListComboBox.Items.AddRange(new object[] {
            "Test",
            "New Clipper",
            "Original Clipper"});
            this.testListComboBox.Location = new System.Drawing.Point(3, 8);
            this.testListComboBox.Name = "testListComboBox";
            this.testListComboBox.Size = new System.Drawing.Size(107, 23);
            this.testListComboBox.TabIndex = 5;
            this.testListComboBox.SelectedIndexChanged += new System.EventHandler(this.TestListComboBox_SelectedIndexChanged);
            // 
            // ViewFilterControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.Controls.Add(this.testListComboBox);
            this.Controls.Add(this.solutionComboBox);
            this.Controls.Add(this.viewFillCheckBox);
            this.Controls.Add(this.viewBoundaryCheckBox);
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
        private System.Windows.Forms.CheckBox viewBoundaryCheckBox;
        private System.Windows.Forms.CheckBox viewFillCheckBox;
        private System.Windows.Forms.ComboBox solutionComboBox;
        private System.Windows.Forms.ComboBox testListComboBox;
    }
}
