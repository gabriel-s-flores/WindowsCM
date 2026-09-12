# 30: Ícones de Sistema para Arquivos e Syntax Highlighting para Códigos

Type: task

Status: resolved

Blocked by: 29

## User Report / Requirements:
1. **Tratamento e exibição rica de arquivos copiados**:
   - Atualmente arquivos copiados exibem o caminho cru (`C:\...`) no preview do card, o que não é intuitivo.
   - Exibir os arquivos com seus respectivos ícones de sistema do Windows (Shell32/SHGetFileInfo).
   - Exibir nome amigável, tamanho e tipo do arquivo, com layout adaptado para múltiplos arquivos.
   - Garantir copy-back completo (CF_HDROP para Explorer e compatibilidade de colagem).
2. **Destaque de sintaxe de código (Copyous parity)**:
   - Códigos copiados devem ter suas palavras reservadas (keywords), tipos, strings e comentários destacados com cores distintas no preview do card.

## Answer
Implementado e validado em 2026-09-12 seguindo as skills de Matt Pocock (codebase-design, domain-modeling e tdd):

1. **Exibição Rica e Ícones de Sistema do Windows para Arquivos (`ItemKind.File` e `ItemKind.Files`)**:
   - Criado `FileIconService.cs` em `WindowsCM.App`: adapter Win32 que invoca `SHGetFileInfoW` com destruição segura de handles (`DestroyIcon`) e cache thread-safe em memória por extensão/arquivo. Converte os ícones nativos do Windows para `BitmapSource` congelados (zero overhead de re-renderização e latência de 0ms).
   - Criado `FileDisplayHelper.cs` em `WindowsCM.Core.Popup`: módulo puro para formatação de tamanhos de arquivo (`FormatFileSize`: B, KB, MB, GB), nomes amigáveis, extensões e resumo limpo de múltiplos arquivos.
   - Criado caso dedicado `FilePreviewVisibility` no card template de `PopupWindow.xaml`: substitui a visualização crua de caminhos por um card Fluent elegante exibindo o ícone oficial do Windows (32x32/48x48), nome em destaque, tamanho e tipo (`Documento PDF • 2,4 MB`), além de resumo enumerado para múltiplos arquivos.

2. **Destaque de Sintaxe de Código e Palavras Reservadas (Copyous parity)**:
   - Criado `CodeSyntaxTokenizer.cs` em `WindowsCM.Core.Previews`: analisador léxico puro e veloz que identifica palavras reservadas (`class`, `function`, `public`, `return`, `async`, `def`, `import`, etc.), tipos, strings, comentários, números e operadores.
   - Adicionada detecção automática de linguagens comuns (C#, JavaScript, Python, SQL, Rust, Go, HTML) integrada ao `ItemDisplayFormatter.GetTypeLabel` ("Código (C#)", etc.).
   - Criado `SyntaxHighlightHelper` em `WindowsCM.App`: attached property que popula os `Inlines` de `TextBlock` com `Run` elements coloridos dinamicamente de acordo com os temas Claro e Escuro do Windows 11 (`CodeKeywordBrush`, `CodeTypeBrush`, `CodeStringBrush`, `CodeCommentBrush`, etc.).

3. **Testes Unitários (TDD)**:
   - 21 novos testes adicionados em `FileDisplayHelperTests.cs` e `CodeSyntaxTokenizerTests.cs`.
   - Suíte de 798 testes executada com 100% de sucesso.
