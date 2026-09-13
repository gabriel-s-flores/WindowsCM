// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using WindowsCM.Core.Localization;

namespace WindowsCM.Core.Transfer;

// Generates modern, responsive Fluent-styled HTML pages for mobile devices (iOS / Android).
// 100% self-contained: requires no external CDNs or internet connectivity.
public static class MobileWebTemplate
{
    public static string RenderDownloadPage(SharedItemSession session, string host, bool? isPortuguese = null)
    {
        var pt = isPortuguese ?? LocalizationManager.IsPortuguese;
        var langAttr = pt ? "pt-BR" : "en";
        var title = EscapeHtml(session.Title);
        var fileName = EscapeHtml(session.FileName);
        var sizeFormatted = FormatSize(session.FileSize, pt);
        var isAudio = session.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
        var isImage = session.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var isVideo = session.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
        var isText = !string.IsNullOrEmpty(session.TextContent);
        var downloadUrl = $"/file/{session.Token}?download=1";
        var streamUrl = $"/file/{session.Token}";

        var copyBtnText = pt ? "📋 Copiar Texto no Celular" : "📋 Copy Text on Mobile";
        var downloadBtnText = pt ? $"📥 Baixar Arquivo ({sizeFormatted})" : $"📥 Download File ({sizeFormatted})";
        var footerLinkText = pt ? "📤 Quer enviar algo do celular para o PC? Toque aqui" : "📤 Want to send something from mobile to PC? Tap here";
        var toastCopied = pt ? "Texto copiado!" : "Text copied!";

        var previewHtml = new StringBuilder();
        if (isAudio)
        {
            previewHtml.AppendLine($"""
              <div class="audio-container">
                <audio controls src="{streamUrl}" preload="metadata" style="width: 100%; border-radius: 8px; outline: none;"></audio>
              </div>
            """);
        }
        else if (isImage)
        {
            previewHtml.AppendLine($"""
              <div class="image-container">
                <img src="{streamUrl}" alt="{title}" loading="lazy" />
              </div>
            """);
        }
        else if (isVideo)
        {
            previewHtml.AppendLine($"""
              <div class="video-container">
                <video controls src="{streamUrl}" preload="metadata" style="width: 100%; border-radius: 12px;"></video>
              </div>
            """);
        }
        else if (isText)
        {
            var encodedText = EscapeHtml(session.TextContent ?? "");
            previewHtml.AppendLine($"""
              <div class="text-container">
                <pre id="textToCopy">{encodedText}</pre>
                <button class="btn btn-secondary" onclick="copyTextToClipboard()">{copyBtnText}</button>
              </div>
            """);
        }

        var actionHtml = new StringBuilder();
        if (!isText && session.FileSize > 0)
        {
            actionHtml.AppendLine($"""
              <a href="{downloadUrl}" class="btn btn-primary" download="{fileName}">
                {downloadBtnText}
              </a>
            """);
        }

        return $$"""
        <!DOCTYPE html>
        <html lang="{{langAttr}}">
        <head>
          <meta charset="UTF-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
          <title>{{title}} - WindowsCM</title>
          <style>
            {{GetCommonCss()}}
          </style>
        </head>
        <body>
          <div class="app-card">
            <div class="header">
              <div class="badge">WindowsCM Transfer</div>
              <h1 class="title">{{title}}</h1>
              <div class="meta">{{session.KindLabel}} • {{sizeFormatted}}</div>
            </div>

            <div class="content-body">
              {{previewHtml}}
              <div class="actions">
                {{actionHtml}}
              </div>
            </div>

            <hr class="divider" />

            <div class="footer-action">
              <a href="/" class="btn-link">{{footerLinkText}}</a>
            </div>
          </div>

          <div id="toast" class="toast">{{toastCopied}}</div>

          <script>
            function copyTextToClipboard() {
              const el = document.getElementById('textToCopy');
              if (!el) return;
              const text = el.innerText || el.textContent;
              if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(text).then(showToast);
              } else {
                const ta = document.createElement('textarea');
                ta.value = text;
                document.body.appendChild(ta);
                ta.select();
                document.execCommand('copy');
                document.body.removeChild(ta);
                showToast();
              }
            }
            function showToast() {
              const t = document.getElementById('toast');
              t.classList.add('show');
              setTimeout(() => t.classList.remove('show'), 2500);
            }
          </script>
        </body>
        </html>
        """;
    }

    public static string RenderUploadPage(string host, bool? isPortuguese = null)
    {
        var pt = isPortuguese ?? LocalizationManager.IsPortuguese;
        var langAttr = pt ? "pt-BR" : "en";
        var pageTitle = pt ? "Enviar para o Computador - WindowsCM" : "Send to PC - WindowsCM";
        var heading = pt ? "Enviar para o PC" : "Send to PC";
        var subtitle = pt ? "Transfira textos, fotos ou arquivos direto para o clipboard do computador." : "Transfer text, photos, or files directly to your computer's clipboard.";
        var tabFiles = pt ? "📁 Arquivos & Mídias" : "📁 Files & Media";
        var tabText = pt ? "📝 Texto & Links" : "📝 Text & Links";
        var dropText = pt ? "Toque para escolher fotos, vídeos, áudio ou documentos" : "Tap to choose photos, videos, audio, or documents";
        var uploadBtnDefault = pt ? "Enviar Arquivo(s) para o PC" : "Send File(s) to PC";
        var textPlaceholder = pt ? "Cole ou digite aqui qualquer texto, código ou link para enviar ao computador..." : "Paste or type any text, code, or link to send to your computer...";
        var pasteBtn = pt ? "📋 Colar" : "📋 Paste";
        var sendTextBtn = pt ? "Enviar para o PC" : "Send to PC";
        var toastSentSuccess = pt ? "Enviado com sucesso!" : "Sent successfully!";
        var toastPcSuccess = pt ? "✅ Enviado com sucesso para o computador!" : "✅ Successfully sent to computer!";
        var failSendMsg = pt ? "Falha ao enviar arquivo(s): " : "Failed to send file(s): ";
        var connErrorMsg = pt ? "Erro de conexão ao enviar para o computador." : "Connection error while sending to computer.";
        var clipPermDenied = pt ? "Permissão de área de transferência negada pelo navegador do celular." : "Clipboard permission denied by mobile browser.";
        var clipNotSupported = pt ? "Área de transferência não suportada neste navegador." : "Clipboard not supported in this browser.";
        var enterSomething = pt ? "Digite ou cole algo antes de enviar." : "Please enter or paste something before sending.";
        var textCopiedToPc = pt ? "✅ Texto copiado para o PC!" : "✅ Text copied to PC!";
        var textSendError = pt ? "Erro ao enviar texto para o PC." : "Error sending text to PC.";
        var textConnError = pt ? "Erro de conexão ao enviar para o PC." : "Connection error while sending to PC.";
        var jsPrefix = pt ? "Enviar " : "Send ";
        var jsSuffix = pt ? " arquivo(s) para o PC" : " file(s) to PC";

        return $$"""
        <!DOCTYPE html>
        <html lang="{{langAttr}}">
        <head>
          <meta charset="UTF-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
          <title>{{pageTitle}}</title>
          <style>
            {{GetCommonCss()}}
            .tabs { display: flex; gap: 8px; margin-bottom: 20px; background: rgba(128,128,128,0.12); padding: 4px; border-radius: 12px; }
            .tab-btn { flex: 1; padding: 10px 14px; border: none; background: transparent; color: inherit; font-size: 14px; font-weight: 600; border-radius: 8px; cursor: pointer; transition: all 0.2s; }
            .tab-btn.active { background: var(--bg-card); color: var(--accent); box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
            .tab-content { display: none; }
            .tab-content.active { display: block; }
            textarea { width: 100%; min-height: 140px; box-sizing: border-box; border-radius: 12px; border: 1px solid var(--border); background: var(--bg-input); color: inherit; padding: 14px; font-size: 15px; font-family: inherit; resize: vertical; margin-bottom: 14px; outline: none; }
            textarea:focus { border-color: var(--accent); box-shadow: 0 0 0 2px var(--accent-glow); }
            .drop-zone { border: 2px dashed var(--border); border-radius: 14px; padding: 32px 16px; text-align: center; cursor: pointer; transition: border-color 0.2s; margin-bottom: 16px; }
            .drop-zone:hover, .drop-zone.dragover { border-color: var(--accent); background: rgba(0,120,212,0.05); }
            .drop-icon { font-size: 38px; margin-bottom: 8px; }
            .drop-text { font-size: 14px; font-weight: 500; color: var(--text-muted); }
            .file-list { margin-bottom: 16px; max-height: 180px; overflow-y: auto; text-align: left; }
            .file-item { display: flex; justify-content: space-between; padding: 8px 12px; background: var(--bg-input); border-radius: 8px; margin-bottom: 6px; font-size: 13px; }
            .progress-bar { width: 100%; height: 6px; background: var(--border); border-radius: 3px; overflow: hidden; margin-bottom: 14px; display: none; }
            .progress-fill { height: 100%; background: var(--accent); width: 0%; transition: width 0.2s; }
          </style>
        </head>
        <body>
          <div class="app-card">
            <div class="header">
              <div class="badge">WindowsCM Transfer</div>
              <h1 class="title">{{heading}}</h1>
              <div class="meta">{{subtitle}}</div>
            </div>

            <div class="tabs">
              <button class="tab-btn active" onclick="switchTab('files')">{{tabFiles}}</button>
              <button class="tab-btn" onclick="switchTab('text')">{{tabText}}</button>
            </div>

            <!-- Tab Files -->
            <div id="tab-files" class="tab-content active">
              <div class="drop-zone" onclick="document.getElementById('fileInput').click()">
                <div class="drop-icon">📤</div>
                <div class="drop-text">{{dropText}}</div>
                <input type="file" id="fileInput" multiple style="display: none;" onchange="onFilesSelected(this.files)" />
              </div>

              <div id="fileList" class="file-list"></div>

              <div id="progressBar" class="progress-bar">
                <div id="progressFill" class="progress-fill"></div>
              </div>

              <button id="uploadBtn" class="btn btn-primary" onclick="uploadFiles()" disabled>
                {{uploadBtnDefault}}
              </button>
            </div>

            <!-- Tab Text -->
            <div id="tab-text" class="tab-content">
              <textarea id="textInput" placeholder="{{textPlaceholder}}"></textarea>
              <div style="display: flex; gap: 8px;">
                <button class="btn btn-secondary" style="flex: 1;" onclick="pasteFromClipboard()">{{pasteBtn}}</button>
                <button class="btn btn-primary" style="flex: 2;" onclick="sendText()">{{sendTextBtn}}</button>
              </div>
            </div>
          </div>

          <div id="toast" class="toast">{{toastSentSuccess}}</div>

          <script>
            let selectedFiles = [];

            function switchTab(name) {
              document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
              document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
              if (name === 'files') {
                document.querySelectorAll('.tab-btn')[0].classList.add('active');
                document.getElementById('tab-files').classList.add('active');
              } else {
                document.querySelectorAll('.tab-btn')[1].classList.add('active');
                document.getElementById('tab-text').classList.add('active');
              }
            }

            function onFilesSelected(files) {
              selectedFiles = Array.from(files);
              renderFileList();
            }

            function renderFileList() {
              const list = document.getElementById('fileList');
              const btn = document.getElementById('uploadBtn');
              list.innerHTML = '';
              if (selectedFiles.length === 0) {
                btn.disabled = true;
                btn.innerText = '{{uploadBtnDefault}}';
                return;
              }
              btn.disabled = false;
              btn.innerText = `{{jsPrefix}}${selectedFiles.length}{{jsSuffix}}`;
              selectedFiles.forEach((f, idx) => {
                const item = document.createElement('div');
                item.className = 'file-item';
                item.innerHTML = `<span>${f.name}</span><span style="opacity:0.7">${formatBytes(f.size)}</span>`;
                list.appendChild(item);
              });
            }

            function formatBytes(bytes) {
              if (bytes === 0) return '0 B';
              const k = 1024;
              const sizes = ['B', 'KB', 'MB', 'GB'];
              const i = Math.floor(Math.log(bytes) / Math.log(k));
              return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
            }

            function uploadFiles() {
              if (selectedFiles.length === 0) return;
              const formData = new FormData();
              for (let i = 0; i < selectedFiles.length; i++) {
                formData.append('files', selectedFiles[i]);
              }

              const progress = document.getElementById('progressBar');
              const fill = document.getElementById('progressFill');
              const btn = document.getElementById('uploadBtn');
              progress.style.display = 'block';
              fill.style.width = '0%';
              btn.disabled = true;

              const xhr = new XMLHttpRequest();
              xhr.open('POST', '/api/upload', true);

              xhr.upload.onprogress = (e) => {
                if (e.lengthComputable) {
                  const pct = Math.round((e.loaded / e.total) * 100);
                  fill.style.width = pct + '%';
                }
              };

              xhr.onload = () => {
                progress.style.display = 'none';
                btn.disabled = false;
                if (xhr.status >= 200 && xhr.status < 300) {
                  showToast('{{toastPcSuccess}}');
                  selectedFiles = [];
                  renderFileList();
                  document.getElementById('fileInput').value = '';
                } else {
                  alert('{{failSendMsg}}' + xhr.statusText);
                }
              };

              xhr.onerror = () => {
                progress.style.display = 'none';
                btn.disabled = false;
                alert('{{connErrorMsg}}');
              };

              xhr.send(formData);
            }

            function pasteFromClipboard() {
              if (navigator.clipboard && navigator.clipboard.readText) {
                navigator.clipboard.readText().then(t => {
                  document.getElementById('textInput').value = t;
                }).catch(() => {
                  alert('{{clipPermDenied}}');
                });
              } else {
                alert('{{clipNotSupported}}');
              }
            }

            function sendText() {
              const text = document.getElementById('textInput').value.trim();
              if (!text) {
                alert('{{enterSomething}}');
                return;
              }
              fetch('/api/upload', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ text: text })
              })
              .then(res => {
                if (res.ok) {
                  showToast('{{textCopiedToPc}}');
                  document.getElementById('textInput').value = '';
                } else {
                  alert('{{textSendError}}');
                }
              })
              .catch(() => alert('{{textConnError}}'));
            }

            function showToast(msg) {
              const t = document.getElementById('toast');
              t.innerText = msg;
              t.classList.add('show');
              setTimeout(() => t.classList.remove('show'), 3000);
            }
          </script>
        </body>
        </html>
        """;
    }

    private static string GetCommonCss() => """
      :root {
        --bg-page: #F3F3F3;
        --bg-card: #FFFFFF;
        --bg-input: #F9F9F9;
        --border: #E5E5E5;
        --text: #1C1C1C;
        --text-muted: #666666;
        --accent: #0078D4;
        --accent-hover: #106EBE;
        --accent-glow: rgba(0, 120, 212, 0.25);
      }

      @media (prefers-color-scheme: dark) {
        :root {
          --bg-page: #181818;
          --bg-card: #242424;
          --bg-input: #2D2D2D;
          --border: #383838;
          --text: #FFFFFF;
          --text-muted: #A0A0A0;
          --accent: #2B88D8;
          --accent-hover: #4CA0E0;
          --accent-glow: rgba(43, 136, 216, 0.35);
        }
      }

      body {
        margin: 0;
        padding: 16px;
        background-color: var(--bg-page);
        color: var(--text);
        font-family: -apple-system, BlinkMacSystemFont, "Segoe UI Variable Text", "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 94vh;
        -webkit-font-smoothing: antialiased;
      }

      .app-card {
        width: 100%;
        max-width: 480px;
        background: var(--bg-card);
        border: 1px solid var(--border);
        border-radius: 20px;
        box-shadow: 0 10px 30px rgba(0,0,0,0.12);
        padding: 24px;
        box-sizing: border-box;
      }

      .header {
        margin-bottom: 20px;
        text-align: center;
      }

      .badge {
        display: inline-block;
        font-size: 11px;
        font-weight: 700;
        letter-spacing: 0.5px;
        text-transform: uppercase;
        background: var(--accent-glow);
        color: var(--accent);
        padding: 4px 10px;
        border-radius: 12px;
        margin-bottom: 8px;
      }

      .title {
        font-size: 20px;
        font-weight: 700;
        margin: 0 0 6px 0;
        word-break: break-word;
      }

      .meta {
        font-size: 13px;
        color: var(--text-muted);
        line-height: 1.4;
      }

      .content-body {
        margin: 16px 0;
      }

      .image-container {
        text-align: center;
        margin: 12px 0;
      }
      .image-container img {
        max-width: 100%;
        max-height: 380px;
        border-radius: 12px;
        box-shadow: 0 4px 14px rgba(0,0,0,0.15);
      }

      .audio-container {
        background: var(--bg-input);
        padding: 14px;
        border-radius: 14px;
        margin: 12px 0;
        border: 1px solid var(--border);
      }

      .text-container {
        margin: 12px 0;
      }
      .text-container pre {
        background: var(--bg-input);
        padding: 14px;
        border-radius: 12px;
        border: 1px solid var(--border);
        font-family: "Cascadia Code", Consolas, Menlo, monospace;
        font-size: 13px;
        max-height: 250px;
        overflow-y: auto;
        white-space: pre-wrap;
        word-break: break-all;
        margin: 0 0 12px 0;
      }

      .actions {
        margin-top: 18px;
      }

      .btn {
        display: block;
        width: 100%;
        box-sizing: border-box;
        padding: 14px 20px;
        border-radius: 12px;
        font-size: 15px;
        font-weight: 600;
        text-align: center;
        text-decoration: none;
        cursor: pointer;
        border: none;
        transition: background 0.15s, transform 0.08s;
      }
      .btn:active { transform: scale(0.98); }
      .btn-primary { background: var(--accent); color: #FFFFFF; }
      .btn-primary:hover { background: var(--accent-hover); }
      .btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
      .btn-secondary { background: var(--bg-input); color: var(--text); border: 1px solid var(--border); }
      .btn-secondary:hover { background: var(--border); }
      .btn-link { display: block; text-align: center; color: var(--accent); text-decoration: none; font-size: 13.5px; font-weight: 500; }
      .btn-link:hover { text-decoration: underline; }

      .divider {
        border: none;
        border-top: 1px solid var(--border);
        margin: 22px 0 16px 0;
      }

      .toast {
        position: fixed;
        bottom: 24px;
        left: 50%;
        transform: translateX(-50%) translateY(100px);
        background: rgba(20, 20, 20, 0.9);
        color: #FFFFFF;
        padding: 12px 24px;
        border-radius: 24px;
        font-size: 14px;
        font-weight: 500;
        box-shadow: 0 6px 20px rgba(0,0,0,0.3);
        opacity: 0;
        transition: transform 0.25s cubic-bezier(0.175, 0.885, 0.32, 1.275), opacity 0.25s;
        pointer-events: none;
        z-index: 9999;
      }
      .toast.show {
        transform: translateX(-50%) translateY(0);
        opacity: 1;
      }
    """;

    private static string FormatSize(long bytes, bool pt = true)
    {
        if (bytes <= 0) return "";
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        var culture = pt ? System.Globalization.CultureInfo.GetCultureInfo("pt-BR") : System.Globalization.CultureInfo.InvariantCulture;
        return $"{size.ToString("0.#", culture)} {units[unit]}";
    }

    private static string EscapeHtml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
