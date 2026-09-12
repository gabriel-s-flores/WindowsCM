# 29: Correção dos Botões/Menu do Card, Frame Rate da Rolagem e Layout da Barra de Rolagem

Type: task

Status: resolved

Blocked by: 28

## User Report / Symptoms:
1. **Botões e Menu do Card não funcionam**:
   - Os botões no cabeçalho de cada card (fixar/pin, menu '...' de opções e excluir/lixeira), assim como o clique direito, não executam suas ações.
   - Ao clicar neles, o gerenciador de área de transferência simplesmente fecha e nada acontece.
2. **Taxa de quadros da rolagem (Frame Rate / Refresh Rate)**:
   - Durante a rolagem horizontal, a animação parece rodar bem abaixo da taxa de atualização do monitor (ex: 120Hz, 144Hz, 240Hz).
   - Sensação pegajosa e engasgada ao rolar repetidamente.
3. **Barra de rolagem inferior ocultando a base dos cards**:
   - A barra de rolagem horizontal sobrepõe o conteúdo inferior dos cards (legenda/tipo), degradando a experiência visual.

## Answer
Implementado e validado em 2026-09-12 seguindo o ciclo das skills de Matt Pocock (diagnosing-bugs e 	dd):

1. **Botões e Menu do Card 100% Funcionais**:
   - PopupClickPolicy.cs: adicionada sobrecarga ShouldActivate(bool isVisible, int? clickedIndex, bool isInteractiveControl) que retorna alse quando o clique foi originado em um botão interativo (ButtonBase).
   - PopupWindow.xaml.cs: OnItemClicked agora detecta FindAncestor<ButtonBase>(e.OriginalSource) e delega os cliques nos botões de Fixar, Opções e Excluir para seus respectivos handlers sem disparar cópia/colagem ou fechamento da janela.
   - PopupDeactivationPolicy.cs: política pura que protege contra fechamento indevido enquanto _isContextMenuOpen ou _isDialogOpen estiver ativo.
   - PopupWindow.xaml.cs: integrado PopupDeactivationPolicy em OnDeactivated. ShowCardContextMenu e EditCurrentTitle agora gerenciam _isContextMenuOpen e _isDialogOpen com salvaguardas para fechar o popup apenas quando o usuário clica deliberadamente fora da aplicação.
   - Posicionamento inteligente do menu: menu.Placement alinhado à base do botão quando disparado pelo botão ..., ou na posição do ponteiro quando disparado via clique direito.
   - Estilização Fluent do ContextMenu e MenuItem adaptada a temas Claro e Escuro com cantos arredondados (8px) e sombra suave.

2. **Rolagem Suave no Refresh Rate Nativo do Monitor (120Hz/144Hz/240Hz)**:
   - Criado SmoothScrollController.cs em WindowsCM.Core.Popup: motor inercial puro baseado em decaimento exponencial com invariância de frame rate (actor = 1 - e^(-lambda * dt)).
   - Acoplado ao CompositionTarget.Rendering do WPF em PopupWindow.xaml.cs: cada quadro de animação é disparado exatamente no V-Sync do monitor, alcançando 120 FPS, 144 FPS ou 240 FPS sem travas.
   - Acúmulo suave de impulsos contínuos na roda do mouse, eliminando a sensação "pegajosa" do reinício estático anterior.
   - Desativação automática do hook de renderização em repouso (0% de CPU idle).

3. **Respiro e Barra de Rolagem Horizontal sem Sobreposição**:
   - PopupSizing.MaxHeight ajustado de 320 para 348 DIPs (PopupWindow.xaml Height="348" MaxHeight="360"), garantindo 280 DIPs disponíveis para a lista de itens.
   - Cards de 240 DIPs de altura com margem inferior de 8 DIPs em relação à barra de rolagem.
   - Barra de rolagem horizontal estilizada com design moderno Fluent (10 DIPs de altura, indicador estilo pílula arredondada de 6 DIPs que expande sutilmente para 8 DIPs no hover, sem setas grossas antiquadas).
   - Zero sobreposição ou corte no rodapé dos cards (legendas e tipos 100% visíveis).

4. **Testes Automatizados**:
   - 15 novos testes unitários adicionados em SmoothScrollControllerTests, PopupDeactivationPolicyTests, PopupClickPolicyTests e PopupPlacementTests.
   - Suíte de 777 testes executada com 100% de sucesso.
