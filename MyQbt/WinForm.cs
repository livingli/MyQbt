﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using Transmission.API.RPC.Entity;

namespace MyQbt
{
    public partial class WinForm : Form
    {
        private static readonly string qBittorrentBackUpFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "qBittorrent", "BT_backup");

        private static readonly string configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "MyQbtConfig.xml");

        private Config.BTClient btClient = Config.BTClient.qBittorrent;
        private Transmission.API.RPC.Client transmissionClient = null;

        private Config configData;

        private bool isManualAddSuccess;
        private string manualAddFailedReason;

        private Func<bool> CheckµTorrentNeedSave = null;

        private Dictionary<string, string> domainCategoryDic = null;

        public WinForm()
        {
            InitializeComponent();
        }

        private void WinForm_Load(object sender, EventArgs e)
        {
            this.Icon = Properties.Resources.icon;
            this.Text = String.Format("MyQbt v{0}", Application.ProductVersion);
            this.groupBoxAdd.Enabled = false;
            this.groupBoxOnlineOther.Enabled = false;

            this.groupBoxOnlineOther.Visible = false;
            this.buttonOther.Visible = false;

            LoadConfig();
            this.cbPathMap.Checked = configData.IsPathMap;
            this.cbPathMap.CheckedChanged +=
                CbSaveDiskOrFolder_SelectedIndexChanged;

            this.rbqBittorrent.Checked = (configData.BTClientType == Config.BTClient.qBittorrent);
            this.rbTransmission.Checked = (!this.rbqBittorrent.Checked);

            this.rbWindows.Checked = (configData.QbtSystemType == Config.SystemType.Windows);
            this.rbLinux.Checked = (!this.rbWindows.Checked);

            InitDomainCategoryDic();

            if (configData.LastUseUrl != null && configData.ConnectList != null)
            {
                this.cbUrl.SuspendLayout();
                this.cbUrl.SelectedIndexChanged -= this.CbUrl_SelectedIndexChanged;
                this.cbUrl.Items.Clear();
                configData.ConnectList.ForEach(x => this.cbUrl.Items.Add(x.Url));
                this.cbUrl.Text = configData.LastUseUrl;
                this.cbUrl.SelectedIndexChanged += this.CbUrl_SelectedIndexChanged;
                this.cbUrl.ResumeLayout();

                Config.Connect c1 = configData.ConnectList.Find(
                    x => x.Url == configData.LastUseUrl);
                if (c1 != null)
                {
                    this.tbUser.Text = c1.User;
                    string decryptedPassword = "";
                    try { decryptedPassword = Helper.Decrypt(c1.Password); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(
                            "密码解密失败，请重新设置密码！\n{0}\n配置文件复制到另一电脑会导致此问题", ex.Message));
                    }
                    this.tbPassword.Text = decryptedPassword;
                }
            }
            if (configData.PathMapString == null)
                this.rtbPathMap.Text =
                    "L:\\|D:\\\nM:\\|E:\\\nN:\\|F:\\\nO:\\|G:\\\n" +
                    "P:\\|H:\\\nQ:\\|I:\\\nR:\\|J:\\\nZ:\\|/mnt/test/";
            else this.rtbPathMap.Text = Encoding.Default.GetString(
                Convert.FromBase64String(configData.PathMapString));

            InitComboxSaveDisk();
            InitComboxSaveFolder();
            InitComboxTrackFindAndReplace();
            InitComboxSavePathFindAndReplace();
            InitComboxSettingSaveFolder();
            CbSaveDiskOrFolder_SelectedIndexChanged(null, null);

            if (configData.CategoryList != null)
            {
                this.cbFindCategory.SuspendLayout();
                this.cbFindCategory.Items.Clear();
                configData.CategoryList.ForEach(x => this.cbFindCategory.Items.Add(x));
                if (configData.LastUseCategory != null)
                    this.cbFindCategory.Text = configData.LastUseCategory;
                else if (this.cbFindCategory.Items.Count > 0)
                    this.cbFindCategory.SelectedIndex = 0;
                else this.cbFindCategory.Text = "";
                this.cbFindCategory.ResumeLayout();
            }

            this.cbTrackerFind.SelectedIndex = 0;
            this.cbTrackerReplace.SelectedIndex = 0;

            µTorrentOfflineUserControl utc = new µTorrentOfflineUserControl()
            { Dock = DockStyle.Fill };
            this.CheckµTorrentNeedSave = utc.CheckNeedSaveWhenFormClosing;
            this.tabPageµTorrentOffline.Controls.Add(utc);
        }

        private void WinForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = this.CheckµTorrentNeedSave();
        }

        private void WinForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            SaveConfig();
        }

        private void InitDomainCategoryDic()
        {
            this.domainCategoryDic = new Dictionary<string, string>();
            if (this.configData.DomainCategoryList != null)
            {
                for (int i = 0; i < this.configData.DomainCategoryList.Count; i++)
                {
                    string domain = this.configData.DomainCategoryList[i].Domain;
                    string category = this.configData.DomainCategoryList[i].Category;

                    if (this.domainCategoryDic.ContainsKey(domain))
                    {
                        this.domainCategoryDic[domain] = category;
                    }
                    else this.domainCategoryDic.Add(domain, category);
                }
            }
        }

        private void LoadConfig()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            if (File.Exists(configPath))
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Config));
                using (FileStream fileStream = File.OpenRead(configPath))
                {
                    configData = xmlSerializer.Deserialize(fileStream) as Config;
                }
            }
            if (configData == null)
            {
                configData = new Config()
                {
                    IsPathMap = false,
                    BTClientType = Config.BTClient.qBittorrent,
                    QbtSystemType = Config.SystemType.Windows
                };
            }
        }

        private void SaveConfig()
        {
            if (this.rbqBittorrent.Checked) configData.BTClientType = Config.BTClient.qBittorrent;
            else configData.BTClientType = Config.BTClient.Transmission;
            if (this.rbWindows.Checked) configData.QbtSystemType = Config.SystemType.Windows;
            else configData.QbtSystemType = Config.SystemType.Linux;
            configData.IsPathMap = this.cbPathMap.Checked;
            configData.PathMapString = Convert.ToBase64String(
                Encoding.Default.GetBytes(this.rtbPathMap.Text));

            configData.LastUseCategory = this.cbFindCategory.Text;
            if (configData.CategoryList == null) configData.CategoryList = new List<string>();
            if (this.cbCategory.Items.Count > 0) configData.CategoryList.Clear();
            for (int i = 0; i < this.cbCategory.Items.Count; i++)
            {
                configData.CategoryList.Add(this.cbCategory.Items[i].ToString());
            }

            configData.LastUseUrl = this.cbUrl.Text;

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Config));
            using (FileStream fileStream = File.Create(configPath))
            {
                xmlSerializer.Serialize(fileStream, configData);
            }
        }

        private void InitComboxSaveDisk()
        {
            DriveInfo[] allDirves = DriveInfo.GetDrives();

            this.cbSaveDisk.SelectedValueChanged -=
                CbSaveDiskOrFolder_SelectedIndexChanged;
            this.cbSaveDisk.SuspendLayout();
            this.cbSaveDisk.Items.AddRange(
                Array.ConvertAll<DriveInfo, string>(
                    allDirves, x => string.Format("{0} ({1})",
                    x.Name, Helper.GetSizeString(x.TotalFreeSpace))));
            int index = 0;
            for (int i = 0; i < allDirves.Length; i++)
            {
                if (allDirves[i].TotalFreeSpace > allDirves[index].TotalFreeSpace)
                {
                    index = i;
                }
            }
            this.cbSaveDisk.SelectedIndex = index;
            this.cbSaveDisk.ResumeLayout();
            this.cbSaveDisk.SelectedValueChanged +=
                CbSaveDiskOrFolder_SelectedIndexChanged;
        }

        private void InitComboxSaveFolder(int selectedIndex = 7)
        {
            string[] folders = { "动画\\非日", "动画\\日",
                "剧集\\欧美", "剧集\\中", "剧集\\日", "剧集\\韩",
                "纪录", "欧美", "中", "日", "韩", "印"};

            this.cbSaveFolder.SelectedValueChanged -=
                CbSaveDiskOrFolder_SelectedIndexChanged;
            this.cbSaveFolder.SuspendLayout();
            this.cbSaveFolder.Items.Clear();
            foreach (string str in folders)
            {
                string strTemp = Path.Combine(
                    this.cbSaveDisk.Text.Substring(0, 3), str);
                int count = 0;
                if (Directory.Exists(strTemp))
                    count = Directory.GetFiles(strTemp).Length +
                        Directory.GetDirectories(strTemp).Length;
                this.cbSaveFolder.Items.Add(string.Format("{0} ({1})", str, count));
            }
            this.cbSaveFolder.SelectedIndex = selectedIndex;
            this.cbSaveFolder.ResumeLayout();
            this.cbSaveFolder.SelectedValueChanged +=
                CbSaveDiskOrFolder_SelectedIndexChanged;
        }

        private void InitComboxSettingSaveFolder()
        {
            this.cbSettingSaveFolder.SuspendLayout();
            this.cbSettingSaveFolder.Items.Clear();
            if (configData.SaveFolderList == null)
                configData.SaveFolderList = new List<string>();
            this.cbSettingSaveFolder.Items.AddRange(configData.SaveFolderList.ToArray());
            if (this.cbSettingSaveFolder.Items.Count > 0)
                this.cbSettingSaveFolder.SelectedIndex = 0;
            this.cbSettingSaveFolder.ResumeLayout();
        }

        private async void InitComboxCategory()
        {
            this.cbCategory.SuspendLayout();
            this.cbCategory.Items.Clear();
            if (btClient == Config.BTClient.qBittorrent)
                this.cbCategory.Items.AddRange(
                    (await QbtWebAPI.API.GetAllCategoryString()).ToArray());
            this.cbCategory.Text = "";
            this.cbCategory.ResumeLayout();
        }

        private void InitComboxTrackFindAndReplace()
        {
            if (this.configData.TrackerFindList == null ||
                this.configData.TrackerFindList.Count == 0)
            {
                this.configData.TrackerFindList = new List<string>();
                this.configData.TrackerFindList.Add("http:");
            }
            if (this.configData.TrackerReplaceList == null ||
                this.configData.TrackerReplaceList.Count == 0)
            {
                this.configData.TrackerReplaceList = new List<string>();
                this.configData.TrackerReplaceList.Add("https:");
            }

            this.cbTrackerFind.SuspendLayout();
            this.cbTrackerReplace.SuspendLayout();
            this.cbTrackerFind.Items.Clear();
            this.cbTrackerReplace.Items.Clear();

            this.cbTrackerFind.Items.AddRange(this.configData.TrackerFindList.ToArray());
            this.cbTrackerReplace.Items.AddRange(this.configData.TrackerReplaceList.ToArray());
            this.cbTrackerFind.SelectedItem = this.configData.TrackerFindList[0];
            this.cbTrackerReplace.SelectedItem = this.configData.TrackerReplaceList[0];

            this.cbTrackerFind.ResumeLayout();
            this.cbTrackerReplace.ResumeLayout();
        }

        private void UpdataComboxTrackFindAndReplace(string trackerFind, string trackerReplace)
        {
            configData.TrackerFindList.Remove(trackerFind);
            configData.TrackerFindList.Insert(0, trackerFind);
            if (configData.TrackerFindList.Count > 30)
                configData.TrackerFindList.RemoveRange(
                    30, configData.TrackerFindList.Count - 30);

            configData.TrackerReplaceList.Remove(trackerReplace);
            configData.TrackerReplaceList.Insert(0, trackerReplace);
            if (configData.TrackerReplaceList.Count > 30)
                configData.TrackerReplaceList.RemoveRange(
                    30, configData.TrackerReplaceList.Count - 30);

            InitComboxTrackFindAndReplace();
        }

        private void InitComboxSavePathFindAndReplace()
        {
            if (this.configData.SavePathFindList == null ||
                this.configData.SavePathFindList.Count == 0)
            {
                this.configData.SavePathFindList =
                    new List<string>(new string[] { "/mnt/t1", "D:" });
            }
            if (this.configData.SavePathReplaceList == null ||
                this.configData.SavePathReplaceList.Count == 0)
            {
                this.configData.SavePathReplaceList =
                    new List<string>(new string[] { "/mnt/t2", "E:" });
            }

            this.cbSavePathFind.SuspendLayout();
            this.cbSavePathReplace.SuspendLayout();
            this.cbSavePathFind.Items.Clear();
            this.cbSavePathReplace.Items.Clear();

            this.cbSavePathFind.Items.AddRange(this.configData.SavePathFindList.ToArray());
            this.cbSavePathReplace.Items.AddRange(this.configData.SavePathReplaceList.ToArray());
            this.cbSavePathFind.SelectedItem = this.configData.SavePathFindList[0];
            this.cbSavePathReplace.SelectedItem = this.configData.SavePathReplaceList[0];

            this.cbSavePathFind.ResumeLayout();
            this.cbSavePathReplace.ResumeLayout();
        }

        private void UpdataComboxSavePathFindAndReplace(string savePathFind, string savePathReplace)
        {
            configData.SavePathFindList.Remove(savePathFind);
            configData.SavePathFindList.Insert(0, savePathFind);
            if (configData.SavePathFindList.Count > 30)
                configData.SavePathFindList.RemoveRange(
                    30, configData.SavePathFindList.Count - 30);

            configData.SavePathReplaceList.Remove(savePathReplace);
            configData.SavePathReplaceList.Insert(0, savePathReplace);
            if (configData.SavePathReplaceList.Count > 30)
                configData.SavePathReplaceList.RemoveRange(
                    30, configData.SavePathReplaceList.Count - 30);

            InitComboxSavePathFindAndReplace();
        }

        private Dictionary<string, string> GetVirtualToActualPathMap()
        {
            string[] strMaps = this.rtbPathMap.Text.Split(
                new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, string> virtualToActualPathMapDic =
                new Dictionary<string, string>();

            foreach (string strMap in strMaps)
            {
                string[] ss = strMap.Split(
                    new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (ss == null || ss.Length != 2) continue;

                // ss[0] 只能为windows路径
                ss[0] = ss[0].Trim();
                if (ss[0].Length >= 2 &&
                    ((ss[0][0] >= 'a' && ss[0][0] <= 'z') || (ss[0][0] >= 'A' && ss[0][0] <= 'Z')) &&
                     (ss[0][1] == ':'))
                {
                    ss[0] = string.Join("\\", ss[0].Split(
                        new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
                    if (!ss[0].EndsWith("\\")) ss[0] = ss[0] + "\\";
                }
                else continue;

                ss[1] = ss[1].Trim();
                if (ss[1].Length >= 1 && (ss[1][0] == '/' || ss[1][0] == '\\'))
                {
                    ss[1] = "/" + string.Join("/", ss[1].Split(
                        new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
                    if (!ss[1].EndsWith("/")) ss[1] = ss[1] + "/";
                }
                else if (ss[1].Length >= 2 &&
                    ((ss[1][0] >= 'a' && ss[1][0] <= 'z') || (ss[1][0] >= 'A' && ss[1][0] <= 'Z')) &&
                     (ss[1][1] == ':'))
                {
                    ss[1] = string.Join("\\", ss[1].Split(
                        new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
                    if (!ss[1].EndsWith("\\")) ss[1] = ss[1] + "\\";
                }
                else continue;

                if (virtualToActualPathMapDic.ContainsKey(ss[0]))
                {
                    virtualToActualPathMapDic[ss[0]] = ss[1];
                }
                else virtualToActualPathMapDic.Add(ss[0], ss[1]);
            }

            return virtualToActualPathMapDic;
        }

        private void CbUrl_SelectedIndexChanged(object sender, EventArgs e)
        {
            Config.Connect c1 = configData.ConnectList.Find(
                x => x.Url == this.cbUrl.Text);
            if (c1 != null)
            {
                this.tbUser.Text = c1.User;
                string decryptedPassword = "";
                try { decryptedPassword = Helper.Decrypt(c1.Password); }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(
                        "密码解密失败，请重新设置密码！\n{0}\n配置文件复制到另一电脑会导致此问题", ex.Message));
                }
                this.tbPassword.Text = decryptedPassword;
            }
        }

        private void CbSaveDiskOrFolder_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender == this.cbSaveDisk)
            {
                int selectedIndex = this.cbSaveFolder.SelectedIndex;
                InitComboxSaveFolder(selectedIndex);
            }
            string str1 = this.cbSaveDisk.Text.Substring(0, 3);
            int index2 = this.cbSaveFolder.Text.LastIndexOf('(');
            string str2 = this.cbSaveFolder.Text.Substring(0, index2 - 1).Trim();
            string strTemp = Path.Combine(str1, str2);

            if (this.cbPathMap.Checked)
            {
                Dictionary<string, string> virtualToActualPathMapDic =
                    GetVirtualToActualPathMap();
                foreach (KeyValuePair<string, string> kv in virtualToActualPathMapDic)
                {
                    int index = strTemp.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase);
                    if (index == 0)
                    {
                        if (kv.Value[0] == '/')
                            strTemp = kv.Value + strTemp.Substring(kv.Key.Length).Replace('\\', '/');
                        else strTemp = kv.Value + strTemp.Substring(kv.Key.Length);
                        break;
                    }
                }
            }

            this.cbSettingSaveFolder.Text = strTemp;
        }

        private void rbqBittorrent_CheckedChanged(object sender, EventArgs e)
        {
            btClient = (this.rbqBittorrent.Checked ?
                Config.BTClient.qBittorrent : Config.BTClient.Transmission);

            this.Text = String.Format("MyQbt v{0}", Application.ProductVersion);
            this.groupBoxAdd.Enabled = false;
            this.groupBoxOnlineOther.Enabled = false;
        }

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            bool? flag = null;
            bool isVersionValid = false;
            string errMsg = "";
            string btClientVersion = "";
            try
            {
                if (btClient == Config.BTClient.qBittorrent)
                {
                    QbtWebAPI.API.Initialize(this.cbUrl.Text);
                    flag = await QbtWebAPI.API.Login(
                        this.tbUser.Text, this.tbPassword.Text);
                    btClientVersion = await QbtWebAPI.API.GetQbittorrentVersion();
                    isVersionValid = (string.Compare(btClientVersion, "v4.1", true) >= 0);
                    if (!isVersionValid)
                        errMsg = string.Format(
                            "qBittorrent 当前版本为 {0}，需 >=v4.1", btClientVersion);
                }
                else if (btClient == Config.BTClient.Transmission)
                {
                    transmissionClient = new Transmission.API.RPC.Client(
                        string.Format("{0}/transmission/rpc",
                        this.cbUrl.Text.Replace('\\', '/').TrimEnd(new char[] { '/' })),
                        null, this.tbUser.Text, this.tbPassword.Text);
                    var info = transmissionClient.GetSessionInformation();
                    Debug.Assert(info != null && info.Version != null);
                    btClientVersion = "v" + info.Version;
                    flag = true;
                    isVersionValid = (string.Compare(btClientVersion, "v2.8", true) >= 0);
                    if (!isVersionValid)
                        errMsg = string.Format(
                            "Transmission 当前版本为 {0}，需 >=v2.8", btClientVersion);
                }
            }
            catch (Exception ex)
            {
                flag = null;
                errMsg = string.Format("{0}{1}", ex.Message,
                    (ex.InnerException != null) ? ex.InnerException.Message : "");
            }

            if ((flag ?? false) && isVersionValid)
            {
                Uri uri = new Uri(this.cbUrl.Text);
                this.Text = string.Format("[{0} {1}]@{2}:{3} [MyQbt v{4}]",
                    btClient == Config.BTClient.qBittorrent ? "qBittorrent" : "Transmission",
                    btClientVersion, uri.Host, uri.Port, Application.ProductVersion);
                this.groupBoxAdd.Enabled = true;
                this.groupBoxOnlineOther.Enabled = true;
                if (btClient == Config.BTClient.qBittorrent)
                {
                    this.cbSkipHashCheck.Enabled = true;
                }
                else
                {
                    this.cbSkipHashCheck.Checked = false;
                    this.cbSkipHashCheck.Enabled = false;
                }
                this.labelCategory.Enabled = (btClient == Config.BTClient.qBittorrent);
                this.cbCategory.Enabled = (btClient == Config.BTClient.qBittorrent);

                if (btClient == Config.BTClient.qBittorrent) InitComboxCategory();

                if (configData.ConnectList == null)
                    configData.ConnectList = new List<Config.Connect>();

                if (this.cbKeepConnectSetting.Checked)
                {
                    Config.Connect c1 = configData.ConnectList.Find(
                        x => x.Url == this.cbUrl.Text);
                    string encryptedPassword = Helper.Encryption(this.tbPassword.Text);
                    if (c1 == null)
                    {
                        c1 = new Config.Connect
                        {
                            Url = this.cbUrl.Text,
                            User = this.tbUser.Text,
                            Password = encryptedPassword
                        };
                        configData.ConnectList.Add(c1);
                        this.cbUrl.Items.Add(this.cbUrl.Text);
                    }
                    else
                    {
                        c1.User = this.tbUser.Text;
                        c1.Password = encryptedPassword;
                    }
                }
            }
            else
            {
                this.groupBoxAdd.Enabled = false;
                this.groupBoxOnlineOther.Enabled = false;
                MessageBox.Show(errMsg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void BtnAddTorrent_Click(object sender, EventArgs e)
        {
            bool isWindowsPath = this.rbWindows.Checked;
            string settingSaveFolder = this.cbSettingSaveFolder.Text;

            if (!Helper.CheckPath(ref settingSaveFolder, isWindowsPath)) return;
            else this.cbSettingSaveFolder.Text = settingSaveFolder;

            OpenFileDialog dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "torrent file|*.torrent"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                Dictionary<string, string> virtualToActualDic = GetVirtualToActualPathMap();
                Dictionary<string, string> actualToVirtualDic = null;
                if (this.cbPathMap.Checked)
                {
                    actualToVirtualDic = new Dictionary<string, string>();
                    foreach (KeyValuePair<string, string> kv in virtualToActualDic)
                    {
                        if (actualToVirtualDic.ContainsKey(kv.Value))
                        {
                            MessageBox.Show(String.Format("多个路径映射到\n{0}", kv.Value));
                            return;
                        }
                        else actualToVirtualDic.Add(kv.Value, kv.Key);
                    }
                }

                List<string> successList = new List<string>();
                Dictionary<string, string> failedDic = new Dictionary<string, string>();
                BencodeNET.Objects.BString trackersBString =
                    new BencodeNET.Objects.BString("trackers");

                foreach (string torrentPath in dlg.FileNames)
                {
                    var bencodeParser = new BencodeNET.Parsing.BencodeParser();
                    var bencodeTorrent = bencodeParser.Parse<BencodeNET.Torrents.Torrent>(torrentPath);

                    if (bencodeTorrent == null ||
                        bencodeTorrent.FileMode == BencodeNET.Torrents.TorrentFileMode.Unknown)
                    {
                        failedDic.Add(torrentPath, "读取种子文件出错");
                        continue;
                    }

                    string category = this.cbCategory.Text.Trim();

                    if (string.IsNullOrWhiteSpace(category) &&
                        bencodeTorrent.Trackers.Count == 1 &&
                        bencodeTorrent.Trackers[0].Count == 1)
                    {
                        string trackerDomain = (new Uri(bencodeTorrent.Trackers[0][0])).Host;
                        if (this.domainCategoryDic.ContainsKey(trackerDomain))
                        {
                            category = this.domainCategoryDic[trackerDomain];
                        }
                    }

                    if (this.rbAddPrefixWithFileName.Checked)
                    {
                        try
                        {
                            await AddPrefixWithFileName.AddTorrent(
                                torrentPath, bencodeTorrent, settingSaveFolder,
                                this.cbSkipHashCheck.Checked,
                                this.cbStartTorrent.Checked, category,
                                btClient, isWindowsPath, actualToVirtualDic, transmissionClient);

                            successList.Add(torrentPath);
                            UpdataComboxSettingSaveFolder(settingSaveFolder);
                        }
                        catch (Exception ex)
                        {
                            failedDic.Add(torrentPath, ex.Message);
                        }
                    }
                    else if (this.rbManual.Checked)
                    {
                        AddTorrentManual form = new AddTorrentManual(
                            torrentPath, bencodeTorrent, settingSaveFolder,
                            this.cbSkipHashCheck.Checked,
                            this.cbStartTorrent.Checked, category,
                            btClient, isWindowsPath, actualToVirtualDic,
                            transmissionClient, this)
                        {
                            UpdataResultAndReason = this.UpdataManualAddResultAddReason
                        };
                        form.ShowDialog();

                        if (this.isManualAddSuccess)
                        {
                            successList.Add(torrentPath);
                            UpdataComboxSettingSaveFolder(settingSaveFolder);
                        }
                        else failedDic.Add(torrentPath, this.manualAddFailedReason);
                    }
                    else
                    {
                        bool hasRootFolder =
                            (bencodeTorrent.FileMode == BencodeNET.Torrents.TorrentFileMode.Multi);

                        if (this.cbSkipHashCheck.Checked &&
                            (!Helper.CanSkipCheck(bencodeTorrent,
                            Helper.GetVirtualPath(settingSaveFolder, actualToVirtualDic), hasRootFolder)))
                        {
                            failedDic.Add(torrentPath, "跳过哈希检测失败");
                            continue;
                        }

                        try
                        {
                            if (btClient == Config.BTClient.qBittorrent)
                            {
                                await QbtWebAPI.API.DownloadFromDisk(
                                    new List<string>() { torrentPath }, settingSaveFolder,
                                    null, string.IsNullOrWhiteSpace(category) ? null : category,
                                    this.cbSkipHashCheck.Checked, !this.cbStartTorrent.Checked,
                                    null, bencodeTorrent.DisplayName, null, null, null, null);
                            }
                            else if (btClient == Config.BTClient.Transmission)
                            {
                                var addedTorrent = new NewTorrent
                                {
                                    Metainfo = Helper.GetTransmissionTorrentAddMetainfo(torrentPath),
                                    DownloadDirectory = settingSaveFolder,
                                    Paused = (!this.cbStartTorrent.Checked)
                                };
                                var addedTorrentInfo = transmissionClient.TorrentAdd(addedTorrent);
                                Debug.Assert(addedTorrentInfo != null && addedTorrentInfo.ID != 0);
                            }

                            successList.Add(torrentPath);
                            UpdataComboxSettingSaveFolder(settingSaveFolder);
                        }
                        catch (Exception ex)
                        {
                            failedDic.Add(torrentPath, ex.Message);
                        }
                    }
                }

                string strSuccess = string.Format("Success: {0}\n", successList.Count);
                int sdl = successList.Count.ToString().Length;
                for (int i = 0; i < successList.Count; i++)
                {
                    strSuccess += string.Format("{0} {1}\n",
                        (i + 1).ToString().PadLeft(sdl, '0'), successList[i]);
                }

                string strFailed = string.Format("Failed: {0}\n", failedDic.Count);
                int fdl = failedDic.Count.ToString().Length;
                int j = 1;
                foreach (KeyValuePair<string, string> kv in failedDic)
                {
                    strFailed += string.Format("{0} {1}\n{2} {3}\n",
                        j.ToString().PadLeft(fdl, '0'), kv.Key,
                        " ".PadLeft(fdl, ' '), kv.Value);
                    j++;
                }

                string strLog = string.Format("{0}\n{1}", strSuccess, strFailed);
                InfoForm infoForm = new InfoForm("Add Torrents Log", strLog, this);
                infoForm.ShowDialog();
            }
        }

        private async void BtnGetCategoryAllTorrentSavePath_Click(object sender, EventArgs e)
        {
            //string category = this.cbCategory.Text.Trim();
            //if (string.IsNullOrEmpty(category)) category = null;

            //List<QbtWebAPI.Data.Torrent> torrentList =
            //    await QbtWebAPI.API.GetTorrents(QbtWebAPI.Enums.Status.All, category);

            //List<string> rstList = new List<string>();

            //foreach (QbtWebAPI.Data.Torrent torrent in torrentList)
            //{
            //    if (torrent.Category != category) continue;

            //    string strTemp = torrent.Save_Path;
            //    if (!strTemp.ToLower().Contains(torrent.Name.ToLower()))
            //    {
            //        strTemp = Path.Combine(strTemp, torrent.Name);
            //    }
            //    rstList.Add(strTemp);
            //}

            //rstList.Sort();

            //InfoForm infoForm = new InfoForm(
            //    "Get Category All Torrent Save Path Log", string.Join("\n", rstList));
            //infoForm.ShowDialog();
            await GetAllDomainAndCategory();
        }

        private async Task GetAllDomainAndCategory()
        {
            List<QbtWebAPI.Data.Torrent> torrentList =
                await QbtWebAPI.API.GetTorrents(QbtWebAPI.Enums.Status.All);

            Dictionary<string, string> onlineDomainCategoryDic =
                new Dictionary<string, string>();

            foreach (QbtWebAPI.Data.Torrent torrent in torrentList)
            {
                if (torrent.Tracker == null ||
                    torrent.Category == "BT" ||
                    onlineDomainCategoryDic.ContainsKey(torrent.Category)) continue;

                string domain = torrent.Tracker.Host;

                onlineDomainCategoryDic.Add(torrent.Category, domain);
            }

            string strLog = "";
            foreach (KeyValuePair<string, string> kv in onlineDomainCategoryDic)
            {
                strLog += string.Format(
                    "<DomainCategory Domain=\"{0}\" Category=\"{1}\" />\n", kv.Value, kv.Key);
            }

            InfoForm infoForm = new InfoForm("Domain And Category", strLog, this);
            infoForm.ShowDialog();
        }

        private void UpdataComboxSettingSaveFolder(string settingSaveFolder)
        {
            configData.SaveFolderList.Remove(settingSaveFolder);
            configData.SaveFolderList.Insert(0, settingSaveFolder);
            if (configData.SaveFolderList.Count > 30)
                configData.SaveFolderList.RemoveRange(
                    30, configData.SaveFolderList.Count - 30);
            InitComboxSettingSaveFolder();
        }

        private void UpdataManualAddResultAddReason(
            bool isAddTorrentSuccess, string failedReason)
        {
            this.isManualAddSuccess = isAddTorrentSuccess;
            this.manualAddFailedReason = failedReason;
        }

        private string GetActionDirectory()
        {
            string actionDirectory = qBittorrentBackUpFolder;
            bool folderBrowse = false;

            if (Directory.Exists(qBittorrentBackUpFolder))
            {
                DialogResult dg = MessageBox.Show(
                    string.Format(
                        "即将在默认目录\n{0}\n执行操作，确定？",
                        qBittorrentBackUpFolder),
                    "提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (dg == DialogResult.Cancel) return null;
                else if (dg == DialogResult.No) folderBrowse = true;
            }
            else
            {
                DialogResult dg = MessageBox.Show(
                    string.Format(
                        "默认目录\n{0}\n不存在，重新选择目录？",
                        qBittorrentBackUpFolder),
                    "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dg == DialogResult.No) return null;
                else folderBrowse = true;
            }

            if (folderBrowse)
            {
                FolderBrowserDialog dlg = new FolderBrowserDialog();
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    actionDirectory = dlg.SelectedPath;
                }
                else return null;
            }

            return actionDirectory;
        }

        private void BtnTrackerFindAndReplace_Click(object sender, EventArgs e)
        {
            string actionDirectory = GetActionDirectory();
            if (actionDirectory == null) return;

            string category = this.cbFindCategory.Text.Trim();
            string replaceFrom = this.cbTrackerFind.Text.Trim();
            string replaceTo = this.cbTrackerReplace.Text.Trim();

            UpdataComboxTrackFindAndReplace(replaceFrom, replaceTo);

            string[] files = Directory.GetFiles(
                actionDirectory, "*.fastresume", SearchOption.AllDirectories);
            var parser = new BencodeNET.Parsing.BencodeParser();
            BencodeNET.Objects.BString trackersBString =
                new BencodeNET.Objects.BString("trackers");
            BencodeNET.Objects.BString categoryBString =
                new BencodeNET.Objects.BString("qBt-category");

            int count = 0;
            foreach (string filePath in files)
            {
                bool changeFlag = false;
                var bdic = parser.Parse<BencodeNET.Objects.BDictionary>(filePath);

                if ((!string.IsNullOrWhiteSpace(category)) &&
                    category != bdic[categoryBString].ToString())
                    continue;

                BencodeNET.Objects.BList trackersBDic =
                    bdic[trackersBString] as BencodeNET.Objects.BList;
                foreach (BencodeNET.Objects.BObject bo in trackersBDic)
                {
                    BencodeNET.Objects.BList bl = bo as BencodeNET.Objects.BList;
                    for (int i = 0; i < bl.Count; i++)
                    {
                        string str = bl[i].ToString();
                        if (str.IndexOf(replaceFrom, StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            bl[i] = new BencodeNET.Objects.BString(
                                Helper.StringReplaceOnceAndIgnoreCase(str, replaceFrom, replaceTo));
                            changeFlag = true;
                        }
                    }
                }

                if (changeFlag)
                {
                    bdic.EncodeTo(filePath);
                    count++;
                }
            }

            MessageBox.Show(string.Format("修改了 {0} 个种子的Trackers", count));
        }

        private void BtnDiskChange_Click(object sender, EventArgs e)
        {
            string actionDirectory = GetActionDirectory();
            if (actionDirectory == null) return;

            bool checkTorrentNeverStart = this.cbTorrentNeverStart.Checked;
            List<string> logList = new List<string>();
            string diskFrom = this.cbSavePathFind.Text;
            string diskTo = this.cbSavePathReplace.Text;

            UpdataComboxSavePathFindAndReplace(diskFrom, diskTo);

            string[] files = Directory.GetFiles(
                actionDirectory, "*.fastresume", SearchOption.AllDirectories);
            var parser = new BencodeNET.Parsing.BencodeParser();

            BencodeNET.Objects.BString s1BString =
                new BencodeNET.Objects.BString("qBt-savePath");
            BencodeNET.Objects.BString qnameBString =
                new BencodeNET.Objects.BString("qBt-name");
            BencodeNET.Objects.BString s2BString =
                new BencodeNET.Objects.BString("save_path");
            BencodeNET.Objects.BString s3BString =
                new BencodeNET.Objects.BString("active_time");
            BencodeNET.Objects.BString qhrfBString =
                new BencodeNET.Objects.BString("qBt-hasRootFolder");

            foreach (string filePath in files)
            {
                string torrentFilePath = filePath.Replace(".fastresume", ".torrent");
                if (!File.Exists(torrentFilePath)) continue;

                bool changeFlag = false;
                var bdic = parser.Parse<BencodeNET.Objects.BDictionary>(filePath);

                if (checkTorrentNeverStart && bdic[s3BString].ToString() != "0") continue;

                string s1 = bdic[s1BString].ToString();
                string s2 = bdic[s2BString].ToString();
                if (s1.IndexOf(diskFrom, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    bdic[s1BString] =
                        new BencodeNET.Objects.BString(
                            Helper.StringReplaceOnceAndIgnoreCase(s1, diskFrom, diskTo));
                    changeFlag = true;
                }
                if (s2.IndexOf(diskFrom, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    bdic[s2BString] =
                        new BencodeNET.Objects.BString(
                            Helper.StringReplaceOnceAndIgnoreCase(s2, diskFrom, diskTo));
                    changeFlag = true;
                }

                if (changeFlag)
                {
                    bdic.EncodeTo(filePath);

                    var bencodeTorrent = parser.Parse<BencodeNET.Torrents.Torrent>(torrentFilePath);

                    string detectPath = bdic[s1BString].ToString();
                    bool hasRootFolder = (bdic[qhrfBString].ToString() == "1" ? true : false);

                    if (hasRootFolder)
                    {
                        string qbtName = bdic[qnameBString].ToString();
                        if (qbtName == "")
                            detectPath = Path.Combine(detectPath, bencodeTorrent.DisplayName);
                        else
                            detectPath = Path.Combine(detectPath, qbtName);
                    }
                    else if (bencodeTorrent.FileMode == BencodeNET.Torrents.TorrentFileMode.Single)
                    {
                        detectPath = Path.Combine(detectPath, bencodeTorrent.DisplayName);
                    }

                    logList.Add(string.Format("{0} {1}",
                        Path.GetFileNameWithoutExtension(filePath),
                        detectPath));
                }
            }

            string strLog = string.Format("Disk Change Count: {0}, {1}\n",
                logList.Count,
                checkTorrentNeverStart ? "On The Torrent Never Start" : "On All Torrent");
            int digitLen = logList.Count.ToString().Length;
            for (int i = 0; i < logList.Count; i++)
            {
                strLog += string.Format("{0} {1}\n",
                    (i + 1).ToString().PadLeft(digitLen, '0'), logList[i]);
            }

            InfoForm form = new InfoForm("Disk Change Log", strLog, this);
            form.ShowDialog();
        }

        private void BtnRemoveNotExistDataFileTorrent_Click(object sender, EventArgs e)
        {
            string actionDirectory = GetActionDirectory();
            if (actionDirectory == null) return;

            string[] files = Directory.GetFiles(
                actionDirectory, "*.fastresume", SearchOption.AllDirectories);

            var parser = new BencodeNET.Parsing.BencodeParser();
            //BencodeNET.Objects.BString categoryBString =
            //    new BencodeNET.Objects.BString("qBt-category");
            BencodeNET.Objects.BString qnameBString =
                new BencodeNET.Objects.BString("qBt-name");
            BencodeNET.Objects.BString qspBString =
                new BencodeNET.Objects.BString("qBt-savePath");
            BencodeNET.Objects.BString qhrfBString =
                new BencodeNET.Objects.BString("qBt-hasRootFolder");
            BencodeNET.Objects.BString ctBString =
                new BencodeNET.Objects.BString("completed_time");

            Dictionary<string, string> removeDic = new Dictionary<string, string>();

            foreach (string filePath in files)
            {
                string torrentFilePath = filePath.Replace(".fastresume", ".torrent");
                if (!File.Exists(torrentFilePath))
                {
                    File.Move(filePath,
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(filePath)));
                    continue;
                }

                var bdic = parser.Parse<BencodeNET.Objects.BDictionary>(filePath);
                if (bdic[ctBString].ToString() == "0") continue;

                var bencodeTorrent = parser.Parse<BencodeNET.Torrents.Torrent>(torrentFilePath);

                string detectPath = bdic[qspBString].ToString();
                bool hasRootFolder = (bdic[qhrfBString].ToString() == "1" ? true : false);

                if (hasRootFolder)
                {
                    string qbtName = bdic[qnameBString].ToString();
                    if (qbtName == "")
                        detectPath = Path.Combine(detectPath, bencodeTorrent.DisplayName);
                    else
                        detectPath = Path.Combine(detectPath, qbtName);
                }
                else if (bencodeTorrent.FileMode == BencodeNET.Torrents.TorrentFileMode.Single)
                {
                    detectPath = Path.Combine(detectPath, bencodeTorrent.DisplayName);
                }

                if (Directory.Exists(detectPath) || File.Exists(detectPath)) continue;
                else removeDic.Add(Path.GetFileNameWithoutExtension(filePath), detectPath);
            }

            string strLog = string.Format("Removed Torrent Count: {0}\n", removeDic.Count);
            if (removeDic.Count > 0)
            {
                string backUpDirectory =
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    DateTime.Now.ToString("yyyyMMddHHmmss"));
                Directory.CreateDirectory(backUpDirectory);

                int i = 1;
                int digitLen = removeDic.Count.ToString().Length;

                foreach (KeyValuePair<string, string> kv in removeDic)
                {

                    File.Move(Path.Combine(actionDirectory, kv.Key + ".fastresume"),
                        Path.Combine(backUpDirectory, kv.Key + ".fastresume"));
                    File.Move(Path.Combine(actionDirectory, kv.Key + ".torrent"),
                            Path.Combine(backUpDirectory, kv.Key + ".torrent"));

                    strLog += string.Format("{0} {1} {2}\n",
                        (i++).ToString().PadLeft(digitLen, '0'), kv.Key, kv.Value);
                }
            }

            InfoForm form = new InfoForm(
                "Remove Torrent Whose Data File Not Exist Log", strLog, this);
            form.ShowDialog();
        }

        private void ButtonOther_Click(object sender, EventArgs e)
        {
            RemoveAllCategoryTorrents();
        }

        private void RemoveAllCategoryTorrents()
        {
            string[] categorys = new string[] {
                "DicMusic", "OpenCD", "Orpheus", "Redacted" };

            string actionDirectory = GetActionDirectory();
            if (actionDirectory == null) return;

            string[] fastresumeFiles = Directory.GetFiles(
                actionDirectory, "*.fastresume", SearchOption.AllDirectories);

            var parser = new BencodeNET.Parsing.BencodeParser();
            BencodeNET.Objects.BString categoryBString =
                new BencodeNET.Objects.BString("qBt-category");
            BencodeNET.Objects.BString qspBString =
                new BencodeNET.Objects.BString("qBt-savePath");
            BencodeNET.Objects.BString ctBString =
                new BencodeNET.Objects.BString("completed_time");

            string strTime = DateTime.Now.ToString("yyyyMMddhhmmss");

            foreach (string fastresumeFile in fastresumeFiles)
            {
                string torrentFilePath = fastresumeFile.Replace(".fastresume", ".torrent");
                if (!File.Exists(torrentFilePath))
                {
                    File.Move(fastresumeFile,
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(fastresumeFile)));
                    continue;
                }

                var bdic = parser.Parse<BencodeNET.Objects.BDictionary>(fastresumeFile);
                if (bdic[ctBString].ToString() == "0") continue;

                string category = bdic[categoryBString].ToString();
                if (Array.IndexOf(categorys, category) == -1) continue;

                string categoryFolder = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, strTime, category);
                if (category == "Redacted" &&
                    bdic[qspBString].ToString().StartsWith("H:", StringComparison.OrdinalIgnoreCase))
                {
                    categoryFolder = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, strTime, "Redacted_H");
                }
                Directory.CreateDirectory(categoryFolder);

                File.Move(fastresumeFile, Path.Combine(
                    categoryFolder, Path.GetFileName(fastresumeFile)));
                File.Move(torrentFilePath, Path.Combine(
                    categoryFolder, Path.GetFileName(torrentFilePath)));
            }

            MessageBox.Show("移动完成");
        }
    }
}
