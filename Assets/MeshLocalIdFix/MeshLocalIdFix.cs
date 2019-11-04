using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class MeshLocalIdFix
{
    // What is this ?
    // We changed the way LocalFileID are generated between Unity 2018.3 and 2019.1,
    // Unfortunately a bug was found later where Meshes with duplicated names in 2018.3
    // are imported with the new ID system in 2019.1 and thus all references to them are lost.
    // This was fixed in 2019.2.2f1 by ensuring that all duplicated Meshes created before 2019.1 keep their old ID.

    // The new emerging problem now is:
    // If you had a Mesh asset created in 2018.3 and migrated your project to 2019.1,
    // references to the imported Meshes end up using the new ID format for duplicated mesh names.
    // Upgrading to 2019.2.2f1 or later breaks all those references.
    // Unfortunately, there is no way for us to fix that internally without introducing breaking changes.

    // This script goes through all ModelImporters in the project and finds possible mismatching IDs.
    // Use it with care and make sure to save your project first because this is a destructive operation that cannot be reverted.
    // Also, it may take a lot of time to run
    // We'd recommend running it on a separate computer or overnight using the following command line:
    // unity.exe -quit -batchmode -executeMethod FixFBXFiles.DoFixReferences
    // see https://docs.unity3d.com/Manual/CommandLineArguments.html for more information.
    [MenuItem("Assets/FBX File ID/Fix All references in prefabs and scenes")]
    static void DoFixReferences()
    {
        // Ask for backup first
        if (!EditorUtility.DisplayDialog("Fixing Mesh IDs from 2019.1 migration",
            "This operation is going through all Scenes and Prefabs and cannot be reverted.\nHave you made a backup?",
            "I'm fine",
            "Cancel"))
            return;

        // AssetImporter.MakeLocalFileIDWithHash is internal, to avoid looking it up on every iteration (like, a lot)
        // We create a delegate once and use it later in the script.
        if (_makeLocalFileIdWithHash == null)
        {
            var methodInfo = typeof(AssetImporter).GetMethod("MakeLocalFileIDWithHash",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (methodInfo != null)
            {
                _makeLocalFileIdWithHash = (MakeLocalFileIdWithHash)Delegate.CreateDelegate(
                    typeof(MakeLocalFileIdWithHash),
                    methodInfo);
                if (_makeLocalFileIdWithHash == null)
                {
                    Debug.LogError("Unable to get create AssetImporter.MakeLocalFileIDWithHash delegate, maybe the signature changed internally ?");
                    return;
                }
            }
            else
            {
                Debug.LogError("Unable to get AssetImporter.MakeLocalFileIDWithHash from reflection, maybe it was renamed or moved ?");
                return;
            }
        }

        EditorUtility.DisplayProgressBar("Check ModelImporter Models FileIDs", "Getting all importers", 0f);
        // Find all ModelImporter that have at least one mesh.
        var importers = AssetDatabase.FindAssets("t:Mesh")
            .Distinct()
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetImporter.GetAtPath)
            .OfType<ModelImporter>();

        // We gather a big map that contains:
        // - Key as the new way to generate hash that were wrongly used in the 2019.1 project and we want to revert.
        // - Value as the old way to compute hash that the ModelImporter may still use with 2019.2.2f1 migration fixes.
        var map = new Dictionary<string, string>();
        // Lets do each importer separately because we need to check their serialized properties.
        foreach (var importer in importers)
        {
            GenerateModelImporterMeshIdsMap(importer, map);
        }

        // Do no fetch all the prefabs and scenes if we couldn't find anything to migrate.
        if (map.Count == 0)
            return;

        EditorUtility.DisplayProgressBar("Check ModelImporter Models FileIDs", "Getting all scenes and prefabs", 0f);
        // Find all Prefab and SceneAsset, those are the one containing missing instances we want to fix.
        // Side note: maybe we should also fetch ScriptableObject in there, or other native assets we have ?
        // Maybe it would be better to just go through any text file we can find in the project ?
        var assets = AssetDatabase.FindAssets("t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetExtension(p) == ".prefab")
            .Union(
                AssetDatabase.FindAssets("t:SceneAsset")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => Path.GetExtension(p) == ".unity")
            ).ToList();

        for (var index = 0; index < assets.Count; index++)
        {
            var asset = assets[index];
            EditorUtility.DisplayProgressBar("Check ModelImporter Models FileIDs",
                "Fixing asset: " + asset,
                (float)index / assets.Count);
            // For each file to fix, we just run a regex looking for the m_Mesh reference and fix it if it's in the map.
            var file = File.ReadAllText(asset);
            file = Regex.Replace(file, @"m_Mesh: {fileID: -?[\d]+, guid: [a-z0-9]+, type: 3}",
                m => map.TryGetValue(m.Value, out var replace) ? replace : m.Value);
            File.WriteAllText(asset, file);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();
    }

    delegate long MakeLocalFileIdWithHash(int persistentTypeId, string name, long offset);
    private static MakeLocalFileIdWithHash _makeLocalFileIdWithHash;

    static void GenerateModelImporterMeshIdsMap(ModelImporter importer, Dictionary<string, string> map)
    {
        SerializedObject obj = new SerializedObject(importer);

        // Get the old name migration table
        var property = obj.FindProperty("m_InternalIDToNameTable");
        Dictionary<string, int> offsets = new Dictionary<string, int>();

        // keep the guid to build the m_Mesh string reference.
        var guid = AssetDatabase.AssetPathToGUID(importer.assetPath);

        // we are using this little trick with Next instead of GetArrayElementAt because it is so much faster.
        var next = property.FindPropertyRelative("Array.size");
        // that should get you m_InternalIDToNameTable.Array.data[0],
        // but because we may have 0 elements it is better to use Next directly.
        next.Next(false);
        // we keep the index just because it is easier to know when to stop and mostly to show a nice progress bar.
        int i = 0;
        // Loop through all the properties in the migration table.
        for (; i < property.arraySize; i++, next.Next(false))
        {
            // Show a progress bar, it may take some time...
            EditorUtility.DisplayProgressBar("Check ModelImporter Models FileIDs", "Checking " + importer.assetPath, (float)i / property.arraySize);

            // We first make sure that we are looking at a Mesh, the internal Id being 43.
            var currentType = next.FindPropertyRelative("first.first");
            if (currentType.intValue == 43)
            {
                // If we do have a Mesh, lets compute its new hash and make it correspond with the old hash.
                // I'll not give much explanation in there, that's just how we compute them internally.
                var currentName = next.FindPropertyRelative("second").stringValue;
                if (!offsets.TryGetValue(currentName, out var offset))
                {
                    offsets.Add(currentName, 0);
                    // we do not want to generate a hash for the first one we found
                    // because it was already using the old map as it should have.
                    continue;
                }
                offsets[currentName]++;
                var newHashReference = $"m_Mesh: {{fileID: {_makeLocalFileIdWithHash(43, currentName, offset)}, guid: {guid}, type: 3}}";
                var oldHashReference = $"m_Mesh: {{fileID: {next.FindPropertyRelative("first.second").intValue}, guid: {guid}, type: 3}}";
                while (map.ContainsKey(newHashReference))
                {
                    // Mimic the native mechanism with duplicate Ids, we just increment the offset until we find a free spot.
                    newHashReference = $"m_Mesh: {{fileID: {_makeLocalFileIdWithHash(43, currentName, ++offset)}, guid: {guid}, type: 3}}";
                }
                // save the new hash in key and the old one in value.
                // We are using them later to change old the new to be the old as they should have stayed when coming from 2018.4
                map.Add(newHashReference, oldHashReference);
            }
        }
    }
}
