using System;
using UnityEngine;

namespace TarkinItemExporter
{
    internal static class UnityExtensions
    {
        public static void ZeroTransformAndItsParents(this Transform tr)
        {
            do
            {
                tr.localPosition = Vector3.zero;
                tr.localRotation = Quaternion.identity;
                tr.localScale = Vector3.one;
                tr = tr.parent;
            }
            while (tr.parent != null);
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