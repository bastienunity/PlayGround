# PlayGround
Some scripts examples I like to write when asked by nice people...

## Notice
This project is just a junk of scripts that may not work all very well together.
I've placed them here just as script example and should probably not be taken to projects as-is.

### ModelImporterExamples
This script is an example of how it is possible to manage imported animation events.
Two methods are available that:
- copy all animations events from one model to another model.
- copy all animation events from the first animation of one model to all its animation tracks.

### PresetExclude
I tried to answer the question of: How can we apply only certain properties of a Preset to a given target.
The project is using a SerializedObject that contains the list of properties to ignore when we apply the Preset.
This is not ideal but do the job until we provide a native partial Presets implementation.

### PresetAPIExample
I was looking at examples we can make to show the Preset API. Though it would be nice to share in this project.
The two first methods take an Object either in a Scene or a Folder and replicate one specific setting on every Object with the same type in the same Scene/Folder.
The last method is dealing with Default Presets in the PresetManager and showcase how to either update an existing Preset with new values or add a new Default Preset in the PresetManager.

### MeshLocalIdFix
This one is a bit tricky, we got a bug with references to Mesh from 2018.3 to 2019.1 that break a lot of references.
While it is now fixed in 2019.2.2f1 and newest, users who didn't notice the bug and continued using 2019.1 with those broken Ids cannot update to 2019.2.2f1 and newest versions of Unity.
Upgrading your project to 2019.2.f1 or newest, performing a full re-import and then running this script will probably solve your Mesh references problems.
