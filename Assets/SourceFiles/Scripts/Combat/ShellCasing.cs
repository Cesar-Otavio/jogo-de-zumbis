using UnityEngine;

namespace Combat
{
    // Plays the tinkle of a spent casing when it first hits the ground
    public class ShellCasing : MonoBehaviour
    {
        private bool _armed;

        public void Arm()
        {
            _armed = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_armed || CombatEffects.Instance == null) return;

            _armed = false;
            CombatEffects.Instance.PlayRandom(CombatEffects.Instance.ShellDropSounds, transform.position, 0.35f);
        }
    }
}
