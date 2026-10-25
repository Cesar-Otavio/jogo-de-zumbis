# Fase 7 — Narrativa e áudio (Marco 4)

**Estado: NÃO INICIADO nesta fase.**

- **Falas**: as 54 estão no `Docs/FASE5_DIALOGOS_MANIFESTO.csv`, com o pipeline de religação pronto (*Phase 5 ▸ 4*).
- **Vozes**: **bloqueado por decisão sua**. As opções são:
  - (a) um TTS que você autoriza, com a credencial configurada por você, fora do repositório;
  - (b) gravação;
  - (c) seguir só com legendas.
  - O primeiro lote seria a missão 1 (9 falas).
- **AudioMixer**: feito (seção abaixo).

## AudioMixer (Marco 4, §9.4): feito

- **Arquivo**: `Assets/SourceFiles/Resources/Audio/GameMixer.mixer`, criado pelo menu *Tools ▸ Combat ▸ Phase 7 ▸ 6*.
  - O Unity não tem API pública para montar grupos de mixer. A ferramenta usa as mesmas chamadas internas da janela Audio Mixer.
  - Rodar de novo só acrescenta o que falta.
- **Grupos**: Master ▸ Music, SFX, Dialogue, Radio, Ambient e UI. Cada volume está exposto como `Vol_<grupo>`.
- **Roteamento** (`AudioRouting.cs`):

| Grupo | O que vai para ele |
|---|---|
| Music | música do `GameFlow` |
| SFX | efeitos do `CombatEffects`, alarme de energia, gerador, fogo, sinalizador, dispositivos de missão, batimento e respiração de ferimento; e qualquer fonte da cena que nenhum sistema rotear |
| Dialogue | voz de personagem presente (Narrador) |
| Radio | voz de rádio e o chiado |
| Ambient | vento, chuva, zumbido das lâmpadas, ambientes do `CombatEffects` |
| UI | sons de interface |

- **Volumes existentes preservados**: as opções (Geral, Música, Efeitos, Diálogos e rádio, Ambiente, Interface) continuam escalando cada fonte como antes. Os grupos começam em 0 dB, então nada muda para quem já tinha volumes salvos.
- **Ducking durante falas**:
  - a música cai para 35%, regra que já existia;
  - o grupo **Ambient** cai **−8 dB**, com transição de cerca de 0,7 s;
  - **SFX não cai**, para que passos, gemidos e ataques continuem audíveis durante o rádio;
  - o tratamento de rádio (passa-altas 380 Hz, passa-baixas 3,4 kHz, leve distorção) continua nos filtros da fonte de voz.
- **Testes no editor (Play, `MilitaryBase_Combat`)**:
  - 92 fontes na cena, todas com grupo: Ambiente 10, Efeitos 80, Música 1, Rádio 1;
  - durante uma fala de rádio (Duarte): ambiente −8 dB, efeitos 0 dB, música 35%, voz no grupo Radio;
  - depois da fala: ambiente 0 dB e música 100%;
  - console sem erros.
- **Release com o mixer** (sonda, onda de 30): Médio 1080p 10,54 ms (antes 10,24), Baixo 720p 7,07 ms (antes 6,88). Diferença dentro da variação entre execuções.
- **Não testado**: a audibilidade real, de ouvido, pelo jogador. Depende de você ouvir uma fala de rádio no meio de uma onda.
