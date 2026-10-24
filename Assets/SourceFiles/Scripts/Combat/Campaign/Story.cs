using System;
using System.Collections.Generic;

namespace Combat
{
    /* The story of Protocolo Aurora, told through documents, military logs and radio
     * transmissions rather than cutscenes (the spoken lines are in the DialogueLibrary asset). Documents open in the reader (the game pauses while
     * reading); radio lines play over the action. Found documents are kept by Progression.
     */
    public static class Story
    {
        public struct Entry
        {
            public string Title;
            public string Author;
            public string Text;
        }

        public static event Action<string, Entry> DocumentOpened;
        public static event Action<string, string> RadioReceived;

        public static readonly Dictionary<string, Entry> Documents = new Dictionary<string, Entry>
        {
            ["doc_shift_log"] = new Entry
            {
                Title = "Diário de turno — Portão Leste",
                Author = "Cabo R. Moraes",
                Text = "22h10 — Chegaram mais três caminhões do Setor 4 com caixas lacradas. Ninguém pode abrir, ordens do Dr. Valença.\n\n" +
                       "23h40 — Gritos vindos dos hangares. O sargento disse que é treinamento.\n\n" +
                       "00h15 — Treinamento não sangra daquele jeito. Fechei o portão por conta própria. Se alguém ler isso: não confie no rádio do comando."
            },
            ["doc_generator_memo"] = new Entry
            {
                Title = "Memorando de manutenção",
                Author = "Eng. P. Siqueira",
                Text = "Os geradores de setor só religam com fusíveis novos — os antigos queimaram na sobrecarga de ontem.\n\n" +
                       "Guardei os sobressalentes espalhados pela base para não 'sumirem' de novo. Quem ligar o gerador vai fazer barulho. Muito barulho.\n\n" +
                       "Coisas lá fora estão atentas ao barulho."
            },
            ["doc_radio_order"] = new Entry
            {
                Title = "ORDEM 7-ALFA (interceptada)",
                Author = "Comando Regional",
                Text = "A instalação está em quarentena total. Unidades de contenção têm autorização para impedir a saída de QUALQUER pessoa, militar ou civil.\n\n" +
                       "Testemunhas do Protocolo Aurora são consideradas contaminadas até prova em contrário.\n\nNão haverá evacuação."
            },
            ["doc_armory_code"] = new Entry
            {
                Title = "Bilhete do intendente",
                Author = "Sgt. L. Arantes",
                Text = "Mudei o acesso do arsenal para cartão magnético depois que o Experimento escapou.\n\n" +
                       "Um cartão ficou com o oficial de dia, perto das torres. Se eu não voltar, que alguém use as armas contra ELES, não contra nós."
            },
            ["doc_e07_report"] = new Entry
            {
                Title = "Relatório de contenção — E-07",
                Author = "Dr. H. Valença",
                Text = "Sujeito 07 respondeu ao composto Aurora com hipertrofia muscular e perda total de cognição.\n\n" +
                       "Investe contra fontes de ruído e luz. Depois de cada investida o sujeito fica atordoado por alguns segundos — a musculatura não sustenta o esforço.\n\n" +
                       "Recomendação: induzir colisões contra estruturas pesadas e atacar durante o atordoamento."
            },
            ["doc_lab_notes"] = new Entry
            {
                Title = "Notas de laboratório — Aurora",
                Author = "Dra. C. Nogueira",
                Text = "O composto Aurora deveria regenerar tecido em combate. Funciona: o tecido não para de regenerar, nem depois da morte cerebral.\n\n" +
                       "Os infectados reagem a som e são cegos em luz forte. Os gritadores parecem coordenar os outros.\n\n" +
                       "Valença sabia desde o primeiro teste. Mandou continuar."
            },
            ["doc_containment"] = new Entry
            {
                Title = "Procedimento de contenção",
                Author = "Segurança da base",
                Text = "Em caso de brecha: fechar as válvulas de ventilação 1, 2 e 3, nessa ordem.\n\n" +
                       "A ordem importa — abrir fora de sequência espalha o agente pelos dutos.\n\nDepois de fechadas, os setores contaminados ficam lacrados."
            },
            ["doc_blackbox"] = new Entry
            {
                Title = "Caixa-preta — registro final",
                Author = "Sistema AURORA",
                Text = "SUJEITO PRIMÁRIO 'AURORA': fotossensível. A luz direta enfraquece a regeneração.\n\n" +
                       "Protocolo de queima ativado às 04h00. Toda a base será esterilizada.\n\n" +
                       "Única janela de extração: heliponto norte, antes do amanhecer."
            },
            ["doc_evac_codes"] = new Entry
            {
                Title = "Códigos de evacuação",
                Author = "Tenente M. Duarte",
                Text = "Se o rádio voltar, o piloto responde ao código AURORA-NOITE.\n\n" +
                       "O helicóptero só pousa com o farol do heliponto aceso. Ele vai atrair tudo o que ainda se move nesta base.\n\nAguente até o pouso."
            }
        };

        public static void Open(string id)
        {
            if (id == null || !Documents.TryGetValue(id, out Entry entry)) return;
            Progression.FoundDocument(id);
            DocumentOpened?.Invoke(id, entry);
        }

        /* Radio lines live in the DialogueLibrary (with their speaker, voice clip and timing) and are played
         * by the DialogueDirector. Without a director in the scene the text still reaches the HUD's radio box. */
        public static void PlayRadio(string id)
        {
            if (id == null) return;
            if (DialogueDirector.Instance != null && DialogueDirector.Instance.Play(id)) return;
            DialogueLine line = DialogueLibrary.Instance != null ? DialogueLibrary.Instance.Find(id) : null;
            RadioReceived?.Invoke(id, line != null ? line.Text : id);
        }
    }
}
