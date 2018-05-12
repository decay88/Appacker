﻿using RavSoft;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XDMessaging;

namespace Appacker
{
    public partial class MainForm : Form
    {
        private readonly CultureInfo CULTURE_RU = CultureInfo.GetCultureInfo("ru-RU");
        private readonly CultureInfo CULTURE_EN = CultureInfo.GetCultureInfo("en-US");

        private string pathToCustomIcon = null;

        public MainForm()
        {
            InitializeComponent();
            // Fix btnIconReset background transparency by setting pictureBox as its Parent (and recalculate position)
            Point pos = PointToScreen(btnIconReset.Location);
            pos = picAppIcon.PointToClient(pos);
            btnIconReset.Parent = picAppIcon;
            btnIconReset.Location = pos;

            // Load language from settings
            SetLanguage(RegistrySettingsProvider.Language);
        }

        #region GUI and controls stuff
        // Open folder dialog box
        private void btnBrowseAppFolder_Click(object sender, EventArgs e)
        {
            if(folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                SetAppFolderPath(folderBrowserDialog.SelectedPath);
            }
        }

        private void SetAppFolderPath(string path)
        {
            txtAppFolderPath.Text = path;
            RebuildTree();
            UpdateComboBoxMainExe(path);
            CheckIfReadyToPack();
        }

        private void txtAppFolderPath_Enter(object sender, EventArgs e) => btnBrowseAppFolder.Focus();

        // Open save file dialog box
        private void btnBrowsePackPath_Click(object sender, EventArgs e)
        {
            if(saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                SetPackPath(saveFileDialog.FileName);
            }
        }

        private void SetPackPath(string path)
        {
            txtPackExePath.Text = path;

            // If treeView is built, make the root node name equal to name of the pack name
            if (treeView.Nodes.Count > 0)
                treeView.Nodes[0].Text = Path.GetFileName(path);
        }

        // Add .exe extension to packExePath if missing
        private void txtPackExePath_Leave(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtPackExePath.Text) && !txtPackExePath.Text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                txtPackExePath.Text = txtPackExePath.Text.TrimEnd('.') + ".exe";
            }
        }

        // Clear tree view and build new tree using path from txtAppFolderPath
        private void RebuildTree()
        {
            treeView.Nodes.Clear();
            BuildTree(new DirectoryInfo(txtAppFolderPath.Text), treeView.Nodes);

            if (treeView.Nodes.Count > 0)
            {
                // Set icon for root node
                var rootNode = treeView.Nodes[0];
                rootNode.ImageIndex = rootNode.ImageIndex = 4;
                rootNode.Expand();

                // If there's defined path to package save file, then set pack file name as root node name
                if (!string.IsNullOrWhiteSpace(txtPackExePath.Text))
                {
                    try
                    {
                        rootNode.Text = Path.GetFileName(txtPackExePath.Text);
                    }
                    catch (ArgumentException) { /* Fucked up pack save path */ }
                }
            }
        }

        // Recursively build hierarchical tree
        private void BuildTree(DirectoryInfo directoryInfo, TreeNodeCollection addInMe)
        {
            TreeNode curNode = addInMe.Add(directoryInfo.Name);
            curNode.ImageIndex = 0;
            curNode.SelectedImageIndex = 1;

            foreach (DirectoryInfo subdir in directoryInfo.GetDirectories())
            {
                BuildTree(subdir, curNode.Nodes);
            }
            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                TreeNode fileNode = curNode.Nodes.Add(file.Name);
                fileNode.Name = fileNode.FullPath.Substring(fileNode.FullPath.IndexOf(Path.DirectorySeparatorChar) + 1);
                if (file.Extension.ToLowerInvariant() == ".exe")
                    fileNode.ImageIndex = fileNode.SelectedImageIndex = 3;
                else
                    fileNode.ImageIndex = fileNode.SelectedImageIndex = 2;
            }
        }

        // Find all .exe files and add them to the combobox
        private void UpdateComboBoxMainExe(string pathToAppFolder)
        {
            string[] exes = Directory.GetFiles(pathToAppFolder, "*.exe", SearchOption.AllDirectories);
            comboMainExePath.Items.Clear();
            foreach (string localPath in exes.Select(x => x.Replace(pathToAppFolder, "").TrimStart(Path.DirectorySeparatorChar)))
                comboMainExePath.Items.Add(localPath);
            comboMainExePath.Enabled = true;
            SetAppIconPreviewFromMainExeIfNoCustom();
        }

        // Only alow selection of .exe files
        private void treeView_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if(e.Node == treeView.SelectedNode || !e.Node.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                return;
            }

            comboMainExePath.SelectedItem = e.Node.Name;
        }
        
        // Change selected .exe in treeView when comboBox selection changes
        private void comboMainExePath_SelectedIndexChanged(object sender, EventArgs e)
        {
            TreeNode node = treeView.Nodes.Find(comboMainExePath.Text, true).First();
            treeView.SelectedNode = node;

            TextBox_TextChanged(sender, e);
            SetAppIconPreviewFromMainExeIfNoCustom();
        }

        // If user chose existing exe file as a save destination, then display file override warning label
        private void txtPackPath_TextChanged(object sender, EventArgs e)
        {
            TextBox_TextChanged(sender, e);
            labOverride.Visible = File.Exists(txtPackExePath.Text);
        }

        private void TextBox_TextChanged(object sender, EventArgs e) => CheckIfReadyToPack();

        // When user have specified path to app directory, path to save package and local path to main exe, button 'Pack' becomes active
        private readonly Color colOk = Color.Green;
        private readonly Color colWrong = Color.FromArgb(200, 0, 0);
        private void CheckIfReadyToPack()
        {
            bool isReady = true;

            if (string.IsNullOrWhiteSpace(txtAppFolderPath.Text))
            {
                isReady = false;
                indAppFolder.BackColor = colWrong;
            }
            else
            {
                indAppFolder.BackColor = colOk;
            }

            if (string.IsNullOrWhiteSpace(txtPackExePath.Text)
                || !txtPackExePath.Text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                isReady = false;
                indPackExePath.BackColor = colWrong;
            }
            else
            {
                indPackExePath.BackColor = colOk;
            }

            if (string.IsNullOrWhiteSpace(comboMainExePath.Text))
            {
                isReady = false;
                indMainExe.BackColor = colWrong;
            }
            else
            {
                indMainExe.BackColor = colOk;
            }

            packToolStripMenuItem.Enabled = btnPack.Enabled = isReady;
        }
        #endregion

        // ============== PACK METHOD ================
        // Launch the packer.exe with needed arguments
        // ===========================================
        private void btnPack_Click(object sender, EventArgs e)
        {
            btnPack.Enabled = packToolStripMenuItem.Enabled = false;
            btnPack.Text = Resources.Strings.btnPackTextPacking1 + Environment.NewLine + Resources.Strings.btnPackTextPacking2;
            btnPack.Update();

            // Copy packer and unpacker into the temp directory
            string tempDir = null;
            while (tempDir == null || Directory.Exists(tempDir))
                tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            File.WriteAllBytes(Path.Combine(tempDir, "packer.exe"), ToolsStorage.Packer);
            File.WriteAllBytes(Path.Combine(tempDir, "unpacker.exe"), ToolsStorage.Unpacker);
            File.WriteAllBytes(Path.Combine(tempDir, "progressBarSplash.exe"), ToolsStorage.ProgressBarSplash);

            // Inject new icon into unpacker.exe (take the icon from the main executable of unpacked app if user did not provide a custom icon)
            string iconPath = pathToCustomIcon ?? Path.Combine(txtAppFolderPath.Text, comboMainExePath.Text);
            IconSwapper.ChangeIcon(Path.Combine(tempDir, "unpacker.exe"), iconPath);

            // Launch packer.exe with arguments:
            // 1. Path to unpacker.exe
            // 2. Path where to save packed app
            // 3. Relative path to main executable inside app directory
            // 4. Path to app directory
            // 5. Whether app is self-repackable, True or False
            ProcessStartInfo packProcInfo = new ProcessStartInfo(Path.Combine(tempDir, "packer.exe"));
            packProcInfo.Arguments = $@"""{Path.Combine(tempDir, "unpacker.exe")}"" ""{txtPackExePath.Text.TrimEnd(Path.DirectorySeparatorChar)}"" ""{comboMainExePath.Text}"" ""{txtAppFolderPath.Text}"" {checkRepackable.Checked}";
#if (!DEBUG)
            packProcInfo.CreateNoWindow = true;
            packProcInfo.WindowStyle = ProcessWindowStyle.Hidden;
#endif

            Process packProc = new Process();
            packProc.StartInfo = packProcInfo;

            // Setup XDMessagingClient listener to receive packing progress updates
            XDMessagingClient client = new XDMessagingClient();
            IXDListener listener = client.Listeners.GetListenerForMode(XDTransportMode.HighPerformanceUI);
            listener.RegisterChannel("AppackerProgress");

            // Attach event handler for incoming messages
            listener.MessageReceived += (o, ea) =>
            {
                if (ea.DataGram.Channel == "AppackerProgress")
                {
                    // 'Done' is sent by Packer when it finished packing and is ready to quit
                    if (ea.DataGram.Message == "Done")
                    {
                        progressBar.Value = progressBar.Maximum;
                        packProc.WaitForExit();

                        // Show error message if return code is abnormal
                        if (packProc.ExitCode != 0)
                            ShowPackingFailMessage(packProc.ExitCode);
                        else
                            System.Media.SystemSounds.Exclamation.Play();

                        packProc.Dispose();

                        // Delete temp directory
                        if (Directory.Exists(tempDir))
                            Directory.Delete(tempDir, true);

                        btnPack.Text = Resources.Strings.btnPackText;
                        btnPack.Enabled = packToolStripMenuItem.Enabled = true;

                        listener.UnRegisterChannel("AppackerProgress");
                        listener.Dispose();
                    }
                    else
                    {
                        string[] tokens = ea.DataGram.Message.Split(' ');
                        progressBar.Maximum = int.Parse(tokens[1]);
                        progressBar.Value = int.Parse(tokens[0]);
                    }
                }
            };
            
            progressBar.Value = 0;
            packProc.Start();
        }
        
        // Display message box with an error explanation
        private void ShowPackingFailMessage(int exitCode)
        {
            string message;
            if (exitCode >= 1 && exitCode <= 6)
                message = Resources.Strings.ResourceManager.GetString($"errorCode{exitCode}");
            else
                message = Resources.Strings.errorCodeUnknown;

            MessageBox.Show($"{Resources.Strings.errorText} 0x{exitCode:X3}.\n{message}", Resources.Strings.errorCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }

        #region == Drag and drop stuff ==
        private void txtAppFolderPath_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void txtAppFolderPath_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[]) e.Data.GetData(DataFormats.FileDrop);

                if (Directory.Exists(files[0]))
                {
                    SetAppFolderPath(files[0]);
                }
            }
        }

        private void txtPackPath_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (File.Exists(files[0]) && files[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    SetPackPath(files[0]);
                }
            }
        }
        #endregion

        #region Language stuff ==
        // Change the CultureInfo and save the language choice to the registry
        private void SetLanguage(CultureInfo language)
        {
            englishToolStripMenuItem.Checked = language.Equals(CULTURE_EN);
            russianToolStripMenuItem.Checked = language.Equals(CULTURE_RU);
            cultureManager.UICulture =
                System.Threading.Thread.CurrentThread.CurrentCulture =
                System.Threading.Thread.CurrentThread.CurrentUICulture = language;
            RegistrySettingsProvider.Language = cultureManager.UICulture;
            CheckIfReadyToPack();
            SetCueBanners();
            CrunchFixControlsVisibility();
        }
        
        // Changing language resets Enabled and Visible parameters of controls to default values
        // So this is rather lazy fix by manually re-checking specific controlls
        private void CrunchFixControlsVisibility()
        {
            if (!string.IsNullOrWhiteSpace(txtAppFolderPath.Text))
                comboMainExePath.Enabled = true;
            if (!string.IsNullOrWhiteSpace(pathToCustomIcon))
                btnIconReset.Visible = true;
            if (!string.IsNullOrWhiteSpace(txtPackExePath.Text) && File.Exists(txtPackExePath.Text))
                labOverride.Visible = true;
        }

        private void SetCueBanners()
        {
            CueProvider.SetCue(txtAppFolderPath, Resources.Strings.cueAppDirPath);
            CueProvider.SetCue(txtPackExePath, Resources.Strings.cuePackExePath);
        }
        #endregion

        #region == Menu strip items stuff ==
        private void englishToolStripMenuItem_Click(object sender, EventArgs e) => SetLanguage(CULTURE_EN);
        private void russianToolStripMenuItem_Click(object sender, EventArgs e) => SetLanguage(CULTURE_RU);

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Application.Exit();

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutForm about = new AboutForm();
            about.ShowDialog(this);
        }
        #endregion

        #region == Package icon stuff ==
        // Displays the icon of the main executable in pictureBox
        private void SetAppIconPreviewFromMainExe()
        {
            if (string.IsNullOrWhiteSpace(comboMainExePath.Text))
                picAppIcon.Image = null;
            else
            {
                Bitmap appIcon = IconSwapper.GetIconFromFile(Path.Combine(txtAppFolderPath.Text, comboMainExePath.Text));
                picAppIcon.Image = appIcon;
            }
        }

        private void SetAppIconPreviewFromMainExeIfNoCustom()
        {
            if (pathToCustomIcon == null)
                SetAppIconPreviewFromMainExe();
        }

        // Displays the custom icon in pictureBox
        private void btnChangeIcon_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if(openIconDialog.ShowDialog() == DialogResult.OK)
            {
                pathToCustomIcon = openIconDialog.FileName;
                Bitmap icon = IconSwapper.GetIconFromFile(pathToCustomIcon);
                picAppIcon.Image = icon;
                btnIconReset.Visible = true;
            }
        }

        // Resets icon in pictureBox to the main executable icon
        private void btnIconReset_Click(object sender, EventArgs e)
        {
            pathToCustomIcon = null;
            SetAppIconPreviewFromMainExe();
            btnIconReset.Visible = false;
        }
        #endregion
    }
}
