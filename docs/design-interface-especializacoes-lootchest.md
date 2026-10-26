# Design: interface, especializações e integração com TSLootChest

> Status: **rascunho para decisão**. Nada aqui está implementado.
> Data: 2026-10-26

Este documento cobre três frentes:

1. [Interface](#1-interface-dá-para-sair-do-chat): como o jogador vê e escolhe habilidades sem depender só do chat.
2. [Especializações e árvore de habilidades](#2-especializações-por-classe): as escolhas dentro de cada classe.
3. [Integração com TSLootChest](#3-integração-com-tslootchest-modo-rpg): baús que mudam de comportamento quando o modo RPG está ativo.

No fim há uma lista de [decisões em aberto](#4-decisões-em-aberto) e uma [ordem sugerida](#5-ordem-sugerida-de-implementação).

---

## Restrição de base

O TSRpg roda **só no servidor**, com o cliente vanilla. O servidor não consegue desenhar janelas novas nem adicionar itens ou botões ao cliente. Tudo o que for "interface" precisa reaproveitar algo que o cliente vanilla já sabe mostrar:

| Recurso do cliente vanilla | O que o servidor consegue fazer com ele | Já usado no TSRpg? |
| --- | --- | --- |
| Chat | Texto colorido, comandos | Sim (`/class`, `/rpg`) |
| Texto de combate flutuante (`CreateCombatTextExtended`) | Popups sobre NPCs ou jogadores | Sim (perks) |
| Texto de status no canto superior direito (`StatusText`, pacote 9) | Um "HUD" de poucas linhas, atualizável | Não |
| Placas (signs) | Janela com texto longo ao clicar | Não |
| Interface de baú | Grade de 40 slots com ícones de itens e tooltip | Não (o TSLootChest já manipula baús) |
| Blocos, tinta, alavancas, placas de pressão, molduras de item | Mundo físico como interface | Não |
| Times vanilla (vermelho, verde, ...) | O jogador escolhe pelo menu do próprio jogo | Não |

---

## 1. Interface: dá para sair do chat?

Sim, mas com limites. Abaixo as opções, da mais simples à mais ambiciosa. Elas não são exclusivas: o chat pode continuar como fallback de tudo.

### 1.1 Chat melhorado (base, sempre existe)

- `/skills` lista a árvore em texto, com cores para comprado, disponível e bloqueado.
- `/skills <número>` compra um nó.
- **Prós:** trivial, zero risco de desync.
- **Contras:** pouco visual; a árvore inteira não cabe numa tela de chat.

### 1.2 HUD de status (complemento barato)

Usar o texto de status no canto da tela para mostrar nível, XP, especialização e pontos livres em tempo real. Vários plugins de TShock fazem isso.

- **Prós:** fácil, sempre visível, não interfere no jogo.
- **Contras:** só leitura; conflita com outros plugins que usam o mesmo texto; some quando o servidor manda outro status (ex.: "Saving world").

### 1.3 Baú virtual como menu

O servidor abre para o jogador uma interface de baú cheia de itens que servem de "ícones" de habilidade. O tooltip do item mostra o nome do item vanilla, então a descrição real vai para o chat ou um popup quando o jogador clica.

O clique é detectado quando o cliente tenta tirar o item do slot (pacote `ChestItem`, 32). O servidor recusa a mudança, reenvia o slot e trata o clique como "comprar nó".

Variantes:

- **Baú real escondido:** um baú físico perto do jogador ou numa área protegida. O cliente fecha a interface se o jogador se afastar do baú, então o baú precisa estar perto dele.
- **Cofre do jogador (porquinho/cofre):** reescrever os slots do banco do jogador com ícones. É arriscado: mexe no inventário real e pode apagar itens em caso de bug ou SSC desligado. **Não recomendado.**

| | |
| --- | --- |
| **Prós** | Visual, familiar, clicável, a árvore cabe em 40 slots (ex.: uma linha por tier). Combina com o código de manipulação de baús que já existe no TSLootChest. |
| **Contras** | Precisa de prova de conceito: confirmar que o cliente aceita abrir um baú enviado pelo servidor, que o "clique" é detectável de forma confiável e que não dá para duplicar itens. |
| **Risco** | Médio. Duplicação de item é o principal perigo; mitigável usando itens sem valor ou impossíveis de tirar (o servidor sempre devolve o slot). |

### 1.4 Sala de habilidades no mundo (a ideia dos blocos)

O jogador é teleportado para uma área do mundo onde **os próprios blocos são a interface**. Em vez de um mundo separado, sugiro **uma região protegida no mesmo mundo** (no espaço acima do mapa ou atrás de paredes no submundo). Um mundo separado exigiria um segundo servidor e um proxy (tipo Dimensions), o que é muito mais trabalho e infraestrutura.

Como funcionaria:

- `/skills` salva a posição do jogador e o teleporta para a sala. Sair (porta, alavanca ou `/skills sair`) devolve ele ao ponto salvo.
- Cada nó da árvore é uma "estação": uma **moldura de item ou suporte de arma** com o ícone, uma **placa** com a descrição (clicar na placa abre o texto longo) e uma **alavanca ou placa de pressão** para comprar.
- O estado do nó aparece pela **tinta do bloco**: cinza para bloqueado, amarelo para disponível, verde para comprado. Também dá para usar tochas de cores diferentes.
- **Truque importante:** o servidor pode mandar blocos diferentes para cada jogador (`SendTileSquare` só para aquele cliente). Assim, dois jogadores na sala ao mesmo tempo veem cada um a sua própria árvore. A sala física é uma só.
- O plugin lê os cliques de alavanca (`HitSwitch`, pacote 59) e de placa de pressão por posição de bloco.

| | |
| --- | --- |
| **Prós** | Imersivo, combina com Terraria, dá para mostrar uma árvore grande com caminhos de verdade (fios ou trilhos ligando os nós). A sala pode ter um "mestre de classe" visual e virar um hub social. |
| **Contras** | Precisa construir e manter a sala (gerar por código ou usar um schematic). Exige proteção de região, nenhum spawn de mob, a sala sem ser alterada e o teleporte de volta seguro (morte ou desconexão dentro da sala). A sincronização por jogador precisa ser reenviada se o cliente pedir a seção do mapa de novo. |
| **Risco** | Médio-alto em esforço, baixo em perda de dados (não toca no inventário). |

### 1.5 Decisão: baú virtual

**Decidido (2026-10-26): a interface principal será o baú virtual (1.3).** O chat (1.1) continua como fallback e para administração. A sala de blocos (1.4) fica arquivada como possível camada visual futura.

Ponto de arquitetura: separar **modelo** (árvore, nós, pontos, pré-requisitos) de **entrada** (chat ou baú). As duas chamam o mesmo `SkillTree.TryUnlock(player, nodeId)`.

### 1.6 Baú virtual: desenho

**Layout (10 colunas; 4 linhas visíveis, o resto rola):**

| Linha | Conteúdo |
| --- | --- |
| 1 | Classes / especializações e ações (respec) |
| 2 | Tier 1 da especialização |
| 3 | Tier 2 |
| 4+ | Tier 3 e seguintes, tronco comum da classe (visível rolando) |

**Como mostrar estado sem UI nova:**

- **Nome do baú** (vem no pacote que abre o baú): vira o cabeçalho, ex. `Guardião · 3 pontos livres`.
- **Item do slot**: o ícone da habilidade (ex.: Cobalt Shield para Muralha, Yoyo Bag para Duelista).
- **Tamanho da pilha**: o rank do nó (1, 2, 3...). O número aparece no canto do slot.
- **Nó bloqueado**: item genérico (ex.: um bloco cinza) no lugar do ícone.
- **Descrição**: o tooltip mostra o nome do item vanilla, então a descrição real vai para o chat ou um popup quando o jogador clica no nó. Exemplo: um clique mostra a descrição e o segundo clique em até 3 s confirma a compra.

**Mais de 40 slots:** o Terraria 1.4.5 tem baús de tamanho variável. O pacote **155** muda o tamanho de um baú no cliente, e a janela de baú passa a **rolar com a roda do mouse** quando há mais de 4 linhas. O pacote 32 manda o slot como byte, então o limite prático é 250 slots (25 linhas). A árvore inteira de uma especialização cabe num baú só, sem paginação.

**Fluxo de pacotes (confirmado lendo o código do cliente 1.4.5, `MessageBuffer.GetData`):**

1. `/menu` → o servidor acha um espaço livre 2×2 em chão firme ao lado do jogador e escolhe um índice de baú que o servidor não usa (do fim de `Main.chest`).
2. Manda **só para aquele cliente**:
   - **tile square (20)** com um baú de madeira naquela posição. O servidor troca os blocos só pelo tempo de montar o pacote e depois desfaz, então o mundo real não muda. Isso é necessário porque o cliente fecha a janela se não houver um bloco de baú em `chestX/chestY` (`Player.HandleBeingInChestRange`). O baú precisa estar apoiado no chão, senão o cliente o quebra ao recalcular os frames.
   - **69 (nome do baú)**: se o índice não existe no cliente, o cliente **cria** o baú (`Chest.CreateWorldChest`). Sem isso o pacote 32 seria ignorado.
   - **155** com o tamanho do menu.
   - **32** × tamanho, com os ícones.
   - **33** com índice e posição, o que abre a janela.
3. O jogador clica num slot → o cliente manda 32. O plugin trata o pacote em `GetDataHandlers.ChestItemChange` com prioridade `Highest` (antes do Bouncer), recusa a mudança, reenvia o ícone e limpa o cursor (slot 58).
4. Os tipos de item envolvidos no clique ficam "vigiados" por 3 s. Qualquer pacote de inventário (5) que mexa nesses tipos é recusado e o servidor reenvia a cópia dele. Um item jogado no chão (21) com esse tipo é apagado no cliente.
5. Fechar a janela → o cliente manda 33 com -1 → o plugin encerra a sessão e reenvia os blocos reais.

Os pacotes são montados à mão (`SendRawData`), porque `NetMessage.SendData` lê `Main.chest[id]` do servidor, que não existe para o baú virtual.

**Como outros plugins fazem:**

| Plugin | Técnica | Diferença para o TSRpg |
| --- | --- | --- |
| [InfiniteChests V3](https://github.com/Zaicon/InfiniteChestsV3) | Põe um `Chest` temporário em `Main.chest[temp]`, usa `SendData` normal (32 e 33) e apaga o `Chest` em seguida. | Depende de um baú físico no mundo. |
| [PerPlayerLoot](https://github.com/xcodian/PerPlayerLoot) | Monta o pacote 32 à mão (`OTAPI.PacketWriter` + `SendRawData`), porque `SendData` é preso a `Main.chest`. | Também depende de baú físico. |
| TSLootChest | Troca o conteúdo real de `Main.chest` e restaura depois. | Também depende de baú físico. |

Nenhum deles abre um menu **sem** baú no mundo. O bloco de baú falso por jogador e o pacote 69 criando o baú no cliente são a parte nova.

**Porquinho virtual (descartado):** o porquinho (`chest = -2`) mostra os itens reais do banco do jogador (slots 99–138 do pacote 5). Desenhar ícones ali exigiria sobrescrever o banco no cliente, com risco de apagar itens. Ele também não pode ser redimensionado, porque o pacote 155 só aceita índices de baús do mundo (0–7999). E ainda precisa de um bloco ou projétil de porquinho perto do jogador. Não traz vantagem sobre o baú virtual.

### 1.7 Riscos que a prova de conceito precisa responder

| Pergunta | Por que importa | Plano B |
| --- | --- | --- |
| O cliente abre a interface sem um baú de verdade naquela posição? | O cliente fecha o baú se o jogador se afasta ou se o bloco em `chestX/chestY` não está ativo. | Usar como posição um bloco sólido ao lado do jogador e reenviar o pacote 33 quando ele andar; ou congelar o jogador enquanto o menu está aberto. |
| **Duplicação de item** | Ao clicar, o item vai para o cursor do jogador no cliente. Sem SSC (Server Side Characters), o servidor não consegue tirar esse item da mão do jogador de forma confiável. | **Com SSC:** o servidor limpa o slot do cursor (58) e qualquer ícone que aparecer no inventário durante a sessão. **Sem SSC:** só usar ícones sem valor (blocos, tochas coloridas, tinta) ou exigir SSC para o menu. |
| O botão "Loot All" ou o shift-clique mandam vários 32 de uma vez? | Poderia comprar vários nós sem querer. | Ignorar mais de um clique por tick; exigir confirmação. |
| Conflito de índice | Um baú novo colocado no mundo pode pegar o mesmo índice enquanto o menu está aberto. | Rechecar o índice a cada atualização; fechar e reabrir em outro índice se mudar. |
| Bouncer do TShock rejeita os pacotes | O TShock confere se o jogador está perto do baú que está mexendo. | Tratar os pacotes no plugin antes do TShock (passo 3 acima). |

**Dois fluxos (decidido):** o menu depende da config do servidor. **Fluxo SSC primeiro** (implementado como prova de conceito em `ChestMenus.cs` e `ClassMenu.cs`). O fluxo sem SSC fica para depois, com ícones sem valor ou só chat.

**Critério de aprovação (fluxo SSC):** abrir, clicar e fechar 100 vezes sem item sobrando no inventário, sem desync visual e sem log de rejeição do TShock.

**Roteiro de teste no jogo:**

1. Ligar SSC (`tshock/sscconfig.json` → `"Enabled": true`), logar com uma conta **sem** `tshock.ignore.ssc`.
2. `/menu`: o baú aparece ao lado e a janela abre com as 4 classes no topo e os desbloqueios da classe atual embaixo (número no canto = nível).
3. Clicar uma classe: aparece a descrição no chat. Clicar de novo em até 4 s: troca a classe e o menu se redesenha.
4. Tentar roubar ícones: arrastar para o inventário, shift-clique, "Pegar tudo", jogar no chão com o cursor, arrastar para um slot de acessório. Nenhum ícone pode ficar.
5. Tentar perder itens: "Depositar tudo", colocar um item próprio em cima de um ícone, trocar item do cursor com um ícone. Tudo tem que voltar para o inventário.
6. Andar para longe com o menu aberto: a janela fecha e o baú some.
7. `/menu test 120` (admin): 120 slots, rolagem com a roda do mouse, cada clique responde com o número do slot.
8. Repetir tudo com outro jogador vendo: ele **não** pode ver o baú.

### 1.8 Documentação consultada

- TShock: [índice](https://tshock.readme.io/docs), [estrutura de pacotes](https://tshock.readme.io/docs/multiplayer-packet-structure), [hooks](https://tshock.readme.io/docs/hooks), [índices de slot](https://tshock.readme.io/docs/slot-indexes), [SSC](https://github.com/Pryaxis/TShock/blob/general-devel/docs/ssc.md), [config SSC](https://github.com/Pryaxis/TShock/blob/general-devel/docs/ssc-config.md).
- A documentação online é antiga (layouts de 1.3/1.4.4) e não cobre o pacote 155 nem a ordem de prioridade dos handlers. Esses pontos foram confirmados descompilando `OTAPI.dll` 3.3.14, `TShockAPI.dll` 6.2.1 e `TerrariaServer.dll` 6.2.1:
  - `HandlerList` do TShock roda os handlers em ordem crescente de `HandlerPriority` (`Highest = 1` primeiro) e pula os seguintes depois de `Handled`, a menos que tenham se registrado com `gethandled`.
  - `NetGetData` do TSAPI roda primeiro os handlers de prioridade maior. O TShock usa `Order = 0`.
  - O cliente só aceita o próprio inventário vindo do servidor quando `Main.ServerSideCharacter` está ligado (`MessageBuffer` case 5).

---

## 2. Especializações por classe

### 2.1 Subtipos de arma (wiki)

Segundo a página [Damage / Classes](https://terraria.wiki.gg/wiki/Classes) da wiki oficial:

| Classe | Subtipos de arma |
| --- | --- |
| Melee | espadas, lanças, manguais (flails), bumerangues, ioiôs |
| Ranged | arcos, bestas/repetidoras, armas de fogo, lançadores, armas consumíveis |
| Magic | varinhas (wands), armas mágicas de disparo, livros de feitiço (+ cajados, que a wiki não separa) |
| Summon | invocação de minions, invocação de sentinelas, chicotes |

A wiki **não define** papéis como "tank" ou "suporte". Esses papéis são decisão nossa, montados em cima dos subtipos e de buffs.

### 2.2 Detectando o subtipo no servidor

O `ClassSystem.UsesClassWeapon` já olha `tsPlayer.SelectedItem`. Dá para refinar por subtipo com dados que o servidor tem:

| Subtipo | Como detectar |
| --- | --- |
| Ioiô | `ItemID.Sets.Yoyo[item.type]` |
| Mangual / lança / bumerangue | `aiStyle` do projétil em `item.shoot` (flail 15, lança 19, bumerangue 3) |
| Espada | `item.melee && item.useStyle == Swing && !item.noMelee` |
| Arco / arma de fogo / lançador | `item.useAmmo` (`AmmoID.Arrow`, `AmmoID.Bullet`, `AmmoID.Rocket`) |
| Sentinela | `item.sentry` |
| Chicote | `ProjectileID.Sets.IsAWhip[item.shoot]` |
| Minion | `item.summon && !item.sentry && !whip` |

Limitação: o hit de um projétil é creditado ao item que o jogador **está segurando agora**, não ao que disparou. Isso já acontece hoje e é aceitável. Para sentinelas e minions dá para checar o tipo do projétil que causou o dano, quando disponível.

### 2.3 O que o servidor consegue mexer

Isso define o que cada especialização pode oferecer de forma realista:

| Alavanca | Viável? | Observação |
| --- | --- | --- |
| Dano extra em hit | ✅ | Já usado (`StrikeNPC` extra). |
| Debuff no inimigo | ✅ | Já usado (`NpcAddBuff`). |
| Buffs no próprio jogador ou em aliados | ✅ | `AddBuff` / pacote `PlayerAddBuff`, funciona em qualquer jogador. |
| Cura própria ou de aliado | ✅ | Já usado (`Heal`). |
| **Puxar aggro** (inimigo mira o tank) | ✅ | O alvo do NPC é decidido no servidor (`npc.target`). Base do Tank. |
| Bônus em acerto crítico | ✅ | O pacote de strike traz a flag de crítico. |
| Reduzir dano recebido | ⚠️ | O dano é calculado no cliente. Só dá para **devolver** parte da vida depois do golpe (escudo/absorção) ou dar buffs de defesa (Ironskin, Endurance). |
| Mudar velocidade de ataque, defesa ou crit do jogador direto | ❌ | Só via buffs vanilla que já fazem isso. |

### 2.4 Modelo de progressão sugerido

- Níveis 1–9: classe base, como hoje.
- **Nível 10:** escolhe a **especialização** (uma por classe). Trocar custa algo (cooldown, moedas ou item).
- A partir do nível 10, **1 ponto de habilidade a cada nível** (ou a cada 2), gasto numa árvore de 3 tiers por especialização.
- Os buffs e perks atuais da classe continuam como "tronco comum"; a especialização adiciona ramos.
- Respec: comando com custo, devolve todos os pontos.

### 2.5 Melee (Guerreiro)

| Especialização | Foco | Ideias de nós |
| --- | --- | --- |
| **Tank (Guardião)** | Segurar inimigos, proteger o grupo | **Provocar:** inimigos próximos passam a mirar o Guardião por X s (cooldown). **Absorção:** cura X% do dano recebido logo após o golpe. **Muralha:** aliados perto ganham Ironskin/Endurance. **Espinhos** escalando com a vida máxima. Bônus de XP compartilhada por "dano sofrido no lugar do grupo". |
| **DPS (Berserker)** | Espadas, lanças, manguais | **Fúria:** dano extra que aumenta conforme a vida cai. **Execução:** +dano contra inimigos abaixo de 25% de vida. Roubo de vida maior. Golpe brutal com mais chance. |
| **Ioiô (Duelista)** | Ioiôs (e bumerangues como ramo) | Bônus de dano exclusivo para ioiô. **Ricochete:** chance de um segundo hit em outro inimigo próximo. Debuffs acumulados por hits rápidos (ioiô bate muitas vezes por segundo, então chance baixa e valor baixo). Buff de velocidade de movimento. |

### 2.6 Magic (Mago)

| Especialização | Foco | Ideias de nós |
| --- | --- | --- |
| **DPS (Arcanista)** | Dano bruto | Explosão arcana com mais chance e mais dano. **Sobrecarga:** dano extra quando a mana está cheia. Debuffs elementais (Frostburn, Shadowflame, On Fire) que se combinam. Recuperação de mana em kill. |
| **Suporte (Clérigo)** | Cura e buffs | Cura em grupo mais forte (evolução do *Spectral Mend*). **Bênçãos:** aplica Regeneration, Ironskin, Swiftness em aliados próximos. **Purificar:** remove debuffs de aliados (o servidor pode mandar o buff com tempo 0). **Ressurreição suave:** reduz o tempo de respawn do grupo ou cura de emergência quando um aliado cai abaixo de 20%. |

### 2.7 Ranged (Atirador)

| Especialização | Foco | Ideias de nós |
| --- | --- | --- |
| **DPS (Atirador de elite)** | Dano e crítico | **Crítico letal:** dano extra só em acertos críticos. Headshot e Deadeye melhorados. Bônus por subtipo: arcos (perfuração), armas de fogo (cadência), lançadores (área). |
| **Suporte (Caçador)** | Debuffs no inimigo | **Marcar presa:** o inimigo atingido fica marcado e recebe +X% de dano de **todo o grupo**. Ichor e Betsy's Curse (reduz defesa). Slow/Frostburn para controle. **Midas:** chance de aplicar Midas (mais moedas na morte). Bônus de XP para o grupo ao matar alvos marcados. |

### 2.8 Summoner (Invocador)

| Especialização | Foco | Ideias de nós |
| --- | --- | --- |
| **Sentinela (Engenheiro)** | Sentinelas, defesa de área | Dano extra de sentinelas. **Aura de torre:** aliados perto de uma sentinela do jogador ganham buff (Ironskin, Regeneration). Debuffs aplicados pelas sentinelas. Bom para defender eventos (invasões, Old One's Army). |
| **Invocação (Domador)** | Minions | Evolução do *Pack Instinct*. Roubo de vida via minions (*Life Bond*). **Frenesi:** quanto mais minions, mais dano por hit. |
| *(opcional)* **Chicote (Comandante)** | Chicotes + minions | Inimigo atingido pelo chicote recebe mais dano dos minions e sentinelas de **todos** os aliados. Combina com os papéis de suporte. |

---

## 3. Integração com TSLootChest: "modo RPG"

### 3.1 Como os dois plugins se conversariam

Hoje são DLLs independentes. Opções:

| Opção | Como | Prós | Contras |
| --- | --- | --- | --- |
| **A. API pública no TSRpg + detecção opcional no LootChest** *(recomendada)* | TSRpg expõe uma classe estática `TSRpg.Api` (nível, classe, especialização, "está em grupo?", dar XP). O LootChest procura o TSRpg em `ServerApi.Plugins` na inicialização. Se não achar, roda como hoje. | Sem dependência obrigatória; cada plugin continua funcionando sozinho. | Reflection ou referência opcional à DLL; precisa manter a API estável. |
| B. Eventos no LootChest, o TSRpg assina | O LootChest dispara `ChestOpened`, `ChestLooted`, `ChestDoomed`. O TSRpg reage (dá XP, spawna emboscada). | Desacoplado; a lógica de RPG fica no plugin de RPG. | O LootChest ainda precisa perguntar "qual o modo deste baú?" ao TSRpg para mudar duração e loot. |
| C. Banco SQLite compartilhado | Um lê a tabela do outro | Simples | Acoplamento frágil, problemas de lock. Não recomendado. |

Sugestão: **A + B**. O LootChest emite eventos e consulta `TSRpg.Api` quando ele existe. Uma flag `RpgMode` no config do LootChest liga ou desliga tudo.

### 3.2 O que é um "grupo" (party)

Hoje o TSRpg só usa **proximidade** (`PartyRangeTiles = 60`). Para o loot é melhor algo explícito. Sugestão: **reaproveitar os times vanilla** (o jogador escolhe a cor no menu do próprio jogo, o servidor lê `player.team`). Grupo = mesmo time + dentro do alcance. Não precisa de comando novo nem de UI.

### 3.3 Mecânicas de baú no modo RPG

Duas situações: **solo** (ou sem grupo por perto) e **em grupo**.

| Mecânica | Solo | Em grupo |
| --- | --- | --- |
| Loot por jogador (comportamento atual) | Igual a hoje | Igual a hoje, cada um ainda pega o seu |
| **Bônus de grupo** | — | Rolagem extra de item por membro (poções, moedas, munição), escalando com o nível médio do grupo |
| **Duração do baú condenado** (`BreakDelayMinutes`) | Curta (ex.: 5 min) | Longa (ex.: 15 min + 5 por membro) para todos terem tempo de chegar |
| **Chance de condenar** (`BreakChanceOnLoot`) | Maior | Menor: explorar junto preserva o mapa |
| **XP de exploração** | XP pequena no primeiro saque | XP dividida pelo grupo com bônus (mesma regra das kills) |
| **Baú de elite** | Raro | Mais comum. Brilha dourado como os mobs elite; ao abrir, spawna uma **emboscada** escalada pelo nível do grupo e **só libera o loot depois que os mobs morrem** |
| **Loot por classe** | Item extra temático: munição (Ranged), poção de mana (Mage), poção de invocação (Summoner), poção de Ironskin (Melee) | Cada membro recebe o da própria classe |
| **Habilidades que interagem** | — | Ex.: *Caçador* (Ranged suporte) "fareja" baús próximos e manda a direção no chat. *Guardião* (Tank) segura a emboscada. *Clérigo* reduz a duração da emboscada. |

### 3.4 Pontos técnicos a verificar

- O LootChest restaura o baú ao estado original quando o jogador sai de perto. O item extra do modo RPG precisa entrar na **visão pessoal** do jogador (`PlayerChestItems`), não no snapshot original, senão vaza para os outros.
- A emboscada precisa de um estado "baú travado" que o LootChest respeite (bloquear `ChestOpen` até os mobs morrerem, com timeout).
- O README do LootChest diz TShock 6.2.1, mas o repositório ainda tem `packages/TShock.5.2.0`. Alinhar as versões antes de criar referência entre os projetos.

---

## 4. Decisões em aberto

1. ~~**Interface:**~~ **decidido: baú virtual** (seção 1.5).
2. ~~**SSC:**~~ **decidido: dois fluxos, começando pelo SSC** (seção 1.7).
3. **Especialização:** escolha única no nível 10, ou árvore livre misturando ramos?
4. **Pontos:** 1 por nível ou 1 a cada 2 níveis? Quanto custa o respec?
5. **Summoner:** 2 especializações (Sentinela, Invocação) ou 3 (com Chicote)?
6. **Melee:** o Duelista cobre só ioiô ou ioiô + bumerangue? Mangual fica no Berserker?
7. **Grupo:** usar times vanilla, proximidade (como hoje) ou um `/party` próprio?
8. **Integração:** a lógica de RPG dos baús fica no TSRpg (eventos) ou no LootChest (consultando a API)?

## 5. Ordem sugerida de implementação

1. **Prova de conceito do baú virtual** (seção 1.7): abrir um menu fixo de ícones e detectar cliques. Feita primeiro porque é o maior risco técnico e define o resto.
2. Modelo de dados: especialização + nós + pontos no `RpgPlayer` e no SQLite; definição da árvore no `Config`.
3. Detecção de subtipo de arma (2.2) e os primeiros nós por especialização usando só alavancas ✅ (2.3).
4. Menu de habilidades no baú virtual + fallback por chat (`/spec`, `/skills`).
5. `TSRpg.Api` + eventos no LootChest + flag `RpgMode`; começar por XP de exploração e duração de baú por grupo.
6. Provocar (aggro) do Tank e baús de elite com emboscada.
