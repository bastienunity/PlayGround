using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;
using Object = UnityEngine.Object;

public class PresetExcludeProperties : ScriptableObject, ISerializationCallbackReceiver
{
    public Preset preset;
    public List<string> excludedProperties = new List<string>();

    /// <summary>
    /// Apply the <see cref="preset"/> to the <paramref name="target"/>,
    /// excluding properties starting with any one contained in <see cref="excludedProperties"/>.
    /// </summary>
    /// <param name="target">Object to apply the <see cref="preset"/> to.</param>
    /// <param name="checkOnly">Set to true to only check if the <paramref name="target"/> would be changed by calling this method.</param>
    /// <returns>True if the <see cref="preset"/> was applied and the <paramref name="target"/> was changed.</returns>
    public bool ApplyTo(Object target, bool checkOnly = false)
    {
        // Early returns in case the target is not supported
        if (!preset.CanBeAppliedTo(target))
            return false;

        int excludedIndex = 0;
        // Sorts propertyModificationList by name so it's easier to compare them to the exclude list
        var modifications = preset.PropertyModifications.Select(p => p.propertyPath).OrderBy(s => s);
        List<string> properties = new List<string>();
        foreach (var modification in modifications)
        {
            // progress into the exclude list until we match current property or are placed after
            while (excludedIndex < excludedProperties.Count &&
                   modification.CompareTo(excludedProperties[excludedIndex]) >= 0 &&
                   !modification.StartsWith(excludedProperties[excludedIndex]))
            {
                excludedIndex++;
            }
            // if we are not starting with the current exclusion name, then add the property to the list to apply to
            if (excludedIndex >= excludedProperties.Count ||
                !modification.StartsWith(excludedProperties[excludedIndex]))
                properties.Add(modification);
        }

        // Only process with the apply to if there is at least one property to apply and we are not in check only.
        if (properties.Count > 0)
            return checkOnly || preset.ApplyTo(target, properties.ToArray());
        return false;
    }

    /// <summary>
    /// Context menu on Preset headers to create a PresetExcludeProperties asset based on the current Preset.
    /// </summary>
    [MenuItem("CONTEXT/Preset/Create Exclude")]
    static void CreateExcludeAsset(MenuCommand command)
    {
        var preset = command.context as Preset;
        if (preset != null)
        {
            string path = AssetDatabase.GetAssetPath(preset);
            var exclude = ScriptableObject.CreateInstance<PresetExcludeProperties>();
            exclude.preset = preset;
            AssetDatabase.CreateAsset(exclude, path.Replace(preset.name + ".preset", preset.name + "-exclude.asset"));
        }
    }

    /// <summary>
    /// Validate method to see if current selection can be applied to the target Object.
    /// </summary>
    [MenuItem("CONTEXT/Object/Apply Selected Preset With Exclude", true)]
    static bool ApplyPreset_Validate(MenuCommand command)
    {
        var exclude = Selection.activeObject as PresetExcludeProperties;
        return exclude != null && exclude.ApplyTo(command.context, true);
    }

    /// <summary>
    /// Context menu on any Object header to apply the currently selected PresetExcludeProperties asset to the object.
    /// </summary>
    /// <remarks>
    /// This is a very bad UX experience, you have to lock the inspector with the target object,
    /// then select a PresetExcludeProperties asset to make it works... but eh, that's just a showcase !
    /// </remarks>
    [MenuItem("CONTEXT/Object/Apply Selected Preset With Exclude")]
    static void ApplyPreset(MenuCommand command)
    {
        Undo.RegisterCompleteObjectUndo(command.context, "Apply partial Preset");
        var exclude = Selection.activeObject as PresetExcludeProperties;
        if (exclude != null)
            exclude.ApplyTo(command.context);
    }

    public void OnBeforeSerialize()
    {
        // making sure properties are always sorted by name so it's easier to iterate during the ApplyTo process.
        excludedProperties.Sort();
    }

    public void OnAfterDeserialize()
    {
    }
}
