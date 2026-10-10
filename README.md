# Paulista Business World - MVP

Mundo empresarial 3D **fictício e estilizado**, inspirado na Avenida Paulista, com prédios de vários setores, executivos demonstrativos, pontos de interação e um Meeting Hub. Inclui demonstração web independente e gerador de cena Unity compatível com o fluxo de autoria de mundos VRChat.

## 1. Executar no navegador

A partir da raiz do projeto:

```bash
cd web
python -m http.server 8000
```

Acesse http://localhost:8000 no navegador. Requer internet para baixar Three.js do CDN na primeira execução.

**Controles:** `W/A/S/D` movem, mouse com botão pressionado gira, `Shift` acelera, `R` retorna ao início. Clique nos edifícios ou nos personagens para consultar informações e propor reuniões simuladas. Selecione o setor no filtro. Use "Ir ao centro de negócios" e "Visão panorâmica".

O navegador fornece **uma demonstração single-player**, não um cliente VRChat. Executivos web são bonecos geométricos animados e **não** avatares reais da plataforma. Convites e reuniões no web são apenas notificações locais sem persistência, calendário, backend, IA ou comunicação por voz.

## 2. Levar a cena para VRChat

1. No Windows, instale **VRChat Creator Companion (VCC)**. Crie um projeto do tipo **Worlds**, com Unity na versão indicada pelo VCC. Na documentação consultada em outubro de 2026, a versão indicada é **2022.3.22f1**.
2. Copie o arquivo `unity/Assets/Editor/PaulistaWorldGenerator.cs` para a pasta `Assets/Editor` do projeto criado pelo VCC.
3. No Unity, abra **Tools > Paulista Business World > Generate MVP Scene**.
4. A cena gerada ficará em `Assets/PaulistaBusinessWorld/Scenes/PaulistaBusinessWorld.unity`.
5. **Obrigatório:** adicione o prefab `VRCWorld` disponibilizado pelo VRChat SDK (ou configure manualmente `VRCSceneDescriptor`) e defina pelo menos um Spawn Point sobre o piso. O objeto `SPAWN - ...` é apenas um marcador auxiliar para orientar o posicionamento, não é o próprio Spawn Point. Configure a geometria e colisões para circulação segura.
6. Execute as validações pelo **VRChat SDK Control Panel** e, se disponível, teste localmente com **ClientSim**. Use Build & Test, corrija avisos, faça login e publique com Build & Publish apenas quando seu projeto e conta estiverem habilitados.
7. No mundo VRChat, os **participantes reais** utilizam avatares da própria plataforma e podem interagir por seu sistema de voz. Para sentar nas cadeiras, adicionar `VRCStation` ao mobiliário como evolução. Para agenda, convite e estados compartilhados, criar componentes UdonSharp e eventual backend externo sujeito às restrições do VRChat.

### Limitações conhecidas

- O mapa **não é uma réplica georreferenciada ou topograficamente fiel** da Avenida Paulista. Nomes comerciais são **fictícios** e não representam empresas nem endereços reais.
- O edifício cultural é uma interpretação abstrata, sem reprodução fiel da arquitetura do MASP.
- **Sem NPCs com IA** dentro do VRChat neste primeiro MVP. Execução de personagens autônomos exigiria desenho específico com componentes permitidos pelo SDK.
- **Sem multiplayer na versão web**. O multiplayer só estará disponível ao entrar em uma instância do mundo publicada ou testada no VRChat.
- A cena Unity é gerada por script de Editor com primitivas, não é um projeto VRChat já compilado ou publicado. O SDK é obtido através do VCC e não é redistribuído no ZIP.
- O cenário gerado é de natureza exploratória. Requer avaliação de desempenho, iluminação, materiais, som, navegação, sinalização e otimização antes de publicação pública.

## 3. Estrutura

```
web/
  index.html          # HUD do protótipo
  main.js             # Cena 3D e interação em Three.js
unity/Assets/Editor/
  PaulistaWorldGenerator.cs  # Gerador Unity
README.md
```

## 4. Evolução comercial sugerida

Fase 2: mesas com VRCStation, salas privativas, painel de empresas, agendamento real por API e autenticação externa. Fase 3: salas temáticas e palestras, estandes patrocinados, CRM, indicadores de networking com consentimento. Fase 4: personagens corporativos interativos e recomendações, sujeitos às capacidades e políticas de conteúdo do VRChat.

## Referências

- https://creators.vrchat.com/worlds/
- https://creators.vrchat.com/getting-started/
- https://creators.vrchat.com/sdk/upgrade/current-unity-version/
- https://creators.vrchat.com/worlds/components/vrc_station/
- https://creators.vrchat.com/worlds/examples/udon-example-scene/
