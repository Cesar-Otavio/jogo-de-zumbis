using UnityEngine;

namespace Combat
{
    // what every machine needs the same copy of: the shared game object, the character model for puppets
    // and the weapon list (shots and held guns travel as an index into it). Filled by the setup menu.
    public class NetSessionPrefabs : MonoBehaviour
    {
        public GameObject Game;
        public GameObject Character;
        public WeaponData[] Weapons = new WeaponData[0];
    }
}
