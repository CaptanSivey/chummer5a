using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using TheArtOfDev.HtmlRenderer.WinForms;
using TreeView = Chummer.helpers.TreeView;

namespace Chummer
{
    public partial class frmCharacterRoster : Form
    {
        List<CharacterCache> lstCharacterCache = new List<CharacterCache>();
        HtmlToolTip tipTooltip = new HtmlToolTip();

        public frmCharacterRoster()
        {
            InitializeComponent();
            LanguageManager.Load(GlobalOptions.Language, this);

            GlobalOptions.MRUChanged += MruChanged;
            treCharacterList.ItemDrag += treCharacterList_ItemDrag;
            treCharacterList.DragEnter += treCharacterList_DragEnter;
            treCharacterList.DragDrop += treCharacterList_DragDrop;
            treCharacterList.DragOver += treCharacterList_DragOver;
            LoadCharacters();
            MoveControls();
        }

        private void MruChanged()
        {
            //TODO: Cheaper way to do this than rebuilding the list every time?
            treCharacterList.Nodes.Clear();
            LoadCharacters();
            MoveControls();
        }


        private void LoadCharacters()
        {
            TreeNode objFavouriteNode = new TreeNode(LanguageManager.GetString("Treenode_Roster_FavouriteCharacters")) {Tag = "Favourite"};
            TreeNode objRecentNode = new TreeNode(LanguageManager.GetString("Treenode_Roster_RecentCharacters")) {Tag = "Recent"};
            TreeNode objWatchNode = new TreeNode(LanguageManager.GetString("Treenode_Roster_WatchFolder")) {Tag = "Watch"};
            bool blnAddRecent = true;
            bool blnAddFavourite = true;
            bool blnAddWatch = true;
            foreach (string strFile in GlobalOptions.ReadMRUList("stickymru").Where(File.Exists) )
            {
                if (blnAddFavourite)
                {
                    treCharacterList.Nodes.Add(objFavouriteNode);

                    blnAddFavourite = false;
                }
                CacheCharacter(strFile, objFavouriteNode);
            }

            foreach (string strFile in GlobalOptions.ReadMRUList().Where(File.Exists))
            {
                if (blnAddRecent)
                {
                    treCharacterList.Nodes.Add(objRecentNode);
                    blnAddRecent = false;
                }
                CacheCharacter(strFile, objRecentNode);
            }

            if (!string.IsNullOrEmpty(GlobalOptions.CharacterRosterPath) && Directory.Exists(GlobalOptions.CharacterRosterPath))
            {
                string[] objFiles = Directory.GetFiles(GlobalOptions.CharacterRosterPath);
                //Make sure we're not loading a character that was already loaded by the MRU list.
                if (objFiles.Length > 0)
                {
                    foreach (string strFile in objFiles.Where(strFile => strFile.EndsWith(".chum5")))
                    {
                        var cache = lstCharacterCache.FirstOrDefault(objCache => objCache.FilePath == strFile);
                        if (cache != null)
                        {
                            foreach (TreeNode rootNode in treCharacterList.Nodes)
                            {
                                if (rootNode.Nodes.Cast<TreeNode>().Any(childNode => Convert.ToInt32(childNode.Tag) == lstCharacterCache.IndexOf(cache)))
                                {
                                    goto Loaded;
                                }
                            }
                        }
                        if (blnAddWatch)
                        {
                            treCharacterList.Nodes.Add(objWatchNode);
                            blnAddWatch = false;
                        }
                        CacheCharacter(strFile, objWatchNode);
                        Loaded:;
                    }
                }
            }
            treCharacterList.ExpandAll();
        }
        /// <summary>
        /// Generates a character cache, which prevents us from repeatedly loading XmlNodes or caching a full character.
        /// </summary>
        /// <param name="strFile"></param>
        /// <param name="objParentNode"></param>
        private void CacheCharacter(string strFile, TreeNode objParentNode)
        {
            if (!File.Exists(strFile))
                return;
            XmlDocument objXmlSource = new XmlDocument();
            // If we run into any problems loading the character cache, fail out early.
            try
            {
                using (StreamReader sr = new StreamReader(strFile, true))
                {
                    objXmlSource.Load(sr);
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (XmlException)
            {
                return;
            }
            CharacterCache objCache = new CharacterCache();
            XmlNode objXmlSourceNode = objXmlSource.SelectSingleNode("/character");
            if (objXmlSourceNode != null)
            {
                objCache.Description = objXmlSourceNode["description"]?.InnerText;
                objCache.BuildMethod = objXmlSourceNode["buildmethod"]?.InnerText;
                objCache.Background = objXmlSourceNode["background"]?.InnerText;
                objCache.CharacterNotes = objXmlSourceNode["notes"]?.InnerText;
                objCache.GameNotes = objXmlSourceNode["gamenotes"]?.InnerText;
                objCache.Concept = objXmlSourceNode["concept"]?.InnerText;
                objCache.Karma = objXmlSourceNode["totalkarma"]?.InnerText;
                objCache.Metatype = objXmlSourceNode["metatype"]?.InnerText;
                objCache.PlayerName = objXmlSourceNode["player"]?.InnerText;
                objCache.CharacterName = objXmlSourceNode["name"]?.InnerText;
                objCache.CharacterAlias = objXmlSourceNode["alias"]?.InnerText;
                objCache.Created = Convert.ToBoolean(objXmlSourceNode["created"]?.InnerText);
                objCache.Essence = objXmlSourceNode["totaless"]?.InnerText;
                string strMugshotBase64 = objXmlSourceNode["mugshot"]?.InnerText;
                if (!string.IsNullOrEmpty(strMugshotBase64))
                {
                    objCache.Mugshot = strMugshotBase64.ToImage();
                }
                else
                {
                    int intMainMugshotIndex = 0;
                    objXmlSourceNode.TryGetInt32FieldQuickly("mainmugshotindex", ref intMainMugshotIndex);
                    XmlNodeList objXmlMugshotsList = objXmlSourceNode.SelectNodes("mugshots/mugshot");
                    List<string> lstMugshots = (from XmlNode objXmlMugshot in objXmlMugshotsList where !string.IsNullOrWhiteSpace(objXmlMugshot.InnerText) select objXmlMugshot.InnerText).ToList();
                    if (intMainMugshotIndex >= lstMugshots.Count)
                        intMainMugshotIndex = 0;
                    else if (intMainMugshotIndex < 0)
                        intMainMugshotIndex = lstMugshots.Count - 1;

                    if (lstMugshots.Count > 0)
                        objCache.Mugshot = lstMugshots.ElementAt(intMainMugshotIndex).ToImage();
                    else
                        objCache.Mugshot = null;
                }
            }
            objCache.FilePath = strFile;
            objCache.FileName = strFile.Split('\\').ToArray().Last();
            lstCharacterCache.Add(objCache);
            TreeNode objNode = new TreeNode();
            objNode.Tag = lstCharacterCache.IndexOf(objCache);

            objNode.Text = CalculatedName(objCache);
            objNode.ToolTipText = objCache.FilePath;
            objParentNode.Nodes.Add(objNode);
        }

        /// <summary>
        /// Generates a name for the treenode based on values contained in the CharacterCache object. 
        /// </summary>
        /// <param name="objCache"></param>
        /// <returns></returns>
        private static string CalculatedName(CharacterCache objCache)
        {
            string strName = objCache.CharacterAlias;
            if (string.IsNullOrEmpty(strName))
            {
                strName = string.IsNullOrEmpty(objCache.CharacterName) ? LanguageManager.GetString("String_UnnamedCharacter") : objCache.CharacterName;
            }
            string strBuildMethod = LanguageManager.GetString("String_"+objCache.BuildMethod) ?? "Unknown build method";
            bool blnCreated = objCache.Created;
            string strCreated = LanguageManager.GetString(blnCreated ? "Title_CareerMode" : "Title_CreateMode");
            string strReturn = $"{strName} ({strBuildMethod} - {strCreated})";
            return strReturn;
        }

        /// <summary>
        /// Update the labels and images based on the selected treenode.
        /// </summary>
        /// <param name="objCache"></param>
        private void UpdateCharacter(CharacterCache objCache)
        {
            txtCharacterBio.Text = objCache.Description;
            txtCharacterBackground.Text = objCache.Background;
            txtCharacterNotes.Text = objCache.CharacterNotes;
            txtGameNotes.Text = objCache.GameNotes;
            txtCharacterConcept.Text = objCache.Concept;
            lblCareerKarma.Text = objCache.Karma;
            lblMetatype.Text = objCache.Metatype;
            lblPlayerName.Text = objCache.PlayerName;
            lblCharacterName.Text = objCache.CharacterName;
            lblCharacterAlias.Text = objCache.CharacterAlias;
            lblEssence.Text = objCache.Essence;
            lblFilePath.Text = objCache.FileName;
            tipTooltip.SetToolTip(lblFilePath,objCache.FilePath);
            picMugshot.Image = objCache.Mugshot;
            picMugshot_SizeChanged(null, EventArgs.Empty);
        }

        #region Form Methods

        private void MoveControls()
        {
            int intWidth = 0;
            int intMargin = 0;
            foreach (TreeNode objNode in treCharacterList.Nodes)
            {
                intMargin = Math.Max(intMargin, objNode.Bounds.Left);
                intWidth =
                    (from TreeNode objChildNode in objNode.Nodes select objChildNode.Bounds.Right).Concat(new[] { intWidth })
                    .Max();
            }
            intWidth += intMargin;

            int intDifference = intWidth - treCharacterList.Width;
            treCharacterList.Width = intWidth;
            tabCharacterText.Left = treCharacterList.Width + 12;
            tabCharacterText.Width -= intDifference;

            lblPlayerNameLabel.Left = tabCharacterText.Left;
            lblCharacterNameLabel.Left = tabCharacterText.Left;
            lblCareerKarmaLabel.Left = tabCharacterText.Left;
            lblMetatypeLabel.Left = tabCharacterText.Left;
            lblCharacterAliasLabel.Left = tabCharacterText.Left;
            lblEssenceLabel.Left = tabCharacterText.Left;
            lblFilePathLabel.Left = tabCharacterText.Left;
            intWidth = lblPlayerNameLabel.Right;
            if (lblCareerKarmaLabel.Right > intWidth)
            {
                intWidth = lblCareerKarmaLabel.Right;
            }
            if (lblCareerKarmaLabel.Right > intWidth)
            {
                intWidth = lblCareerKarmaLabel.Right;
            }
            if (lblMetatypeLabel.Right > intWidth)
            {
                intWidth = lblMetatypeLabel.Right;
            }
            if (lblCharacterAliasLabel.Right > intWidth)
            {
                intWidth = lblCharacterAliasLabel.Right;
            }
            if (lblEssenceLabel.Right > intWidth)
            {
                intWidth = lblEssenceLabel.Right;
            }
            if (lblFilePathLabel.Right > intWidth)
            {
                intWidth = lblFilePathLabel.Right;
            }
            intWidth += 12;
            lblEssence.Left = intWidth;
            lblPlayerName.Left = intWidth;
            lblCareerKarma.Left = intWidth;
            lblCharacterAlias.Left = intWidth;
            lblMetatype.Left = intWidth;
            lblCharacterName.Left = intWidth;
            lblFilePath.Left = intWidth;
        }

        private void treCharacterList_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (treCharacterList.SelectedNode.Level > 0)
            {
                CharacterCache objCache = lstCharacterCache[Convert.ToInt32(treCharacterList.SelectedNode.Tag)];
                UpdateCharacter(objCache);
            }
            treCharacterList.ClearNodeBackground(treCharacterList.SelectedNode);
        }

        private void treCharacterList_DoubleClick(object sender, EventArgs e)
        {
            if (treCharacterList.SelectedNode.Level > 0)
            {
                CharacterCache objCache = lstCharacterCache[Convert.ToInt32(treCharacterList.SelectedNode.Tag)];
                GlobalOptions.MainForm.LoadCharacter(objCache.FilePath);
            }
        }
        private void treCharacterList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && treCharacterList.SelectedNode?.Level > 0)
            {
                RemoveSelected(treCharacterList.SelectedNode);
            }
        }

        private void treCharacterList_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void treCharacterList_DragOver(object sender, DragEventArgs e)
        {
            TreeView treSenderView = sender as TreeView;
            if (treSenderView == null)
                return;
            Point pt = treSenderView.PointToClient(new Point(e.X, e.Y));
            TreeNode objNode = treSenderView.GetNodeAt(pt).Parent;
            if (objNode.Tag.ToString() != "Watch")
            {
                objNode.BackColor = SystemColors.ControlDark;
            }

            // Clear the background colour for all other Nodes.
            treCharacterList.ClearNodeBackground(objNode);
        }
        private void treCharacterList_DragDrop(object sender, DragEventArgs e)
        {
            // Do not allow the root element to be moved.
            if (treCharacterList.SelectedNode == null || treCharacterList.SelectedNode.Level == 0 || treCharacterList.SelectedNode.Parent.Tag.ToString() == "Watch")
                return;

            if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
            {
                TreeView treSenderView = sender as TreeView;
                if (treSenderView == null)
                    return;
                Point pt = treSenderView.PointToClient(new Point(e.X, e.Y));
                TreeNode nodDestinationNode = treSenderView.GetNodeAt(pt);
                if (nodDestinationNode.Level > 0)
                    nodDestinationNode = nodDestinationNode.Parent;
                if (nodDestinationNode.Tag.ToString() != "Watch")
                {
                    TreeNode nodNewNode = e.Data.GetData("System.Windows.Forms.TreeNode") as TreeNode;
                    int intCharacterIndex;
                    CharacterCache objCache = null;
                    if (nodNewNode == null)
                        return;
                    if (int.TryParse(nodNewNode.Tag.ToString(), out intCharacterIndex) && intCharacterIndex >= 0 && intCharacterIndex < lstCharacterCache.Count)
                        objCache = lstCharacterCache[intCharacterIndex];

                    if (objCache == null)
                        return;
                    switch (nodDestinationNode.Tag.ToString())
                    {
                        case "Recent":
                            GlobalOptions.RemoveFromMRUList(objCache.FilePath, "stickymru");
                            GlobalOptions.AddToMRUList(objCache.FilePath);
                            break;
                        case "Favourite":
                            GlobalOptions.RemoveFromMRUList(objCache.FilePath);
                            GlobalOptions.AddToMRUList(objCache.FilePath, "stickymru");
                            break;
                        default:
                            return;
                    }
                    TreeNode nodClonedNewNode = nodNewNode.Clone() as TreeNode;
                    if (nodClonedNewNode != null)
                    {
                        nodDestinationNode.Nodes.Add(nodClonedNewNode);
                        nodDestinationNode.Expand();
                    }
                    nodNewNode.Remove();
                }
            }
        }

        private void treCharacterList_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void RemoveSelected(TreeNode sender)
        {
            CharacterCache objCache = lstCharacterCache[Convert.ToInt32(treCharacterList.SelectedNode.Tag)];
            GlobalOptions.RemoveFromMRUList(objCache.FilePath);
            GlobalOptions.RemoveFromMRUList(objCache.FilePath, "stickymru");
            sender.Remove();
        }

        private void picMugshot_SizeChanged(object sender, EventArgs e)
        {
            if (picMugshot.Image != null && picMugshot.Height >= picMugshot.Image.Height && picMugshot.Width >= picMugshot.Image.Width)
                picMugshot.SizeMode = PictureBoxSizeMode.CenterImage;
            else
                picMugshot.SizeMode = PictureBoxSizeMode.Zoom;
        }
        #endregion
        #region Classes
        /// <summary>
        /// Caches a subset of a full character's properties for loading purposes. 
        /// </summary>
        private class CharacterCache
        {
            internal string FilePath { get; set; }
            internal string FileName { get; set; }
            internal string Description { get; set; }
            internal string Background { get; set; }
            internal string GameNotes { get; set; }
            internal string CharacterNotes { get; set; }
            internal string Concept { get; set; }
            internal string Karma { get; set; }
            internal string Metatype { get; set; }
            internal string PlayerName { get; set; }
            internal string CharacterName { get; set; }
            internal string CharacterAlias { get; set; }
            internal Image Mugshot { get; set; }
            public string BuildMethod { get; internal set; }
            public bool Created { get; internal set; }
            public string Essence { get; internal set; }
        }
        #endregion
    }
}
