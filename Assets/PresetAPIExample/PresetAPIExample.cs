using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

public static class PresetAPIExample
{
    [MenuItem("CONTEXT/Light/Replicate Color in Scene")]
    public static void ApplyAllLights(MenuCommand command)
    {
        // Get our current selected light.
        var referenceLight = command.context as Light;
        if (referenceLight != null)
        {
            // Create a Preset out of it.
            var lightPreset = new Preset(referenceLight);
            // Find all Light components in the scene of our reference light.
            var allLights = referenceLight.gameObject.scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Light>(true));
            // Choose which serialized property we want to apply to everyone
            var propertyToApply = new[] { "m_Color" };
            // Apply the Preset with only the selected property to all the Lights.
            foreach (var light in allLights)
            {
                lightPreset.ApplyTo(light, propertyToApply);
            }
        }
    }

    [MenuItem("CONTEXT/Material/Replicate Material Color in Folder")]
    public static void ApplyAllMaterialsInFolder(MenuCommand command)
    {
        // Get our current selected Material.
        var referenceMaterial = command.context as Material;
        if (referenceMaterial != null)
        {
            var assetPath = AssetDatabase.GetAssetPath(referenceMaterial);
            if (!string.IsNullOrEmpty(assetPath))
            {
                // Create a Preset out of it.
                var materialPreset = new Preset(referenceMaterial);
                // Find all Material assets in the same folder.
                var assetFolder = Path.GetDirectoryName(assetPath);
                var allMaterials = AssetDatabase.FindAssets("t:Material", new[] { assetFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<Material>);
                // Select the first color entry, in the standard shader this entry is _Color
                var propertyToApply = new[] { "m_SavedProperties.m_Colors.Array.data[0]" };
                // Apply the Preset with only the selected property to all the Materials.
                foreach (var material in allMaterials)
                {
                    materialPreset.ApplyTo(material, propertyToApply);
                }
            }
        }
    }

    [MenuItem("CONTEXT/Light/Update Default Preset")]
    public static void UpdateOrAddLightDefaults(MenuCommand command)
    {
        // Get our current selected light.
        var referenceLight = command.context as Light;
        // Get the list of default Presets that apply to that light.
        var defaults = Preset.GetDefaultPresetsForObject(referenceLight);

        if (defaults.Length == 0)
        {
            // We don't have a default yet, let's create one !
            var defaultLight = new Preset(referenceLight);
            // Nicely ask the user where to save this default.
            var path = EditorUtility.SaveFilePanelInProject("Create Default Light preset", "Light", "preset", "Select a folder to save the new default");
            // If the selected path already contains a Preset, it's instanceID would be changed by a plain replace.
            // We use this trick to replace the values only so Object referencing the existing asset will not break...
            var existingAsset = AssetDatabase.LoadAssetAtPath<Preset>(path);
            if (existingAsset != null)
            {
                EditorUtility.CopySerialized(defaultLight, existingAsset);
                defaultLight = existingAsset;
            }
            else
            {
                AssetDatabase.CreateAsset(defaultLight, path);
            }
            // Load the existing default list.
            // We don't want to loose any configuration that may point specific GameObject because of the filters.
            var existingDefault = Preset.GetDefaultPresetsForType(defaultLight.GetPresetType()).ToList();
            // Insert the new one at the beginning of the list with no filter, so it applies to any Light that didn't had a default.
            existingDefault.Insert(0, new DefaultPreset("", defaultLight));
            // Set the new list as default for Lights.
            Preset.SetDefaultPresetsForType(defaultLight.GetPresetType(), existingDefault.ToArray());
        }
        else
        {
            // We want to update the values only to the last default because maybe other Presets apply to other objects first
            // and we don't want to change them.
            var lastPreset = defaults.Last();
            lastPreset.UpdateProperties(referenceLight);
        }
    }
}
