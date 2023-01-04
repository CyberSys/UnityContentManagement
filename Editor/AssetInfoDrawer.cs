using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

[CustomPropertyDrawer(typeof(AssetInfo), true)]
class AssetInfoDrawer : PropertyDrawer
{
    public AssetInfo info; 
    public SerializedProperty assetProperty;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property == null || label == null)
        {
            Debug.LogError("Error rendering drawer for AssetInfo property.");
            return;
        }

        string labelText = label.text;
        info = property.GetActualObjectForSerializedProperty<AssetInfo>(fieldInfo, ref labelText);
        assetProperty = property;

        if (labelText != label.text || string.IsNullOrEmpty(label.text))
        {
            label = new GUIContent(labelText, label.tooltip);
        }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label(label);
        GUILayout.BeginVertical("box"); 
        
        if (ContentDatabase.TryGetAssetInfoByGUID(info.guid, out info))
        {
            if (info.GetAsset<Object>())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Box(AssetDatabase.GetCachedIcon(info.path), GUILayout.Width(48), GUILayout.Height(48));
                EditorGUILayout.HelpBox($"Name: {info.name}\nPath: {info.path}\nGUID: {info.guid}\nType: {info.type}", MessageType.None, true);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Focus"))
                {
                    EditorGUIUtility.PingObject(info.GetAsset<Object>());
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"Asset isn't assigned!", MessageType.Warning, true);
            }
        }
        if (GUILayout.Button("Select Asset"))
        {
            var popup = EditorWindow.GetWindow<AssetInfoPopup>(true, "Select Asset");
            popup.SetDrawer(this);
        }
        GUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndProperty();
    }
}

class AssetInfoPopup : EditorWindow
{
    public AssetInfoDrawer drawer;

    public void SetDrawer(AssetInfoDrawer drawer)
    {
        this.drawer = drawer;
    }

    AssetInfoTreeView m_Tree;
    TreeViewState m_TreeState;
    SearchField m_SearchField;
    string m_CurrentFindName = string.Empty;
    bool m_ShouldClose;

    public void ForceClose()
    {
        m_ShouldClose = true;
    }

    private void OnGUI()
    {
        Rect rect = position;

        int border = 4;
        int topPadding = 12;
        int searchHeight = 20;
        var searchRect = new Rect(border, topPadding, rect.width - border * 2, searchHeight);
        var remainTop = topPadding + searchHeight + border;
        var remainingRect = new Rect(border, topPadding + searchHeight + border, rect.width - border * 2, rect.height - remainTop - border);

        if(m_SearchField == null)
        {
            m_SearchField = new SearchField();
            m_SearchField.SetFocus();
            m_SearchField.downOrUpArrowKeyPressed += () => { m_Tree.SetFocus(); };
        }

        m_CurrentFindName = m_SearchField.OnGUI(searchRect, m_CurrentFindName);
        if (m_Tree == null)
        {
            if (m_TreeState == null)
                m_TreeState = new TreeViewState();
            m_Tree = new AssetInfoTreeView(m_TreeState, this);
            m_Tree.Reload();
        }

        bool isKeyPressed = Event.current.type == EventType.KeyDown && Event.current.isKey;
        bool isEnterKeyPressed = isKeyPressed && (Event.current.keyCode == KeyCode.KeypadEnter || Event.current.keyCode == KeyCode.Return);
        bool isUpOrDownArrowPressed = isKeyPressed && (Event.current.keyCode == KeyCode.UpArrow || Event.current.keyCode == KeyCode.DownArrow);

        m_Tree.searchString = m_CurrentFindName;
        m_Tree.IsEnterKeyPressed = isEnterKeyPressed;
        m_Tree.OnGUI(remainingRect);

        if (m_ShouldClose || isEnterKeyPressed)
        {
            GUIUtility.hotControl = 0;
            Close();
        }
    }
}

sealed class AssetInfoTreeViewItem : TreeViewItem
{
    public AssetInfo info;

    public AssetInfoTreeViewItem(AssetInfo info)
        : base(info.GetHashCode(), 0, info.name)
    {
        this.info = info;
        icon = AssetDatabase.GetCachedIcon(info.path) as Texture2D;
    }
}

class AssetInfoTreeView : TreeView
{
    public AssetInfoTreeView(TreeViewState state, AssetInfoPopup popup) : base(state)
    {
        this.popup = popup;
    }

    AssetInfoPopup popup;
    public bool IsEnterKeyPressed { get; set; }

    public override void OnGUI(Rect rect)
    {
        base.OnGUI(rect);
        if (IsEnterKeyPressed && HasFocus())
        {
            popup.ForceClose();
        }
    }

    protected override bool CanMultiSelect(TreeViewItem item)
    {
        return false;
    }

    protected override void DoubleClickedItem(int id)
    {
        var assetInfoItem = FindItem(id, rootItem) as AssetInfoTreeViewItem;
        popup.drawer.fieldInfo.SetValue(popup.drawer.assetProperty.serializedObject.targetObject, assetInfoItem.info);
        popup.drawer.assetProperty.serializedObject.ApplyModifiedProperties();
        popup.drawer.assetProperty.serializedObject.Update();
        EditorUtility.SetDirty(popup.drawer.assetProperty.serializedObject.targetObject);
        popup.ForceClose();
    }

    protected override void SelectionChanged(IList<int> selectedIds)
    {
        if (selectedIds != null && selectedIds.Count == 1)
        {
            var assetInfoItem = FindItem(selectedIds[0], rootItem) as AssetInfoTreeViewItem;
            popup.drawer.fieldInfo.SetValue(popup.drawer.assetProperty.serializedObject.targetObject, assetInfoItem.info);
            popup.drawer.assetProperty.serializedObject.ApplyModifiedProperties();
            popup.drawer.assetProperty.serializedObject.Update();
            EditorUtility.SetDirty(popup.drawer.assetProperty.serializedObject.targetObject);
            SetFocus();
        }
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem(-1, -1);

        for(int i = 0; i < ContentDatabase.Assets;i++)
        {
            var info = ContentDatabase.Get()[i];
            var child = new AssetInfoTreeViewItem(info);
            root.AddChild(child);
        }
        
        return root;
    }
}

public static class SerializedPropertyExtensions
{
    /// <summary>
    /// Used to extract the target object from a serialized property.
    /// </summary>
    /// <typeparam name="T">The type of the object to extract.</typeparam>
    /// <param name="property">The property containing the object.</param>
    /// <param name="field">The field data.</param>
    /// <param name="label">The label name.</param>
    /// <returns>Returns the target object type.</returns>
    public static T GetActualObjectForSerializedProperty<T>(this SerializedProperty property, FieldInfo field, ref string label)
    {
        try
        {
            if (property == null || field == null)
                return default(T);
            var serializedObject = property.serializedObject;
            if (serializedObject == null)
            {
                return default(T);
            }

            var targetObject = serializedObject.targetObject;

            if (property.depth > 0)
            {
                var slicedName = property.propertyPath.Split('.').ToList();
                List<int> arrayCounts = new List<int>();
                for (int index = 0; index < slicedName.Count; index++)
                {
                    arrayCounts.Add(-1);
                    var currName = slicedName[index];
                    if (currName.EndsWith("]"))
                    {
                        var arraySlice = currName.Split('[', ']');
                        if (arraySlice.Length >= 2)
                        {
                            arrayCounts[index - 2] = Convert.ToInt32(arraySlice[1]);
                            slicedName[index] = string.Empty;
                            slicedName[index - 1] = string.Empty;
                        }
                    }
                }

                while (string.IsNullOrEmpty(slicedName.Last()))
                {
                    int i = slicedName.Count - 1;
                    slicedName.RemoveAt(i);
                    arrayCounts.RemoveAt(i);
                }

                if (property.propertyPath.EndsWith("]"))
                {
                    var slice = property.propertyPath.Split('[', ']');
                    if (slice.Length >= 2)
                        label = "Element " + slice[slice.Length - 2];
                }

                return DescendHierarchy<T>(targetObject, slicedName, arrayCounts, 0);
            }

            var obj = field.GetValue(targetObject);
            return (T)obj;
        }
        catch
        {
            return default(T);
        }
    }

    static T DescendHierarchy<T>(object targetObject, List<string> splitName, List<int> splitCounts, int depth)
    {
        if (depth >= splitName.Count)
            return default(T);

        var currName = splitName[depth];

        if (string.IsNullOrEmpty(currName))
            return DescendHierarchy<T>(targetObject, splitName, splitCounts, depth + 1);

        int arrayIndex = splitCounts[depth];

        var newField = targetObject.GetType().GetField(currName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (newField == null)
        {
            Type baseType = targetObject.GetType().BaseType;
            while (baseType != null && newField == null)
            {
                newField = baseType.GetField(currName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                baseType = baseType.BaseType;
            }
        }

        var newObj = newField.GetValue(targetObject);
        if (depth == splitName.Count - 1)
        {
            T actualObject = default(T);
            if (arrayIndex >= 0)
            {
                if (newObj.GetType().IsArray && ((System.Array)newObj).Length > arrayIndex)
                    actualObject = (T)((System.Array)newObj).GetValue(arrayIndex);

                var newObjList = newObj as IList;
                if (newObjList != null && newObjList.Count > arrayIndex)
                {
                    actualObject = (T)newObjList[arrayIndex];

                    //if (actualObject == null)
                    //    actualObject = new T();
                }
            }
            else
            {
                actualObject = (T)newObj;
            }

            return actualObject;
        }
        else if (arrayIndex >= 0)
        {
            if (newObj is IList)
            {
                IList list = (IList)newObj;
                newObj = list[arrayIndex];
            }
            else if (newObj is System.Array)
            {
                Array a = (Array)newObj;
                newObj = a.GetValue(arrayIndex);
            }
        }

        return DescendHierarchy<T>(newObj, splitName, splitCounts, depth + 1);
    }

    internal static string GetPropertyPathArrayName(string propertyPath)
    {
        if (propertyPath.EndsWith("]"))
        {
            int leftBracket = propertyPath.LastIndexOf('[');
            if (leftBracket > -1)
            {
                string arrayString = propertyPath.Substring(0, leftBracket);
                if (arrayString.EndsWith(".data"))
                    return arrayString.Substring(0, arrayString.Length - 5); // remove ".data"
            }
        }

        return string.Empty;
    }
}
