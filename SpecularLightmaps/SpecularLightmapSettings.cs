using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpecularLightmapping
{
    [CreateAssetMenu(fileName = "SpecularLightmapSettings.asset")]
    public class SpecularLightmapSettings : ScriptableObject
    {  
    [Range(1f, 10f)]
    public int sliceCountLevel = 2;
    [Range(1f, 10f)]
    public int resolutionLevel = 2;


    }

}
