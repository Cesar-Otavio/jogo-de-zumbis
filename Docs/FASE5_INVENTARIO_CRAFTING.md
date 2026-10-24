# Fase 5 — Inventário e crafting

## 1. Onde os itens ficam

| Lugar | O que guarda | Regras | Código |
|---|---|---|---|
| Barra rápida (6) | kit médico, granada, molotov, sinalizador, barricada, distração | capacidade por item (3/3/2/3/2/3, +1 por bolsa tática); teclas 1–6, Z, X | `InventoryManager` (sem mudança de regras) |
| Armas (2) | em mãos e nas costas | trocar, largar e pegar como antes | `PlayerCombat.Slots` |
| Colete | proteção até 60 | absorve metade do dano | `Health.Armor` |
| **Mochila (18)** | materiais, bandagens, reforços, pacotes e **excedente da barra rápida** | espaços com pilhas por item (`ItemDefinition.MaxStack`) | `Backpack` (novo) |
| Itens de missão | fusível, cartão, caixa-preta, códigos… | **não ocupam a mochila** e nunca bloqueiam a história; não podem ser descartados | `RunState` (sem mudança) |

A mochila dura uma partida, como a barra rápida e os créditos: `RunState.Reset` a esvazia.

## 2. Integração com a barra rápida

- **Coletar** um item rápido com a barra cheia: o excedente vai para a mochila, com o aviso "Barra cheia: N na mochila". Antes ele ficava no chão.
- **Usar** um item da barra: se houver reserva na mochila, ela repõe a barra na hora.
- **Na mochila**:
  - um item rápido mostra "MOVER PARA A BARRA";
  - na barra, "GUARDAR 1 NA MOCHILA" e "PEGAR DA MOCHILA".
- **Comprar** na loja com a barra cheia vai para a mochila. Só mostra "SEM ESPAÇO" se as duas estiverem cheias.

## 3. Tela (Tab, ou "MOCHILA" no menu de pausa)

- **Esquerda — equipamento**:
  - as duas armas, com munição, calibre e melhorias instaladas (inspeção de arma);
  - o colete e a vida;
  - a barra rápida;
  - os itens de missão.
- **Centro — mochila**: grade 6×3 com ícone, quantidade e cor por categoria. O botão ORGANIZAR junta pilhas e ordena por categoria.
- **Direita — detalhes**: ícone, nome, categoria, quantidade, pilha máxima, descrição e "Usado em: …" para materiais.
  - Só aparecem as ações possíveis agora: USAR, MOVER PARA A BARRA, SEPARAR PILHA, MOVER (clique no destino), DESCARTAR.
  - Quando uma ação não é possível, o motivo aparece em vermelho ("Vida cheia", "Colete cheio", "Material de fabricação", "Mochila cheia").
- **Descartar** larga o item no chão como um objeto coletável (`WorldItem`). Nada é destruído.
- **Mochila cheia** ao pegar algo: o objeto fica no mundo com o aviso "mochila cheia" no próprio prompt e a dica "[Tab]".
- **Tempo**: enquanto a tela está aberta, o jogo roda a 25% (`GameplayOverlay`), sem pausar, e os tiros ficam bloqueados. Esc, Tab ou o B do gamepad fecham.

## 4. Materiais

| ID | Nome | Pilha | Raridade | Onde aparece |
|---|---|---|---|---|
| `scrap` | Sucata metálica | 10 | comum | baús (mais frequente), depósito, área inicial |
| `cloth` | Tecido | 10 | comum | baús, depósito, área inicial |
| `wiring` | Fiação | 10 | comum | baús, depósito, sala do gerador |
| `med_supplies` | Material médico | 5 | incomum | baús, laboratório, torre |
| `electronics` | Componentes eletrônicos | 5 | incomum | baús, estação de rádio |
| `mech_parts` | Peças mecânicas | 5 | incomum | baús, sala do gerador |
| `chemicals` | Componentes químicos | 5 | incomum | baús, laboratório |
| `tactical_parts` | Peças táticas | 3 | rara | baús (raro), arsenal |

- **Baús**: 75% de chance (× suprimentos da dificuldade) de deixar 1–2 materiais. Sucata e tecido vêm em 1–2 unidades; os outros, 1.
- **Locais fixos**: 12 materiais colocados no mapa perto dos locais que os explicam (menu *Phase 5 ▸ 3*). Voltam a cada partida, porque a cena recarrega.

Produtos novos (vão para a mochila e são usados de lá):

| ID | Nome | Uso |
|---|---|---|
| `bandage` | Bandagem | +20 de vida |
| `vest_patch` | Reforço de colete | +30 de colete (máx. 60) |
| `supply_pack` | Pacote de suprimentos | 2 pentes por arma (ainda sem receita nem fonte no mapa) |

## 5. Receitas

| Receita | Materiais | Produz | Onde |
|---|---|---|---|
| Bandagem | 2 tecido | bandagem | na mochila (aba FABRICAÇÃO) |
| Kit médico improvisado | 2 material médico + 1 tecido | kit médico (barra) | bancada |
| Dispositivo de distração | 1 eletrônico + 1 sucata + 1 fiação | distração (barra) | bancada |
| Sinalizador improvisado | 1 químico + 1 sucata | sinalizador (barra) | bancada |
| Kit de barricada | 2 sucata + 1 peça mecânica | barricada (barra) | bancada |
| Reforço de colete | 2 sucata + 1 peça tática + 1 tecido | reforço de colete | bancada |

**Balanceamento**: cada receita custa de 2 a 4 materiais e produz um item que também se encontra ou compra. O kit médico da loja custa 80 créditos, a distração 50. O crafting complementa a loja e o loot, sem substituí-los. Nenhum item fabricado é melhor que o comprado.

Dados: `Assets/SourceFiles/Combat/Crafting/Recipe_*.asset` e `Resources/CraftingCatalog.asset`. Para mudar custos, edite os assets ou a tabela em `CombatSetup.Phase5.cs` e rode *Phase 5 ▸ 1*.

## 6. Regras de fabricação (anti-duplicação)

`Crafting.Craft`:

1. **Verifica tudo antes de mudar qualquer coisa**:
   - receita desbloqueada (nível e flag);
   - estação certa;
   - materiais;
   - espaço para o resultado.
2. **Uma fabricação por quadro**: dois cliques no mesmo quadro não fabricam duas vezes.
3. **Retira os materiais** (cada retirada é tudo ou nada) e entrega o produto na mesma chamada. Se alguma retirada falhar, devolve o que já tinha tirado.
4. **Espaço**:
   - um produto que é item rápido vai para a barra, e o excedente para a mochila;
   - os espaços que os materiais vão liberar contam como livres.
5. **Tempo**: a fabricação é instantânea (`CraftTime` existe, mas não é usado no MVP). Não há estado "em andamento" para cancelar, portanto não há regra de devolução por cancelamento.

## 7. Bancada

- **Onde**: `Workbench_Depot`, no **Depósito de suprimentos**.
- **Visual — provisório**: montado com primitivas e os materiais `Loot_Crate` e `Loot_Metal` do projeto, porque o pacote de cenário não tem móveis.
  - Tem tampo, pernas, prateleira, morsa, caixa de ferramentas, painel, placa "OFICINA" e uma luz de trabalho.
  - Um colisor único na bancada inteira e um `NavMeshObstacle` com escavação, para os inimigos desviarem.
- **Interação**: E abre a tela da mochila direto na aba de fabricação, com o nome da bancada. Toca o som de baú e, na primeira vez, Duarte comenta (`fx_workbench`).
- **No mapa**: ícone de bancada, depois que a área é descoberta.
- **Tipos previstos**: `Medical` e `Maintenance` já existem como tipos de estação, mas ainda não há bancadas desses tipos.

## 8. Persistência

| Estado | Duração |
|---|---|
| Mochila, barra rápida, créditos, itens de missão | uma partida (`RunState.Reset`) |
| Materiais no mapa | voltam a cada partida (a cena recarrega) |
| Descobertas do mapa | permanentes (`PlayerPrefs`); zeradas ao reiniciar a campanha |
| Marcas do jogador | sessão atual (até 5) |

Não há salvamento no meio do capítulo nesta entrega.
