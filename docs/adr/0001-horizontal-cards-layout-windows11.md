# 1. Layout de Cards Horizontais e Identidade Visual Windows 11

Data: 2026-09-12

## Contexto

O WindowsCM foi concebido com paridade ao gerenciador de área de transferência Copyous (GNOME). Durante o protótipo inicial (Variante A - Cards), validou-se que o formato de cards horizontais (250x170px) proporcionava excelente densidade e área de preview para inspecionar grandes blocos de código e imagens. No entanto, a primeira implementação do popup restringiu o layout a uma janela vertical estreita (380x520px), truncando o preview a uma única linha de 40px e limitando os botões da barra superior a textos literais que sofriam cortes em diferentes resoluções.

Além disso, a exibição de itens do tipo arquivo e imagem expunha caminhos absolutos do sistema operacional (`C:\Users\...` ou `file:///C:/Users/.../AppData/Local/.../hash.png`), gerando poluição visual e prejudicando a usabilidade.

## Decisão

1. **Retomada do Layout de Cards Horizontais (Variante A do Copyous):**
   - O popup passa a adotar largura de 880px e altura de 320px.
   - A lista de histórico exibe cards horizontais (`Width="250"`, `Height="230"`) alinhados lado a lado, com suporte a rolagem horizontal suave pelo scroll do mouse e pelas setas de navegação (Esquerda/Direita).

2. **Isolamento da Lógica de Exibição (`ItemDisplayFormatter`):**
   - Criação de um formatador puro em `WindowsCM.Core.Popup` para isolar e testar unitariamente:
     - Títulos: oculta caminhos absolutos e URIs internas, exibindo apenas o nome do arquivo (`Path.GetFileName`) para arquivos e "Imagem" para capturas diretas.
     - Tipos: classifica extensões em categorias amigáveis ("Imagem PNG", "Vídeo MP4", "Áudio MP3", "Código C#", "Documento PDF").
     - Previews multilinhas: permite exibir 6 a 8 linhas de código ou texto com fonte monoespaçada (`Cascadia Code` / `Consolas`).
     - Resolução de caminhos de miniaturas: suporta tanto imagens cacheadas quanto arquivos de imagem copiados do Windows Explorer.

3. **Botões de Ação com Ícones Segoe Fluent (Windows 11):**
   - Substituição dos botões com texto por botões icônicos compactos (32x32px) utilizando a tipografia nativa `Segoe Fluent Icons` (com fallback para `Segoe MDL2 Assets`).
   - Cada botão possui ToolTip com descrição da ação e respectivo atalho de teclado (`Alt+P`, `Ctrl+Shift+Alt+V`, etc.).

4. **Identidade Visual Fluent Design:**
   - Superfície escura (`#202020`), bordas sutis (`#383838`), cantos arredondados (`CornerRadius="8"`) e sombras suaves.
   - Destaque ativo para seleção de card e filtros com a cor de acento do sistema (`#0078D4` / `#ED5B00`).

## Consequências

- **Positivas:**
  - Código fonte e textos copiados tornam-se imediatamente legíveis no preview antes de colar.
  - Usuário identifica rapidamente arquivos e fotos pelo nome e miniatura, sem visualização confusa de caminhos do sistema.
  - Interface alinhada às diretrizes estéticas do Windows 11.
  - Zero corte de texto em botões, melhorando a consistência internacional e ergonomia.
  - Lógica 100% coberta por testes automatizados.
- **Negativas / Desafios:**
  - O popup ocupa maior largura de tela (880px), requerendo clamping determinístico próximo às bordas do monitor (mantido e testado em `PopupPlacement`).
