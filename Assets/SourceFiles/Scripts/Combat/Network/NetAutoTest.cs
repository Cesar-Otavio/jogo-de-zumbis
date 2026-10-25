using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Combat
{
    /* Two-instance testing without a person at the second machine. Does nothing unless the game is
     * started with these command-line arguments:
     *   -net-join 127.0.0.1:7777   join that host as soon as the menu is up, and get ready
     *   -net-name Teste            player name
     *   -net-log C:\path\log.txt   write what this client sees (players, infected, health, hits) once a second,
     *                              and take screenshots next to it
     *   -net-hit                   once a replica infected is near, hit it through the same entry point a
     *                              bullet uses (its Health, which hands the hit to the host)
     */
    public class NetAutoTest : MonoBehaviour
    {
        private string _join;
        private string _name = "Teste";
        private string _log;
        private bool _hit;
        private float _nextLine;
        private float _nextHit;
        private int _shots;
        private bool _joined;
        private bool _readied;
        private int _screens;
        private float _startHealth = -1.0f;

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-net-join": _join = next; break;
                    case "-net-name": _name = next; break;
                    case "-net-log": _log = next; break;
                    case "-net-hit": _hit = true; break;
                }
            }

            if (_join == null) enabled = false;
            else Write($"teste iniciado, versão {Application.version}, protocolo {NetSession.Protocol}");
        }

        private void Update()
        {
            if (!_joined && GameFlow.Instance != null && NetSession.Instance != null && Time.unscaledTime > 2.0f)
            {
                _joined = true;
                string[] parts = _join.Split(':');
                ushort port = parts.Length > 1 && ushort.TryParse(parts[1], out ushort p) ? p : NetSession.DefaultPort;
                bool ok = NetSession.Instance.Join(parts[0], port, _name, "");
                Write($"conectando a {parts[0]}:{port} -> {ok}");
            }

            if (!_readied && NetPlayer.Local != null && NetPlayer.Local.IsSpawned)
            {
                _readied = true;
                NetPlayer.Local.Ready.Value = true;
                Write("no lobby, pronto");
                Screenshot("lobby");
            }

            if (Time.unscaledTime >= _nextLine)
            {
                _nextLine = Time.unscaledTime + 1.0f;
                Report();
            }

            if (_hit && Net.Active) HitNearest();
        }

        private void Report()
        {
            if (NetSession.Instance == null) return;
            PlayerCombat me = FindAnyObjectByType<PlayerCombat>();
            var line = new StringBuilder($"estado {NetSession.Instance.Current} '{NetSession.Instance.Message}'");
            if (NetGame.Instance != null) line.Append($" | partida {NetGame.Instance.Started} jogadores {NetPlayer.All.Count}");
            if (me != null && Net.Active)
            {
                if (_startHealth < 0.0f) _startHealth = me.Health.Current;
                line.Append($" | eu {me.transform.position} vida {me.Health.Current:F0}");
            }

            foreach (NetPlayer player in NetPlayer.All)
            {
                if (player.IsOwner || player.Puppet == null) continue;
                line.Append($" | boneco {player.DisplayName} em {player.Puppet.transform.position} vida {player.Puppet.Health.Current:F0} arma {player.State.Value.Weapon}");
            }

            EnemyReplica[] replicas = FindObjectsByType<EnemyReplica>();
            int dead = 0;
            foreach (EnemyReplica r in replicas) if (r.GetComponent<Health>().IsDead) dead++;
            line.Append($" | réplicas {replicas.Length} (mortas {dead}) zumbis locais com IA {FindObjectsByType<ZombieAI>().Length} tiros {_shots}");
            if (GameFlow.Instance != null) line.Append($" | abates {RunState.Kills} pontos {GameFlow.Instance.Score} créditos {RunState.Credits}");
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner != null && Net.Active)
                line.Append($" | onda {spawner.Wave} ativa {spawner.WaveInProgress} intervalo {spawner.InIntermission} ({spawner.IntermissionLeft:F0} s) restantes {spawner.RemainingInWave}");
            Write(line.ToString());

            if (Net.Active && _screens < 3 && replicas.Length > 0)
            {
                _screens++;
                Screenshot("partida" + _screens);
            }
        }

        private void HitNearest()
        {
            if (Time.unscaledTime < _nextHit) return;
            PlayerCombat me = FindAnyObjectByType<PlayerCombat>();
            if (me == null) return;
            EnemyReplica target = null;
            float best = 40.0f;
            foreach (EnemyReplica replica in FindObjectsByType<EnemyReplica>())
            {
                Health health = replica.GetComponent<Health>();
                if (health.IsDead) continue;
                float d = Vector3.Distance(replica.transform.position, me.transform.position);
                if (d < best)
                {
                    best = d;
                    target = replica;
                }
            }

            if (target == null) return;
            _nextHit = Time.unscaledTime + 0.4f;
            // what an M1911 body shot does, through the same door a bullet comes in by
            Vector3 chest = target.transform.position + Vector3.up * 1.3f;
            Vector3 from = me.transform.position + Vector3.up * 1.4f;
            target.GetComponent<Health>().TakeDamage(new DamageInfo
            {
                Amount = 28.0f,
                Point = chest,
                Direction = (chest - from).normalized,
                Force = 3.0f,
                Instigator = me.gameObject
            });
            if (NetPlayer.Local != null) NetPlayer.Local.ReportShot(from, chest);
            _shots++;
            Write($"tiro {_shots} na réplica {target.Id} a {best:F1} m");
        }

        private void Screenshot(string label)
        {
            if (string.IsNullOrEmpty(_log)) return;
            string dir = Path.GetDirectoryName(_log) ?? ".";
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"cliente_{label}.png"));
        }

        private void Write(string text)
        {
            string line = $"{DateTime.Now:HH:mm:ss} {text}";
            Debug.Log("[NetTest] " + line);
            if (string.IsNullOrEmpty(_log)) return;
            try
            {
                File.AppendAllText(_log, line + "\n");
            }
            catch (IOException)
            {
                // a busy log file is not worth stopping the test for
            }
        }
    }
}
