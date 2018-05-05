
# BakedReflectionsUnity
![exampleImage](https://github.com/julhe/BakedReflectionsUnity/blob/master/bakedReflectionExample.JPG "exampleImage")
BakedReflections implementation for Unity by Julian Heinken (@schneckerstein) v1
USAGE:
1. Place this script on the object you like to have reflections for.
   NOTE: The implementation relies on the second uv channel (UV2). Therefore, it will only work if you activated "Generate Lightmap UVs" in the import settings of your mesh.
2. Change the shader of the material to "Unlit/displayBakedReflections" (or modify your own shader)
3. Modify "Slice Count Level" and "Resolution Level" to your preferences, click on "Start" to start baking the reflection atlas.
   Its not recommended to let "Total Axis Size" exceed 8192, since this is the highest texture resolution unity is able to import later.
4. Click on "Export to Exr" to export the reflection atlas. (Default location is "Assets/Baked SurfaceReflections")
