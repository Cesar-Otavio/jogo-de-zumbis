using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Combat.EditorTools
{
    /* Phase 3 narrative: the dialogue library (every spoken line of the campaign, with its trigger) and the
     * director + subtitles in the scene. The script lives here, in one table; Docs/FASE3_NARRATIVA.md has the
     * same lines for review and voice recording. Re-running keeps voice clips already assigned to a line.
     */
    public static partial class CombatSetup
    {
        private struct Line
        {
            public string Id;
            public DialogueSpeaker Speaker;
            public string Text;
            public DialoguePriority Priority;
            public DialogueInterrupt Interrupt;
            public DialogueTrigger Trigger;
            public string Mission;
            public int Objective;
            public string Item;
            public int Count;
            public bool Repeat;
            public float Cooldown;
            public bool RequireActive;
            public string SkipIfPlayed;
            public string Group;
        }

        private static Line L(string id, DialogueSpeaker speaker, string text, DialoguePriority priority = DialoguePriority.Medium,
            DialogueTrigger trigger = DialogueTrigger.Manual, string mission = null, int objective = -1, string item = null, int count = 0)
        {
            return new Line { Id = id, Speaker = speaker, Text = text, Priority = priority, Trigger = trigger, Mission = mission, Objective = objective, Item = item, Count = count };
        }

        private static Line With(this Line line, DialogueInterrupt interrupt) { line.Interrupt = interrupt; return line; }
        private static Line Repeating(this Line line, float cooldown) { line.Repeat = true; line.Cooldown = cooldown; return line; }
        private static Line WhileActive(this Line line) { line.RequireActive = true; return line; }
        private static Line Skip(this Line line, string id) { line.SkipIfPlayed = id; return line; }
        private static Line InGroup(this Line line, string group) { line.Group = group; return line; }

        private static List<Line> Script()
        {
            const DialogueSpeaker N = DialogueSpeaker.Narrator, D = DialogueSpeaker.Duarte, P = DialogueSpeaker.Pilot;
            const DialoguePriority Lo = DialoguePriority.Low, Me = DialoguePriority.Medium, Hi = DialoguePriority.High, Cr = DialoguePriority.Critical;
            const DialogueTrigger Obj = DialogueTrigger.ObjectiveCompleted, Done = DialogueTrigger.MissionCompleted, Item = DialogueTrigger.ItemCollected;
            const DialogueInterrupt Cut = DialogueInterrupt.InterruptLower, Drop = DialogueInterrupt.DropIfBusy;

            return new List<Line>
            {
                // ---- prologue (played by CampaignManager.PlayIntro, cut as a group when skipped)
                L("prologue_1", N, "Às vinte e três e quarenta, os primeiros gritos vieram dos hangares. O sargento de serviço disse que era treinamento.", Cr).InGroup("prologue"),
                L("prologue_2", N, "À meia-noite e quinze, a base parou de responder ao comando regional. Pouco depois, a contenção do laboratório começou a falhar.", Cr).InGroup("prologue"),
                L("prologue_3", N, "O último relatório falava de um composto chamado Aurora. Nenhuma equipe de resgate veio confirmar o que aconteceu.", Cr).InGroup("prologue"),
                L("prologue_4", D, "...alô? Alguém na escuta? Aqui é o Tenente Duarte, Controle Aurora. Se tem alguém vivo aí dentro, responda.", Cr).InGroup("prologue"),
                L("prologue_5", D, "A comunicação está instável, e eu não sei quanto tempo a gente tem.", Cr).InGroup("prologue"),
                L("prologue_6", D, "Pegue uma arma e saia do pátio. Tem alguma coisa se movendo entre os alojamentos.", Cr).InGroup("prologue"),

                // ---- chapter 1: first contact
                L("radio_wake", D, "...está me ouvindo? Aqui é o Tenente Duarte. Se você está vivo, pegue uma arma. Eles vêm atrás do barulho.", Hi).Skip("prologue_6"),
                L("fc_armed", D, "Boa. Agora você tem com o que se defender. Dê uma olhada em volta, mas sem pressa.", Me, Obj, "m_first_contact", 0),
                L("fc_area", D, "Estou ouvindo movimento perto do seu sinal. Muito movimento. Prepare-se.", Hi, Obj, "m_first_contact", 1).With(Cut),
                L("fc_done", D, "Você ainda está de pé. Isso já é mais do que a maioria aqui conseguiu.", Me, Done, "m_first_contact"),

                // ---- generator
                L("radio_power", D, "Estou vendo uma queda no gerador do setor noroeste. Se a energia cair de vez, os portões de segurança param. Vá até a sala de máquinas.", Hi),
                L("gen_found", D, "Achei a origem da falha. Tiraram dois fusíveis do painel. Devem estar espalhados pela base.", Me, Obj, "m_generator", 0),
                L("gen_fuse1", D, "Ótimo, um já foi. Falta mais um. Cuidado: estou ouvindo movimento perto de você.", Me, Item, "m_generator", -1, "fuse", 1).WhileActive(),
                L("gen_fuse2", D, "Os dois fusíveis. Volte para o painel e instale.", Me, Item, "m_generator", -1, "fuse", 2).WhileActive(),
                L("gen_power", D, "Energia restaurada... espere. O barulho atraiu alguma coisa. Segure a posição até o gerador estabilizar!", Cr, Obj, "m_generator", 3).With(Cut),
                L("gen_done", D, "O gerador está estável. Boa. Respire um pouco, enquanto pode.", Me, Done, "m_generator"),

                // ---- lost signal
                L("radio_signal", D, "...tchhh... estação de comunicação... módulo... repetindo... protocolo... tchhh...", Hi),
                L("sig_station", D, "Essa estação é a nossa chance de ouvir o que o comando está dizendo. Falta o módulo de rádio.", Me, Obj, "m_signal", 0),
                L("sig_module", D, "Achou o módulo. Instale no console e vamos ver o que sai daí.", Me, Obj, "m_signal", 1).WhileActive(),
                L("radio_signal_ok", D, "Sinal limpo! Escute... o comando não vem nos buscar. A ordem é conter a base. Conter a gente.", Hi),

                // ---- armory
                L("radio_armory", D, "O arsenal está trancado por cartão. O intendente deixou um com o oficial de dia, perto das torres.", Hi),
                L("arm_card", D, "É esse cartão. O leitor fica na porta do arsenal.", Me, Obj, "m_armory", 0).WhileActive(),
                L("arm_done", D, "Com isso dá para aguentar mais uma noite. Talvez.", Me, Done, "m_armory"),

                // ---- experiment 07
                L("radio_e07", D, "Alguma coisa enorme rompeu a contenção do laboratório. O Experimento 07. Faça ele bater em algo pesado e acerte quando cambalear.", Cr).With(Cut),
                L("radio_e07_dead", D, "Você derrubou o 07... Isso não foi acidente. Foi um teste. E você passou.", Hi),

                // ---- watchtower
                L("radio_tower", D, "A torre de vigilância tem refletores. A luz segura essas coisas por um tempo. Religue a alimentação dela.", Hi),
                L("tow_lights", D, "Refletores acesos! Agora eles vão querer ver o que é. Defenda a torre.", Hi, Obj, "m_tower", 2).With(Cut),
                L("tow_done", D, "Consegui ver a base pelas câmeras da torre. Está pior do que eu pensava.", Me, Done, "m_tower"),

                // ---- laboratory
                L("radio_lab", D, "O laboratório fica no setor sudeste. Os registros de lá devem explicar tudo. Recupere a caixa-preta.", Hi),
                L("lab_enter", D, "Você está dentro do laboratório. Fique atento: foi daqui que tudo saiu.", Hi, Obj, "m_blackbox", 0),
                L("lab_docs", D, "Valença... eu conheço esse nome. Era ele quem assinava as ordens do Setor 4.", Me, Obj, "m_blackbox", 1),
                L("lab_box", D, "A caixa-preta fala de um protocolo de queima às quatro da manhã. Eles vão apagar a base inteira. Com a gente dentro.", Cr, Done, "m_blackbox"),

                // ---- side jobs
                L("radio_alarm", D, "O alarme disparou sozinho e está chamando a horda inteira. Desligue os painéis!", Hi).With(Cut),
                L("alarm_done", D, "Silêncio de novo. Ou o mais perto disso que dá para ter aqui.", Lo, Done, "m_alarm"),
                L("radio_supply", D, "Tem um depósito de suprimentos no fundo da base. Abrir aquilo vai fazer barulho, mas está cheio de munição.", Lo),
                L("radio_gates", D, "Os portões estão abertos. Feche todos e reforce as passagens. Cada minuto conta.", Me),

                // ---- containment
                L("radio_contain", D, "Os dutos de ventilação estão abrindo! Feche as válvulas na ordem certa, ou a base inteira vira um ninho.", Cr).With(Cut),
                L("cont_done", D, "Setores lacrados. Isso compra um pouco de tempo.", Me, Done, "m_containment"),

                // ---- evacuation
                L("evac_codes", D, "AURORA-NOITE. É esse o código. Leve até a estação de rádio.", Hi, Item, "m_evac_prep", -1, "evac_codes", 1),
                L("evac_sent", P, "Controle Aurora, aqui é Águia 2. Código recebido. Preparem a zona de pouso e acendam o farol.", Hi, Obj, "m_evac_prep", 1),
                L("radio_evac", D, "Você ouviu. Prepare o heliponto e acenda o farol. Ele chega em minutos.", Hi),
                L("radio_final", D, "A Aurora está vindo. É ela, a coisa do laboratório. Use a luz contra ela e aguente até o helicóptero tocar o chão!", Cr).With(Cut),
                L("evac_beacon", P, "Farol à vista. Estou a quatro minutos. Mantenham a zona limpa.", Hi, Obj, "m_evacuation", 0),
                L("aurora_down", D, "Ela caiu... ela caiu! Aguente mais um pouco, ele está chegando!", Cr, DialogueTrigger.BossDefeated, "aurora").With(Cut),
                L("evac_done", P, "Tocando o chão! Embarca, embarca!", Cr, Done, "m_evacuation").With(Cut),

                // ---- reactions (repeatable, with cooldowns; they never talk over something more important)
                L("react_blackout", D, "Perdi a leitura de energia de um setor. Sem luz eles ficam mais ousados.", Lo, DialogueTrigger.Blackout).Repeating(150.0f).With(Drop),
                L("react_lowammo", D, "Sua munição está acabando. Procure as caixas de suprimento.", Lo, DialogueTrigger.LowAmmo).Repeating(120.0f).With(Drop),
                L("react_critical", D, "Você está ferido! Saia da linha de fogo e use um kit médico!", Hi, DialogueTrigger.CriticalHealth).Repeating(90.0f).With(Cut),
                L("react_failed", D, "Perdemos essa. Respire. Vamos tentar de novo.", Me, DialogueTrigger.MissionFailed).Repeating(30.0f),
                L("react_lastnight", D, "É a última noite. Se o piloto vier, vai ser agora.", Me, DialogueTrigger.ChapterStarted, null, -1, "9"),
            };
        }

        [MenuItem("Tools/Combat/Phase 3/4 - Narrative and Dialogue")]
        public static void SetupPhase3Narrative()
        {
            EnsureFolder("Assets/SourceFiles/Resources");
            string path = $"Assets/SourceFiles/Resources/{DialogueLibrary.ResourcePath}.asset";
            var library = AssetDatabase.LoadAssetAtPath<DialogueLibrary>(path);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<DialogueLibrary>();
                AssetDatabase.CreateAsset(library, path);
            }

            // keep voice clips that were already recorded and assigned
            var clips = new Dictionary<string, AudioClip>();
            foreach (DialogueLine old in library.Lines)
            {
                if (old != null && old.Clip != null) clips[old.Id] = old.Clip;
            }

            var lines = new List<DialogueLine>();
            foreach (Line row in Script())
            {
                lines.Add(new DialogueLine
                {
                    Id = row.Id,
                    Speaker = row.Speaker,
                    LocalizationKey = "dlg." + row.Id,
                    Text = row.Text,
                    Clip = clips.TryGetValue(row.Id, out AudioClip clip) ? clip : FindVoiceClip(row.Id),
                    Priority = row.Priority,
                    Interrupt = row.Interrupt,
                    Trigger = row.Trigger,
                    MissionId = row.Mission,
                    ObjectiveIndex = row.Objective,
                    ItemId = row.Item,
                    ItemCount = row.Count,
                    Once = !row.Repeat,
                    Cooldown = row.Cooldown,
                    RequireMissionActive = row.RequireActive,
                    SkipIfPlayed = row.SkipIfPlayed,
                    Group = row.Group,
                    MaxQueueTime = row.Group == "prologue" ? 90.0f : row.Priority >= DialoguePriority.High ? 20.0f : 10.0f,
                });
            }

            library.Lines = lines.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            GameObject root = FindOrCreate("CampaignSystems");
            Ensure<DialogueDirector>(root);
            Ensure<DialogueSubtitleView>(root);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"Dialogue library: {lines.Count} lines");
        }

        // voice files dropped into SoundFX/Dialogue/pt-BR named after the line id are picked up automatically
        private static AudioClip FindVoiceClip(string id)
        {
            foreach (string ext in new[] { ".wav", ".ogg", ".mp3" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/SourceFiles/SoundFX/Dialogue/pt-BR/{id}{ext}");
                if (clip != null) return clip;
            }

            return null;
        }
    }
}
