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
