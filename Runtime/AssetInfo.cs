using System;
using Object = UnityEngine.Object;

[Serializable]
public class AssetInfo
{
    public string name;
    public string path;
    public string guid;
    public string base_type;
    public string type;

    public override int GetHashCode()
    {
        int hash = 0;

        for (int i = 0; i < path.Length; i++)
        {
            hash += path[i] ^ i;
        }

        return hash;
    }

    public T LoadAsset<T>() where T : Object
    {

        return default(T);
    }
}
