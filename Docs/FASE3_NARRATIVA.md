# Fase 3 — Narrativa, rádio e legendas

## 1. Personagens

| Personagem | Papel | Voz | Observação |
|---|---|---|---|
| **Narrador** | abertura e momentos-chave | voz grave, calma, sem filtro de rádio | legenda em itálico |
| **Tenente M. Duarte** — indicativo **"Controle Aurora"** | operador de rádio; orienta, informa missões, reage aos eventos | rádio militar (filtro passa-banda, leve distorção, cliques de squelch, chiado) | **Já existia no projeto** (falas de rádio do Prompt 2 e autor do documento "Códigos de evacuação"). Foi mantido no lugar do "Elias" proposto, para preservar a continuidade |
| **Piloto — Águia 2** | resgate na reta final | rádio, mais limpo | novo |

Personalidade de Duarte: sério, contido, experiente. Fica cada vez mais desconfiado ("não confie no rádio do comando", "foi um teste").

## 2. Arco (sobre as missões que já existiam, sem reescrevê-las)

| Ato | Capítulos | O que o jogador descobre |
|---|---|---|
| 1 — Isolamento | 1–3 | está sozinho; a energia e o rádio caíram; o comando não vem buscar ninguém |
| 2 — Investigação | 4–6 | arsenal, Experimento 07, torre; "não foi acidente, foi um teste" |
| 3 — Revelação | 7–8 | laboratório, Dr. Valença, protocolo de queima às 4h |
| 4 — Evacuação | 9–10 | código AURORA-NOITE, piloto Águia 2, Aurora, helicóptero |

## 3. Prólogo (~45 s, pode ser pulado com Espaço/Enter/Esc)

São 4 planos de câmera sobre a base: alto, hangares, heliponto e o jogador. Por cima, as falas:

| id | Quem | Texto |
|---|---|---|
| prologue_1 | Narrador | Às vinte e três e quarenta, os primeiros gritos vieram dos hangares. O sargento de serviço disse que era treinamento. |
| prologue_2 | Narrador | À meia-noite e quinze, a base parou de responder ao comando regional. Pouco depois, a contenção do laboratório começou a falhar. |
| prologue_3 | Narrador | O último relatório falava de um composto chamado Aurora. Nenhuma equipe de resgate veio confirmar o que aconteceu. |
| prologue_4 | Duarte | ...alô? Alguém na escuta? Aqui é o Tenente Duarte, Controle Aurora. Se tem alguém vivo aí dentro, responda. |
| prologue_5 | Duarte | A comunicação está instável, e eu não sei quanto tempo a gente tem. |
| prologue_6 | Duarte | Pegue uma arma e saia do pátio. Tem alguma coisa se movendo entre os alojamentos. |

- Só toca numa campanha nova (capítulo 0). Não toca em respawn nem ao recomeçar uma onda.
- Pular corta as falas do grupo `prologue`.
- A fala antiga `radio_wake` tem `SkipIfPlayed = prologue_6`: não repete o que o prólogo acabou de dizer.

## 4. Falas por gatilho

Todas estão em `Assets/SourceFiles/Resources/DialogueLibrary.asset`, geradas por *Tools ▸ Combat ▸ Phase 3 ▸ 4*. A tabela-fonte é o método `Script()` em `Editor/CombatSetup.Narrative.cs`.

| id | Quem | Gatilho | Prioridade | Texto |
|---|---|---|---|---|
| radio_wake | Duarte | objetivo 1 (já existia) | Alta | ...está me ouvindo? Aqui é o Tenente Duarte. Se você está vivo, pegue uma arma. Eles vêm atrás do barulho. |
| fc_armed | Duarte | m_first_contact obj. 0 | Média | Boa. Agora você tem com o que se defender. Dê uma olhada em volta, mas sem pressa. |
| fc_area | Duarte | m_first_contact obj. 1 | Alta (corta) | Estou ouvindo movimento perto do seu sinal. Muito movimento. Prepare-se. |
| fc_done | Duarte | m_first_contact concluída | Média | Você ainda está de pé. Isso já é mais do que a maioria aqui conseguiu. |
| radio_power | Duarte | início de m_generator | Alta | Estou vendo uma queda no gerador do setor noroeste. Se a energia cair de vez, os portões de segurança param. Vá até a sala de máquinas. |
| gen_found | Duarte | m_generator obj. 0 | Média | Achei a origem da falha. Tiraram dois fusíveis do painel. Devem estar espalhados pela base. |
| gen_fuse1 | Duarte | 1º fusível (só com a missão ativa) | Média | Ótimo, um já foi. Falta mais um. Cuidado: estou ouvindo movimento perto de você. |
| gen_fuse2 | Duarte | 2º fusível | Média | Os dois fusíveis. Volte para o painel e instale. |
| gen_power | Duarte | gerador religado | Crítica (corta) | Energia restaurada... espere. O barulho atraiu alguma coisa. Segure a posição até o gerador estabilizar! |
| gen_done | Duarte | m_generator concluída | Média | O gerador está estável. Boa. Respire um pouco, enquanto pode. |
| radio_signal | Duarte | início de m_signal | Alta | ...tchhh... estação de comunicação... módulo... repetindo... protocolo... tchhh... |
| sig_station | Duarte | m_signal obj. 0 | Média | Essa estação é a nossa chance de ouvir o que o comando está dizendo. Falta o módulo de rádio. |
| sig_module | Duarte | m_signal obj. 1 | Média | Achou o módulo. Instale no console e vamos ver o que sai daí. |
| radio_signal_ok | Duarte | m_signal concluída | Alta | Sinal limpo! Escute... o comando não vem nos buscar. A ordem é conter a base. Conter a gente. |
| radio_armory | Duarte | início de m_armory | Alta | O arsenal está trancado por cartão. O intendente deixou um com o oficial de dia, perto das torres. |
| arm_card | Duarte | m_armory obj. 0 | Média | É esse cartão. O leitor fica na porta do arsenal. |
| arm_done | Duarte | m_armory concluída | Média | Com isso dá para aguentar mais uma noite. Talvez. |
| radio_e07 | Duarte | início de m_e07 | Crítica (corta) | Alguma coisa enorme rompeu a contenção do laboratório. O Experimento 07. Faça ele bater em algo pesado e acerte quando cambalear. |
| radio_e07_dead | Duarte | E07 derrotado | Alta | Você derrubou o 07... Isso não foi acidente. Foi um teste. E você passou. |
| radio_tower | Duarte | início de m_tower | Alta | A torre de vigilância tem refletores. A luz segura essas coisas por um tempo. Religue a alimentação dela. |
| tow_lights | Duarte | refletores acesos | Alta (corta) | Refletores acesos! Agora eles vão querer ver o que é. Defenda a torre. |
| tow_done | Duarte | m_tower concluída | Média | Consegui ver a base pelas câmeras da torre. Está pior do que eu pensava. |
| radio_lab | Duarte | início de m_blackbox | Alta | O laboratório fica no setor sudeste. Os registros de lá devem explicar tudo. Recupere a caixa-preta. |
| lab_enter | Duarte | entrou no laboratório | Alta | Você está dentro do laboratório. Fique atento: foi daqui que tudo saiu. |
| lab_docs | Duarte | registros lidos | Média | Valença... eu conheço esse nome. Era ele quem assinava as ordens do Setor 4. |
| lab_box | Duarte | caixa-preta recuperada | Crítica | A caixa-preta fala de um protocolo de queima às quatro da manhã. Eles vão apagar a base inteira. Com a gente dentro. |
| radio_alarm | Duarte | início de m_alarm | Alta (corta) | O alarme disparou sozinho e está chamando a horda inteira. Desligue os painéis! |
| alarm_done | Duarte | m_alarm concluída | Baixa | Silêncio de novo. Ou o mais perto disso que dá para ter aqui. |
| radio_supply | Duarte | início de m_supply | Baixa | Tem um depósito de suprimentos no fundo da base. Abrir aquilo vai fazer barulho, mas está cheio de munição. |
| radio_gates | Duarte | início de m_gates | Média | Os portões estão abertos. Feche todos e reforce as passagens. Cada minuto conta. |
| radio_contain | Duarte | início de m_containment | Crítica (corta) | Os dutos de ventilação estão abrindo! Feche as válvulas na ordem certa, ou a base inteira vira um ninho. |
| cont_done | Duarte | m_containment concluída | Média | Setores lacrados. Isso compra um pouco de tempo. |
| evac_codes | Duarte | pegou os códigos | Alta | AURORA-NOITE. É esse o código. Leve até a estação de rádio. |
| evac_sent | Piloto | código transmitido | Alta | Controle Aurora, aqui é Águia 2. Código recebido. Preparem a zona de pouso e acendam o farol. |
| radio_evac | Duarte | m_evac_prep concluída | Alta | Você ouviu. Prepare o heliponto e acenda o farol. Ele chega em minutos. |
| radio_final | Duarte | início de m_evacuation | Crítica (corta) | A Aurora está vindo. É ela, a coisa do laboratório. Use a luz contra ela e aguente até o helicóptero tocar o chão! |
| evac_beacon | Piloto | farol aceso | Alta | Farol à vista. Estou a quatro minutos. Mantenham a zona limpa. |
| aurora_down | Duarte | Aurora derrotada | Crítica (corta) | Ela caiu... ela caiu! Aguente mais um pouco, ele está chegando! |
| evac_done | Piloto | evacuação concluída | Crítica (corta) | Tocando o chão! Embarca, embarca! |
| react_blackout | Duarte | apagão (intervalo de 150 s, só se ninguém estiver falando) | Baixa | Perdi a leitura de energia de um setor. Sem luz eles ficam mais ousados. |
| react_lowammo | Duarte | reserva 0 e pente ≤ 1/3 (intervalo de 120 s) | Baixa | Sua munição está acabando. Procure as caixas de suprimento. |
| react_critical | Duarte | vida < 25% (intervalo de 90 s) | Alta (corta) | Você está ferido! Saia da linha de fogo e use um kit médico! |
| react_failed | Duarte | missão falhou (intervalo de 30 s) | Média | Perdemos essa. Respire. Vamos tentar de novo. |
| react_lastnight | Duarte | início do capítulo 10 | Média | É a última noite. Se o piloto vier, vai ser agora. |

## 5. Regras do diretor (`DialogueDirector`)

- **Fila com prioridade**: Crítica > Alta > Média > Baixa; com prioridades iguais, a mais antiga toca primeiro. Nunca há duas falas ao mesmo tempo.
- **Interrupção**:
  - `InterruptLower` corta uma fala de prioridade menor, com um clique de rádio.
  - `DropIfBusy` descarta a fala se alguém já estiver falando (comentários de ambiente).
- **Falas obsoletas** saem da fila:
  - quando ficam velhas demais (10 s nas comuns, 20 s nas altas, 90 s no prólogo);
  - quando a missão delas acabou (`RequireMissionActive`);
  - quando o objetivo mudou.
- **Repetição**: "uma vez por partida" ou com intervalo mínimo. `ResetAll` a cada nova partida.
- **Música**: cai para 35% enquanto alguém fala e volta suavemente. Os sons de inimigos não são abaixados.
- **Sem internet**: tudo é local. Sem áudio gravado, a fala vira legenda e sons de rádio, com duração pelo tamanho do texto (15 caracteres/s).

## 6. Legendas

- **Diálogo** (`DialogueSubtitleView`):
  - nome do personagem colorido e texto entre aspas (narrador em itálico), embaixo e no centro;
  - painel do tamanho do texto, que também aparece sobre a cinemática, na faixa preta;
  - opções: liga/desliga e tamanho de 80% a 150%;
  - suporta segmentos temporizados (`SubtitleSegment`) quando houver áudio sincronizado.
- **Efeitos sonoros** (*closed captions*): o sistema `Subtitles` já existia (passos, alarmes, portas, rajadas de vento, com o lado de onde vem o som). Tem opção separada ("Legendas de sons") e intervalo para não repetir.

## 7. Localização

- Cada fala tem `LocalizationKey = dlg.<id>`, e os áudios ficam em pastas por idioma (`SoundFX/Dialogue/pt-BR/`).
- O pacote Unity Localization **não** foi instalado (não estava no projeto). Para inglês ou espanhol:
  1. instale o pacote;
  2. crie a String Table `Dialogue` com as chaves `dlg.*`;
  3. troque `DialogueLine.Text` pela entrada da tabela.
- Nenhuma tradução automática foi feita.

## 8. Dublagem (pipeline)

1. Gere cada fala com um serviço TTS licenciado (ElevenLabs, Azure Speech), em pt-BR. Use vozes de catálogo; **não clone vozes de pessoas reais sem autorização**.
2. Salve como `Assets/SourceFiles/SoundFX/Dialogue/pt-BR/<id>.wav`, por exemplo `gen_power.wav`.
3. Rode *Tools ▸ Combat ▸ Phase 3 ▸ 4*: cada clipe é ligado à sua fala, e os já ligados são preservados.
4. A duração passa a ser a do clipe. Para legendas sincronizadas por trecho, preencha `Segments` na fala.
5. O tratamento de rádio é aplicado em tempo real (passa-alta 380 Hz, passa-baixa 3,4 kHz, distorção 0,18, squelch e chiado). Grave as vozes **limpas**.

Nenhuma chamada de API acontece durante o jogo.
