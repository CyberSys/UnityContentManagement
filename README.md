### I didn't like Unity Addressable because of its schizocode, no comments, no detailed description of the package for its modification, the developers are silent on the forum... 
I freaked out and wrote my asset loader in 2-3 hours

## Getting started

### To include an asset to the database
![Alt-текст](https://raw.githubusercontent.com/redheadgektor/ContentManagement/main/IncludingAsset.png "...")
#### This action is to skip the asset through the filters and either add or not add it by returning an error in the console

### To exclude an asset from the database
![Alt-текст](https://raw.githubusercontent.com/redheadgektor/ContentManagement/main/ExcludingAsset.png "...")

### To load asset from bundles
```csharp
ContentDatabase.LoadAsset<GameObject>("ClientUI", delegate (GameObject go)
{
    if (go)
    {
        Instantiate(go);
    }
});
```

![Alt-текст](https://raw.githubusercontent.com/redheadgektor/ContentManagement/main/ContentDatabase.png "...")
> 1) All possible chains of loading bundles to get an asset
> 2) All assets included to the database
> 3) Bundle Nameing Mode
> > Only GUID - the name of the bundles will correspond to a unique id with a file extension

> > Only GUID Without Extension - The same thing but without the file extension

> > Name Type - The name will match the name of the asset in the project with the addition of a type name (for example floor_Texture2D or Penis_GameObject)

> > Name - The name will match the name of the asset in the project without adding a type name (_when duplicating names, the type name will be added_)

> > Raw Path - The bundles will be arranged according to the location of the files in the project

> 4) Does not include information about the unity version in bundles, slightly complicates the decompilation of bundles due to the difference in serialization from version to version
