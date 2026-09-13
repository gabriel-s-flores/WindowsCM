# 2. Categorias Semânticas de Arquivos, Customização de Extensões e Miniaturas Nativas do Windows

Data: 2026-09-13

## Contexto

Originalmente, o WindowsCM herdou do Copyous um mecanismo de "Tags", no qual o usuário podia atribuir manualmente uma de nove cores pré-definidas a qualquer item da área de transferência pelo menu de clique direito. Com o uso prático, essa abordagem provou-se redundante e pouco intuitiva para o gerenciamento de arquivos. Ao mesmo tempo, todos os arquivos copiados do sistema compartilhavam a mesma cor genérica ("File"), independentemente de serem imagens, músicas, vídeos, planilhas ou documentos do Office, e a pré-visualização no card de arquivo se restringia a um ícone estático genérico de 40x40px (exceto para imagens raster básicas já salvas localmente).

O Windows já possui uma base sólida de classificação semântica no Registro (`HKEY_CLASSES_ROOT\<ext>\PerceivedType` e associações com aplicativos padrão como Word, Excel, PowerPoint, Players de Mídia), além de uma infraestrutura madura de geração de miniaturas ricas (`IShellItemImageFactory` / Thumbnail Cache do Explorer) capaz de gerar previews visuais de fotos, quadros de vídeos, capas de álbuns de música e capas de apresentações/documentos.

## Decisão

1. **Aposentadoria de Tags na Interface do Usuário**:
   - Remoção do submenu "Tags" do menu de clique direito dos cards no popup.
   - Desativação dos atalhos de atribuição rápida de tags (`Ctrl+Shift+1..9` e `Ctrl+\``).
   - Preservação da coluna `tag` no esquema SQLite para compatibilidade com registros existentes, sem impacto visual.

2. **Categorias Semânticas de Arquivo Configuráveis**:
   - Criação do modelo `FileCategorySettings`, permitindo agrupar extensões em categorias (ex.: Imagens, Vídeos, Áudio, Documentos, Planilhas, Apresentações, Código/Scripts, Compactados).
   - Cada categoria possui seu próprio rótulo exibido no card, lista de extensões mapeadas e cor de acento dedicada.
   - O usuário pode criar novas categorias, alterar cores e editar livremente quais extensões pertencem a quais tipos na aba de Cores das Configurações.
   - Integração com `WindowsFileTypeResolver`: fallback dinâmico que consulta o `PerceivedType` do Windows e associações ativas para classificar extensões que ainda não foram customizadas pelo usuário.

3. **Miniaturas Ricas Nativas via `IShellItemImageFactory`**:
   - Introdução de um serviço nativo `ThumbnailService` que extrai miniaturas de alta resolução (250x160) diretamente do subsistema de Shell do Windows.
   - Quando um arquivo copiado possui miniatura gerada pelo Windows (fotos, vídeos, capas de áudio, PDFs, slides de apresentação), o card horizontal do popup exibe essa miniatura em destaque no corpo do card.
   - Quando o arquivo não possui miniatura disponível, mantém-se a visualização elegante com ícone do sistema e resumo descritivo (nome, extensão, tamanho).
   - Execução assíncrona com cache em memória (`ConcurrentDictionary`) para garantir 60fps sem bloquear a renderização da janela do popup.

## Consequências

- **Positivas**:
  - Distinção visual imediata entre vídeos, músicas, planilhas, apresentações e documentos pelo card e pela cor de destaque.
  - O usuário ganha controle total para organizar suas próprias extensões e preferências de cor.
  - Pré-visualizações visuais nativas de arquivos (fotos, vídeos e documentos) sem depender de bibliotecas pesadas de terceiros.
  - Remoção de complexidade desnecessária com a eliminação do submenu de tags.
- **Negativas / Desafios**:
  - Extração de miniaturas do Shell requer chamadas COM Win32 e gerenciamento cuidadoso de handles `HBITMAP` e bitmaps WPF congelados (`Freeze()`).
