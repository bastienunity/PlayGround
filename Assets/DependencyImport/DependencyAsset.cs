using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace DependencyExample
{
    public class DependencyAsset : ScriptableObject
    {
        public List<SomeDependency> dependencies = new List<SomeDependency>();

        [MenuItem("Assets/Create/Dependency/Create Grouping asset", true)]
        public static bool CreateGroupingAsset_Validation()
        {
            return Selection.objects.Any(o => o is SomeDependency && AssetDatabase.Contains(o));
        }

        [MenuItem("Assets/Create/Dependency/Create Grouping asset")]
        public static void CreateGroupingAsset()
        {
            var assets = Selection.objects.OfType<SomeDependency>().Where(AssetDatabase.Contains).ToList();
            var dependency = CreateInstance<DependencyAsset>();
            dependency.dependencies = assets;
            var dependencyCreation = CreateInstance<DoCreateDependencyAsset>();
            dependencyCreation.dependency = dependency;

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                dependencyCreation,
                Path.GetDirectoryName(AssetDatabase.GetAssetPath(assets.First())) + "/DependencyAsset.dependency",
                AssetPreview.GetMiniThumbnail(dependency),
                null);
        }
    }

    public class DoCreateDependencyAsset : EndNameEditAction
    {
        public DependencyAsset dependency;
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            var finalPath = AssetDatabase.GenerateUniqueAssetPath(pathName);
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] {dependency}, finalPath, true);
            AssetDatabase.ImportAsset(finalPath);
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(finalPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }
}
