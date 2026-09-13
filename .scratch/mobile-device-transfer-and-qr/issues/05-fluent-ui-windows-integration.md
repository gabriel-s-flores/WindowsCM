Status: resolved
Type: task

## Answer
Implementado em `PopupWindow.xaml`, `PopupWindow.xaml.cs`, `CompactPopupWindow.xaml`, `CompactPopupWindow.xaml.cs` e `MobileTransferWindow.xaml/.cs`:
- Adicionado botão `ReceiveMobileButton` com ícone de celular `\uE8EA` ao lado do botão de modo anônimo em ambas as interfaces.
- Criada janela `MobileTransferWindow` com design Fluent, exibição de QR Code de alta qualidade, IP local, botão de copiar link e indicador de status com feedback em tempo real ao receber dados do celular.
- Habilitada opção "Gerar código QR" no menu de contexto dos cards e atalho de ação rápida.

## Descrição

Adicionar na interface do `PopupWindow` e `CompactPopupWindow` o botão com ícone de celular para iniciar o recebimento de itens do smartphone, bem como criar a janela `MobileTransferWindow` com QR Code persistente e status em tempo real.

## Requisitos

- `PopupWindow.xaml`:
  - Ao lado de `IncognitoButton`, adicionar `ReceiveMobileButton` com ícone de smartphone (`\uE8EA`), tooltip "Enviar do celular para o computador" e evento de clique `OnReceiveMobileClicked`.
- `CompactPopupWindow.xaml`:
  - No rodapé ao lado de `IncognitoButton`, adicionar o botão correspondente.
- Criação de `MobileTransferWindow`:
  - Visual Fluent Windows 11 com cantos arredondados, fundo escuro/claro com brushes dinâmicos.
  - QR Code proeminente apontando para `http://<ip>:<porta>/`.
  - Instrução clara: "Aponte a câmera do celular para este QR Code para enviar textos e arquivos para o computador."
  - Seletor/Exibição de IP local com botão para copiar o link direto.
  - Lista/Card de status com feedback em tempo real de itens recebidos ("Aguardando conexão...", "Recebido há pouco: foto.jpg").
  - Botão "Abrir pasta de transferências" e "Concluído / Fechar".
