using System.Linq;
using UnityEditor.Experimental.AssetImporters;

namespace DependencyExample
{
    [ScriptedImporter(1, ".dependency")]
    public class DependencyImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var dependencyAsset = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(ctx.assetPath).Cast<DependencyAsset>().FirstOrDefault();
            ctx.AddObjectToAsset("dependency", dependencyAsset);
            ctx.SetMainObject(dependencyAsset);

            // Do some other stuff with the list of assets linked in dependencyAsset at this point as you like!
            // If you are using the dependencyAsset.dependencies values at that point,
            // you need to add an explicit dependency to them using ctx.DependsOnSourceAsset();
        }
    }
}
