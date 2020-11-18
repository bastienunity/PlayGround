using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEditor.Presets;
using UnityEngine;

namespace PresetsPerFolder
{
    /// <summary>
    /// This sample class applies presets automatically to assets in the folder containing the Preset, & any subfolders.
    /// The code is divided into 3 parts that setup importer dependencies to make sure the importers of all assets stay deterministic.
    ///
    /// OnPreprocessAsset:
    /// For each imported asset, this method goes from the root folder and down to the asset folder
    /// and registers a CustomDependency to this folder in case a Preset is being added/removed at a later time.
    /// It then loads all Presets from that folder and tries to apply them to the asset importer.
    /// If it is applied, we add a direct dependency to the Preset itself so that the asset can be re-imported when the Preset's values are changed.
    /// </summary>
    public class EnforcePresetPostProcessor : AssetPostprocessor
    {
        void OnPreprocessAsset()
        {
            // We first need to make sure we don't apply this AssetPostprocessor to assets in packages.
            // And not a .cs file as we don't want to trigger a code compilation every time we create/remove a new Preset.
            // Last but not least, we need to make sure Presets don't depend on themselves, or that will end-up in an infinite import loop...
            // There may be more exceptions to add here depending upon your project.
            if (assetPath.StartsWith("Assets/") && !assetPath.EndsWith(".cs") && !assetPath.EndsWith(".preset"))
            {
                var path = Path.GetDirectoryName(assetPath);
                ApplyPresetsFromFolderRecursively(path);
            }
        }

        void ApplyPresetsFromFolderRecursively(string folder)
        {
            // Apply everything from the parent folder first so the preset closer to our asset will be applied last.
            var parentFolder = Path.GetDirectoryName(folder);
            if (!string.IsNullOrEmpty(parentFolder))
                ApplyPresetsFromFolderRecursively(parentFolder);

            // Add a dependency to the folder preset custom key
            // so whenever a Preset is added or removed from this folder our asset is re-imported.
            context.DependsOnCustomDependency($"PresetPostProcessor_{folder}");

            // Find all Preset assets in this folder. We are using the System.Directory method here instead of the AssetDatabase
            // because the import may run in a separate process which prevents the AssetDatabase from doing global searching.
            var presetPaths =
                Directory.EnumerateFiles(folder, "*.preset", SearchOption.TopDirectoryOnly)
                    .OrderBy(a => a);

            foreach (var presetPath in presetPaths)
            {
                // Load the Preset and try to apply it to the importer.
                var preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);

                // In case we are being imported before the preset, we can't know if we need to depend on it or not.
                // To be sure, we add a dependency to it anyway so we are being re-imported once it's imported.
                // If the preset has been loaded correctly, we want to depends on it only if it applies to our asset.
                if (preset == null || preset.ApplyTo(assetImporter))
                {
                    // Using DependsOnArtifact here because Presets are native assets and using DependsOnSourceAsset would not work.
                    context.DependsOnArtifact(presetPath);
                }
            }
        }
    }

    /// <summary>
    /// InitPresetDependencies:
    /// This method is called when the project is loaded, it finds every imported Preset in the project.
    /// For each folder containing a Preset, we create a CustomDependency from the folder name and a hash being the list of Presets found in it.
    ///
    /// OnAssetsModified:
    /// Whenever a Preset is added, removed, or moved from a folder, we need to update the CustomDependency for this folder
    /// so assets that may depend on those Presets are re-imported.
    ///
    /// TODO: Ideally each CustomDependency should also be dependent on the PresetType,
    /// so Textures are not re-imported by adding a new FBXImporterPreset in a folder.
    /// But this makes the InitPresetDependencies and OnPostprocessAllAssets methods too complex for the purpose of this example.
    /// What you'd like to have is the CustomDependency to be of the form "Preset_{presetType}_{folder}",
    /// and the hash containing only Presets of the given presetType in that folder.
    /// </summary>
    public class UpdateFolderPresetDependency : AssetsModifiedProcessor
    {
        /// <summary>
        /// This method with the InitializeOnLoadMethod will be called every time the project is being loaded or a code compilation happen.
        /// It is very important to set all of the hashes correctly at startup
        /// because for each Preset already imported we will not go through the OnPostprocessAllAssets method
        /// and the CustomDependencies are not saved between sessions and need to be rebuilt every time.
        /// </summary>
        [InitializeOnLoadMethod]
        static void InitPresetDependencies()
        {
            // We are using AssetDatabase.FindAssets using a glob filter to avoid importing all objects in the project
            // because we don't have to load everything to know where Presets files live.
            var allPaths = AssetDatabase.FindAssets("glob:\"**.preset\"")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(a => a)
                .ToList();

            bool atLeastOnUpdate = false;
            string previousPath = string.Empty;
            Hash128 hash = new Hash128();
            for (var index = 0; index < allPaths.Count; index++)
            {
                var path = allPaths[index];
                var folder = Path.GetDirectoryName(path);
                if (folder != previousPath)
                {
                    // we reached a new folder, register to total hash of the presets in the previous folder
                    if (previousPath != string.Empty)
                    {
                        AssetDatabase.RegisterCustomDependency($"PresetPostProcessor_{previousPath}", hash);
                        atLeastOnUpdate = true;
                    }

                    hash = new Hash128();
                    previousPath = folder;
                }

                // We have to append both path and preset type to make sure assets get re-imported whenever a Preset type is being changed.
                hash.Append(path);
                hash.Append(AssetDatabase.LoadAssetAtPath<Preset>(path).GetTargetFullTypeName());
            }

            // register the last path
            if (previousPath != string.Empty)
            {
                AssetDatabase.RegisterCustomDependency($"PresetPostProcessor_{previousPath}", hash);
                atLeastOnUpdate = true;
            }

            // Only trigger a Refresh if there is at least one Dependency updated here.
            if (atLeastOnUpdate)
                AssetDatabase.Refresh();
        }

        /// <summary>
        /// This is being called whenever an asset has been changed in the project.
        /// In this method, we check if any Preset has been added, removed, or moved
        /// and we update the CustomDependency related to the changed folder.
        /// </summary>
        protected override void OnAssetsModified(string[] changedAssets, string[] addedAssets, string[] deletedAssets, AssetMoveInfo[] movedAssets)
        {
            HashSet<string> folders = new HashSet<string>();
            foreach (var asset in changedAssets)
            {
                // a Preset has been changed, we need to make sure to update the dependency for this folder in case the Preset type has been changed
                if (asset.EndsWith(".preset"))
                {
                    folders.Add(Path.GetDirectoryName(asset));
                }
            }

            foreach (var asset in addedAssets)
            {
                // a new Preset has been added, we need to make sure to update the dependency for this folder
                if (asset.EndsWith(".preset"))
                {
                    folders.Add(Path.GetDirectoryName(asset));
                }
            }

            foreach (var asset in deletedAssets)
            {
                // a Preset has been removed, we need to make sure to update the dependency for this folder
                if (asset.EndsWith(".preset"))
                {
                    folders.Add(Path.GetDirectoryName(asset));
                }
            }

            foreach (var movedAsset in movedAssets)
            {
                // a Preset has been moved, we need to make sure to update the dependency for the previous and new folder
                if (movedAsset.destinationAssetPath.EndsWith(".preset"))
                {
                    folders.Add(Path.GetDirectoryName(movedAsset.destinationAssetPath));
                }

                if (movedAsset.sourceAssetPath.EndsWith(".preset"))
                {
                    folders.Add(Path.GetDirectoryName(movedAsset.sourceAssetPath));
                }
            }

            // Make sure we don't add the dependency update for no reason.
            if (folders.Count != 0)
            {
                // The dependencies need to be updated outside of the AssetPostprocessor calls
                // so we register the method to the next Editor update
                EditorApplication.delayCall += () =>
                {
                    DelayedDependencyRegistration(folders);
                };
            }
        }

        /// <summary>
        /// This method loads all Presets in each of the given folder paths
        /// and updates the CustomDependency hash based on the presets currently in that folder.
        /// </summary>
        static void DelayedDependencyRegistration(HashSet<string> folders)
        {
            foreach (var folder in folders)
            {
                var presetPaths =
                    AssetDatabase.FindAssets("glob:\"**.preset\"", new[] { folder })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Where(presetPath => Path.GetDirectoryName(presetPath) == folder)
                        .OrderBy(a => a);

                Hash128 hash = new Hash128();
                foreach (var presetPath in presetPaths)
                {
                    // We have to append both path and preset type to make sure assets get re-imported whenever a Preset type is being changed.
                    hash.Append(presetPath);
                    hash.Append(AssetDatabase.LoadAssetAtPath<Preset>(presetPath).GetTargetFullTypeName());
                }

                AssetDatabase.RegisterCustomDependency($"PresetPostProcessor_{folder}", hash);
            }

            // We have to manually trigger a Refresh
            // so that the AssetDatabase triggers a dependency check on the folder hash we updated.
            AssetDatabase.Refresh();
        }
    }
}
