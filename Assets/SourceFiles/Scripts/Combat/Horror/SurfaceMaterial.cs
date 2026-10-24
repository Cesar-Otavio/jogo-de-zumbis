using UnityEngine;

namespace Combat
{
    // put on a collider (or a parent) to say what it is made of, overriding the guess from its name
    public class SurfaceMaterial : MonoBehaviour
    {
        public SurfaceType Surface = SurfaceType.Concrete;
    }
}
