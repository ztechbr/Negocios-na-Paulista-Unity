# Paulista Business World — MVP Unity

Versão jogável em Unity do protótipo web (`../web`). Mundo empresarial 3D fictício e estilizado inspirado na Avenida Paulista, com 12 empresas de setores diferentes, 12 executivos de terno com quem você pode conversar, seu próprio avatar personalizável, museu estilizado e Meeting Hub.

## Avatares e conversas

- **Seu avatar:** na primeira execução você define nome, cargo, empresa e aparência (terno masculino ou feminino com saia ou calça, tom de pele, cor do terno, cabelo, gravata/blusa). Para alterar depois, use **Meu avatar**. O perfil fica salvo localmente.
- **Executivos:** 6 homens de terno e gravata e 6 mulheres de terninho. Oito caminham pela avenida e quatro aguardam no Meeting Hub.
- **Conversar:** aproxime-se e pressione **E**, ou clique no executivo e depois em **Conversar**. Eles se viram para você e a câmera passa a enquadrar o executivo por cima do seu ombro.
- **IA (Claude):** com uma chave da API da Anthropic, as conversas são livres. Cada executivo interpreta sua persona (nome, cargo, empresa e setor) e conhece seu nome e cargo. Configure de um destes jeitos:
  - variável de ambiente `ANTHROPIC_API_KEY` antes de abrir o Unity ou o executável;
  - botão **IA** no jogo: cole a chave, opcionalmente marcando "lembrar neste computador".

  Modelo: `claude-opus-5-5` com esforço `low`, para respostas rápidas, e fallback automático do servidor em caso de recusa. O uso é cobrado na sua conta Anthropic.
- **Sem chave:** os executivos usam respostas prontas (modo offline).

> No build WebGL, a chave digitada fica no navegador de quem joga e é enviada direto para `api.anthropic.com`. Para publicar na web para outras pessoas, use um backend/proxy próprio em vez de distribuir uma chave.

## Banco de negócios, negociação e modelo local

Funciona **sem API de LLM**. Os dados iniciais ficam em `Assets/Resources/business_seed.json` e podem ser editados:

- **Portfólio:** 48 produtos e serviços (4 por empresa), com preço de tabela, unidade, público-alvo, desconto máximo e volume para desconto. Fica visível na aba **Portfólio** da conversa.
- **Perguntas e respostas:**
  - 19 intenções de negócio, como apresentação, preço, cases, implantação, parcerias, tendências e desconto, com variações de pergunta e de resposta;
  - perguntas específicas de cada produto;
  - tendências e dores de cada setor.
- **Negociação** (aba **Negociar**, motor de regras, sem LLM):
  - Escolha produto, quantidade e prazo (12, 24 ou 36 meses); depois use argumentos (à vista, case, concorrente) e envie propostas.
  - O executivo tem um preço de reserva secreto, faz contrapropostas e perde a paciência com ofertas muito baixas.
  - Ele fica mais firme com quem sempre fecha no limite do desconto.
  - Os negócios fechados ou perdidos ficam em **Banco de negócios → Meus negócios**.

### Modelo local (SQLite com vetores)

- **Armazenamento:**
  - No Windows, usa o SQLite nativo do sistema (`winsqlite3.dll`), em `%USERPROFILE%\AppData\LocalLow\Paulista Business World\Paulista Business World\paulista_business.db`.
  - No WebGL e em outras plataformas, usa um arquivo JSON ou o armazenamento do navegador.
- **Tabelas:**
  - `knowledge`: perguntas, respostas, intenção, empresa, origem (`seed`, `derived`, `learned`, `pending`), pontuação e vetor (`vec`, BLOB de 512 floats);
  - `interactions`: cada pergunta, resposta, origem e avaliação;
  - `deals`: negociações;
  - `meta`: versões do seed e da vetorização.
- **Vetorização:** feita localmente, por hashing de termos normalizados (sem acento, sem stopwords, radical por prefixo), bigramas e trigramas de caracteres. Não depende de serviço externo de embeddings.
- **Resposta:** busca por similaridade de cosseno, priorizando a empresa do executivo e as respostas bem avaliadas. Abaixo do limiar, a pergunta vira **pendente**.
- **Aprendizado:**
  - O botão "A última resposta ajudou? Sim/Não" ajusta o peso da resposta no ranking.
  - Com IA, cada resposta do Claude é salva como `learned` (com vetor) e passa a ser usada offline. As perguntas parecidas atualizam a mesma linha.
  - Com IA, ao iniciar uma conversa, o Claude (saída estruturada em JSON schema) **deriva até 6 perguntas novas** a partir das existentes e **responde às pendentes** daquela empresa. O limite é 40 derivadas por empresa.
  - As respostas aprendidas também servem de contexto para o Claude manter coerência (RAG local).
- **Perguntas sugeridas:** o chat mostra perguntas do banco, priorizando as derivadas e as aprendidas.

## Requisitos

- Unity Hub + Unity **6000.6.5f1** (outras versões Unity 6 devem funcionar; o Hub oferece a migração).
- Módulos opcionais: *Windows Build Support* e *WebGL Build Support* para gerar builds.

## Abrir e jogar no editor

1. Unity Hub → **Projects → Add → Add project from disk** → selecione a pasta `unity-mvp`.
2. Abra o projeto. Na primeira abertura a cena `Assets/Scenes/PaulistaBusinessWorld.unity` é criada automaticamente.
3. Menu **Paulista Business World → Abrir cena principal** e pressione **Play**.

Todo o mundo é gerado por código em tempo de execução (`Assets/Scripts`), então não há assets binários; pressionar Play em qualquer cena também inicia o jogo.

## Controles

| Ação | Tecla |
|---|---|
| Caminhar | W A S D / setas |
| Conversar com executivo próximo | E (Esc fecha a conversa) |
| Câmera 3ª / 1ª pessoa | V |
| Correr | Shift |
| Girar a câmera | arrastar com o mouse (botão esquerdo ou direito) |
| Ver informações | clicar em um edifício, executivo, museu ou Meeting Hub |
| Reiniciar posição | R |
| Visão panorâmica | P |

HUD: filtro por setor (destaca os prédios do setor), "Ir ao centro de negócios", propor reunião/convidar executivo (gera um horário na **Minha agenda**, salva localmente via PlayerPrefs), visão panorâmica e minimapa.

## Builds

Menu **Paulista Business World → Build Windows** ou **Build WebGL**. Saída em `Builds/`.

Linha de comando:

```bash
Unity.exe -batchmode -nographics -projectPath <pasta>/unity-mvp -executeMethod ProjectSetup.BuildWindows -logFile build.log
Unity.exe -batchmode -nographics -projectPath <pasta>/unity-mvp -executeMethod ProjectSetup.BuildWebGL  -logFile build.log
```

O build WebGL usa Gzip com *decompression fallback*, funcionando em qualquer hospedagem estática (GitHub Pages, itch.io, Netlify).

## Estrutura

```
Assets/Scripts/
  GameRoot.cs          # Ponto de entrada (monta mundo, jogador e HUD)
  WorldData.cs         # Empresas e executivos fictícios
  Avatar.cs            # Avatares de terno (masculino/feminino) e animação de caminhada
  Conversation.cs      # Perfil do jogador, conversas com executivos e respostas offline
  ClaudeClient.cs      # Chamada à Messages API da Anthropic (UnityWebRequest), inclusive saída estruturada
  BusinessData.cs      # Leitura do banco inicial, formatação em reais e preenchimento de respostas
  LocalStore.cs        # Persistência: SQLite (Windows) ou JSON
  LocalModel.cs        # Vetorização, busca, aprendizado, pendentes e derivação com IA
  Negotiation.cs       # Motor de negociação por regras
  WorldBuilder.cs      # Geração procedural da avenida, prédios, museu, executivos e Meeting Hub
  PlayerController.cs  # Caminhada em 1ª pessoa, câmera, clique
  GameHUD.cs           # Interface (IMGUI), agenda local, avisos, minimapa
  InputCompat.cs       # Suporte ao Input System novo e ao Input Manager legado
  Components.cs        # Interactable, Billboard, NpcController
Assets/Resources/
  business_seed.json   # Portfólio, perguntas, respostas e falas de negociação
Assets/Editor/
  ProjectSetup.cs      # Criação da cena e builds
```

## Limitações do MVP

- Single-player: os executivos são personagens de IA, não outras pessoas. Reuniões e convites são simulados e locais (sem backend, calendário ou voz).
- Empresas e pessoas são fictícias; o mapa não é réplica georreferenciada da Avenida Paulista.
- Geometria com primitivas e materiais simples; requer arte, iluminação e otimização antes de uso comercial.
