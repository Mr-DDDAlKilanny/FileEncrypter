using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace FileEncrypter
{
    internal delegate void ReportProgressCallback(int progress);

    public partial class Form1 : Form
    {
        private readonly Dictionary<ListViewItem, string> errors = new Dictionary<ListViewItem, string>();

        private int workingIdx;

        private bool operationIsEncrypting = true;

        private string currentPassword;

        private bool lastOperationDone = false;

        public Form1()
        {
            InitializeComponent();
            Encryption.ReportProgress = reportProgress;
        }

        public bool OperationIsEncryption
        {
            get { return operationIsEncrypting; }
            set { radioButtonDec.Checked = !(operationIsEncrypting = radioButtonEnc.Checked = value); }
        }

        private bool uiEnabled
        {
            get { return groupBox1.Enabled; }
            set
            {
                groupBox1.Enabled = groupBox2.Enabled = groupBox3.Enabled =
                    buttonStart.Enabled = buttonAdd.Enabled = buttonRemove.Enabled = value;
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            string param = e.Argument as string;
            if (operationIsEncrypting)
            {
                Encryption.Encrypt(param, currentPassword);
            }
            else
            {
                Encryption.Decrypt(param, currentPassword);
            }
        }

        private void reportProgress(int progress)
        {
            backgroundWorker1.ReportProgress(progress);
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (listView1.SelectedIndices.Count == 0 || workingIdx != listView1.SelectedIndices[0])
            {
                listView1.SelectedIndices.Clear();
                listView1.SelectedIndices.Add(workingIdx);
            }
            listView1.Items[workingIdx].SubItems[1].Text = string.Format("%{0}", e.ProgressPercentage);
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (Encryption.LastError != null)
            {
                errors.Add(listView1.Items[workingIdx], Encryption.LastError);
                listView1.Items[workingIdx].SubItems[1].Text = "Failed: " + Encryption.LastError;
            }
            else
            {
                listView1.Items[workingIdx].SubItems[1].Text = "Done";
            }
            if (++workingIdx == listView1.Items.Count)
            {
                uiEnabled = true;
                toolStripStatusLabel1.Text = "Operation Finished";
                lastOperationDone = true;
            }
            else
            {
                startOperation();
            }
        }

        private void radioButtonDec_CheckedChanged(object sender, EventArgs e)
        {
            if (listView1.Items.Count > 0 && MessageBox.Show(this, "Clear the current list?",
                "FileEncrypter", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                == System.Windows.Forms.DialogResult.Yes)
                listView1.Items.Clear();
            operationIsEncrypting = radioButtonEnc.Checked;
        }

        private bool preCheck()
        {
            if (listView1.Items.Count == 0)
            {
                MessageBox.Show(this, "You must add at least one file to the list",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (radioButtonSpFolder.Checked)
            {
                if (!Directory.Exists(textBoxFolder.Text))
                {
                    MessageBox.Show(this, "You have selected the option \"Specific Folder\", but have not specified the folder.\n\n"
                        + "Please set the output directory.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                Encryption.OutputDirectory = textBoxFolder.Text;
            }
            else
            {
                Encryption.OutputDirectory = null;
            }
            if (textBoxPass1.TextLength > 4 && textBoxPass1.Text == textBoxPass2.Text)
                return true;
            else if (textBoxPass1.Text == textBoxPass2.Text)
                MessageBox.Show(this, "The password must be (5 - 16) characters length",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
                MessageBox.Show(this, "The two passwords do not match",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            if (preCheck())
            {
                currentPassword = textBoxPass1.Text;
                workingIdx = 0;
                errors.Clear();
                toolStripStatusLabel1.Text = "Working in progress";
                uiEnabled = false;
                startOperation();
            }
        }

        private void buttonAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this, "FileEncrypter, v 1.0\n\nIbraheem AlKilanny, 2013",
                "About FileEncrypter", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            if (lastOperationDone)
            {
                listView1.Items.Clear();
                lastOperationDone = false;
            }
            openFileDialog1.Filter = radioButtonEnc.Checked ? "All Files (*.*)|*.*" : "";
            if (openFileDialog1.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                AddFileToList(openFileDialog1.FileNames);
            }
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            foreach (var item in listView1.SelectedItems)
                listView1.Items.Remove(item as ListViewItem);
        }

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this, "This tool allows you to encrypt\\decrypt files with a powerful encryption schema.\n"
                + "The file bytes are encrypted, so that a purely new file is produced.\n\n"
                + "All you need is the password. Once a file has been encrypted with a password, "
                + "it cannot be restored without the password it was encrypted with.\n\n"
                + "WARNING: The file name will be encrypted too. The encrypted file name MUST NOT\n"
                + "  be changed, otherwise the decryption will fail",
                "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void AddFileToList(params string[] fileName)
        {
            bool duplicates = false;
            bool invalidDecFiles = false;
            foreach (var file in fileName)
            {
                if (fileExistsInListView(file))
                {
                    duplicates = true;
                    continue;
                }
                if (!operationIsEncrypting && !Encryption.IsValidMd5Hash(Path.GetFileName(file)))
                {
                    invalidDecFiles = true;
                    continue;
                }
                listView1.Items.Add(new ListViewItem(new string[] {
                    Path.GetFileName(file),
                    "Ready",
                    file
                }));
            }
            if (duplicates)
                MessageBox.Show(this, "Some files were not added because they exist in the list already",
                    "Attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (invalidDecFiles)
                MessageBox.Show(this, "Some files were not added to decryption list, because their file names are invalid",
                    "Attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private bool fileExistsInListView(string fileName)
        {
            foreach (var item in listView1.Items)
                if ((item as ListViewItem).SubItems[2].Text == fileName)
                    return true;
            return false;
        }

        private void startOperation()
        {
            backgroundWorker1.RunWorkerAsync(listView1.Items[workingIdx].SubItems[2].Text);
        }

        private void listView1_ItemMouseHover(object sender, ListViewItemMouseHoverEventArgs e)
        {
            if (errors.ContainsKey(e.Item))
            {
                toolTip1.ToolTipIcon = ToolTipIcon.Error;
                toolTip1.ToolTipTitle = "Operation Failed";
                toolTip1.Show(errors[e.Item], listView1, e.Item.Position, 3000);
            }
        }

        private void toolTip1_Popup(object sender, PopupEventArgs e)
        {
            if (e.AssociatedControl != listView1)
            {
                toolTip1.ToolTipIcon = ToolTipIcon.None;
                toolTip1.ToolTipTitle = "";
            }
        }

        private void radioButtonSameFolder_CheckedChanged(object sender, EventArgs e)
        {
            textBoxFolder.Enabled = buttonBrowseFolder.Enabled = radioButtonSpFolder.Checked;
        }

        private void buttonBrowseFolder_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                textBoxFolder.Text = folderBrowserDialog1.SelectedPath;
        }

        private void checkBoxShowPass_CheckedChanged(object sender, EventArgs e)
        {
            textBoxPass1.UseSystemPasswordChar = textBoxPass2.UseSystemPasswordChar = 
                !checkBoxShowPass.Checked;
        }
    }
}
