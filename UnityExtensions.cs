using System;
using UnityEngine;

namespace gltfmod
{
    internal static class UnityExtensions
    {
        public static Transform GetRoot(this Transform tr)
        {
            while (tr.parent != null)
            {
                tr = tr.parent;
            }

            return tr;
        }

        public static void DestroyAll<T>(this T[] components) where T : Component
        {
            if (components == null)
                return;

            foreach (T component in components)
            {
                if (component != null)
                {
                    UnityEngine.Object.Destroy(component);
                }
            }
        }
    }
}