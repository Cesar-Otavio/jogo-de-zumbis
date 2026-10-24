# Fase 5 — Assets

## 1. Criados nesta fase

| Asset | Local | Origem / licença | Estado |
|---|---|---|---|
| 12 ícones: sucata, tecido, eletrônicos, material médico, peças mecânicas, químicos, fiação, peças táticas, bandagem, bancada, reforço de colete, pacote de suprimentos | `Assets/SourceFiles/UI/Icons/Items/*.png` | Desenhados por código (`Tools/IconGen/make_icons.py`), mesmo estilo dos 25 anteriores. Arte original | final (estilo do projeto) |
| Mapa da base | `Assets/SourceFiles/UI/Map/BaseMap.png` + `Resources/BaseMap.asset` | Gerado dos colisores da cena (*Phase 5 ▸ 2*) | final; refazer se o cenário mudar |
| 11 itens novos no catálogo (8 materiais + bandagem, reforço de colete, pacote de suprimentos) | `Combat/Items/Item_*.asset` (30 no total) | Dados | final |
| 6 receitas + catálogo | `Combat/Crafting/Recipe_*.asset`, `Resources/CraftingCatalog.asset` | Dados | final (balanceamento inicial) |
| Formas do mapa (círculo, aro, seta) | geradas em tempo de execução (`MapSprites`) | Procedural | final |
| **Bancada de trabalho** | `Workbench_Depot` na cena | Primitivas + materiais `Loot_Crate`/`Loot_Metal` do projeto | **provisória**: trocar por um modelo de bancada |
| Visual dos materiais no chão | caixinha com o ícone do item e um brilho (`WorldItem`) | Primitivas + material do projeto | **provisório** |
| Manifesto de falas | `Docs/FASE5_DIALOGOS_MANIFESTO.csv` | Exportado da biblioteca (*Phase 5 ▸ 4*) | atualizado a cada execução |

Nenhum pacote foi instalado e nada foi baixado.

## 2. Pendentes (infraestrutura pronta, asset pendente)

| Necessidade | Para quê | Sugestão de origem |
|---|---|---|
| Modelo de bancada de trabalho, armários, prateleiras, caixas de ferramentas | bancada definitiva, oficina, depósito | pacote gratuito de props industriais/militares com licença verificada (CC0 ou licença da Asset Store); conferir compatibilidade com URP |
| Móveis de interior (camas, beliches, macas, mesas, monitores) | enfermaria, alojamentos, sala de controle (seção 8 do pedido) | idem |
| Vozes das 54 falas | dublagem | TTS autorizado ou gravação; ver `FASE5_NARRATIVA_AUDIO.md` |
| Clipes de animação para os inimigos novos (blindado, cuspidor, perseguidor, contenção) | Marco 5 | Mixamo (conta do usuário); ver `FASE3_ASSETS.md` §3.1 |
| Sons de interface do inventário (abrir, mover, fabricar) | feedback | hoje usam sons existentes (troca de arma, coleta, baú) |
