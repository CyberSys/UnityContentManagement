using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class ContentDatabaseEditor : EditorWindow
{
    private static ContentDatabaseEditor Window;

    [MenuItem("Content Management/Open", priority = 2056)]
    static void ShowWindow()
    {
        Window = (ContentDatabaseEditor)GetWindow(typeof(ContentDatabaseEditor), true);
        Window.Show();
        Window.titleContent = new GUIContent("Content Management");
        Window.minSize = new Vector2(640, 480);
    }

    ContentDatabase db;

    private void OnEnable()
    {
        db = ContentDatabase.Get();
        Window = this;
    }

    void OnFocus()
    {
        m_AssetsTree?.Reload();
    }


    ContentDatabaseTreeView m_AssetsTree;
    TreeViewState m_AssetsTreeState;
    SearchField m_AssetsSearchField;

    private void OnGUI()
    {
        if (db)
        {
            titleContent = new GUIContent($"Content Management | Groups: {db.GetContentInfo().Groups.Count}");
        }

        int border = 4;
        int topPadding = 12;
        int searchHeight = 20;
        Rect rect = new Rect(0, searchHeight + topPadding, position.width, position.height);

        {
            var searchRect = new Rect(border, topPadding, rect.width - border * 2, searchHeight);
            var remainTop = topPadding + searchHeight + border;
            var remainingRect = new Rect(border, topPadding + searchHeight + border, rect.width - border * 2, rect.height - remainTop - border);

            if (m_AssetsSearchField == null)
            {
                m_AssetsSearchField = new SearchField();
                m_AssetsSearchField.SetFocus();
                m_AssetsSearchField.downOrUpArrowKeyPressed += () => { m_AssetsTree.SetFocus(); };
            }

            if (m_AssetsTree == null)
            {
                if (m_AssetsTreeState == null) { m_AssetsTreeState = new TreeViewState(); }

                var headerState = ContentDatabaseTreeView.GetColumns();
                m_AssetsTree = new ContentDatabaseTreeView(m_AssetsTreeState, headerState);
                m_AssetsTree.Reload();
            }
            else
            {
                m_AssetsTree.searchString = m_AssetsSearchField.OnGUI(searchRect, m_AssetsTree.searchString);
                m_AssetsTree.OnGUI(rect);
            }
        }
    }

    /* ----------------------------------------------------------------- */

    public sealed class ContentDatabaseGroupTreeViewItem : TreeViewItem
    {
        public ContentDatabase.ContentInfo.Group group;
        private static Texture2D EmptyIcon;
        private static Texture2D FilledIcon;
        private static Texture2D NullIcon;

        private static void CacheIcon()
        {
            if (!EmptyIcon) { EmptyIcon = EditorGUIUtility.IconContent("d_FolderEmpty Icon").image as Texture2D; }
            if (!FilledIcon) { FilledIcon = EditorGUIUtility.IconContent("d_Folder Icon").image as Texture2D; }
            if (!NullIcon) { NullIcon = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D; }
        }

        public void UpdateIcon()
        {
            CacheIcon();

            if (group != null)
            {
                if (group.Assets.Count > 0)
                {
                    icon = FilledIcon;
                }
                else
                {
                    icon = EmptyIcon;
                }
            }
            else
            {
                icon = NullIcon;
            }
        }

        public ContentDatabaseGroupTreeViewItem(int id, ContentDatabase.ContentInfo.Group group) : base(id, 0, group.Name)
        {
            this.group = group;
            UpdateIcon();
        }
    }

    public sealed class ContentDatabaseAssetInfoTreeViewItem : TreeViewItem
    {
        public AssetInfo asset;

        public long size { get; private set; } = 0;

        public void UpdateSize()
        {
            try
            {
                size = new System.IO.FileInfo(asset.path).Length;
            }
            catch { }
        }

        public ContentDatabaseAssetInfoTreeViewItem(ContentDatabaseGroupTreeViewItem group, AssetInfo info) : base(info.guid.GetHashCode(), 1, info.name)
        {
            parent = group;
            asset = info;
            icon = AssetDatabase.GetCachedIcon(info.path) as Texture2D;
            UpdateSize();
        }
    }

    public class ContentDatabaseTreeView : TreeView
    {
        public ContentDatabaseTreeView(TreeViewState state, MultiColumnHeaderState headerState) : base(state, new MultiColumnHeader(headerState))
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
        }
        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return true;
        }

        private bool m_IsMultiSelected = false;
        private IList<int> selectedIds;

        protected override void SelectionChanged(IList<int> ids)
        {
            selectedIds = ids;
            m_IsMultiSelected = selectedIds.Count > 1;

            if (m_IsMultiSelected)
            {
                SetFocus();
                return;
            }

            if (selectedIds != null && selectedIds.Count == 1)
            {
                var element = FindItem(selectedIds[0], rootItem) as ContentDatabaseAssetInfoTreeViewItem;

                if (element != null)
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(element.asset.path);

                    if (asset)
                    {
                        EditorGUIUtility.PingObject(asset);
                    }
                }

                SetFocus();
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            var element = FindItem(id, rootItem);

            if (element is ContentDatabaseAssetInfoTreeViewItem)
            {
                var element_asset = (element as ContentDatabaseAssetInfoTreeViewItem);
                if (element_asset.asset != null)
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(element_asset.asset.path);
                    EditorGUIUtility.PingObject(obj);
                }
            }
        }

        protected override TreeViewItem BuildRoot()
        {
            var Root = new TreeViewItem(-1, -1, "Root");

            if (ContentDatabase.Get().GetContentInfo().Groups.Count > 0)
            {
                for (int i = 0; i < ContentDatabase.Get().GetContentInfo().Groups.Count; i++)
                {
                    var group = ContentDatabase.Get().GetContentInfo().Groups[i];
                    var group_item = new ContentDatabaseGroupTreeViewItem(i, group);
                    Root.AddChild(group_item);

                    for (int j = 0; j < group.Assets.Count; j++)
                    {
                        var asset = group.Assets[j];
                        var asset_item = new ContentDatabaseAssetInfoTreeViewItem(group_item, asset);
                        group_item.AddChild(asset_item);
                    }
                }
            }
            else
            {
                Root.AddChild(new TreeViewItem() { id = Random.Range(0, int.MaxValue), displayName = "No groups!" });
            }

            return Root;
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            args.draggedItemIDs = GetSelection();

            foreach (var id in args.draggedItemIDs)
            {
                var element = FindItem(id, rootItem);

                if (element is ContentDatabaseGroupTreeViewItem)
                {
                    return false;
                }
            }

            return true;
        }

        private bool m_DraggingIntoAnotherGroup = false;

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            DragAndDrop.PrepareStartDrag();

            for(var i = 0; i < args.draggedItemIDs.Count; i++)
            {
                if(FindItem(args.draggedItemIDs[i], rootItem) is ContentDatabaseGroupTreeViewItem)
                {
                    return;
                }
            }

            var selected = args.draggedItemIDs.Select(id => FindItem(id, rootItem) as ContentDatabaseAssetInfoTreeViewItem);

            var items = new List<ContentDatabaseAssetInfoTreeViewItem>(selected);

            DragAndDrop.paths = items.Select(a => a.asset.path).ToArray();

            DragAndDrop.visualMode = items.Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            m_DraggingIntoAnotherGroup = items.Count > 0;
            DragAndDrop.StartDrag("DraggingAssetsIntoGroup");
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            var paths = DragAndDrop.paths;

            if (!m_DraggingIntoAnotherGroup)
            {
                if (args.performDrop)
                {
                    if (args.parentItem == null)
                    {
                        if (paths.Length == 1)
                        {
                            ContentDatabase.AssetError error = ContentDatabase.AssetError.Unknown;
                            Object asset = AssetDatabase.LoadMainAssetAtPath(paths[0]);
                            ContentDatabase.ContentInfo.Group group = ContentDatabase.Get().GetContentInfo().AddGroupOrFind(asset.name);

                            error = ContentDatabase.AddAsset(group, asset);
                            if (error == ContentDatabase.AssetError.NoError)
                            {
                                Window.ShowNotification(new GUIContent($"{asset.name} added in {group.Name}"));
                            }
                            else
                            {
                                Window.ShowNotification(new GUIContent($"{asset.name} asset add failed! {error}"));
                            }
                        }
                        else if (paths.Length > 1)
                        {
                            var group = ContentDatabase.Get().GetContentInfo().AddGroupOrFind("Shared");
                            int added = 0;
                            foreach (var path in paths)
                            {
                                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                                if (ContentDatabase.AddAsset(group, asset) == ContentDatabase.AssetError.NoError)
                                {
                                    added++;
                                }
                            }

                            if (added > 0)
                            {
                                Window.ShowNotification(new GUIContent($"Added {added} assets in {group.Name}"));
                            }
                        }
                    }
                    else if (args.parentItem is ContentDatabaseGroupTreeViewItem)
                    {
                        if (paths.Length == 1)
                        {
                            var group_item = args.parentItem as ContentDatabaseGroupTreeViewItem;
                            ContentDatabase.AssetError error = ContentDatabase.AssetError.Unknown;
                            Object asset = AssetDatabase.LoadMainAssetAtPath(paths[0]);
                            error = ContentDatabase.AddAsset(group_item.group, asset);
                            if (error == ContentDatabase.AssetError.NoError)
                            {
                                Window.ShowNotification(new GUIContent($"{asset.name} added in {group_item.group.Name}"));
                            }
                            else if (error == ContentDatabase.AssetError.GroupOnlyForScenes)
                            {
                                Window.ShowNotification(new GUIContent($"Group {group_item.displayName} group can only contain scenes"));
                            }
                            else
                            {
                                Window.ShowNotification(new GUIContent($"{asset.name} adding failed! error: {error}"));
                            }
                        }
                        else if (paths.Length > 1)
                        {
                            var group_item = args.parentItem as ContentDatabaseGroupTreeViewItem;
                            int added = 0;
                            foreach (var path in paths)
                            {
                                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                                var error = ContentDatabase.AddAsset(group_item.group, asset);
                                if (error == ContentDatabase.AssetError.NoError)
                                {
                                    added++;
                                }
                            }

                            if (added > 0)
                            {
                                Window.ShowNotification(new GUIContent($"Added {added} assets in {group_item.group.Name}"));
                            }
                        }
                    }
                }
            }
            else //moving assets
            {
                if (args.performDrop)
                {
                    if (paths.Length == 1)
                    {
                        var group_item = args.parentItem as ContentDatabaseGroupTreeViewItem;

                        var group = group_item != null ? group_item.group : ContentDatabase.Get().GetContentInfo().AddGroupOrFind("Shared");

                        ContentDatabase.AssetError error = ContentDatabase.AssetError.Unknown;
                        Object asset = AssetDatabase.LoadMainAssetAtPath(paths[0]);
                        ContentDatabase.RemoveAsset(asset);
                        error = ContentDatabase.AddAsset(group, asset);
                        if (error == ContentDatabase.AssetError.NoError)
                        {
                            Window.ShowNotification(new GUIContent($"{asset.name} moved in {group_item.group.Name}"));
                        }
                        else if (error == ContentDatabase.AssetError.GroupOnlyForScenes)
                        {
                            Window.ShowNotification(new GUIContent($"Group {group_item.displayName} group can only contain scenes"));
                        }
                        else
                        {
                            Window.ShowNotification(new GUIContent($"{asset.name} moving failed! error: {error}"));
                        }
                    }
                    else if (paths.Length > 1)
                    {
                        var group_item = args.parentItem as ContentDatabaseGroupTreeViewItem;

                        var group = group_item != null ? group_item.group : ContentDatabase.Get().GetContentInfo().AddGroupOrFind("Shared");

                        int added = 0;
                        foreach (var path in paths)
                        {
                            var asset = AssetDatabase.LoadMainAssetAtPath(path);
                            ContentDatabase.RemoveAsset(asset);
                            var error = ContentDatabase.AddAsset(group, asset);
                            if (error == ContentDatabase.AssetError.NoError)
                            {
                                added++;
                            }
                        }

                        if (added > 0)
                        {
                            Window.ShowNotification(new GUIContent($"{added} assets moved in {group.Name}"));
                        }
                    }
                }
            }

            Reload();

            return DragAndDropVisualMode.Copy;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (args.item is ContentDatabaseGroupTreeViewItem)
            {
                base.RowGUI(args);
            }
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                if (args.item is ContentDatabaseAssetInfoTreeViewItem)
                {
                    var rect = args.GetCellRect(i);
                    CellGUIForAssets(rect, args.item as ContentDatabaseAssetInfoTreeViewItem, args.GetColumn(i), ref args);
                }
            }
        }

        public static MultiColumnHeaderState GetColumns()
        {
            var retVal = new MultiColumnHeaderState.Column[] {
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column()
            };
            retVal[0].headerContent = new GUIContent("Short name", "Short name of asset.");
            retVal[0].minWidth = 50;
            retVal[0].width = 300;
            retVal[0].maxWidth = 300;
            retVal[0].headerTextAlignment = TextAlignment.Left;
            retVal[0].canSort = true;
            retVal[0].autoResize = true;

            retVal[1].headerContent = new GUIContent("Path", "Full path to asset");
            retVal[1].minWidth = 50;
            retVal[1].width = 500;
            retVal[1].maxWidth = 800;
            retVal[1].headerTextAlignment = TextAlignment.Left;
            retVal[1].canSort = true;
            retVal[1].autoResize = true;

            retVal[2].headerContent = new GUIContent("Type", string.Empty);
            retVal[2].minWidth = 30;
            retVal[2].width = 200;
            retVal[2].maxWidth = 200;
            retVal[2].headerTextAlignment = TextAlignment.Left;
            retVal[2].canSort = true;
            retVal[2].autoResize = true;

            retVal[3].headerContent = new GUIContent("Base Type", string.Empty);
            retVal[3].minWidth = 30;
            retVal[3].width = 200;
            retVal[3].maxWidth = 200;
            retVal[3].headerTextAlignment = TextAlignment.Left;
            retVal[3].canSort = true;
            retVal[3].autoResize = true;

            retVal[4].headerContent = new GUIContent("Size", string.Empty);
            retVal[4].minWidth = 30;
            retVal[4].width = 100;
            retVal[4].maxWidth = 100;
            retVal[4].headerTextAlignment = TextAlignment.Left;
            retVal[4].canSort = true;
            retVal[4].autoResize = true;

            return new MultiColumnHeaderState(retVal);
        }

        void CellGUIForAssets(Rect cellRect, ContentDatabaseAssetInfoTreeViewItem item, int column, ref RowGUIArgs args)
        {
            switch (column)
            {
                case 0:
                    {
                        var iconRect = new Rect(cellRect.x + 1 + 20, cellRect.y + 1, cellRect.height - 2, cellRect.height - 2);
                        if (item.icon != null)
                        {
                            GUI.DrawTexture(iconRect, item.icon, ScaleMode.ScaleToFit);
                        }
                        DefaultGUI.Label(
                            new Rect(cellRect.x + iconRect.xMax + 1, cellRect.y, cellRect.width - iconRect.width, cellRect.height),
                            item.displayName,
                            args.selected,
                            args.focused);
                    }
                    break;
                case 1:
                    DefaultGUI.Label(cellRect, item.asset.path, args.selected, args.focused);
                    break;
                case 2:
                    DefaultGUI.Label(cellRect, item.asset.type, args.selected, args.focused);
                    break;
                case 3:
                    DefaultGUI.Label(cellRect, item.asset.base_type, args.selected, args.focused);
                    break;

                case 4:
                    var sz = item.size;
                    var sz_s = string.Empty;
                    if (sz < 1024)
                    {
                        sz_s = $"{sz} b";
                    }

                    if (sz >= 1024)
                    {
                        sz_s = $"{sz / 1024} kb";
                    }

                    if (sz >= 1024 * 1024)
                    {
                        sz_s = $"{sz / 1024 / 1024} mb";
                    }

                    if (sz >= 1024 * 1024 * 1024)
                    {
                        sz_s = $"{sz / 1024 / 1024} gb";
                    }

                    DefaultGUI.Label(cellRect, sz_s, args.selected, args.focused);
                    break;
            }
        }

        protected override bool CanChangeExpandedState(TreeViewItem item)
        {
            if (item != null)
            {
                if (item is ContentDatabaseGroupTreeViewItem)
                {
                    var group_item = item as ContentDatabaseGroupTreeViewItem;
                    var result = group_item != null && group_item.group != null && group_item.group.Assets.Count > 0;
                    return result;
                }
            }

            return false;
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return item != null;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            base.RenameEnded(args);

            if (args.newName.Length > 0 && args.newName != args.originalName)
            {
                var renamedItem = FindItem(args.itemID, rootItem);

                if (renamedItem is ContentDatabaseAssetInfoTreeViewItem)
                {
                    if (!ContentDatabase.TryGetAssetInfoByName(args.newName, out var asset))
                    {
                        var asset_item = FindItem(args.itemID, rootItem) as ContentDatabaseAssetInfoTreeViewItem;
                        asset_item.asset.name = args.newName;
                        args.acceptedRename = true;
                        Reload();
                        ContentDatabase.Save();
                    }
                    else
                    {
                        Window.ShowNotification(new GUIContent($"Asset with that short name already exists"), 3);
                        args.acceptedRename = false;
                    }
                }

                if (renamedItem is ContentDatabaseGroupTreeViewItem)
                {
                    if (!ContentDatabase.Get().GetContentInfo().TryGetGroup(args.newName, out var group))
                    {
                        var group_item = FindItem(args.itemID, rootItem) as ContentDatabaseGroupTreeViewItem;
                        group_item.group.Name = args.newName;
                        args.acceptedRename = true;
                        Reload();
                        ContentDatabase.Save();
                    }
                    else
                    {
                        Window.ShowNotification(new GUIContent($"Group with that name already exists"), 3);
                        var group_item = FindItem(args.itemID, rootItem) as ContentDatabaseGroupTreeViewItem;
                        group_item.group.Name = args.newName+"_1";
                        args.acceptedRename = true;
                        Reload();
                        ContentDatabase.Save();
                    }
                }
            }
            else
            {
                args.acceptedRename = false;
            }
        }

        private bool m_ContextOnItem = false;

        protected override void ContextClicked()
        {
            if (m_ContextOnItem)
            {
                m_ContextOnItem = false;
                return;
            }

            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("New Group"), false, delegate
            {
                ContentDatabase.Get().GetContentInfo().AddGroup("New Group_" + ContentDatabase.Get().GetContentInfo().Groups.Count);
                Reload();
            });

            menu.ShowAsContext();
        }

        protected override void ContextClickedItem(int id)
        {
            m_ContextOnItem = true;

            var clickItem = FindItem(id, rootItem);

            GenericMenu menu = new GenericMenu();

            if (!m_IsMultiSelected)
            {
                if (clickItem is ContentDatabaseAssetInfoTreeViewItem)
                {
                    menu.AddItem(new GUIContent("Add dependencies"), false, delegate
                    {
                        ContentDatabase.AddDependenciesForAsset((FindItem(id, rootItem) as ContentDatabaseAssetInfoTreeViewItem).asset, delegate { Reload(); });
                    });

                    menu.AddItem(new GUIContent("Rename"), false, delegate
                    {
                        BeginRename(FindItem(id, rootItem));
                    });

                    menu.AddItem(new GUIContent("Delete"), false, delegate
                    {
                        var item = FindItem(id, rootItem) as ContentDatabaseAssetInfoTreeViewItem;

                        ContentDatabase.RemoveAsset(item.asset.guid);

                        Reload();
                    });
                }
                else if (clickItem is ContentDatabaseGroupTreeViewItem)
                {
                    menu.AddItem(new GUIContent("Build as Custom Content"), false, delegate
                    {
                        var group = (clickItem as ContentDatabaseGroupTreeViewItem).group;
                        var folder = EditorUtility.SaveFolderPanel("Select folder for group", ContentDatabase.ContentFolder, group.Name);
                        ContentDatabase.Get().BuildContentCustom(folder, new List<ContentDatabase.ContentInfo.Group>() { group });
                    });

                    menu.AddItem(new GUIContent("Rename"), false, delegate
                    {
                        BeginRename(FindItem(id, rootItem));
                    });
                    menu.AddItem(new GUIContent("Delete"), false, delegate
                    {
                        var item = FindItem(id, rootItem) as ContentDatabaseGroupTreeViewItem;

                        if (ContentDatabase.Get().GetContentInfo().TryRemoveGroup(item.group.Name))
                        {
                            Window.ShowNotification(new GUIContent($"{item.group.Name} removed"));
                        }

                        Reload();
                    });
                }
            }
            else
            {
                menu.AddItem(new GUIContent("Build as Custom Content"), false, delegate
                {
                    var groups = new List<ContentDatabase.ContentInfo.Group>();
                    foreach (var i in selectedIds)
                    {
                        var item = FindItem(i, rootItem);

                        if (item is ContentDatabaseGroupTreeViewItem)
                        {
                            var group_ = (clickItem as ContentDatabaseGroupTreeViewItem).group;

                            if (group_ != null)
                            {
                                groups.Add(group_);
                            }
                        }
                    }

                    var folder = EditorUtility.SaveFolderPanel("Select folder for groups", ContentDatabase.ContentFolder, "CustomContent");
                    ContentDatabase.Get().BuildContentCustom(folder, groups);
                });

                menu.AddItem(new GUIContent("Delete"), false, delegate
                {
                    foreach (var i in selectedIds)
                    {
                        var item = FindItem(i, rootItem);

                        if (item is ContentDatabaseAssetInfoTreeViewItem)
                        {
                            ContentDatabase.RemoveAsset((item as ContentDatabaseAssetInfoTreeViewItem).asset.guid);
                        }
                        else if (item is ContentDatabaseGroupTreeViewItem)
                        {
                            ContentDatabase.Get().GetContentInfo().TryRemoveGroup((item as ContentDatabaseGroupTreeViewItem).group.Name);
                        }
                    }

                    Reload();
                });
            }

            menu.ShowAsContext();
        }
    }
}

static class ContentDatabaseEditorExtensions
{
    internal static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
    {
        if (ascending)
        {
            return source.OrderBy(selector);
        }
        else
        {
            return source.OrderByDescending(selector);
        }
    }

    internal static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
    {
        if (ascending)
        {
            return source.ThenBy(selector);
        }
        else
        {
            return source.ThenByDescending(selector);
        }
    }
}