using System.Diagnostics;
using System.Drawing;

namespace PortableRdpManager;

internal sealed class MainForm : Form
{
    private readonly TextBox _host = new();
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Value = 3389 };
    private readonly TextBox _userName = new();
    private readonly NumericUpDown _width = new() { Minimum = 200, Maximum = 16384, Value = 1920 };
    private readonly NumericUpDown _height = new() { Minimum = 200, Maximum = 16384, Value = 1080 };
    private readonly CheckBox _fullScreen = new() { Text = "Полный экран", AutoSize = true };
    private readonly CheckBox _multiMonitor = new() { Text = "Использовать все мониторы", AutoSize = true };
    private readonly CheckBox _adminSession = new() { Text = "Административная сессия (/admin)", AutoSize = true };
    private readonly CheckBox _clipboard = new() { Text = "Буфер обмена", AutoSize = true, Checked = true };
    private readonly CheckBox _drives = new() { Text = "Локальные диски", AutoSize = true };
    private readonly CheckBox _printers = new() { Text = "Принтеры", AutoSize = true };
    private readonly CheckBox _smartCards = new() { Text = "Смарт-карты", AutoSize = true };
    private readonly ComboBox _audio = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _useGateway = new() { Text = "Использовать RD Gateway", AutoSize = true };
    private readonly TextBox _gateway = new();
    private readonly TextBox _raw = new()
    {
        Multiline = true,
        AcceptsReturn = true,
        AcceptsTab = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font("Consolas", 10)
    };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly TabPage _rawTab = new("Все параметры");

    private RdpDocument _document = RdpDocument.Create();
    private bool _loading;
    private bool _dirty;
    private TabPage? _previousTab;

    public MainForm()
    {
        Text = "Portable RDP Manager";
        MinimumSize = new Size(720, 580);
        Size = new Size(820, 650);
        StartPosition = FormStartPosition.CenterScreen;

        _audio.Items.AddRange(["На этом компьютере", "На удалённом компьютере", "Не воспроизводить"]);
        _audio.SelectedIndex = 0;

        var menu = BuildMenu();
        var buttons = BuildButtons();
        Controls.Add(_tabs);
        Controls.Add(buttons);
        Controls.Add(menu);
        MainMenuStrip = menu;

        _tabs.TabPages.Add(BuildConnectionTab());
        _tabs.TabPages.Add(BuildResourcesTab());
        _rawTab.Controls.Add(_raw);
        _tabs.TabPages.Add(_rawTab);
        _previousTab = _tabs.SelectedTab;
        _tabs.Selecting += TabsOnSelecting;
        _tabs.Selected += (_, _) => _previousTab = _tabs.SelectedTab;

        RegisterChangeHandlers(this);
        FormClosing += OnFormClosing;
        NewDocument();
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("Файл");
        file.DropDownItems.Add("Новый", null, (_, _) => NewDocument());
        file.DropDownItems.Add("Открыть…", null, (_, _) => OpenDocument());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Сохранить", null, (_, _) => SaveDocument(false));
        file.DropDownItems.Add("Сохранить как…", null, (_, _) => SaveDocument(true));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Выход", null, (_, _) => Close());

        var session = new ToolStripMenuItem("Сессия");
        session.DropDownItems.Add("Подключиться", null, (_, _) => Connect());
        session.DropDownItems.Add("Открыть папку файла", null, (_, _) => OpenContainingFolder());

        var help = new ToolStripMenuItem("Справка");
        help.DropDownItems.Add("О программе", null, (_, _) => MessageBox.Show(
            "Portable RDP Manager\n\nРедактор файлов Microsoft Remote Desktop (.rdp).\n" +
            "Пароли не читаются и не сохраняются приложением.",
            "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information));

        menu.Items.AddRange([file, session, help]);
        return menu;
    }

    private Control BuildButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var connect = new Button { Text = "Подключиться", Width = 140, Height = 32 };
        var save = new Button { Text = "Сохранить", Width = 110, Height = 32 };
        connect.Click += (_, _) => Connect();
        save.Click += (_, _) => SaveDocument(false);
        panel.Controls.Add(connect);
        panel.Controls.Add(save);
        return panel;
    }

    private TabPage BuildConnectionTab()
    {
        var page = new TabPage("Подключение");
        var table = CreateTable();
        AddRow(table, "Компьютер:", _host);
        AddRow(table, "Порт:", _port);
        AddRow(table, "Пользователь:", _userName);
        AddRow(table, "Ширина:", _width);
        AddRow(table, "Высота:", _height);
        AddRow(table, "", _fullScreen);
        AddRow(table, "", _multiMonitor);
        AddRow(table, "", _adminSession);
        AddRow(table, "", _useGateway);
        AddRow(table, "RD Gateway:", _gateway);
        _useGateway.CheckedChanged += (_, _) => _gateway.Enabled = _useGateway.Checked;
        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildResourcesTab()
    {
        var page = new TabPage("Локальные ресурсы");
        var table = CreateTable();
        AddRow(table, "Звук:", _audio);
        AddRow(table, "", _clipboard);
        AddRow(table, "", _drives);
        AddRow(table, "", _printers);
        AddRow(table, "", _smartCards);
        page.Controls.Add(table);
        return page;
    }

    private static TableLayoutPanel CreateTable() => new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        Padding = new Padding(24),
        ColumnCount = 2,
        RowCount = 0,
        ColumnStyles =
        {
            new ColumnStyle(SizeType.Absolute, 165),
            new ColumnStyle(SizeType.Percent, 100)
        }
    };

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var caption = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        if (control is TextBox or ComboBox)
            control.Width = 430;
        table.Controls.Add(caption, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void NewDocument()
    {
        if (!CanDiscardChanges())
            return;

        _document = RdpDocument.Create();
        _document.SetInt("screen mode id", 2);
        _document.SetString("full address", "");
        _document.SetInt("server port", 3389);
        _document.SetInt("desktopwidth", 1920);
        _document.SetInt("desktopheight", 1080);
        _document.SetBool("redirectclipboard", true);
        _document.SetInt("audiomode", 0);
        _document.SetBool("prompt for credentials", true);
        _document.SetBool("enablecredsspsupport", true);
        _document.SetBool("networkautodetect", true);
        _document.SetBool("bandwidthautodetect", true);
        _document.SetBool("compression", true);
        LoadControls();
        SetDirty(false);
    }

    private void OpenDocument()
    {
        if (!CanDiscardChanges())
            return;

        using var dialog = new OpenFileDialog
        {
            Filter = "Файлы RDP (*.rdp)|*.rdp|Все файлы (*.*)|*.*",
            Title = "Открыть RDP-профиль"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            _document = RdpDocument.Load(dialog.FileName);
            LoadControls();
            SetDirty(false);
        }
        catch (Exception ex)
        {
            ShowError("Не удалось открыть файл.", ex);
        }
    }

    private bool SaveDocument(bool saveAs)
    {
        if (!ValidateInput())
            return false;

        SyncCurrentTab();
        var path = _document.FilePath;
        if (saveAs || string.IsNullOrWhiteSpace(path))
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Файлы RDP (*.rdp)|*.rdp",
                DefaultExt = "rdp",
                AddExtension = true,
                FileName = string.IsNullOrWhiteSpace(_host.Text) ? "connection.rdp" : SafeFileName(_host.Text) + ".rdp",
                Title = "Сохранить RDP-профиль"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return false;
            path = dialog.FileName;
        }

        try
        {
            _document.Save(path);
            SetDirty(false);
            return true;
        }
        catch (Exception ex)
        {
            ShowError("Не удалось сохранить файл.", ex);
            return false;
        }
    }

    private void Connect()
    {
        if (!ValidateInput())
            return;
        if (_dirty || string.IsNullOrWhiteSpace(_document.FilePath))
        {
            if (!SaveDocument(false))
                return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "mstsc.exe",
                Arguments = $"\"{_document.FilePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError("Не удалось запустить клиент удалённого рабочего стола.", ex);
        }
    }

    private void OpenContainingFolder()
    {
        if (string.IsNullOrWhiteSpace(_document.FilePath))
        {
            MessageBox.Show("Сначала сохраните профиль.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{_document.FilePath}\"",
            UseShellExecute = true
        });
    }

    private void LoadControls()
    {
        _loading = true;
        _host.Text = _document.GetString("full address");
        _port.Value = Math.Clamp(_document.GetInt("server port", 3389), 1, 65535);
        _userName.Text = _document.GetString("username");
        _width.Value = Math.Clamp(_document.GetInt("desktopwidth", 1920), 200, 16384);
        _height.Value = Math.Clamp(_document.GetInt("desktopheight", 1080), 200, 16384);
        _fullScreen.Checked = _document.GetInt("screen mode id", 2) == 2;
        _multiMonitor.Checked = _document.GetBool("use multimon");
        _adminSession.Checked = _document.GetBool("administrative session");
        _clipboard.Checked = _document.GetBool("redirectclipboard", true);
        _drives.Checked = _document.GetString("drivestoredirect").Contains('*');
        _printers.Checked = _document.GetBool("redirectprinters");
        _smartCards.Checked = _document.GetBool("redirectsmartcards");
        _audio.SelectedIndex = Math.Clamp(_document.GetInt("audiomode"), 0, 2);
        _gateway.Text = _document.GetString("gatewayhostname");
        _useGateway.Checked = _document.GetInt("gatewayusagemethod") == 1 || _gateway.TextLength > 0;
        _gateway.Enabled = _useGateway.Checked;
        _raw.Text = _document.ToRawText();
        _loading = false;
        UpdateTitle();
    }

    private void ApplyControls()
    {
        _document.SetString("full address", _host.Text.Trim());
        _document.SetInt("server port", (int)_port.Value);
        _document.SetString("username", _userName.Text.Trim());
        _document.SetInt("desktopwidth", (int)_width.Value);
        _document.SetInt("desktopheight", (int)_height.Value);
        _document.SetInt("screen mode id", _fullScreen.Checked ? 2 : 1);
        _document.SetBool("use multimon", _multiMonitor.Checked);
        _document.SetBool("administrative session", _adminSession.Checked);
        _document.SetBool("redirectclipboard", _clipboard.Checked);
        _document.SetString("drivestoredirect", _drives.Checked ? "*" : "");
        _document.SetBool("redirectprinters", _printers.Checked);
        _document.SetBool("redirectsmartcards", _smartCards.Checked);
        _document.SetInt("audiomode", _audio.SelectedIndex);
        _document.SetInt("gatewayusagemethod", _useGateway.Checked ? 1 : 4);
        _document.SetString("gatewayhostname", _useGateway.Checked ? _gateway.Text.Trim() : "");
        _raw.Text = _document.ToRawText();
    }

    private void SyncCurrentTab()
    {
        if (_tabs.SelectedTab == _rawTab)
            _document.ReplaceRawText(_raw.Text);
        else
            ApplyControls();
    }

    private void TabsOnSelecting(object? sender, TabControlCancelEventArgs e)
    {
        if (_loading || e.TabPage == _previousTab)
            return;

        _loading = true;
        if (_previousTab == _rawTab)
        {
            _document.ReplaceRawText(_raw.Text);
            LoadControls();
        }
        else if (e.TabPage == _rawTab)
        {
            ApplyControls();
        }
        _loading = false;
    }

    private bool ValidateInput()
    {
        if (_tabs.SelectedTab == _rawTab)
            _document.ReplaceRawText(_raw.Text);
        else
            ApplyControls();

        if (string.IsNullOrWhiteSpace(_document.GetString("full address")))
        {
            MessageBox.Show("Укажите имя или IP-адрес компьютера.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _tabs.SelectedIndex = 0;
            _host.Focus();
            return false;
        }

        return true;
    }

    private void RegisterChangeHandlers(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.TextChanged += (_, _) => MarkChanged();
                    break;
                case CheckBox checkBox:
                    checkBox.CheckedChanged += (_, _) => MarkChanged();
                    break;
                case NumericUpDown numeric:
                    numeric.ValueChanged += (_, _) => MarkChanged();
                    break;
                case ComboBox combo:
                    combo.SelectedIndexChanged += (_, _) => MarkChanged();
                    break;
            }
            RegisterChangeHandlers(control);
        }
    }

    private void MarkChanged()
    {
        if (!_loading)
            SetDirty(true);
    }

    private bool CanDiscardChanges()
    {
        if (!_dirty)
            return true;

        var result = MessageBox.Show("Сохранить изменения?", Text,
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return result switch
        {
            DialogResult.Yes => SaveDocument(false),
            DialogResult.No => true,
            _ => false
        };
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!CanDiscardChanges())
            e.Cancel = true;
    }

    private void SetDirty(bool value)
    {
        _dirty = value;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var name = string.IsNullOrWhiteSpace(_document.FilePath)
            ? "Новый профиль"
            : Path.GetFileName(_document.FilePath);
        Text = $"{(_dirty ? "* " : "")}{name} — Portable RDP Manager";
    }

    private static string SafeFileName(string value)
    {
        foreach (var character in Path.GetInvalidFileNameChars())
            value = value.Replace(character, '_');
        return value;
    }

    private void ShowError(string message, Exception exception) =>
        MessageBox.Show($"{message}\n\n{exception.Message}", Text,
            MessageBoxButtons.OK, MessageBoxIcon.Error);
}
