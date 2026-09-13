// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.Localization;

public sealed class PortugueseAppStrings : IAppStrings
{
    public static PortugueseAppStrings Instance { get; } = new();

    public string AppName => "WindowsCM";
    public string SettingsTitle => "Configurações — WindowsCM";

    // Tray Menu & Notifications
    public string TrayOpen => "Abrir";
    public string TrayCompactMenu => "Menu compacto";
    public string TrayIncognito => "Modo anônimo";
    public string TrayIncognitoActive => "Modo anônimo (ativo)";
    public string TrayClearHistory => "Limpar histórico (manter fixados e tags)";
    public string TraySettings => "Configurações";
    public string TrayExit => "Sair";
    public string TrayShortcutConflictTitle => "Conflito de atalhos";
    public string TrayMissingItemBalloon(long id) => $"Item {id} não está mais no histórico, então nada foi copiado.";
    public string TrayPasteFailedBalloon(string message) => $"Falha ao colar após a cópia: {message}";
    public string TrayIconTooltip => "WindowsCM";
    public string TrayIconTooltipIncognito => "WindowsCM — Modo anônimo";

    // Popup (Cards & Top Bar)
    public string PopupSettingsTooltip => "Configurações";
    public string PopupIncognitoTooltip => "Ativar modo anônimo (Ctrl+Shift+Alt+V)";
    public string PopupIncognitoTooltipActive => "Modo anônimo ativo (Ctrl+Shift+Alt+V)";
    public string PopupIncognitoTooltipInactive => "Ativar modo anônimo (Ctrl+Shift+Alt+V)";
    public string PopupReceiveMobileTooltip => "Enviar do celular para o computador";
    public string PopupSearchPlaceholder => "Digite para pesquisar...";
    public string PopupSearchTooltip => "Pesquisar no histórico em tempo real";
    public string PopupFilterTooltip => "Filtrar por tipo de conteúdo";
    public string PopupFilterActiveTooltip => "Filtro ativo: {0} (Clique para alterar)";
    public string FilterAll => "Todos";
    public string FilterLinks => "Links";
    public string FilterCode => "Códigos";
    public string FilterFiles => "Arquivos";
    public string FilterImages => "Imagens";
    public string FilterEmojis => "Emojis & Símbolos";
    public string FilterColors => "Cores";
    public string FilterText => "Textos";
    public string PopupPinsTooltip => "Mostrar apenas fixados (Alt+P)";
    public string PopupClearText => "Limpar";
    public string PopupClearTooltip => "Limpar histórico (mantém fixados e tags)";

    // Incognito Banner & Empty State
    public string IncognitoBannerTitle => "MODO ANÔNIMO ATIVO";
    public string IncognitoBannerSubtitle => "· Sessão privada temporária";
    public string IncognitoBannerDescription => "Os clipboards desta sessão ficam salvos apenas enquanto você estiver anônimo e serão totalmente apagados ao sair.";
    public string IncognitoSwitchToNormal => "Ver histórico normal";
    public string IncognitoSwitchToIncognito => "Ver sessão anônima";
    public string IncognitoSwitchTooltip => "Alternar entre histórico normal e sessão anônima";
    public string IncognitoExitButton => "Sair do anônimo";
    public string IncognitoExitTooltip => "Encerrar modo anônimo e limpar histórico temporário";
    public string IncognitoEmptyStateTitle => "Clipboard anônimo vazio";
    public string IncognitoEmptyStateDescription => "Tudo o que você copiar nesta sessão privada ficará salvo apenas temporariamente aqui. Ao sair do modo anônimo, todo o conteúdo será destruído sem deixar nenhum rastro.";

    // Card Action Tooltips
    public string CardPinnedBadge => "Item fixado";
    public string CardQrTooltip => "Gerar QR Code (Ctrl+Q)";
    public string CardPinTooltip => "Fixar / Desafixar (Alt+P)";
    public string CardMoreOptionsTooltip => "Mais opções";
    public string CardDeleteTooltip => "Excluir (Delete)";

    // Card Context Menu
    public string ContextMenuPaste => "Colar";
    public string ContextMenuCopy => "Copiar";
    public string ContextMenuPin => "Fixar";
    public string ContextMenuUnpin => "Desafixar";
    public string ContextMenuConvert => "Converter";
    public string ContextMenuGenerateQr => "Gerar código QR";
    public string ContextMenuEditTitle => "Editar título...";
    public string ContextMenuEditContent => "Editar conteúdo...";
    public string ContextMenuDelete => "Excluir";

    // Compact Popup
    public string CompactSearchPlaceholder => "Pesquisar...";
    public string CompactClearSearchTooltip => "Limpar busca";
    public string CompactIncognitoSubtitle => "Sessão temporária sob o cursor";
    public string CompactIncognitoExitTooltip => "Sair do modo anônimo";
    public string CompactSettingsTooltip => "Configurações (Alt+S)";
    public string CompactIncognitoTooltip => "Modo Anônimo (Ctrl+Shift+Alt+V)";
    public string CompactIncognitoTooltipActive => "Modo anônimo ativo (Ctrl+Shift+Alt+V)";
    public string CompactIncognitoTooltipInactive => "Ativar modo anônimo (Ctrl+Shift+Alt+V)";
    public string CompactMobileTransferTooltip => PopupReceiveMobileTooltip;
    public string CompactClear => PopupClearText;
    public string CompactClearTooltip => "Limpar histórico (Alt+C)";
    public string CompactClearConfirm => CompactClearConfirmMessage;
    public string CompactClearConfirmMessage => "Deseja limpar todo o histórico de transferências?\n(Itens fixados serão preservados)";
    public string CompactQrTooltip => CardQrTooltip;
    public string CompactMoreOptionsTooltip => CardMoreOptionsTooltip;
    public string CompactDeleteTooltip => CardDeleteTooltip;
    public string CompactPinTooltip => CardPinTooltip;

    // Popup UI & Incognito Helpers
    public string PopupClearSearchTooltip => CompactClearSearchTooltip;
    public string PopupClearButton => PopupClearText;
    public string PopupPinnedBadgeTooltip => CardPinnedBadge;
    public string PopupQrTooltip => CardQrTooltip;
    public string PopupPinTooltip => CardPinTooltip;
    public string PopupMoreOptionsTooltip => CardMoreOptionsTooltip;
    public string PopupDeleteTooltip => CardDeleteTooltip;
    public string PopupIncognitoBannerTitle => IncognitoBannerTitle;
    public string PopupIncognitoBannerSubtitle => IncognitoBannerSubtitle;
    public string PopupIncognitoBannerDesc => IncognitoBannerDescription;
    public string PopupSwitchHistoryNormal => IncognitoSwitchToNormal;
    public string PopupSwitchHistoryIncognito => IncognitoSwitchToIncognito;
    public string PopupIncognitoBackgroundTitle => IncognitoEmptyStateTitle;
    public string PopupIncognitoBackgroundSubtitle => IncognitoBannerSubtitle;
    public string PopupIncognitoBackgroundDesc => IncognitoEmptyStateDescription;
    public string PopupIncognitoEmptyTitle => IncognitoEmptyStateTitle;
    public string PopupIncognitoEmptyDesc => IncognitoEmptyStateDescription;
    public string PopupExitIncognitoTooltip => IncognitoExitTooltip;

    // Item Kinds & Types
    public string KindImage => "Imagem";
    public string KindFile => "Arquivo";
    public string KindFiles => "Arquivos";
    public string KindFilesCount(int count, string first) => $"{count} arquivos ({first})";
    public string KindCode => "Código";
    public string KindText => "Texto";
    public string KindColor => "Cor";
    public string KindEmoji => "Emoji";
    public string KindCharacter => "Caractere";
    public string KindLink => "Link";

    // Type Labels
    public string LabelPngImage => "Imagem PNG";
    public string LabelMultipleFiles => "Múltiplos arquivos";
    public string LabelFilesCount(int count) => $"{count} arquivos";
    public string LabelCodeWithLanguage(string lang) => $"Código ({lang})";
    public string LabelTextLength(int length) => $"Texto • {length} caracteres";
    public string LabelWebLink => "Link Web";
    public string LabelEmojiCount(int count) => $"Emoji • {count} emojis";
    public string LabelEmojiCode(string code) => $"Emoji • {code}";
    public string LabelCharacterCode(string code) => $"Caractere • {code}";
    public string LabelFilesSelected(int count) => $"{count} arquivos selecionados";
    public string LabelMoreFilesRemaining(int count) => $"• + {count} outros arquivos...";

    // Builtin Categories
    public string CategoryImages => "Imagens";
    public string CategoryCode => "Códigos e Scripts";
    public string CategoryLinks => "Links e Páginas";
    public string CategoryDocuments => "Documentos";
    public string CategorySpreadsheets => "Planilhas";
    public string CategoryPresentations => "Apresentações";
    public string CategoryAudio => "Áudio";
    public string CategoryVideo => "Vídeos";
    public string CategoryArchives => "Compactados";
    public string CategoryTextOther => "Textos e Outros";

    // Relative Time
    public string TimeJustNow => "agora";
    public string TimeMinutesAgo(int minutes) => $"há {minutes} min";
    public string TimeHoursAgo(int hours) => $"há {hours} h";
    public string TimeDaysAgo(int days) => $"há {days} d";

    // Toast
    public string ToastAddedToClipboard => "Adicionado à área de transferência";
    public string ToastLinkCopied => "Link copiado para a área de transferência!";

    // Settings Window
    public string SettingsNavGeneral => "Histórico & Geral";
    public string SettingsNavLayout => "Layout & Posição";
    public string SettingsNavColors => "Cores dos Tipos";
    public string SettingsNavStorage => "Cache & Pastas";
    public string SettingsNavShortcuts => "Atalhos Globais";
    public string SettingsNavAbout => "Sobre & Sistema";
    public string SettingsThemeStatusFluent => "Modo Fluent Ativo";
    public string SettingsThemeStatusLight => "Modo Claro Ativo";
    public string SettingsThemeStatusDark => "Modo Escuro Ativo";
    public string SettingsThemeStatusHighContrast => "Modo Alto Contraste Ativo";

    // Settings Panel: Layout & Placement
    public string SettingsSectionLayoutTitle => "Layout e Posicionamento";
    public string SettingsSectionLayoutSubtitle => "Personalize a orientação das janelas, posição de ancoragem na tela e o fluxo temporal dos itens.";
    public string SettingsLayoutLargeTitle => "Área de Transferência Grande";
    public string SettingsLayoutLargeDesc => "Janela completa com pré-visualização rica de código, imagens, links e arquivos.";
    public string SettingsLayoutOrientation => "Orientação da Janela";
    public string SettingsLayoutOrientationHorizontal => "Horizontal (Faixa em tela cheia)";
    public string SettingsLayoutOrientationVertical => "Vertical (Painel lateral)";
    public string SettingsLayoutPosition => "Posicionamento na Tela";
    public string SettingsLayoutPosBottom => "Embaixo (Inferior da tela)";
    public string SettingsLayoutPosTop => "No Topo (Superior da tela)";
    public string SettingsLayoutPosLeft => "À Esquerda (Lateral esquerda)";
    public string SettingsLayoutPosRight => "À Direita (Lateral direita)";
    public string SettingsLayoutItemOrder => "Ordem dos Itens (Sentido do Fluxo)";
    public string SettingsLayoutOrderRecentLeft => "Mais recentes à esquerda (Padrão)";
    public string SettingsLayoutOrderRecentRight => "Mais recentes à direita";
    public string SettingsLayoutOrderRecentTop => "Mais recentes de cima para baixo (Padrão)";
    public string SettingsLayoutOrderRecentBottom => "Mais recentes de baixo para cima";

    public string SettingsLayoutScrollbarPosition => "Posição da Barra de Rolagem";
    public string SettingsLayoutScrollbarHorizontal => "Posição da Barra de Rolagem (Horizontal)";
    public string SettingsLayoutScrollbarVertical => "Posição da Barra de Rolagem (Vertical)";
    public string SettingsLayoutScrollbarPosRight => "À Direita (Padrão)";
    public string SettingsLayoutScrollbarPosLeft => "À Esquerda";
    public string SettingsLayoutScrollbarPosBottom => "Embaixo (Padrão)";
    public string SettingsLayoutScrollbarPosTop => "No Topo";

    public string SettingsLayoutCompactTitle => "Menu Compacto (Atalho Rápido)";
    public string SettingsLayoutCompactDesc => "Janela leve e ágil aberta sob o cursor do mouse para colagem imediata.";
    public string SettingsLayoutCompactOrientation => "Formato do Menu Compacto";
    public string SettingsLayoutCompactOrientationVertical => "Vertical (Lista sob o cursor - Padrão)";
    public string SettingsLayoutCompactOrientationHorizontal => "Horizontal (Cards compactos sob o cursor)";
    public string SettingsLayoutCompactOrder => "Ordem dos Itens no Menu Compacto";
    public string SettingsLayoutCompactOrderRecentLeft => "Mais recentes da esquerda para a direita (Padrão)";
    public string SettingsLayoutCompactOrderRecentRight => "Mais recentes da direita para a esquerda";
    public string SettingsLayoutCompactOrderRecentTop => "Mais recentes de cima para baixo (Padrão)";
    public string SettingsLayoutCompactOrderRecentBottom => "Mais recentes de baixo para cima";

    public string SettingsLayoutPreviewTitle => "Simulador Interativo de Layout e Fluxo";
    public string SettingsLayoutPreviewSubtitle => "Visualização dinâmica com dados simulados ilustrando a ancoragem na tela e a ordem dos cards.";
    public string SettingsLayoutPreviewRecentBadge => "⭐ Mais Recente";
    public string SettingsLayoutPreviewOldestBadge => "Itens Anteriores";
    public string SettingsLayoutPreviewToggleLarge => "Área Principal";
    public string SettingsLayoutPreviewToggleCompact => "Menu Compacto";
    public string SettingsLayoutMockImageTitle => "CapturaDeTela.png";
    public string SettingsLayoutMockLinkTitle => "github.com/copyous";
    public string SettingsLayoutMockLinkDesc => "Copyous para Windows";
    public string SettingsLayoutMockNotesTitle => "Anotações do projeto...";
    public string SettingsLayoutMockNotesDesc => "Alinhamento e layout v2";
    public string SettingsLayoutFlowHorizontalRecentLeft => "★ Recente ➔ ➔ ➔ Antigo";
    public string SettingsLayoutFlowHorizontalRecentRight => "Antigo ➔ ➔ ➔ ★ Recente";
    public string SettingsLayoutFlowVerticalRecentTop => "★ Recente ⬇ Antigo";
    public string SettingsLayoutFlowVerticalRecentBottom => "Antigo ⬇ ★ Recente";
    public string SettingsLayoutFlowCompactRecentLeft => "★ Recente ➔ Antigo";
    public string SettingsLayoutFlowCompactRecentRight => "Antigo ➔ ★ Recente";

    // Settings Panel 1: General
    public string SettingsSectionGeneralTitle => "Área de Transferência & Geral";
    public string SettingsSectionGeneralSubtitle => "Configure o limite de retenção do histórico, inicialização e preservação de dados.";
    public string SettingsLanguageTitle => "Idioma do Aplicativo";
    public string SettingsLanguageSubtitle => "Escolha entre seguir o idioma do Windows ou definir português/inglês manualmente.";
    public string SettingsLanguageSystem => "💻 Seguir o Windows (Padrão)";
    public string SettingsLanguageEnglish => "🇺🇸 English";
    public string SettingsLanguagePortuguese => "🇧🇷 Português";
    public string SettingsThemeTitle => "Tema e Aparência";
    public string SettingsThemeSubtitle => "Escolha entre modo escuro, modo claro, alto contraste para acessibilidade ou sincronizar com o Windows.";
    public string SettingsThemeOptionSystem => "💻 Seguir o Windows (Padrão)";
    public string SettingsThemeOptionDark => "🌙 Modo Escuro";
    public string SettingsThemeOptionLight => "☀️ Modo Claro";
    public string SettingsThemeOptionHighContrast => "🔲 Alto Contraste (Acessibilidade)";
    public string SettingsHistoryLimitTitle => "Limite máximo de itens no histórico";
    public string SettingsHistoryLimitSubtitle => "Define a quantidade de itens mantidos na clipboard. O valor recomendado e máximo é de 100 itens.";
    public string SettingsHistoryLimitBadge(int count) => $"{count} itens";
    public string SettingsHistoryLimitRestore => "Restaurar recomendado (100)";
    public string SettingsAutostartTitle => "Iniciar com o Windows";
    public string SettingsAutostartSubtitle => "Executar o WindowsCM silenciosamente na bandeja do sistema ao ligar o computador.";
    public string SettingsEndOfSessionTitle => "Limpeza ao encerrar a sessão";
    public string SettingsEndOfSessionSubtitle => "Comportamento do histórico ao reiniciar, deslogar ou desligar o Windows.";
    public string SettingsEndOfSessionClearAll => "Limpar tudo";
    public string SettingsEndOfSessionKeepPinsTags => "Manter favoritos e etiquetas (Recomendado)";
    public string SettingsEndOfSessionKeepAll => "Manter tudo";

    // Settings Panel 2: Colors & Categories
    public string SettingsSectionColorsTitle => "Cores e Tipos de Itens";
    public string SettingsSectionColorsSubtitle => "Personalize as cores semânticas, badges e extensões de arquivos associadas a cada tipo de conteúdo da clipboard.";
    public string SettingsAddCategoryButton => "+ Nova Categoria";
    public string SettingsResetAllColorsButton => "Restaurar padrões";
    public string SettingsResetAllColorsTooltip => "Restaurar todas as cores e categorias para os padrões recomendados";
    public string SettingsNewCategoryTitle => "Criar Nova Categoria de Arquivos";
    public string SettingsNewCategoryNameLabel => "Nome da Categoria (ex: Modelos 3D):";
    public string SettingsNewCategoryHexLabel => "Cor (Hex):";
    public string SettingsNewCategoryPickColor => "Escolher cor";
    public string SettingsNewCategoryExtLabel => "Extensões associadas separadas por vírgula (ex: .obj, .blend, .fbx, .stl):";
    public string SettingsNewCategoryCancel => "Cancelar";
    public string SettingsNewCategorySave => "Salvar Categoria";
    public string SettingsColorLabel => "Cor:";
    public string SettingsChooseColorButton => "Escolher cor";
    public string SettingsResetButton => "Restaurar";
    public string SettingsDeleteButton => "Excluir";
    public string SettingsExtensionsLabel => "Extensões:";
    public string SettingsAddExtensionButton => "+ Adicionar";
    public string SettingsAddExtensionPrompt => "Adicionar extensão (ex: .dat):";
    public string SettingsLinkHint => "Detectado automaticamente por URLs e links web (http, https, ftp...)";
    public string SettingsTextHint => "Detectado automaticamente para textos puros da área de transferência";
    public string BadgeDocument => "Documento";
    public string BadgeSpreadsheet => "Planilha";
    public string BadgePresentation => "Apresentação";
    public string BadgeAudio => "Áudio";
    public string BadgeVideo => "Vídeo";
    public string BadgeArchive => "Compactado";
    public string BadgeCharacter => "Caractere";
    public string BadgeColor => "Cor";
    public string UnifiedTypeOtherFilesName => "Outros Arquivos e Pastas";
    public string UnifiedTypeOtherFilesHint => "Usado para arquivos sem extensão catalogada ou múltiplos arquivos selecionados";
    public string UnifiedTypePlainTextName => "Textos Simples";
    public string UnifiedTypeCharacterName => "Caracteres / Emojis";
    public string UnifiedTypeCharacterHint => "Detectado automaticamente para caracteres individuais e emojis";
    public string UnifiedTypeColorName => "Cores (Color)";
    public string UnifiedTypeColorHint => "Detectado automaticamente para códigos de cores (HEX, RGB, HSL)";

    // Settings Panel 3: Storage
    public string SettingsSectionStorageTitle => "Cache & Armazenamento";
    public string SettingsSectionStorageSubtitle => "Gerencie o espaço em disco ocupado por dados temporários e acesse as pastas do sistema.";
    public string SettingsClearCacheTitle => "Limpar cache diretamente";
    public string SettingsClearCacheSubtitle => "Exclui miniaturas de páginas baixadas (favicons), imagens de links em cache e arquivos temporários para liberar espaço. O histórico de itens e imagens coladas não são afetados.";
    public string SettingsClearCacheButton => "Limpar Cache Agora";
    public string SettingsClearCacheSuccess(int files, string sizeFormatted) => $"Cache limpo com sucesso: {files} arquivo(s) removido(s) ({sizeFormatted} liberados).";
    public string SettingsClearCacheAlreadyEmpty => "O cache já estava vazio. Nenhum arquivo temporário pendente.";
    public string SettingsClearCacheWarning(string message) => $"Aviso ao limpar cache: {message}";
    public string SettingsClearCacheDefaultError => "Falha na exclusão de alguns arquivos.";
    public string SettingsStorageFoldersTitle => "Pastas de Armazenamento";
    public string SettingsStorageFoldersSubtitle => "Abra rapidamente os diretórios locais no Explorador de Arquivos:";
    public string SettingsOpenDataFolder => "Abrir pasta de dados";
    public string SettingsOpenConfigFolder => "Abrir pasta de configurações";
    public string SettingsOpenCacheFolder => "Abrir pasta de cache";
    public string SettingsPathsLabelData => "Dados";
    public string SettingsPathsLabelConfig => "Configurações";
    public string SettingsPathsLabelCache => "Cache";
    public string SettingsPathsLabelDb => "Banco de dados";
    public string SettingsPathsLabelActions => "Ações";
    public string SettingsPathsLabelSettings => "Configuração";

    // Settings Panel 4: Shortcuts
    public string SettingsSectionShortcutsTitle => "Atalhos Globais de Teclado";
    public string SettingsSectionShortcutsSubtitle => "Personalize as combinações de teclas para invocar a clipboard e o modo privado de qualquer aplicativo.";
    public string SettingsShortcutCompactTitle => "Menu Compacto (Histórico e Colagem)";
    public string SettingsShortcutCompactSubtitle => "Atalho global para abrir o menu compacto sob o cursor do mouse com busca rápida (Padrão: Ctrl+Shift+V).";
    public string SettingsShortcutIncognitoTitle => "Histórico Anônimo (Privado)";
    public string SettingsShortcutIncognitoSubtitle => "Abre o menu compacto sob o cursor em modo anônimo, suspendendo gravações persistentes (Padrão: Ctrl+Shift+Alt+V).";
    public string SettingsShortcutApplyButton => "Aplicar";
    public string SettingsShortcutStatusInfo => "Padrões recomendados: Ctrl+Shift+V para menu compacto sob o mouse e Ctrl+Shift+Alt+V para anônimo. Combinações com a tecla Windows são reservadas pelo sistema.";
    public string SettingsShortcutStatusUnavailable => "Atalhos indisponíveis nesta sessão.";
    public string SettingsShortcutStatusSuccess(string gesture) => $"Atalho registrado com sucesso: {gesture}.";
    public string SettingsShortcutStatusFailed => "Falha ao remapear atalho.";

    // Settings Panel 5: About
    public string SettingsSectionAboutTitle => "Sobre o WindowsCM";
    public string SettingsSectionAboutSubtitle => "Informações de versão, licença e créditos do projeto.";
    public string SettingsVersionTitle => "Versão do Aplicativo & Runtime";
    public string SettingsTrayGuidanceTitle => "Bandeja do Sistema (System Tray)";
    public string SettingsTrayGuidanceBody => TrayOnboarding.Guidance;
    public string SettingsCreditsTitle => "Créditos & Reconhecimentos";

    // Mobile Transfer
    public string MobileWindowTitle => "Enviar do Celular para o Computador";
    public string MobileHeaderTitle => "Enviar do Celular para o PC";
    public string MobileHeaderSubtitle => "Aponte a câmera do smartphone para o QR Code abaixo:";
    public string MobileCopyLinkButton => "Copiar Link";
    public string MobileStatusActive => "Servidor ativo. Aguardando envio do celular...";
    public string MobileStatusSubtext => "Tudo o que enviar cairá direto na área de transferência do Windows.";
    public string MobileTextReceived(string excerpt) => $"✅ Texto recebido: \"{excerpt}\"";
    public string MobileTextCopiedSuccess => "Copiado com sucesso para a área de transferência do Windows.";
    public string MobileFilesReceived(int count, string first) => $"✅ {count} arquivos recebidos (ex: {first})";
    public string MobileSingleFileReceived(string first) => $"✅ Arquivo recebido: {first}";
    public string MobileFilesSavedSuccess => "Salvo em Downloads\\WindowsCM Transfers e adicionado ao clipboard.";
    public string MobileOpenFolderButton => "Abrir Pasta de Transferências";
    public string MobileCloseButton => "Fechar";
    public string MobileFolderOpenError(string error) => $"Não foi possível abrir a pasta: {error}";

    // QR Window
    public string QrWindowTitle => "Compartilhar via QR Code";
    public string QrHeaderScanTitle => "Escanear com o Celular";
    public string QrHeaderScanSubtitle => "Aponte a câmera para abrir e baixar no seu dispositivo";
    public string QrHeaderDownloadTitle => "Baixar no Smartphone";
    public string QrHeaderDownloadSubtitle => "Escaneie o QR Code com a câmera para acessar o item";
    public string QrCopyAccessLink => "Copiar Link de Acesso";
    public string QrPickAnotherFile => "Escolher outro arquivo...";
    public string QrCloseButton => "Fechar";
    public string QrFileDialogTitle => "Selecionar arquivo para enviar ao celular";
    public string QrFileDialogFilter => "Todos os Arquivos (*.*)|*.*|Áudio (*.mp3;*.wav;*.m4a;*.ogg)|*.mp3;*.wav;*.m4a;*.ogg|Imagens (*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp|Documentos (*.pdf;*.docx;*.xlsx;*.txt)|*.pdf;*.docx;*.xlsx;*.txt";
    public string QrFallbackFileKind => "Arquivo do Computador";

    // Common Dialogs
    public string DialogOk => "OK";
    public string DialogCancel => "Cancelar";
    public string DialogEditTitle => "Editar título do item";
    public string DialogEditContent => "Editar conteúdo do item";
    public string DialogEditTitlePrompt => "Editar título";
    public string DialogEditContentPrompt => "Editar conteúdo";

    // CLI & System errors
    public string CliHelpText =>
        "WindowsCM — Gerenciador de Área de Transferência\n\n" +
        "--toggle | --show | --hide | --clear | --clear-all\n" +
        "--hidden   inicia apenas na bandeja (inicialização automática)\n" +
        "--help     exibe esta ajuda";
    public string ErrorSidDetermination => "Não foi possível determinar o SID do usuário atual; encerrando.";
    public string ErrorForwardFailed => "O WindowsCM não está em execução e o comando não pôde ser entregue.";
}
