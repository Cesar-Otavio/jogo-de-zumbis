# Fase 5 — Narrativa e áudio

## 1. Estado da dublagem

**Pendente.** Nenhum arquivo de voz foi produzido.

- Esta sessão não tem acesso autorizado a um serviço de TTS (ElevenLabs, Azure Speech ou outro), e o pedido proíbe enviar dados a serviços externos ou executar serviços pagos sem autorização. Por isso nada foi gerado nem enviado.
- O jogo funciona sem voz: cada fala aparece como legenda, com bip e chiado de rádio (`RadioSounds`) e a música abaixando durante a fala.
- Tudo roda offline, sem chamada de rede durante a partida.

## 2. Personagens (sem mudanças)

| Voz | Papel | Estilo pedido para a gravação |
|---|---|---|
| Narrador | prólogo, revelações, encerramento | grave, ritmo controlado, tom documental; sem rádio |
| Tenente M. Duarte — Controle Aurora | operador de rádio durante o jogo | militar natural, frases curtas, urgência contida; passa pelo filtro de rádio do jogo |
| Piloto — Águia 2 | evacuação | ruído de cabine, pressa |

Não usar clonagem de voz de pessoas reais. As vozes devem ser sintéticas genéricas, do catálogo do serviço, ou gravadas por quem autorizar.

## 3. Manifesto das falas

`Docs/FASE5_DIALOGOS_MANIFESTO.csv`, gerado pelo menu *Tools ▸ Combat ▸ Phase 5 ▸ 4 - Export Dialogue Manifest*. É separado por `;` e salvo em UTF-8 com BOM, para abrir direto no Excel.

| Coluna | Conteúdo |
|---|---|
| id | ID estável da fala (também o nome do arquivo) |
| personagem | quem fala |
| missao | missão do gatilho, quando houver |
| gatilho | MissionStarted, ObjectiveCompleted, Blackout, Manual… |
| prioridade | Low, Medium, High, Critical |
| texto | o texto em português |
| arquivo | `SoundFX/Dialogue/pt-BR/<id>.wav` |
| duracao_estimada_s | pela velocidade de leitura do diretor (15 caracteres/s); com o arquivo, a duração real |
| producao | pendente / gravada |
| revisao | a revisar, depois de gravada |
| clip | caminho do AudioClip ligado |

## 4. Como adicionar as vozes

1. Gerar ou gravar cada fala como WAV mono, 44,1 ou 48 kHz, sem efeito de rádio (o jogo aplica o filtro nas falas de Duarte e do piloto).
2. Salvar em `Assets/SourceFiles/SoundFX/Dialogue/pt-BR/<id>.wav`, com o nome igual ao `id` do manifesto.
3. Rodar *Phase 5 ▸ 4*. O menu liga os arquivos novos às falas, sem reconstruir a biblioteca, e atualiza o manifesto.
4. A duração e a legenda passam a seguir o arquivo. Para legendas em partes, preencha `Segments` na fala.

## 5. Primeira missão preparada para dublagem

A missão "Primeiro contato" (capítulo 1) mais o prólogo formam o primeiro lote recomendado.

| Ordem | ID | Quem |
|---|---|---|
| 1–3 | `prologue_1` … `prologue_3` | Narrador |
| 4–6 | `prologue_4` … `prologue_6` | Duarte |
| 7 | `fc_armed` (pegou a arma) | Duarte |
| 8 | `fc_area` (área investigada) | Duarte |
| 9 | `fc_done` (missão concluída) | Duarte |

São 9 arquivos. Com esse lote gravado, o critério "uma missão completa com áudio produzido" fica atendido. Hoje ele **não está atendido**.

## 6. Falas novas desta fase

| ID | Quem | Quando | Texto |
|---|---|---|---|
| `fx_workbench` | Duarte | primeiro uso de uma bancada | "Parece que ainda existem ferramentas utilizáveis. Talvez você consiga improvisar algum equipamento com os materiais da base." |
| `poi_lab` | Duarte | o laboratório aparece no mapa pela primeira vez | "Esse setor não aparece nos registros oficiais da instalação. Tenha cuidado com o que encontrar." |
| `poi_depot` | Duarte | o depósito aparece no mapa | "Depósito de suprimentos. Se sobrou alguma coisa útil nesta base, deve estar aí." |
| `poi_watchtower` | Duarte | a torre aparece no mapa | "Da torre dá para ver boa parte da base. Use isso a seu favor." |

Todas usam prioridade baixa e `DropIfBusy`: nunca interrompem nem esperam na fila atrás de algo importante, e tocam uma vez por partida (`Once`). O gatilho `poi_<id>` vem do minimapa quando um local passa a ser conhecido. Qualquer fala nova com esse padrão de ID funciona sem mexer em código.

## 7. AudioMixer

Continua **não criado**.

- Os volumes por grupo (geral, música, efeitos, diálogo, ambiente, interface) e o ducking da música funcionam por código desde a Fase 3.
- Criar o asset do mixer exige o editor (não há API pública para montar grupos por código). As instruções estão em `FASE3_ENTREGA.md` §6.
