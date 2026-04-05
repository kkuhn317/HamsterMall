
namespace HamsterMall
{
    partial class HamsterMall
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
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.loadButton = new System.Windows.Forms.Button();
            this.meshFileText = new System.Windows.Forms.Label();
            this.saveButton = new System.Windows.Forms.Button();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.colorDialog1 = new System.Windows.Forms.ColorDialog();
            this.Ambient = new System.Windows.Forms.PictureBox();
            this.Background = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.button2 = new System.Windows.Forms.Button();
            this.meshworld_label = new System.Windows.Forms.Label();
            this.loadMeshworld = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.Ambient)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Background)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // loadButton
            // 
            this.loadButton.Location = new System.Drawing.Point(6, 6);
            this.loadButton.Name = "loadButton";
            this.loadButton.Size = new System.Drawing.Size(156, 42);
            this.loadButton.TabIndex = 0;
            this.loadButton.Text = "Load Mesh";
            this.loadButton.UseVisualStyleBackColor = true;
            this.loadButton.Click += new System.EventHandler(this.loadButton_Click);
            // 
            // meshFileText
            // 
            this.meshFileText.AutoSize = true;
            this.meshFileText.Location = new System.Drawing.Point(6, 51);
            this.meshFileText.Name = "meshFileText";
            this.meshFileText.Size = new System.Drawing.Size(0, 13);
            this.meshFileText.TabIndex = 1;
            // 
            // saveButton
            // 
            this.saveButton.Location = new System.Drawing.Point(8, 205);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(156, 48);
            this.saveButton.TabIndex = 2;
            this.saveButton.Text = "Export MeshWorld";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            // 
            // saveFileDialog1
            // 
            this.saveFileDialog1.DefaultExt = "MESHWORLD";
            // 
            // Ambient
            // 
            this.Ambient.BackColor = System.Drawing.Color.White;
            this.Ambient.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Ambient.Location = new System.Drawing.Point(127, 88);
            this.Ambient.Name = "Ambient";
            this.Ambient.Size = new System.Drawing.Size(32, 32);
            this.Ambient.TabIndex = 3;
            this.Ambient.TabStop = false;
            this.Ambient.Click += new System.EventHandler(this.Ambient_Click);
            // 
            // Background
            // 
            this.Background.BackColor = System.Drawing.Color.Blue;
            this.Background.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Background.Location = new System.Drawing.Point(127, 126);
            this.Background.Name = "Background";
            this.Background.Size = new System.Drawing.Size(32, 32);
            this.Background.TabIndex = 4;
            this.Background.TabStop = false;
            this.Background.Click += new System.EventHandler(this.Background_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(26, 97);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(75, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "Ambient Color:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 136);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(95, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Background Color:";
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(12, 12);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(318, 285);
            this.tabControl1.TabIndex = 7;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.loadButton);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.meshFileText);
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Controls.Add(this.saveButton);
            this.tabPage1.Controls.Add(this.Background);
            this.tabPage1.Controls.Add(this.Ambient);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(310, 259);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Create MESHWORLD";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.button2);
            this.tabPage2.Controls.Add(this.meshworld_label);
            this.tabPage2.Controls.Add(this.loadMeshworld);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(310, 259);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Extract MESHWORLD";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(9, 199);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(143, 54);
            this.button2.TabIndex = 2;
            this.button2.Text = "Export to glTF";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.exportGLTF_Click);
            // 
            // meshworld_label
            // 
            this.meshworld_label.AutoSize = true;
            this.meshworld_label.Location = new System.Drawing.Point(6, 111);
            this.meshworld_label.Name = "meshworld_label";
            this.meshworld_label.Size = new System.Drawing.Size(0, 13);
            this.meshworld_label.TabIndex = 1;
            // 
            // loadMeshworld
            // 
            this.loadMeshworld.Location = new System.Drawing.Point(6, 3);
            this.loadMeshworld.Name = "loadMeshworld";
            this.loadMeshworld.Size = new System.Drawing.Size(143, 55);
            this.loadMeshworld.TabIndex = 0;
            this.loadMeshworld.Text = "Load MESHWORLD";
            this.loadMeshworld.UseVisualStyleBackColor = true;
            this.loadMeshworld.Click += new System.EventHandler(this.meshWorldLoad_Click);
            // 
            // HamsterMall
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(345, 315);
            this.Controls.Add(this.tabControl1);
            this.Name = "HamsterMall";
            this.Text = "HamsterMall";
            ((System.ComponentModel.ISupportInitialize)(this.Ambient)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Background)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Button loadButton;
        private System.Windows.Forms.Label meshFileText;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.ColorDialog colorDialog1;
        private System.Windows.Forms.PictureBox Ambient;
        private System.Windows.Forms.PictureBox Background;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Label meshworld_label;
        private System.Windows.Forms.Button loadMeshworld;
    }
}

