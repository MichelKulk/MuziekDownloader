using System.Diagnostics;
using MuziekDownloader.Models;
using MuziekDownloader.Services;

namespace MuziekDownloader;

internal sealed class MainForm : Form
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly ToolManager _tools = new();
    private readonly BindingSource _source = new();
    private readonly List<DownloadItem> _items = [];
    private readonly TextBox _url = new() { PlaceholderText = "Plak hier een YouTube-link…", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11) };
    private readonly Button _paste = new() { Text = "＋ Plakken", AutoSize = true, BackColor = Color.FromArgb(0, 176, 117), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    private readonly Button _download = new() { Text = "↓ Downloaden", AutoSize = true, BackColor = Color.FromArgb(52, 143, 245), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    private readonly Button _update = new() { Text = "Controleren op updates", AutoSize = true };
    private readonly ComboBox _format = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
    private readonly ComboBox _quality = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White, BorderStyle = BorderStyle.None };
    private readonly ToolStripStatusLabel _status = new("Gereed");
    private readonly ToolStripProgressBar _overall = new() { Width = 180, Visible = false };
    private readonly Label _folder = new() { AutoEllipsis = true, Dock = DockStyle.Fill, ForeColor = Color.DimGray };
    private readonly CancellationTokenSource _closing = new();

    public MainForm()
    {
        Text = "Muziek Downloader";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        MinimumSize = new Size(780, 500);
        Size = new Size(920, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);
        AllowDrop = true;
        BuildUi();
        _source.DataSource = _items;
        _grid.DataSource = _source;
        _folder.Text = _settings.OutputFolder;
        _format.Items.AddRange(["MP3 - Audio", "MP4 - Video"]);
        _quality.Items.AddRange(["Beste", "1080p", "720p", "480p"]);
        _format.SelectedIndex = _settings.OutputFormat == "MP4" ? 1 : 0;
        _quality.SelectedIndex = _settings.VideoHeight switch { 0 => 0, 720 => 2, 480 => 3, _ => 1 };
        UpdateFormatControls();
        WireEvents();
    }

    private void BuildUi()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("Bestand");
        file.DropDownItems.Add("Afsluiten", null, (_, _) => Close());
        var settings = new ToolStripMenuItem("Instellingen");
        settings.DropDownItems.Add("Uitvoermap kiezen…", null, (_, _) => ChooseFolder());
        settings.DropDownItems.Add("Uitvoermap openen", null, (_, _) => OpenFolder());
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add("Over Muziek Downloader", null, (_, _) => MessageBox.Show(this,
            "Muziek Downloader 0.2.1\nApp4you2 internetservice B.V.\n\nMP3-audio en MP4-video.\nGeen account of apparatenlimiet.\nGebruik alleen voor materiaal dat je mag downloaden.", "Over"));
        menu.Items.AddRange([file, settings, help]);

        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 62, Padding = new Padding(14, 12, 14, 8), ColumnCount = 6 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(_paste, 0, 0); top.Controls.Add(_url, 1, 0); top.Controls.Add(_format, 2, 0); top.Controls.Add(_quality, 3, 0); top.Controls.Add(_update, 4, 0); top.Controls.Add(_download, 5, 0);
        foreach (Control control in top.Controls) control.Margin = new Padding(5);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DownloadItem.Title), HeaderText = "Titel", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 240 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DownloadItem.Duration), HeaderText = "Duur", Width = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DownloadItem.Status), HeaderText = "Status", Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DownloadItem.Progress), HeaderText = "%", Width = 55 });

        var empty = new Label { Text = "Plak hierboven een URL om te beginnen", Dock = DockStyle.Top, Height = 45, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gray };
        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(14, 7, 14, 7), ColumnCount = 3 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var choose = new Button { Text = "Map kiezen…", AutoSize = true };
        choose.Click += (_, _) => ChooseFolder();
        bottom.Controls.Add(choose, 0, 0); bottom.Controls.Add(_folder, 1, 0);
        var options = new CheckBox { Text = "Bestaande bestanden overslaan", Checked = _settings.SkipExisting, AutoSize = true, Anchor = AnchorStyles.Right };
        options.CheckedChanged += (_, _) => { _settings.SkipExisting = options.Checked; _settings.Save(); };
        bottom.Controls.Add(options, 2, 0);

        var status = new StatusStrip(); status.Items.Add(_status); status.Items.Add(new ToolStripStatusLabel { Spring = true }); status.Items.Add(_overall);
        Controls.Add(_grid); Controls.Add(empty); Controls.Add(bottom); Controls.Add(top); Controls.Add(menu); Controls.Add(status);
        MainMenuStrip = menu;
    }

    private void WireEvents()
    {
        _paste.Click += async (_, _) => { if (Clipboard.ContainsText()) _url.Text = Clipboard.GetText().Trim(); await AddUrlAsync(); };
        _url.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await AddUrlAsync(); } };
        _download.Click += async (_, _) => await DownloadAllAsync();
        _update.Click += async (_, _) => await UpdateToolsAsync();
        _format.SelectedIndexChanged += (_, _) => { UpdateFormatControls(); SaveFormatSettings(); };
        _quality.SelectedIndexChanged += (_, _) => SaveFormatSettings();
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.Text) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += async (_, e) => { if (e.Data?.GetData(DataFormats.Text) is string text) { _url.Text = text.Trim(); await AddUrlAsync(); } };
        FormClosing += (_, _) => { _closing.Cancel(); _settings.Save(); };
    }

    private async Task EnsureToolsAsync()
    {
        if (File.Exists(_tools.YtDlpPath)) return;
        _status.Text = "Downloadcomponent voorbereiden…";
        await _tools.UpdateYtDlpAsync(new Progress<string>(s => _status.Text = s));
    }

    private async Task UpdateToolsAsync()
    {
        SetBusy(true);
        try
        {
            var progress = new Progress<string>(s => _status.Text = s);
            await _tools.UpdateYtDlpAsync(progress);
            await _tools.EnsureFfmpegAsync(progress);
            _status.Text = "Alles is bijgewerkt";
            MessageBox.Show(this, "De downloadcomponent en media-omzetter zijn bijgewerkt.", "Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { ShowError("Bijwerken is mislukt", ex); }
        finally { SetBusy(false); }
    }

    private async Task AddUrlAsync()
    {
        var url = _url.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.Host.Contains("youtube", StringComparison.OrdinalIgnoreCase) && !uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        { MessageBox.Show(this, "Plak een geldige YouTube-link.", "Ongeldige link", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        SetBusy(true);
        try
        {
            await EnsureToolsAsync();
            var service = new YtDlpService(_tools);
            var downloadPlaylist = HasQueryParameter(uri, "list");
            if (downloadPlaylist)
            {
                var choice = MessageBox.Show(this, "Deze link hoort bij een afspeellijst.\n\nJa = volledige afspeellijst\nNee = alleen deze video", "Afspeellijst gevonden", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel) return;
                downloadPlaylist = choice == DialogResult.Yes;
            }
            var item = await service.InspectAsync(url, downloadPlaylist, _closing.Token);
            if (!HasQueryParameter(uri, "list") && item.IsPlaylist)
            {
                var choice = MessageBox.Show(this, "Deze link hoort bij een afspeellijst.\n\nJa = volledige afspeellijst\nNee = alleen deze video", "Afspeellijst gevonden", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel) return;
                downloadPlaylist = choice == DialogResult.Yes;
                if (!downloadPlaylist) item = await service.InspectAsync(url, false, _closing.Token);
            }
            item.IsPlaylist = downloadPlaylist;
            _items.Add(item); _source.ResetBindings(false); _url.Clear();
            _status.Text = $"Toegevoegd: {item.Title}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowError("Link kon niet worden gelezen", ex); }
        finally { SetBusy(false); }
    }

    private static bool HasQueryParameter(Uri uri, string name) => uri.Query
        .TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Any(part => part.Split('=', 2)[0].Equals(name, StringComparison.OrdinalIgnoreCase));

    private async Task DownloadAllAsync()
    {
        if (_items.Count == 0) { MessageBox.Show(this, "Voeg eerst een link toe.", "Geen downloads"); return; }
        SetBusy(true); _overall.Visible = true;
        try
        {
            await EnsureToolsAsync();
            _overall.Visible = true;
            await _tools.EnsureFfmpegAsync(new Progress<string>(s => {
                _status.Text = s;
                var marker = s.LastIndexOf(':');
                if (marker >= 0 && int.TryParse(s[(marker + 1)..].Trim().TrimEnd('%'), out var percentage))
                    _overall.Value = Math.Clamp(percentage, 0, 100);
            }));
            var service = new YtDlpService(_tools);
            foreach (var item in _items.Where(i => i.Status != "Voltooid"))
            {
                var progress = new Progress<(int percent, string status)>(p => { item.Progress = p.percent; item.Status = p.status; _overall.Value = p.percent; _source.ResetBindings(false); });
                try
                {
                    await DownloadItemAsync(service, item, progress);
                }
                catch (Exception ex) when (IsYouTubeAccessError(ex))
                {
                    item.Status = "YouTube-koppeling bijwerken";
                    _source.ResetBindings(false);
                    _status.Text = "YouTube is gewijzigd; downloadcomponent automatisch bijwerken…";
                    await _tools.UpdateYtDlpAsync(new Progress<string>(s => _status.Text = s));
                    item.Progress = 0;
                    item.Status = "Opnieuw proberen";
                    _source.ResetBindings(false);
                    await DownloadItemAsync(service, item, progress);
                }
                catch (Exception ex) { item.Status = "Mislukt"; _source.ResetBindings(false); ShowError($"Download mislukt: {item.Title}", ex); }
            }
            _status.Text = "Wachtrij verwerkt";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowError("Downloaden is mislukt", ex); }
        finally { _overall.Visible = false; SetBusy(false); }
    }

    private Task DownloadItemAsync(YtDlpService service, DownloadItem item, IProgress<(int percent, string status)> progress) =>
        service.DownloadAsync(item, _settings.OutputFolder, item.IsPlaylist, _settings.SkipExisting,
            _settings.EmbedThumbnail, _settings.AddMetadata, _settings.OutputFormat, _settings.VideoHeight,
            progress, _closing.Token);

    private static bool IsYouTubeAccessError(Exception ex) =>
        ex.Message.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase);

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Kies waar de gedownloade bestanden worden opgeslagen", SelectedPath = _settings.OutputFolder, UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) { _settings.OutputFolder = dialog.SelectedPath; _folder.Text = dialog.SelectedPath; _settings.Save(); }
    }

    private void OpenFolder()
    {
        Directory.CreateDirectory(_settings.OutputFolder);
        Process.Start(new ProcessStartInfo("explorer.exe", _settings.OutputFolder) { UseShellExecute = true });
    }

    private void SetBusy(bool busy)
    {
        _paste.Enabled = _download.Enabled = _update.Enabled = _format.Enabled = !busy;
        _quality.Enabled = !busy && _settings.OutputFormat == "MP4";
        UseWaitCursor = false;
        Cursor = Cursors.Default;
    }

    private void UpdateFormatControls()
    {
        var isMp4 = _format.SelectedIndex == 1;
        _quality.Enabled = isMp4;
        _quality.Visible = isMp4;
    }

    private void SaveFormatSettings()
    {
        if (_format.SelectedIndex < 0 || _quality.SelectedIndex < 0) return;
        _settings.OutputFormat = _format.SelectedIndex == 1 ? "MP4" : "MP3";
        _settings.VideoHeight = _quality.SelectedIndex switch { 0 => 0, 2 => 720, 3 => 480, _ => 1080 };
        _settings.Save();
    }
    private void ShowError(string title, Exception ex) { _status.Text = title; MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error); }
}

