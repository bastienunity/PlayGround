using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using Object = UnityEngine.Object;

namespace DependencyExample
{
    [CustomEditor(typeof(DependencyImporter))]
    public class DependencyImporterEditor : AssetImporterEditor
    {
        public override bool showImportedObject => false;
        protected override Type extraDataType => typeof(DependencyAsset);

        private Editor extraEditor;

        protected override void InitializeExtraDataInstance(Object extraData, int targetIndex)
        {
            var importer = (AssetImporter)targets[targetIndex];
            var dependencyAsset = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(importer.assetPath).Cast<DependencyAsset>().FirstOrDefault();
            EditorUtility.CopySerialized(dependencyAsset, extraData);
        }

        public override void OnEnable()
        {
            base.OnEnable();
            extraEditor = CreateEditor(extraDataTargets);
        }

        protected override void Apply()
        {
            base.Apply();
            for (var index = 0; index < extraDataTargets.Length; index++)
            {
                var data = extraDataTargets[index];
                var importer = (AssetImporter)targets[index];
                UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new[] {data}, importer.assetPath, true);
            }
        }

        public override void OnInspectorGUI()
        {
            extraEditor.OnInspectorGUI();
            ApplyRevertGUI();
        }
    }
}
