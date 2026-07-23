using SharpGLTF.Runtime;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HamsterMall
{
    public partial class HamsterMall : Form
    {
        private string loadedMeshWorldPath = "";
        private string loadedTexturePath = "";

        public HamsterMall()
        {
            InitializeComponent();
        }

        // ═══════════════════════════════════════════════════════════
        //  Create MESHWORLD tab
        // ═══════════════════════════════════════════════════════════


        private void Ambient_Click(object sender, EventArgs e)
        {
            using (Cyotek.Windows.Forms.ColorPickerDialog dialog = new Cyotek.Windows.Forms.ColorPickerDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Ambient.BackColor = dialog.Color;
                }
            }
        }

        private void Background_Click(object sender, EventArgs e)
        {
            using (Cyotek.Windows.Forms.ColorPickerDialog dialog = new Cyotek.Windows.Forms.ColorPickerDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Background.BackColor = dialog.Color;
                }
            }
        }


        private void loadButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                meshFileText.Text = openFileDialog1.FileName;
            }

        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.FileName != null && saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                MeshWorldExporter.ExportFromGLTF(
                    openFileDialog1.FileName,
                    saveFileDialog1.FileName,
                    Ambient.BackColor,
                    Background.BackColor
                );

                MessageBox.Show("Successfully exported to MESHWORLD!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Extract MESHWORLD tab
        // ═══════════════════════════════════════════════════════════

        private void meshWorldLoad_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "MeshWorld Files (*.MESHWORLD)|*.MESHWORLD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    loadedMeshWorldPath = openFileDialog.FileName;
                    meshworld_label.Text = loadedMeshWorldPath;
                }
            }
        }

        private void textureFolder_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog folderDialog = new OpenFileDialog())
            {
                folderDialog.Title = "Select the Hamsterball Textures folder";
                folderDialog.CheckFileExists = false;
                folderDialog.FileName = "Folder Selection.";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    loadedTexturePath = Path.GetDirectoryName(folderDialog.FileName);
                    textures_label.Text = loadedTexturePath;
                }
            }
        }

        private void exportGLTF_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(loadedMeshWorldPath))
            {
                MessageBox.Show("Please load a MESHWORLD file first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool keepFolders = chkUseHierarchy.Checked;
            bool thorough = chkThorough.Checked;

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "glTF Binary (*.glb)|*.glb";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    MeshWorldExtractor.ExtractToGLTF(loadedMeshWorldPath, saveFileDialog.FileName, loadedTexturePath, keepFolders, thorough);
                    MessageBox.Show("Extracted to glTF successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}
