using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    public class ArrayIndexConv
    {
        const int ReflectionAtlas_AxisSize = 32;
        private Vector2Int ToCoordinate(int index)
        {
            return new Vector2Int(index % ReflectionAtlas_AxisSize, index / ReflectionAtlas_AxisSize);
        }

        private int ToIndex(Vector2Int coordinate)
        {   
            return coordinate.y * ReflectionAtlas_AxisSize + coordinate.x;
        }


        // A Test behaves as an ordinary method
        [Test]
        public void ArrayIndexConvSimplePasses()
        {
            for (int x = 0; x < ReflectionAtlas_AxisSize; x++)
            {
                for (int y = 0; y < ReflectionAtlas_AxisSize; y++)
                {
                    Vector2Int coord = new Vector2Int(x,y);
                    int indexFromCoord = ToIndex(coord);
                    Vector2Int coordFromIndex = ToCoordinate(indexFromCoord);

                    Assert.AreEqual(coord, coordFromIndex);
                }
            }
            // Use the Assert class to test conditions
        }

        

        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator ArrayIndexConvWithEnumeratorPasses()
        {
            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return null;
        }
    }
}
