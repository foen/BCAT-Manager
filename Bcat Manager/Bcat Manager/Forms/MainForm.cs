﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using System.Media;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Resources;
using Bcat_Manager.Properties;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Bcat_Manager.Forms;

namespace Bcat_Manager
{
    public partial class Form1 : Form
    {
        BcatList DataList = null;

        //will be removed in the future
        public byte[] ListCache = null;

        string CurrentTopicId = BCAT.MAIN_TOPIC_ID;

        public Form1()
        {
            InitializeComponent();

            //hackjob
            tabControl1.Location = new Point(tabControl1.Location.X, tabControl1.Location.Y + tabControl1.ItemSize.Height);
            tabControl1.ItemSize = new Size(0, 1);
            tabControl1.SizeMode = TabSizeMode.Fixed;
            tabControl1.Appearance = TabAppearance.FlatButtons;

            Updater.GetUpdate();
            
            SetCurrentTopic(BCAT.GetBcatTopic(CurrentTopicId));
        }

        //select pass and tid
        private void button_select_Click(object sender, EventArgs e)
        {
            DatabaseSelect selector = new DatabaseSelect();
            if (selector.ShowDialog() == DialogResult.OK)
            {
                var title = TitleDatabase.GetDatabase()[selector.SelectedIndex];
                textBox_tid.Text = title.TitleID.ToString("X16");
                textBox_pass.Text = title.Passphrase;
            }
        }

        //download/parse bcat
        private void button_getList_Click(object sender, EventArgs e)
        {
            if (button_getList.Text == "Get List")
            {
                try
                {
                    ListCache = BCAT.GetRawBcatList(ulong.Parse(textBox_tid.Text, NumberStyles.HexNumber), textBox_pass.Text);
                    DataList = MsgPack.Deserialize<BcatList>(ListCache);

                    treeView1.Nodes.Clear();

                    var rootnode = treeView1.Nodes.Add(DataList.topic_id);
                    rootnode.SelectedImageIndex = 0;
                    rootnode.ImageIndex = 0;
                    tabControl1.SelectedIndex = 0;

                    foreach (var dir in DataList.directories)
                    {
                        var dirnode = rootnode.Nodes.Add(dir.name);
                        dirnode.SelectedImageIndex = 0;
                        dirnode.ImageIndex = 0;
                        foreach (var file in dir.data_list)
                        {
                            var filenode = dirnode.Nodes.Add(file.filename);
                            filenode.SelectedImageIndex = 1;
                            filenode.ImageIndex = 1;
                        }
                    }

                    SystemSounds.Asterisk.Play();
                    textBox_tid.Enabled = false;
                    textBox_pass.Enabled = false;
                    button_select.Enabled = false;
                    button_getList.Text = "New";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else
            {
                treeView1.Nodes.Clear();
                tabControl1.SelectedIndex = 0;
                textBox_tid.Enabled = true;
                textBox_pass.Enabled = true;
                button_select.Enabled = true;
                button_getList.Text = "Get List";
            }
        }

        //check tid and pass
        private void textBox_pass_TextChanged(object sender, EventArgs e)
        {
            checkPassAndTitleId();
        }
        private void textBox_tid_TextChanged(object sender, EventArgs e)
        {
            checkPassAndTitleId();
        }
        private void checkPassAndTitleId()
        {
            bool correctPass = Utils.IsValidPassphrase(textBox_pass.Text);
            bool correctTid = Utils.IsValidTid(textBox_tid.Text);

            textBox_pass.BackColor = (correctPass) ? Color.White : Color.Red;
            textBox_tid.BackColor = (correctTid) ? Color.White : Color.Red;

            button_getList.Enabled = correctPass && correctTid;
        }

        //manage database
        private void manageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DatabaseManager manager = new DatabaseManager();
            manager.ShowDialog();
        }
        //settings
        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SettingForm form = new SettingForm();
            form.ShowDialog();
        }
        //about
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutForm form = new AboutForm();
            form.ShowDialog();
        }



        //show info
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var node = treeView1.SelectedNode;

            if (node == null)
            {
                tabControl1.SelectedIndex = 0;
                return;
            }

            tabControl1.SelectedIndex = node.Level+1;

            switch (node.Level)
            {
                case 0: //root
                    textBox_topicId.Text = DataList.topic_id;
                    textBox_serviceStatus.Text = DataList.service_status;
                    value_reqAppVer.Value = DataList.required_application_version;
                    checkBox_requiredNA.Checked = DataList.na_required;
                    break;
                case 1: //directory
                    var dir = DataList.directories[node.Index];
                    textBox_dirHash.Text = dir.digest;
                    textBox_dirMode.Text = dir.mode;
                    textBox_dirName.Text = dir.name;
                    checkBox_dirGroupCountry.Checked = dir.by_country_group;
                    break;
                case 2: //file
                    var file = DataList.directories[node.Parent.Index].data_list[node.Index];
                    textBox_fileHash.Text = file.digest;
                    textBox_fileName.Text = file.filename;
                    textBox_fileUrl.Text = file.url;
                    value_fileID.Value = file.data_id;
                    value_fileSize.Value = file.size;
                    break;
            }
        }
        //adjust/show contextmenu
        private void treeView1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var node = treeView1.SelectedNode;
                if (node == null) return;

                foreach (ToolStripMenuItem item in contextMenuStrip1.Items)
                    item.Visible = false;

                switch (node.Level)
                {
                    case 0: //root
                        contextMenuStrip1.Items[0].Visible = true; //download
                        contextMenuStrip1.Items[1].Visible = false; //add folder (temporarily false)
                        contextMenuStrip1.Items[4].Visible = true; //export
                        break;
                    case 1: //directory
                        contextMenuStrip1.Items[0].Visible = true; //download
                        contextMenuStrip1.Items[2].Visible = false; //add file (temporarily false)
                        contextMenuStrip1.Items[3].Visible = true; //remove
                        break;
                    case 2: //file
                        contextMenuStrip1.Items[0].Visible = true; //download
                        contextMenuStrip1.Items[3].Visible = true; //remove
                        break;
                }

                contextMenuStrip1.Show(Cursor.Position);
            }
        }

        //hexadecimal
        private void checkBox_fileID_CheckedChanged(object sender, EventArgs e)
        {
            value_fileID.Hexadecimal = checkBox_fileID.Checked;
        }
        private void checkBox_fileSize_CheckedChanged(object sender, EventArgs e)
        {
            value_fileSize.Hexadecimal = checkBox_fileSize.Checked;
        }
        private void checkBox_reqAppVer_CheckedChanged(object sender, EventArgs e)
        {
            value_reqAppVer.Hexadecimal = checkBox_reqAppVer.Checked;
        }

        //add folder
        private void addFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DataList.directories.Add(new BcatList.DirectoryEntry());
            var dir = DataList.directories.Last();

            //some default values
            dir.by_country_group = false;
            dir.data_list = new List<BcatList.DirectoryEntry.DataEntry>();
            dir.digest = "00000000000000000000000000000000";
            dir.mode = "async";
            dir.name = "untitled";

            var node = treeView1.SelectedNode.Nodes.Add("untitled");
            node.SelectedImageIndex = 0;
            node.ImageIndex = 0;
        }
        //add file
        private void addFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var sel = treeView1.SelectedNode;

            DataList.directories[sel.Index].data_list.Add(new BcatList.DirectoryEntry.DataEntry());
            var file = DataList.directories[sel.Index].data_list.Last();

            //some default values
            file.data_id = 0;
            file.digest = "00000000000000000000000000000000";
            file.url = "https://example.com/file.bin";
            file.filename = "untitled";
            file.size = 0;

            var node = treeView1.SelectedNode.Nodes.Add("untitled");
            node.SelectedImageIndex = 1;
            node.ImageIndex = 1;
        }
        //remove folder/file
        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var sel = treeView1.SelectedNode;
            switch (sel.Level)
            {
                case 1: //folder
                    DataList.directories.RemoveAt(sel.Index);
                    sel.Remove();
                    break;
                case 2: //file
                    DataList.directories[sel.Parent.Index].data_list.RemoveAt(sel.Index);
                    sel.Remove();
                    break;
            }
        }

        //download raw
        private void rawToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode sel = treeView1.SelectedNode;
            string tid = textBox_tid.Text;
            string pass = textBox_pass.Text;

            switch (sel.Level)
            {
                case 0: //root
                    DownloadAll(false, tid, pass);
                    break;
                case 1: //folder
                    DownloadDir(false, sel, tid, pass);
                    break;
                case 2: //file
                    DownloadFile(false, sel, tid, pass);
                    break;
            }
        }
        //download and decrypt
        private void decryptedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode sel = treeView1.SelectedNode;
            string tid = textBox_tid.Text;
            string pass = textBox_pass.Text;

            switch (sel.Level)
            {
                case 0: //root
                    DownloadAll(true, tid, pass);
                    break;
                case 1: //folder
                    DownloadDir(true, sel, tid, pass);
                    break;
                case 2: //file
                    DownloadFile(true, sel, tid, pass);
                    break;
            }
        }

        private void DownloadFile(bool decrypt, TreeNode node, string tid, string pass)
        {
            var dir = DataList.directories[node.Parent.Index];
            var file = dir.data_list[node.Index];

            saveFileDialog1.FileName = file.filename;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Thread t = new Thread(() =>
                {
                    Invoke(new Action(() => {
                        foreach (Control c in Controls)
                            c.Enabled = false;
                    }));

                    byte[] data = (decrypt)
                        ? BCAT.GetDecBcat(file.url, BCAT.Module.nnBcat, ulong.Parse(tid, NumberStyles.HexNumber), pass)
                        : BCAT.GetRawBcat(file.url, BCAT.Module.nnBcat);

                    File.WriteAllBytes(saveFileDialog1.FileName, data);

                    Invoke(new Action(() => {
                        foreach (Control c in Controls)
                            c.Enabled = true;
                    }));

                    SystemSounds.Asterisk.Play();
                });
                t.IsBackground = true;
                t.Start();
            }
        }
        private void DownloadDir(bool decrypt, TreeNode node, string tid, string pass)
        {
            var dir = DataList.directories[node.Index];

            FolderSelectDialog fbox = new FolderSelectDialog();
            if (fbox.ShowDialog() == DialogResult.OK)
            {
                string dirPath = $@"{fbox.SelectedPath}\{dir.name}\";

                //create folder if doesn't exist
                if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

                Thread t = new Thread(() =>
                {
                    Invoke(new Action(() => {
                        foreach (Control c in Controls)
                            c.Enabled = false;
                    }));

                    foreach (var file in dir.data_list)
                    {
                        string filePath = dirPath + file.filename;
                        byte[] data = (decrypt)
                        ? BCAT.GetDecBcat(file.url, BCAT.Module.nnBcat, ulong.Parse(tid, NumberStyles.HexNumber), pass)
                        : BCAT.GetRawBcat(file.url, BCAT.Module.nnBcat);

                        File.WriteAllBytes(filePath, data);
                    }

                    Invoke(new Action(() => {
                        foreach (Control c in Controls)
                            c.Enabled = true;
                    }));

                    SystemSounds.Asterisk.Play();
                });
                t.IsBackground = true;
                t.Start();
            }
        }
        private void DownloadAll(bool decrypt, string tid, string pass)
        {
            FolderSelectDialog fbox = new FolderSelectDialog();
            if (fbox.ShowDialog() == DialogResult.OK)
            {
                string rootPath = $@"{fbox.SelectedPath}\{DataList.topic_id}\";

                //create folder if doesn't exist
                if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

                Thread t = new Thread(() =>
                {
                    Invoke(new Action(() => {
                        foreach (Control c in Controls)
                            c.Enabled = false;
                    }));

                    foreach (var dir in DataList.directories)
                    {
                        string dirPath = $@"{rootPath}\{dir.name}\";

                        //create folder if doesn't exist
                        if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

                        foreach (var file in dir.data_list)
                        {
                            string filePath = dirPath + file.filename;
                            byte[] data = (decrypt)
                                ? BCAT.GetDecBcat(file.url, BCAT.Module.nnBcat, ulong.Parse(tid, NumberStyles.HexNumber), pass)
                                : BCAT.GetRawBcat(file.url, BCAT.Module.nnBcat);

                            File.WriteAllBytes(filePath, data);
                        }
                    }

                    Invoke(new Action(() => {
                        foreach (Control c in Controls)
                            c.Enabled = true;
                    }));

                    SystemSounds.Asterisk.Play();
                });
                t.IsBackground = true;
                t.Start();
            }
        }

        //open encrypt/decrypt form
        private void decryptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DecryptForm form = new DecryptForm();
            form.ShowDialog();
        }
        private void encryptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EncryptForm form = new EncryptForm();
            form.ShowDialog();
        }

        private void rawToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            saveFileDialog1.FileName = DataList.topic_id;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllBytes(saveFileDialog1.FileName, ListCache);
                SystemSounds.Asterisk.Play();
                //File.WriteAllBytes(saveFileDialog1.FileName, DataList.GetMsgPack());
            }
        }
        private void encryptedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EncryptForm form = new EncryptForm(ListCache);
            form.ShowDialog();
        }

        //how to get the pass
        private void linkLabel_infoPass_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/Random0666/BCAT-Manager/blob/master/passphrases.md");
        }

        private void button_refreshNews_Click(object sender, EventArgs e)
        {
            Thread t = new Thread(() =>
            {
                Invoke(new Action(() =>
                {
                    flowLayoutPanel1.Enabled = false;
                    button_clearNewsCache.Enabled = false;
                    button_chooseTopic.Enabled = false;
                    button_refreshNews.Enabled = false;
                    progressBar1.Visible = true;
                    label_newsProg.Visible = true;
                }));

                var newsdata = NewsCache.RefreshNews(CurrentTopicId, progressBar1, label_newsProg);

                Invoke(new Action(() =>
                {
                    progressBar1.Value = 0;
                    label_newsProg.Text = "...";
                    button_refreshNews.Enabled = true;

                    flowLayoutPanel1.SuspendLayout();
                    flowLayoutPanel1.Controls.Clear();
                    foreach (var data in newsdata)
                    {
                        TopicBox tb = new TopicBox();
                        tb.Size = new Size(178 + 2 * tb.SelectionSize, 155 + 2 * tb.SelectionSize);
                        tb.BackColor = SystemColors.Control;
                        tb.TopicColor = Color.White;
                        tb.Click += Tb_Click;
                        tb.Topic = data;
                        flowLayoutPanel1.Controls.Add(tb);
                    }
                    flowLayoutPanel1.ResumeLayout();

                    flowLayoutPanel1.Enabled = true;
                    button_clearNewsCache.Enabled = true;
                    button_chooseTopic.Enabled = true;
                    button_refreshNews.Enabled = true;
                    progressBar1.Visible = false;
                    label_newsProg.Visible = false;

                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        private void Tb_Click(object sender, EventArgs e)
        {
            foreach (TopicBox tb in flowLayoutPanel1.Controls)
                if (tb.Selected && tb != sender)
                    tb.Selected = false;

            if (sender.GetType() == typeof(TopicBox))
                ((TopicBox)sender).Selected = true;
        }
        
        private void SetCurrentTopic(BcatTopic topic)
        {
            CurrentTopicId = topic.Details.topic_id;
            pictureBox_topicIcon.Image = topic.Icon;
            textBox_topicDesc.Text = topic.Details.description;
        }

        private void button_clearNewsCache_Click(object sender, EventArgs e)
        {
            NewsCache.ClearCache();
        }

        private void button_chooseTopic_Click(object sender, EventArgs e)
        {
            CatalogForm form = new CatalogForm();
            if (form.ShowDialog() == DialogResult.OK)
            {
                SetCurrentTopic(form.SelectedTopic);
            }
        }
    }
}
