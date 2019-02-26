using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ModelImporterEventsManagement
{
    /// <summary>
    /// This method is taking each events array of each clip animation from the current ModelImporter
    /// and copy it to every selected fbx file.
    /// In order to use it, you have to lock the Inspector with the FBX you want to read the events from,
    /// select any FBX you want to paste this values to, and then use this method. 
    /// </summary>
    /// <param name="command"></param>
    [MenuItem("CONTEXT/FBXImporter/Replicate animation events to selection")]
    static void CopyEvents(MenuCommand command)
    {
        var paths = Selection.objects.Select(AssetDatabase.GetAssetPath).Where(p => !string.IsNullOrEmpty(p));
        foreach (var path in paths)
        {
            var destination = new SerializedObject(AssetImporter.GetAtPath(path));

            var importer = new SerializedObject(command.context);
            var animationsProperty = importer.FindProperty("m_ClipAnimations");
            var eAnimationsProperty = destination.FindProperty("m_ClipAnimations");

            for (int i = 0; i < animationsProperty.arraySize && i < eAnimationsProperty.arraySize; i++)
            {
                var events = animationsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("events");
                destination.CopyFromSerializedProperty(events);
            }

            destination.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.StartAssetEditing();
        foreach (var path in paths)
            AssetDatabase.ImportAsset(path);
        AssetDatabase.StopAssetEditing();
    }

    /// <summary>
    /// This method takes the first clip animation event settings of an AssetImporter and propagate it to each clip animation.
    /// This method is using CopyFromSerializedProperty to do so.
    /// The trick here is that CopyFromSerializedProperty is checking for the actual property path to copy it and does not allow to copy/paste a property at another location.
    /// This is why we are duplicating the importer to get a temporary copy where we can modify the serialization as we please and then copy the parts we want.
    /// </summary>
    [MenuItem("CONTEXT/FBXImporter/Replicate animation events to all tracks")]
    static void PropagateEvents(MenuCommand command)
    {
        // we create a temporary duplicate of the importer so we can change its serialization and fake whatever data we need to copy on the real one.
        var fakeImporter = new SerializedObject(Object.Instantiate(command.context));
        var fakeAnimation = fakeImporter.FindProperty("m_ClipAnimations");

        // get a serialization of the actual importer we want to modify.
        var importer = new SerializedObject(command.context);
        var animationsProperty = importer.FindProperty("m_ClipAnimations");

        // force array size 1, so we remove any events information for other animations
        fakeAnimation.arraySize = 1;
        // set it back to the correct size, the serializedProperty system will then duplicate the last entry on each new element
        fakeAnimation.arraySize = animationsProperty.arraySize;

        // for each entry, lets take the fake one that got duplicated and paste it in the real importer.
        for (int i = 0; i < animationsProperty.arraySize; i++)
        {
            // get the duplicated event property
            var events = fakeAnimation.GetArrayElementAtIndex(i).FindPropertyRelative("events");
            // apply it to the actual importer
            importer.CopyFromSerializedProperty(events);
        }

        importer.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.ImportAsset(((AssetImporter)command.context).assetPath);
    }
}
