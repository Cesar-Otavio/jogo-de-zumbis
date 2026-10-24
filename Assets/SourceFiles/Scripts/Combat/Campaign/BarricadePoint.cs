using UnityEngine;
using UnityEngine.AI;

namespace Combat
{
    /* A passage that can be blocked with a barricade kit: planks across the gap that the infected
     * have to batter through (tanks and bosses much faster). Shows its resistance, can be
     * reinforced with another kit, and the player can always take it down again, so it never
     * seals a route for good. While standing it cuts the navigation mesh.
     */
    public class BarricadePoint : MonoBehaviour, IInteractable, IBreakable
    {
        [Tooltip("Missions count barricades by id prefix, e.g. gate_")]
        public string Id = "barricade";
        public float Width = 3.0f;
        public float Height = 1.6f;
        public float MaxHealth = 150.0f;
        public float ReinforceAmount = 75.0f;
        public bool StartsBuilt;
        [Tooltip("Damage taken from ordinary infected is multiplied by this; heavy ones by HeavyMultiplier")]
        public float WeakMultiplier = 2.0f;
        public float HeavyMultiplier = 2.5f;
        public Material PlankMaterial;

        public bool Built { get; private set; }
        public float Health { get; private set; }

        public Vector3 InteractionPoint => transform.position + Vector3.up * 1.0f;
        public float InteractionRange => 2.5f;
        public Vector3 Position => transform.position;
        public bool BlocksPath => Built;

        private Transform _planks;
        private NavMeshObstacle _obstacle;
        private float _lastHitCaption;

        public string Prompt
        {
            get
            {
                int kits = Kits;
                if (!Built) return kits > 0 ? Interactables.Key + "Montar barricada (1 kit)" : "<color=#AAAAAA>Ponto de barricada — precisa de kit</color>";
                string health = $"{Mathf.CeilToInt(Health)}/{Mathf.CeilToInt(MaxHealth)}";
                if (Health < MaxHealth - 1.0f && kits > 0) return Interactables.Key + $"Reforçar barricada ({health})";
                return Interactables.Key + $"Remover barricada ({health})";
            }
        }

        private int Kits
        {
            get
            {
                InventoryManager inventory = InventoryManager.Player;
                return inventory != null ? inventory.Count(QuickItem.Barricade) : 0;
            }
        }

        private void OnEnable()
        {
            Interactables.Register(this);
            Breakables.Register(this);
        }

        private void OnDisable()
        {
            Interactables.Unregister(this);
            Breakables.Unregister(this);
        }

        private void Awake()
        {
            _obstacle = gameObject.AddComponent<NavMeshObstacle>();
            _obstacle.shape = NavMeshObstacleShape.Box;
            _obstacle.center = new Vector3(0.0f, Height * 0.5f, 0.0f);
            _obstacle.size = new Vector3(Width, Height, 0.6f);
            _obstacle.carving = true;
            _obstacle.enabled = false;
            if (StartsBuilt) Build(MaxHealth);
        }

        public void Interact(PlayerCombat player)
        {
            InventoryManager inventory = player != null ? player.GetComponent<InventoryManager>() : null;
            if (!Built)
            {
                if (inventory == null || !inventory.Take(QuickItem.Barricade)) return;
                Build(MaxHealth);
                Play(CampaignAssets.Clip(a => a.BarricadeBuild), 1.0f);
                NoiseSystem.Emit(transform.position, 12.0f, NoiseCategory.Door, Factions.Player, player.gameObject);
                return;
            }

            if (Health < MaxHealth - 1.0f && inventory != null && inventory.Take(QuickItem.Barricade))
            {
                Health = Mathf.Min(MaxHealth, Health + ReinforceAmount);
                Play(CampaignAssets.Clip(a => a.BarricadeBuild), 0.8f);
                return;
            }

            // taking it down returns the kit if it's still in decent shape
            bool refund = Health >= MaxHealth * 0.5f;
            Remove();
            if (refund && inventory != null) inventory.Add(QuickItem.Barricade, 1, false);
            if (player != null) player.ShowNotice(refund ? "Barricada desmontada (kit recuperado)" : "Barricada desmontada");
        }

        public void Build(float health)
        {
            Built = true;
            Health = health;
            _obstacle.enabled = true;
            if (_planks != null) Destroy(_planks.gameObject);
            _planks = new GameObject("Planks").transform;
            _planks.SetParent(transform, false);

            // a few crooked planks nailed across the gap, with two posts
            Plank(new Vector3(-Width * 0.5f, Height * 0.5f, 0.0f), new Vector3(0.14f, Height + 0.2f, 0.14f), 0.0f);
            Plank(new Vector3(Width * 0.5f, Height * 0.5f, 0.0f), new Vector3(0.14f, Height + 0.2f, 0.14f), 0.0f);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.35f + i * (Height - 0.4f) / 3.0f;
                Plank(new Vector3(0.0f, y, 0.05f), new Vector3(Width + 0.3f, 0.18f, 0.05f), (i % 2 == 0 ? 1 : -1) * Random.Range(3.0f, 9.0f));
            }
        }

        private void Plank(Vector3 position, Vector3 size, float roll)
        {
            GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "BarricadePlank";
            plank.transform.SetParent(_planks, false);
            plank.transform.localPosition = position;
            plank.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, roll);
            plank.transform.localScale = size;
            Material material = PlankMaterial != null ? PlankMaterial : (CombatEffects.Instance != null ? CombatEffects.Instance.CrateMaterial : null);
            if (material != null) plank.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        public void Remove()
        {
            Built = false;
            Health = 0.0f;
            _obstacle.enabled = false;
            if (_planks != null) Destroy(_planks.gameObject);
            _planks = null;
        }

        public void Hit(float damage, bool heavy, GameObject attacker)
        {
            if (!Built) return;
            Health -= damage * (heavy ? HeavyMultiplier : WeakMultiplier);
            Play(CampaignAssets.Clip(a => a.BarricadeHit), 1.0f);
            NoiseSystem.Emit(transform.position, 14.0f, NoiseCategory.Door, Factions.Environment, gameObject);
            if (Time.time - _lastHitCaption > 3.0f)
            {
                _lastHitCaption = Time.time;
                Subtitles.Show("[barricada sendo atacada]", transform.position, 2.5f);
            }

            if (Health <= 0.0f) Break(attacker);
        }

        private void Break(GameObject attacker)
        {
            Play(CampaignAssets.Clip(a => a.BarricadeBreak), 1.0f);
            Subtitles.Show("[barricada destruída]", transform.position, 3.0f);
            // the planks fly off
            if (_planks != null)
            {
                Vector3 push = attacker != null ? (transform.position - attacker.transform.position).normalized : transform.forward;
                foreach (Transform plank in _planks.GetComponentsInChildren<Transform>())
                {
                    if (plank == _planks) continue;
                    plank.gameObject.layer = CombatLayers.Debris;
                    var body = plank.gameObject.AddComponent<Rigidbody>();
                    body.mass = 4.0f;
                    body.AddForce((push + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f) * 30.0f, ForceMode.Impulse);
                }

                _planks.SetParent(null, true);
                Destroy(_planks.gameObject, 8.0f);
                _planks = null;
            }

            Built = false;
            Health = 0.0f;
            _obstacle.enabled = false;
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null && CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(clip, InteractionPoint, volume, 0.08f, 1.0f, 2.0f, 40.0f);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0.0f, Height * 0.5f, 0.0f), new Vector3(Width, Height, 0.3f));
        }
    }
}
