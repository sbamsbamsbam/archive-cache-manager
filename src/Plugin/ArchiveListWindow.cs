using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArchiveCacheManager
{
    public partial class ArchiveListWindow : Form
    {
        public string SelectedFile;
        public int EmulatorIndex;

        public ArchiveListWindow(string archiveName, string[] fileList, string[] emulatorList, string selection = "")
        {
            InitializeComponent();

            archiveNameLabel.Text = archiveName;

            emulatorComboBox.Items.Clear();
            if (emulatorList.Count() > 0)
            {
                emulatorComboBox.Items.AddRange(emulatorList);
                emulatorComboBox.SelectedIndex = 0;
                EmulatorIndex = emulatorComboBox.SelectedIndex;
                emulatorComboBox.Enabled = true;
            }
            else
            {
                emulatorComboBox.Enabled = false;
            }

            fileListGridView.Rows.Clear();
            for (int i = 0; i < fileList.Length; i++)
            {
                fileListGridView.Rows.Add(new object[] { fileList[i] });
				if (!string.IsNullOrEmpty(selection))
				{
					if (string.Equals(fileList[i], selection.Replace("\"", ""), StringComparison.InvariantCultureIgnoreCase))
					{
						fileListGridView.Rows[i].Selected = true;
						fileListGridView.CurrentCell = fileListGridView.Rows[i].Cells["File"];
					}
				}
            }

            // Check that setting the selected item above actually worked. If not, set it to the first item.
            if (fileListGridView.SelectedRows.Count == 0)
            {
				Logger.Log(string.Format("Could not find {0}\r\n", selection));
                fileListGridView.Rows[0].Selected = true;
                fileListGridView.CurrentCell = fileListGridView.Rows[0].Cells["File"];
            }
            SelectedFile = string.Empty;

            UserInterface.ApplyTheme(this);
            //fileListGridView.Columns["File"].DefaultCellStyle.Padding = new Padding(34, 0, 0, 0);
            //fileListGridView.CellPainting += fileListGridView_CellPainting;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            SelectedFile = fileListGridView.SelectedRows[0].Cells["File"].Value.ToString();
            EmulatorIndex = emulatorComboBox.SelectedIndex;
        }

        private void fileListBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            okButton.PerformClick();
        }

        /*
        private void fileListGridView_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            int priorityIndex = 0;
            int selectedIndex = 0;

            if (e.RowIndex < 0)
                return;

            if (e.ColumnIndex == fileListGridView.Columns["File"].Index)
            {
                if (e.RowIndex == priorityIndex)
                {
                    UserInterface.DrawCellIcon(e, Resources.star_blue);
                }
                
                if (e.RowIndex == selectedIndex)
                {
                    UserInterface.DrawCellIcon(e, Resources.star, 15, false);
                }
            }
        }
        */
    }
}
