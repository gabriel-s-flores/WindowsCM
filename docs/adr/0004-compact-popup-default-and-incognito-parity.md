# 4. Menu Compacto como Padrão em Atalhos Globais e Paridade de Modo Anônimo

Data: 2026-09-13

## Contexto

O atalho global padrão do WindowsCM (`Ctrl+Shift+V`) abria originalmente a janela completa em formato de cards horizontais (880x320px). Embora essa visão ampla ofereça rica visualização de código e imagens, seu tamanho acaba ocupando uma porção considerável da tela durante a digitação cotidiana, interrompendo o fluxo de trabalho do usuário que deseja apenas selecionar e colar rapidamente um item recente.

Adicionalmente:
1. Havia sido criado um atalho secundário isolado (`Ctrl+\``) exclusivamente para o menu compacto, gerando fragmentação desnecessária de atalhos e potenciais colisões.
2. O menu compacto não possuía uma barra de pesquisa visual permanente no topo, recorrendo a um mecanismo de captura oculta de digitação pouco intuitivo.
3. O ícone de modo privado no menu compacto utilizava um glifo genérico (`\uE727`), destoando do clássico ícone vetorial de chapéu Fedora e óculos escuros adotado na visão completa.

## Decisão

1. **Menu Compacto como Alvo Padrão dos Atalhos Globais:**
   - O atalho principal `Ctrl+Shift+V` passa a abrir diretamente o `CompactPopupWindow` posicionado de maneira inteligente sob o cursor do mouse.
   - O atalho de modo anônimo `Ctrl+Shift+Alt+V` passa a abrir também o menu compacto sob o cursor, ativando a sessão efêmera e exibindo feedback visual claro.
   - Remoção completa do atalho avulso `Ctrl+\`` (`HotkeySlot.Compact`), consolidando o registrador global nos dois slots essenciais: `Open` e `Incognito`.

2. **Barra de Pesquisa no Topo do Menu Compacto:**
   - Inclusão de um container Fluent de pesquisa fixo no topo (`Height="32"`, `CornerRadius="8"`), com ícone de lupa, caixa de texto com placeholder amigável ("Pesquisar...") e botão de limpeza instantânea.
   - Foco automático na barra de busca ao abrir o menu, com suporte a navegação vertical direta na lista via setas Cima/Baixo e ativação por Enter sem necessidade de alternar foco manualmente.

3. **Unificação Visual do Modo Anônimo:**
   - Aplicação da mesma geometria vetorial `IncognitoHatAndGlassesGeometry` tanto no botão do rodapé quanto no novo banner de alerta superior do menu compacto.
   - Destaque ativo em roxo com borda brilhante quando a sessão anônima estiver em curso.

## Consequências

- **Positivas:**
  - Experiência de uso muito mais rápida, fluida e discreta: colagem rápida sem obstrução da tela de trabalho.
  - Eliminação de atalhos redundantes e simplificação do modelo de configuração.
  - Busca rápida e intuitiva imediatamente acessível via digitação direta.
  - Consistência visual e de identidade em todos os estados do aplicativo.
- **Negativas / Desafios:**
  - A visão detalhada de cards horizontais (`PopupWindow`) permanece acessível primariamente via menu de contexto da bandeja do sistema.
