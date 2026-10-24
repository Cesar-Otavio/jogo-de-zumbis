# Fase 5 — Mapa e exploração

## 1. Mapa da base (sem segunda câmera)

O mapa é uma **imagem pré-gerada no editor**, não uma câmera renderizando o cenário a cada quadro.

- **Geração** (*Tools ▸ Combat ▸ Phase 5 ▸ 2 - Bake Map*):
  - uma grade de 512×512 raios de cima para baixo sobre o quadrado que contém todos os locais com nome, mais 62 m de margem;
  - cada ponto é classificado pela altura acima do terreno: chão, obstáculo baixo, parede ou contêiner, telhado;
  - recebe uma borda clara onde algo se ergue do chão e uma grade fraca a cada 25 m.
- **Saída**:
  - `Assets/SourceFiles/UI/Map/BaseMap.png`;
  - `Resources/BaseMap.asset` (`MapDefinition`), com a textura e o quadrado do mundo.
- **Resultado atual**: quadrado de 287 m, de (−165, −113) a (122, 174), com cerca de 0,56 m por pixel.
- **Custo em jogo**: uma textura de 512 px e algumas imagens de UI. Nenhuma renderização extra.
- **Coordenadas**:
  - `MapDefinition.ToMap(mundo)` → 0..1 (x = x do mundo, y = z do mundo; norte para cima);
  - `ToWorld` faz o inverso;
  - minimapa, mapa tático e névoa usam a mesma conversão.
- **Refazer** depois de mudar o cenário: *Phase 5 ▸ 2*.

## 2. Minimapa (`MinimapView`)

- **Posição**: canto superior direito, abaixo do cartão de pontuação. Disco de 260 px com borda âmbar.
- **Conteúdo**:
  - o mapa movendo-se sob o jogador;
  - a névoa;
  - a seta do jogador (direção do corpo);
  - o "N" na borda quando o mapa gira;
  - locais conhecidos e bancadas;
  - as marcas do jogador;
  - o objetivo atual (losango pulsante), que fica preso na borda quando está longe.
- **Abaixo do disco**: "◆ objetivo · distância".
- **Opções** (Opções ▸ Imagem):
  - ligar ou desligar;
  - orientação pela câmera ou fixa no norte;
  - zoom perto, médio ou longe (raio de 32, 55 ou 85 m).
- **Some** no menu, em cinemáticas, com o HUD escondido e com o mapa tático aberto.
- **Inimigos**: nunca aparecem. O mapa não faz o trabalho de ouvir pelo jogador.

## 3. Mapa tático (`TacticalMapView`)

- **Abrir**: M, ou "MAPA" no menu de pausa (o caminho pelo gamepad). Fecha com M, Esc ou o B do gamepad.
- **Tempo**: o jogo continua a 25% enquanto o mapa está aberto.
- **Mostra**:
  - a base inteira com a névoa;
  - os locais conhecidos com nome;
  - a bancada;
  - o jogador;
  - o objetivo com nome;
  - as marcas do jogador.
- **Legenda à direita**:
  - objetivo e distância;
  - locais conhecidos (X/13), com cor e tipo;
  - porcentagem explorada;
  - marcas usadas.
- **Marcas**: clique esquerdo marca (até 5; a mais antiga sai quando passa disso); clique direito remove a mais próxima.

## 4. Descoberta (névoa)

- **Grade**: `MapDiscovery`, 64×64 células sobre o mapa, cada uma com cerca de 4,5 m.
- **Revelação**: a cada 0,25 s, tudo num raio de 22 m em volta do jogador.
- **Outras formas de revelar**: a API `MapDiscovery.Reveal(posição, raio)` serve para documentos, terminais e missões.
  - Ela existe, mas **ainda não está ligada a nenhum objeto**; isso fica para o Marco 4.
- **Persistência**:
  - fica em `PlayerPrefs`, gravada no máximo a cada 5 s e ao sair;
  - a base não muda entre partidas, então o que foi visto continua conhecido;
  - **reiniciar a campanha apaga o mapa**.
- **Locais**: um local só aparece quando a sua célula foi vista.
  - Na primeira vez entra no feed "Local descoberto: …".
  - Se existir a fala `poi_<id>`, Duarte comenta: laboratório, depósito e torre têm fala.

## 5. Marcadores

`MapMarker` (componente), criado automaticamente a partir das 13 `MissionAnchor`:

| Tipo | Locais | Cor |
|---|---|---|
| Portão (saída) | Portão 1, 2, 3 | verde |
| Extração | Heliponto | ciano |
| Suprimentos | Depósito de suprimentos | amarelo claro |
| Local | Arsenal, Laboratório, Relé central, Alarme, Torre de vigilância, Sala do gerador, Estação de rádio, Área próxima | branco |
| Bancada | Bancada (depósito) | âmbar, ícone próprio |

Áreas seguras: o tipo existe, mas **ainda não há área segura no mapa**.

## 6. Área escolhida para exploração (objetivo F)

**Depósito de suprimentos**, a oeste (x −103, z 45): um pátio de contêineres longe do centro.

**Feito nesta entrega:**
- bancada de trabalho;
- sucata, tecido e fiação por perto;
- fala de Duarte ao descobrir;
- marcador de suprimentos.

**Próximo passo para o depósito (Marco 3/4):**
1. **Contêiner aberto como sala**: o pacote tem contêineres; uma porta abrindo para um interior pequeno com prateleiras.
2. **Armário com código**: o código fica num documento novo achado no relé central. É uma pista que leva de um lugar a outro.
3. **Terminal de inventário do depósito**: revela no mapa os outros pontos de suprimento (usa `MapDiscovery.Reveal`).
4. **Missão secundária "Oficina isolada"**: reativar a luz do depósito pelo painel, pegar ferramentas e desbloquear a receita do reforço de colete por flag (`CraftingRecipe.UnlockFlag` já existe).

**Limite real**: o projeto não tem móveis nem interiores (camas, armários, monitores, mesas de laboratório). Enfermaria, alojamentos e sala de controle precisam de um pacote de props, gratuito e com licença verificada, antes de serem construídos.

## 7. Materiais colocados

São 12 objetos `WorldItem` em `CraftingMaterials`, cada um colocado no ponto livre mais próximo do lugar planejado (nunca dentro de contêiner ou parede):

| Local | Materiais |
|---|---|
| Depósito | 2 sucata, 2 tecido, 1 fiação |
| Sala do gerador | 1 peça mecânica, 2 fiação |
| Estação de rádio | 1 eletrônico |
| Laboratório | 1 químico, 2 material médico |
| Arsenal | 1 peça tática |
| Área próxima (início) | 1 tecido, 1 sucata |
| Torre | 1 material médico |

Os materiais da área inicial permitem fabricar uma bandagem logo no começo, o que ensina o sistema.
