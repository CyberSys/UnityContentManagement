using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

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


    ContentDatabaseAssetInfoTreeView m_AssetsTree;
    TreeViewState m_AssetsTreeState;
    SearchField m_AssetsSearchField;

    private void OnGUI()
    {

        if (db)
        {
            titleContent = new GUIContent($"Content Management | Assets: {db.GetContentInfo().Assets.Count}");
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

                var headerState = ContentDatabaseAssetInfoTreeView.GetColumns();
                m_AssetsTree = new ContentDatabaseAssetInfoTreeView(m_AssetsTreeState, headerState);
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

        public ContentDatabaseAssetInfoTreeViewItem(int index, AssetInfo info) : base(index, 0, info.name)
        {
            asset = info;
            icon = AssetDatabase.GetCachedIcon(info.path) as Texture2D;
        }
    }

    public class ContentDatabaseAssetInfoTreeView : TreeView
    {
        public ContentDatabaseAssetInfoTreeView(TreeViewState state, MultiColumnHeaderState headerState) : base(state, new MultiColumnHeader(headerState))
        {
            showAlternatingRowBackgrounds = true;
            multiColumnHeader.sortingChanged += delegate (MultiColumnHeader multiColumnHeader)
            {
                var root = rootItem;
                var rows = GetRows();

                IOrderedEnumerable<ContentDatabaseAssetInfoTreeViewItem> InitialOrder(IEnumerable<ContentDatabaseAssetInfoTreeViewItem> myTypes, int[] columnList)
                {
                    var sortOption = columnList[0];
                    bool ascending = multiColumnHeader.IsSortedAscending(columnList[0]);
                    switch (sortOption)
                    {
                        case 1:
                            return myTypes.Order(l => l.asset.name, ascending);
                        case 2:
                            return myTypes.Order(l => l.asset.path, ascending);
                        case 3:
                            return myTypes.Order(l => l.asset.type, ascending);
                        case 4:
                            return myTypes.Order(l => l.asset.base_type, ascending);
                        case 5:
                            return myTypes.Order(l => l.size, ascending);
                        default:
                            return myTypes.Order(l => l.displayName, ascending);
                    }
                }

                void SortByColumn()
                {
                    var sortedColumns = multiColumnHeader.state.sortedColumns;

                    if (sortedColumns.Length == 0)
                        return;

                    List<ContentDatabaseAssetInfoTreeViewItem> assetList = new List<ContentDatabaseAssetInfoTreeViewItem>();
                    foreach (var item in rootItem.children)
                    {
                        assetList.Add(item as ContentDatabaseAssetInfoTreeViewItem);
                    }
                    var orderedItems = InitialOrder(assetList, sortedColumns);

                    rootItem.children = orderedItems.Cast<TreeViewItem>().ToList();
                }

                if (rows.Count <= 1)
                    return;

                if (multiColumnHeader.sortedColumnIndex == -1)
                    return;

                SortByColumn();

                rows.Clear();
                for (int i = 0; i < root.children.Count; i++)
                    rows.Add(root.children[i]);

                Repaint();
            };
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
                SetFocus();
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            var element = FindItem(id, rootItem) as ContentDatabaseAssetInfoTreeViewItem;

            if(element.asset != null)
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(element.asset.path);
                EditorGUIUtility.PingObject(obj);
            }
        }

        protected override TreeViewItem BuildRoot()
        {
            var Root = new TreeViewItem(-1, -1);

            if (ContentDatabase.Get().GetContentInfo().Assets.Count > 0)
            {
                for (int i = 0; i < ContentDatabase.Get().GetContentInfo().Assets.Count; i++)
                {
                    var asset = ContentDatabase.Get().GetContentInfo().Assets[i];
                    var child = new ContentDatabaseAssetInfoTreeViewItem(i, asset);
                    child.UpdateSize();
                    Root.AddChild(child);
                }
            }
            else
            {
                Root.AddChild(new TreeViewItem() { id = int.MinValue, displayName = "No Assets" });
            }

            return Root;
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            args.draggedItemIDs = GetSelection();
            return true;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            DragAndDrop.PrepareStartDrag();
            var items = new List<ContentDatabaseAssetInfoTreeViewItem>(args.draggedItemIDs.Select(id => FindItem(id, rootItem) as ContentDatabaseAssetInfoTreeViewItem));
            DragAndDrop.paths = items.Select(a => a.asset.path).ToArray();
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (args.performDrop)
            {
                var paths = DragAndDrop.paths;

                int added = 0;
                foreach (var path in paths)
                {
                    if (ContentDatabase.AddAsset(AssetDatabase.LoadMainAssetAtPath(path)) == ContentDatabase.AssetError.NoError)
                    {
                        added++;
                    }
                }

                if (added > 0)
                {
                    Window.ShowNotification(new GUIContent($"Added {added} assets"));
                }
            }

            Reload();

            return DragAndDropVisualMode.Copy;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (ContentDatabase.Get().GetContentInfo().Assets.Count <= 0) { return; }

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), args.item as ContentDatabaseAssetInfoTreeViewItem, args.GetColumn(i), ref args);
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

        void CellGUI(Rect cellRect, ContentDatabaseAssetInfoTreeViewItem item, int column, ref RowGUIArgs args)
        {
            switch (column)
            {
                case 0:
                    {
                        var iconRect = new Rect(cellRect.x + 1, cellRect.y + 1, cellRect.height - 2, cellRect.height - 2);
                        if (item.icon != null)
                            GUI.DrawTexture(iconRect, item.icon, ScaleMode.ScaleToFit);
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
                    if(sz < 1024)
                    {
                        sz_s = $"{sz} b";
                    }

                    if (sz >= 1024)
                    {
                        sz_s = $"{sz/1024} kb";
                    }

                    if (sz >= 1024 * 1024)
                    {
                        sz_s = $"{sz/1024/1024} mb";
                    }

                    if (sz >= 1024 * 1024 * 1024)
                    {
                        sz_s = $"{sz/1024/1024} gb";
                    }

                    DefaultGUI.Label(cellRect, sz_s, args.selected, args.focused);
                    break;
            }
        }


        protected override bool CanRename(TreeViewItem item)
        {
            return item != null;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            if (args.itemID == int.MinValue)
            {
                return;
            }

            base.RenameEnded(args);

            if (args.newName.Length > 0 && args.newName != args.originalName)
            {
                if (!ContentDatabase.TryGetAssetInfoByName(args.newName, out var asset))
                {
                    var renamedItem = FindItem(args.itemID, rootItem) as ContentDatabaseAssetInfoTreeViewItem;
                    renamedItem.asset.name = args.newName;
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
            else
            {
                args.acceptedRename = false;
            }
        }

        protected override void ContextClickedItem(int id)
        {
            if (id == int.MinValue)
            {
                return;
            }

            GenericMenu menu = new GenericMenu();

            if (!m_IsMultiSelected)
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

                    if (ContentDatabase.TryGetAssetInfoByGUID(item.asset.guid, out var asset))
                    {
                        ContentDatabase.RemoveAsset(asset.guid);
                    }

                    Reload();
                });
            }
            else
            {
                menu.AddItem(new GUIContent("Delete"), false, delegate
                {
                    foreach (var i in selectedIds)
                    {
                        var item = FindItem(i, rootItem) as ContentDatabaseAssetInfoTreeViewItem;

                        if (ContentDatabase.TryGetAssetInfoByGUID(item.asset.guid, out var asset))
                        {
                            ContentDatabase.RemoveAsset(asset.guid);
                        }
                    }

                    Reload();
                });
            }

            menu.ShowAsContext();
        }
    }
}

static class ContentDatabaseEditorOrderExtensions
{
    internal static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, System.Func<T, TKey> selector, bool ascending)
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

    internal static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, System.Func<T, TKey> selector, bool ascending)
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