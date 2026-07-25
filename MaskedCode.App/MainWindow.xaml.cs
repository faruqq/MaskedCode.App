using MaskedCode.App.Masking;
using MaskedCode.App.Masking.Egl;
using MaskedCode.App.Masking.Pl1;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MaskedCode.App;

public partial class MainWindow : Window
{
    private const long MaximumVaultFileSizeInBytes = 64L * 1024L * 1024L;
    private const int MinimumPasswordLength = 12;

    private static readonly Regex SyntaxTokenRegex = new(
        @"(?<comment>/\*[\s\S]*?\*/|//[^\r\n]*|--[^\r\n]*)" +
        @"|(?<string>'(?:''|\\.|[^'])*'|""(?:""""|\\.|[^""])*"")" +
        @"|(?<number>\b\d+(?:\.\d+)?\b)" +
        @"|(?<keyword>\b(?:DCL|DECLARE|PROCEDURE|END|CALL|IF|THEN|ELSE|" +
        @"DO|WHILE|UNTIL|SELECT|WHEN|OTHERWISE|RETURN|PUT|GET|SKIP|LIST|" +
        @"CHAR|FIXED|DECIMAL|BIN|INIT|STATIC|RECORD|FUNCTION|PROGRAM|" +
        @"PRIVATE|PUBLIC|MAIN|SQL|INTO|FROM|WHERE|UPDATE|INSERT|DELETE|" +
        @"VALUES|SET|FOR|FOREACH|IN|OUT|INOUT|TRY|ONEXCEPTION|THROW|" +
        @"NEW|NULL|TRUE|FALSE|STRING|INT|SMALLINT|BIGINT|NUM|DATE|" +
        @"TIMESTAMP|BOOLEAN|CASE|DEFAULT|OPEN|CLOSE|EXECUTE)\b)",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant);

    private string? _selectedFilePath;
    private string? _selectedMaskedFilePath;
    private string? _selectedVaultFilePath;
    private string? _vaultPasswordFilePath;
    private string? _restorePasswordFilePath;
    private string? _openDrawer;
    private string? _expandedEditor;

    private IMaskingResult? _lastMaskingResult;
    private SourceLanguage? _restoredSourceLanguage;

    private bool _isUpdatingPasswordControls;
    private bool _isVaultPasswordVisible;
    private bool _isRestorePasswordVisible;

    private static readonly Duration HeaderAnimationTransitionDuration =
    new(TimeSpan.FromMilliseconds(160));

    private MediaElement? _activeHeaderAnimationMediaElement;
    private MediaElement? _loadingHeaderAnimationMediaElement;

    private HeaderAnimation _currentHeaderAnimation;
    private HeaderAnimation _loadingHeaderAnimation;

    private bool _currentAnimationReturnsToDefault;
    private bool _loadingAnimationReturnsToDefault;
    private bool _isHeaderAnimationTransitionRunning;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateLineNumbers(SourceCodeTextBox, SourceLineNumbersTextBlock);
        UpdateLineNumbers(MaskedCodeTextBox, MaskedLineNumbersTextBlock);
        UpdateLineNumbers(MaskedInputTextBox, MaskedInputLineNumbersTextBlock);
        UpdateLineNumbers(RestoredCodeTextBox, RestoredLineNumbersTextBlock);
        UpdateSyntaxHighlighting(SourceCodeTextBox.Text, SourceSyntaxRichTextBox);
        UpdateSyntaxHighlighting(MaskedCodeTextBox.Text, MaskedSyntaxRichTextBox);
        UpdateSyntaxHighlighting(MaskedInputTextBox.Text, MaskedInputSyntaxRichTextBox);
        UpdateSyntaxHighlighting(RestoredCodeTextBox.Text, RestoredSyntaxRichTextBox);
        UpdatePasswordSourceState();
        UpdateMaskButton();
        UpdateUnmaskButton();

        SetStatus(
            "Kod yapıştırabilir veya bir kaynak dosya seçebilirsiniz.",
            StatusTone.Neutral,
            isRestore: false);

        SetStatus(
            "Maskelenmiş dosya, kasa ve parola seçerek kodu geri açabilirsiniz.",
            StatusTone.Neutral,
            isRestore: true);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_openDrawer is not null)
        {
            CloseSettingsDrawer();
            e.Handled = true;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.Source != MainTabControl)
        {
            return;
        }

        CloseSettingsDrawer();

        if (MainTabControl.SelectedIndex == 1)
        {
            PlayHeaderAnimation(HeaderAnimation.UnlockRestore);
            return;
        }

        PlayHeaderAnimation(HeaderAnimation.EncryptedScan);
    }

    private void MaskSettingsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSettingsDrawer("Mask");
    }

    private void RestoreSettingsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSettingsDrawer("Restore");
    }

    private void ToggleSettingsDrawer(string drawer)
    {
        if (string.Equals(_openDrawer, drawer, StringComparison.Ordinal))
        {
            CloseSettingsDrawer();
            return;
        }

        OpenSettingsDrawer(drawer);
    }

    private void OpenSettingsDrawer(string drawer)
    {
        CloseSettingsDrawer();
        _openDrawer = drawer;

        if (drawer == "Mask")
        {
            MaskFocusContent.IsEnabled = false;
            ShowDrawer(MaskDrawerLayer, MaskSettingsDrawer);
            MaskDrawerCloseButton.Focus();
            return;
        }

        RestoreFocusContent.IsEnabled = false;
        ShowDrawer(RestoreDrawerLayer, RestoreSettingsDrawer);
        RestoreDrawerCloseButton.Focus();
    }

    private static void ShowDrawer(Grid layer, Border drawer)
    {
        layer.Visibility = Visibility.Visible;

        var transform = new TranslateTransform(450, 0);
        drawer.RenderTransform = transform;

        var animation = new DoubleAnimation
        {
            From = 450,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };

        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void CloseSettingsDrawerButton_Click(object sender, RoutedEventArgs e)
    {
        CloseSettingsDrawer();
    }

    private void DrawerScrim_Click(object sender, RoutedEventArgs e)
    {
        CloseSettingsDrawer();
    }

    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var message = button.Tag?.ToString() switch
        {
            "Privacy" =>
                "Maksimum Gizlilik, özgün değerlerin uzunluğunu ve yapısını " +
                "mümkün olduğunca gizler ve önerilen seçenektir." +
                Environment.NewLine +
                Environment.NewLine +
                "Biçim Korumalı mod, uzunluk ve karakter biçimini koruduğu " +
                "için kaynak hakkında sınırlı biçim bilgisi gösterebilir.",

            _ =>
                "Kasa parolası en az 12 karakter olmalıdır." +
                Environment.NewLine +
                Environment.NewLine +
                "Dosyadan Kullan seçeneğinde, yalnızca parolayı içeren " +
                "güvenli bir metin dosyası seçin. Parola dosyasını, kasa " +
                "dosyasını ve maskelenmiş kodu aynı konumda saklamayın."
        };

        MessageBox.Show(
            this,
            message,
            "MaskedCode bilgisi",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void CloseSettingsDrawer()
    {
        MaskDrawerLayer.Visibility = Visibility.Collapsed;
        RestoreDrawerLayer.Visibility = Visibility.Collapsed;
        MaskFocusContent.IsEnabled = true;
        RestoreFocusContent.IsEnabled = true;
        _openDrawer = null;
    }

    private void ExpandEditorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string editor)
        {
            return;
        }

        if (string.Equals(_expandedEditor, editor, StringComparison.Ordinal))
        {
            RestoreEditorLayout(editor.StartsWith("Mask", StringComparison.Ordinal));
            return;
        }

        RestoreEditorLayout(editor.StartsWith("Mask", StringComparison.Ordinal));
        _expandedEditor = editor;

        switch (editor)
        {
            case "MaskLeft":
                ExpandLeftEditor(
                    MaskLeftColumn,
                    MaskRightColumn,
                    MaskSplitterColumn,
                    MaskRightEditorCard,
                    MaskGridSplitter);
                break;

            case "MaskRight":
                ExpandRightEditor(
                    MaskLeftColumn,
                    MaskRightColumn,
                    MaskSplitterColumn,
                    MaskLeftEditorCard,
                    MaskGridSplitter);
                break;

            case "RestoreLeft":
                ExpandLeftEditor(
                    RestoreLeftColumn,
                    RestoreRightColumn,
                    RestoreSplitterColumn,
                    RestoreRightEditorCard,
                    RestoreGridSplitter);
                break;

            case "RestoreRight":
                ExpandRightEditor(
                    RestoreLeftColumn,
                    RestoreRightColumn,
                    RestoreSplitterColumn,
                    RestoreLeftEditorCard,
                    RestoreGridSplitter);
                break;
        }
    }

    private static void ExpandLeftEditor(ColumnDefinition left, ColumnDefinition right, ColumnDefinition splitter, FrameworkElement rightCard, FrameworkElement gridSplitter)
    {
        right.MinWidth = 0;
        left.Width = new GridLength(1, GridUnitType.Star);
        right.Width = new GridLength(0);
        splitter.Width = new GridLength(0);
        rightCard.Visibility = Visibility.Collapsed;
        gridSplitter.Visibility = Visibility.Collapsed;
    }

    private static void ExpandRightEditor(ColumnDefinition left, ColumnDefinition right, ColumnDefinition splitter, FrameworkElement leftCard, FrameworkElement gridSplitter)
    {
        left.MinWidth = 0;
        left.Width = new GridLength(0);
        right.Width = new GridLength(1, GridUnitType.Star);
        splitter.Width = new GridLength(0);
        leftCard.Visibility = Visibility.Collapsed;
        gridSplitter.Visibility = Visibility.Collapsed;
    }

    private void RestoreEditorLayout(bool isMaskEditor)
    {
        if (isMaskEditor)
        {
            ResetEditorColumns(
                MaskLeftColumn,
                MaskRightColumn,
                MaskSplitterColumn,
                MaskLeftEditorCard,
                MaskRightEditorCard,
                MaskGridSplitter);
        }
        else
        {
            ResetEditorColumns(
                RestoreLeftColumn,
                RestoreRightColumn,
                RestoreSplitterColumn,
                RestoreLeftEditorCard,
                RestoreRightEditorCard,
                RestoreGridSplitter);
        }

        _expandedEditor = null;
    }

    private static void ResetEditorColumns(ColumnDefinition left, ColumnDefinition right, ColumnDefinition splitter, FrameworkElement leftCard, FrameworkElement rightCard, FrameworkElement gridSplitter)
    {
        left.MinWidth = 300;
        right.MinWidth = 300;
        left.Width = new GridLength(1, GridUnitType.Star);
        right.Width = new GridLength(1, GridUnitType.Star);
        splitter.Width = new GridLength(14);
        leftCard.Visibility = Visibility.Visible;
        rightCard.Visibility = Visibility.Visible;
        gridSplitter.Visibility = Visibility.Visible;
    }

    private void EditorGridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        RestoreEditorLayout(ReferenceEquals(sender, MaskGridSplitter));
        e.Handled = true;
    }

    private void EditorGridSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var isMaskSplitter = ReferenceEquals(sender, MaskGridSplitter);
        var left = isMaskSplitter ? MaskLeftColumn : RestoreLeftColumn;
        var right = isMaskSplitter ? MaskRightColumn : RestoreRightColumn;
        var totalWidth = left.ActualWidth + right.ActualWidth;

        if (totalWidth <= 0)
        {
            return;
        }

        var minimumWidth = totalWidth * 0.30;
        var clampedLeftWidth = Math.Clamp(
            left.ActualWidth,
            minimumWidth,
            totalWidth - minimumWidth);

        left.Width = new GridLength(clampedLeftWidth);
        right.Width = new GridLength(totalWidth - clampedLeftWidth);
    }

    private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Maskelenecek kaynak dosyayı seçin",
            Filter = GetOpenFileFilter(),
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var sourceCode = await File.ReadAllTextAsync(dialog.FileName);

            _selectedFilePath = dialog.FileName;
            SourceCodeTextBox.Text = sourceCode;
            SelectedFileTextBlock.Text = Path.GetFileName(dialog.FileName);
            SelectedFileSummaryTextBlock.Text = Path.GetFileName(dialog.FileName);
            SelectedFileSummaryTextBlock.ToolTip = dialog.FileName;
            SelectedFileMetaTextBlock.Text = CreateFileMetaText(dialog.FileName, sourceCode);
            SetValidationIcon(MaskSourceSummaryIcon, isValid: true);
            SetValidationIcon(MaskSelectedFileDrawerIcon, isValid: true);

            SelectLanguageFromFileExtension(dialog.FileName);

            SetStatus(
                $"Dosya yüklendi: {Path.GetFileName(dialog.FileName)}",
                StatusTone.Success,
                isRestore: false);
        }
        catch (Exception exception)
        {
            SetStatus(
                "Dosya okunamadı: " + exception.Message,
                StatusTone.Error,
                isRestore: false);
        }
    }

    private async void MaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SourceCodeTextBox.Text))
        {
            SetStatus(
                "Maskelenecek kaynak kod bulunamadı.",
                StatusTone.Error,
                isRestore: false);
            return;
        }

        if (!HasVaultPasswordSource())
        {
            SetStatus(
                "Kasa parolası en az 12 karakter olmalı veya geçerli bir parola dosyası seçilmelidir.",
                StatusTone.Error,
                isRestore: false);
            return;
        }

        PlayHeaderAnimation(HeaderAnimation.VaultSeal);

        MaskButton.IsEnabled = false;

        SetStatus(
            "Kod güvenli biçimde maskeleniyor...",
            StatusTone.Loading,
            isRestore: false);

        await System.Windows.Threading.Dispatcher.Yield(
            System.Windows.Threading.DispatcherPriority.Render);

        try
        {
            var selectedMode = GetSelectedMaskingMode();

            IMaskingResult result = GetSelectedLanguage() switch
            {
                "PL1" => new Pl1CodeMasker().Mask(
                    SourceCodeTextBox.Text,
                    selectedMode),

                "EGL" => new EglCodeMasker().Mask(
                    SourceCodeTextBox.Text,
                    selectedMode),

                _ => throw new NotSupportedException(
                    "Seçilen kaynak dili için maskeleme henüz desteklenmiyor.")
            };

            _lastMaskingResult = result;
            MaskedCodeTextBox.Text = result.MaskedCode;
            MaskModeSummaryTextBlock.Text =
                GetMaskingModeDisplayName(result.Mode);

            UpdateOutputButtons();
            CloseSettingsDrawer();

            SetStatus(
                "Kod başarıyla maskelendi.",
                StatusTone.Success,
                isRestore: false);
        }
        catch (Exception exception)
        {
            ClearMaskingOutput(clearPassword: false);

            PlayHeaderAnimation(
                HeaderAnimation.EncryptedScan);

            SetStatus(
                "Maskeleme işlemi tamamlanamadı: " +
                exception.Message,
                StatusTone.Error,
                isRestore: false);
        }
        finally
        {
            UpdateMaskButton();
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(MaskedCodeTextBox.Text))
        {
            return;
        }

        Clipboard.SetText(MaskedCodeTextBox.Text);

        SetStatus(
            "Maskelenmiş kod panoya kopyalandı.",
            StatusTone.Success,
            isRestore: false);
    }

    private async void SaveFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(MaskedCodeTextBox.Text))
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Maskelenmiş kaynak dosyayı kaydedin",
            Filter = GetSaveFileFilter(),
            FileName = CreateMaskedFileName()
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, MaskedCodeTextBox.Text);

            SetStatus(
                $"Maskelenmiş dosya kaydedildi: {dialog.FileName}",
                StatusTone.Success,
                isRestore: false);
        }
        catch (Exception exception)
        {
            SetStatus(
                "Dosya kaydedilemedi: " + exception.Message,
                StatusTone.Error,
                isRestore: false);
        }
    }

    private async void SaveVaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastMaskingResult is null ||
            _lastMaskingResult.Mappings.Count == 0)
        {
            SetStatus(
                "Şifrelenecek maskeleme eşlemesi bulunamadı.",
                StatusTone.Error,
                isRestore: false);
            return;
        }

        string password;

        try
        {
            password = await ResolvePasswordAsync(isRestorePassword: false);
        }
        catch (Exception exception)
        {
            SetStatus(
                "Kasa parolası alınamadı: " + exception.Message,
                StatusTone.Error,
                isRestore: false);
            OpenSettingsDrawer("Mask");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Şifreli eşleme kasasını kaydedin",
            Filter = "MaskedCode şifreli kasa dosyası (*.mcvault)|*.mcvault",
            DefaultExt = ".mcvault",
            AddExtension = true,
            FileName = "masked-code.mcvault"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var vault = new EncryptedMappingVault();
            var encryptedVault = vault.Encrypt(_lastMaskingResult, password);

            await File.WriteAllBytesAsync(dialog.FileName, encryptedVault);

            SetStatus(
                "Şifreli eşleme kasası kaydedildi. Kasa dosyasını maskelenmiş koddan ayrı saklayın.",
                StatusTone.Success,
                isRestore: false);
        }
        catch (Exception exception)
        {
            SetStatus(
                "Şifreli eşleme kasası kaydedilemedi: " + exception.Message,
                StatusTone.Error,
                isRestore: false);
        }
    }

    private void SourceCodeTextBox_TextChanged(object sender,TextChangedEventArgs e)
    {
        if (SourceCodeTextBox is null)
        {
            return;
        }

        UpdateLineNumbers(
            SourceCodeTextBox,
            SourceLineNumbersTextBlock);

        UpdateSyntaxHighlighting(
            SourceCodeTextBox.Text,
            SourceSyntaxRichTextBox);

        if (!IsLoaded)
        {
            return;
        }

        ClearMaskingOutput(clearPassword: false);

        if (_selectedFilePath is not null &&
            SourceCodeTextBox.IsKeyboardFocusWithin)
        {
            _selectedFilePath = null;
        }

        UpdateMaskSourceSummary();
        UpdateMaskButton();
    }

    private void UpdateMaskSourceSummary()
    {
        var hasSourceCode =
            !string.IsNullOrWhiteSpace(SourceCodeTextBox.Text);

        SetValidationIcon(
            MaskSourceSummaryIcon,
            hasSourceCode);

        SetValidationIcon(
            MaskSelectedFileDrawerIcon,
            hasSourceCode);

        if (!string.IsNullOrWhiteSpace(_selectedFilePath))
        {
            var fileName = Path.GetFileName(_selectedFilePath);

            SelectedFileTextBlock.Text = fileName;
            SelectedFileSummaryTextBlock.Text = fileName;
            SelectedFileSummaryTextBlock.ToolTip = _selectedFilePath;

            SelectedFileMetaTextBlock.Text =
                CreateFileMetaText(
                    _selectedFilePath,
                    SourceCodeTextBox.Text);

            return;
        }

        SelectedFileSummaryTextBlock.ToolTip = null;

        if (hasSourceCode)
        {
            SelectedFileTextBlock.Text =
                "Editöre yapıştırılan kaynak kod";

            SelectedFileSummaryTextBlock.Text =
                "Yapıştırılan kaynak kod";

            SelectedFileMetaTextBlock.Text =
                CreateTextMetaText(SourceCodeTextBox.Text);

            return;
        }

        SelectedFileTextBlock.Text =
            "Kaynak kod bekleniyor";

        SelectedFileSummaryTextBlock.Text =
            "Kod bekleniyor";

        SelectedFileMetaTextBlock.Text =
            "Kod yapıştırabilir veya dosya seçebilirsiniz";
    }

    private void CodeOutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender == MaskedCodeTextBox)
        {
            UpdateLineNumbers(MaskedCodeTextBox, MaskedLineNumbersTextBlock);
            UpdateSyntaxHighlighting(MaskedCodeTextBox.Text, MaskedSyntaxRichTextBox);
            return;
        }

        if (sender == RestoredCodeTextBox)
        {
            UpdateLineNumbers(RestoredCodeTextBox, RestoredLineNumbersTextBlock);
            UpdateSyntaxHighlighting(RestoredCodeTextBox.Text, RestoredSyntaxRichTextBox);
            UpdateRestoredOutputButtons();
        }
    }

    private void UpdateRestoredOutputButtons()
    {
        if (CopyRestoredButton is null ||
            SaveRestoredFileButton is null ||
            RestoredCodeTextBox is null)
        {
            return;
        }

        var hasRestoredCode =
            !string.IsNullOrWhiteSpace(RestoredCodeTextBox.Text);

        CopyRestoredButton.IsEnabled = hasRestoredCode;
        SaveRestoredFileButton.IsEnabled = hasRestoredCode;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ClearMaskingOutput(clearPassword: false);
        MaskLanguageSummaryTextBlock.Text = GetSelectedLanguageDisplayName();

        SetStatus(
            $"Kaynak dil değiştirildi: {GetSelectedLanguageDisplayName()}",
            StatusTone.Neutral,
            isRestore: false);
    }

    private void SelectLanguageFromFileExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        LanguageComboBox.SelectedIndex = extension.ToLowerInvariant() switch
        {
            ".pli" or ".pl1" => 0,
            ".egl" => 1,
            _ => LanguageComboBox.SelectedIndex
        };
    }

    private string GetOpenFileFilter()
    {
        return GetSelectedLanguage() switch
        {
            "PL1" =>
                "PL/I kaynak dosyaları (*.pli;*.pl1)|*.pli;*.pl1|" +
                "Tüm dosyalar (*.*)|*.*",

            "EGL" =>
                "EGL kaynak dosyaları (*.egl)|*.egl|" +
                "Tüm dosyalar (*.*)|*.*",

            _ => "Tüm dosyalar (*.*)|*.*"
        };
    }

    private string GetSaveFileFilter()
    {
        return GetSelectedLanguage() switch
        {
            "PL1" =>
                "PL/I kaynak dosyaları (*.pli)|*.pli|" +
                "PL/I kaynak dosyaları (*.pl1)|*.pl1",

            "EGL" =>
                "EGL kaynak dosyaları (*.egl)|*.egl",

            _ =>
                "Metin dosyaları (*.txt)|*.txt"
        };
    }

    private string CreateMaskedFileName()
    {
        if (!string.IsNullOrWhiteSpace(_selectedFilePath))
        {
            var fileName = Path.GetFileNameWithoutExtension(_selectedFilePath);
            var extension = Path.GetExtension(_selectedFilePath);

            return $"{fileName}.masked{extension}";
        }

        return GetSelectedLanguage() switch
        {
            "PL1" => "masked-code.pli",
            "EGL" => "masked-code.egl",
            _ => "masked-code.txt"
        };
    }

    private string GetSelectedLanguage()
    {
        return LanguageComboBox.SelectedItem is ComboBoxItem item
            ? item.Tag?.ToString() ?? string.Empty
            : string.Empty;
    }

    private string GetSelectedLanguageDisplayName()
    {
        return LanguageComboBox.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString() ?? string.Empty
            : string.Empty;
    }

    private void ClearMaskingOutput(bool clearPassword)
    {
        _lastMaskingResult = null;
        MaskedCodeTextBox.Clear();

        if (clearPassword)
        {
            ClearManualPassword(isRestorePassword: false);
        }

        UpdateOutputButtons();
    }

    private void UpdateOutputButtons()
    {
        var hasMaskedCode = !string.IsNullOrEmpty(MaskedCodeTextBox.Text);

        CopyButton.IsEnabled = hasMaskedCode;
        SaveFileButton.IsEnabled = hasMaskedCode;
        SaveVaultButton.IsEnabled =
            hasMaskedCode &&
            _lastMaskingResult is
            {
                Mappings.Count: > 0
            };
    }

    private MaskingMode GetSelectedMaskingMode()
    {
        return FormatPreservingRadioButton.IsChecked == true
            ? MaskingMode.FormatPreserving
            : MaskingMode.MaximumPrivacy;
    }

    private static string GetMaskingModeDisplayName(MaskingMode mode)
    {
        return mode switch
        {
            MaskingMode.MaximumPrivacy => "Maksimum Gizlilik",
            MaskingMode.FormatPreserving => "Biçim Korumalı",
            _ => throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Desteklenmeyen maskeleme modu.")
        };
    }

    private void MaskingModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized ||
            MaskedCodeTextBox is null)
        {
            return;
        }

        MaskModeSummaryTextBlock.Text =
            FormatPreservingRadioButton?.IsChecked == true
                ? "Biçim Korumalı"
                : "Maksimum Gizlilik";

        if (string.IsNullOrEmpty(MaskedCodeTextBox.Text))
        {
            return;
        }

        ClearMaskingOutput(clearPassword: false);

        SetStatus(
            "Maskeleme yöntemi değiştirildiği için önceki sonuç temizlendi.",
            StatusTone.Neutral,
            isRestore: false);
    }

    private async void SelectMaskedFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Geri açılacak maskelenmiş dosyayı seçin",
            Filter =
                "Maskelenmiş kod dosyaları (*.pli;*.pl1;*.egl;*.txt)|" +
                "*.pli;*.pl1;*.egl;*.txt|" +
                "Metin dosyaları (*.txt)|*.txt|" +
                "Tüm dosyalar (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var maskedCode = await File.ReadAllTextAsync(dialog.FileName);

            if (string.IsNullOrWhiteSpace(maskedCode))
            {
                SetStatus(
                    "Seçilen dosyada geri açılacak maskelenmiş kod bulunamadı.",
                    StatusTone.Error,
                    isRestore: true);

                return;
            }

            _selectedMaskedFilePath = dialog.FileName;
            MaskedInputTextBox.Text = maskedCode;

            var fileName = Path.GetFileName(dialog.FileName);

            SelectedMaskedFileTextBlock.Text = fileName;
            SelectedMaskedFileTextBlock.ToolTip = dialog.FileName;
            SelectedMaskedFileSummaryTextBlock.Text = fileName;
            SelectedMaskedFileSummaryTextBlock.ToolTip = dialog.FileName;
            SelectedMaskedFileMetaTextBlock.Text =
                CreateFileMetaText(dialog.FileName, maskedCode);

            SelectMaskedFileButton.Content = "Dosyayı Değiştir";

            SetValidationIcon(RestoreMaskedFileSummaryIcon, isValid: true);
            SetValidationIcon(RestoreMaskedFileDrawerIcon, isValid: true);

            ClearUnmaskingOutput();
            UpdateUnmaskButton();

            SetStatus(
                $"Maskelenmiş dosya yüklendi: {fileName}",
                StatusTone.Success,
                isRestore: true);
        }
        catch (DecoderFallbackException)
        {
            SetStatus(
                "Seçilen dosya okunabilir bir metin dosyası değil.",
                StatusTone.Error,
                isRestore: true);
        }
        catch (Exception exception)
        {
            SetStatus(
                "Maskelenmiş dosya okunamadı: " + exception.Message,
                StatusTone.Error,
                isRestore: true);
        }
    }

    private void SelectVaultFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Şifreli eşleme kasası dosyasını seçin",
            Filter =
                "MaskedCode şifreli kasa dosyaları (*.mcvault)|*.mcvault|" +
                "Tüm dosyalar (*.*)|*.*",
            DefaultExt = ".mcvault",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _selectedVaultFilePath = dialog.FileName;

        var fileName = Path.GetFileName(dialog.FileName);

        SelectedVaultFileTextBlock.Text = fileName;
        SelectedVaultFileTextBlock.ToolTip = dialog.FileName;
        SelectedVaultFileSummaryTextBlock.Text = fileName;
        SelectedVaultFileSummaryTextBlock.ToolTip = dialog.FileName;
        SelectedVaultFileMetaTextBlock.Text =
            CreateFileSizeText(dialog.FileName);

        SelectVaultFileButton.Content = "Kasa Dosyasını Değiştir";

        SetValidationIcon(RestoreVaultFileSummaryIcon, isValid: true);
        SetValidationIcon(RestoreVaultFileDrawerIcon, isValid: true);

        ClearUnmaskingOutput();
        UpdateUnmaskButton();

        SetStatus(
            $"Şifreli kasa dosyası seçildi: {fileName}",
            StatusTone.Success,
            isRestore: true);
    }

    private void MaskedInputTextBox_TextChanged(object sender,TextChangedEventArgs e)
    {
        if (MaskedInputTextBox is null)
        {
            return;
        }

        UpdateLineNumbers(
            MaskedInputTextBox,
            MaskedInputLineNumbersTextBlock);

        UpdateSyntaxHighlighting(
            MaskedInputTextBox.Text,
            MaskedInputSyntaxRichTextBox);

        if (!IsLoaded)
        {
            return;
        }

        ClearUnmaskingOutput();

        if (_selectedMaskedFilePath is not null &&
            MaskedInputTextBox.IsKeyboardFocusWithin)
        {
            _selectedMaskedFilePath = null;
        }

        UpdateRestoreSourceSummary();
        UpdateUnmaskButton();
    }

    private void UpdateRestoreSourceSummary()
    {
        var hasMaskedCode =
            !string.IsNullOrWhiteSpace(MaskedInputTextBox.Text);

        SetValidationIcon(
            RestoreMaskedFileSummaryIcon,
            hasMaskedCode);

        SetValidationIcon(
            RestoreMaskedFileDrawerIcon,
            hasMaskedCode);

        if (!string.IsNullOrWhiteSpace(_selectedMaskedFilePath))
        {
            var fileName =
                Path.GetFileName(_selectedMaskedFilePath);

            SelectedMaskedFileTextBlock.Text = fileName;
            SelectedMaskedFileSummaryTextBlock.Text = fileName;
            SelectedMaskedFileSummaryTextBlock.ToolTip =
                _selectedMaskedFilePath;

            SelectedMaskedFileMetaTextBlock.Text =
                CreateFileMetaText(
                    _selectedMaskedFilePath,
                    MaskedInputTextBox.Text);

            return;
        }

        SelectedMaskedFileSummaryTextBlock.ToolTip = null;

        if (hasMaskedCode)
        {
            SelectedMaskedFileTextBlock.Text =
                "Editöre yapıştırılan maskelenmiş kod";

            SelectedMaskedFileSummaryTextBlock.Text =
                "Yapıştırılan maskelenmiş kod";

            SelectedMaskedFileMetaTextBlock.Text =
                CreateTextMetaText(MaskedInputTextBox.Text);

            return;
        }

        SelectedMaskedFileTextBlock.Text =
            "Maskelenmiş kod bekleniyor";

        SelectedMaskedFileSummaryTextBlock.Text =
            "Kod bekleniyor";

        SelectedMaskedFileMetaTextBlock.Text =
            "Kod yapıştırabilir veya dosya seçebilirsiniz";
    }

    private void RestorePasswordInput_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPasswordControls)
        {
            return;
        }

        UpdatePasswordPlaceholder(isRestorePassword: true);
        UpdatePasswordValidation(isRestorePassword: true);

        if (!IsLoaded)
        {
            return;
        }

        ClearUnmaskingOutput();
        UpdateUnmaskButton();
    }

    private async void UnmaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MaskedInputTextBox.Text))
        {
            SetStatus(
                "Geri açılacak maskelenmiş kod bulunamadı.",
                StatusTone.Error,
                isRestore: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedVaultFilePath))
        {
            SetStatus(
                "Şifreli eşleme kasası seçilmedi.",
                StatusTone.Error,
                isRestore: true);
            return;
        }

        string password;

        try
        {
            password = await ResolvePasswordAsync(
                isRestorePassword: true);
        }
        catch (Exception exception)
        {
            SetStatus(
                "Kasa parolası alınamadı: " +
                exception.Message,
                StatusTone.Error,
                isRestore: true);
            return;
        }

        var maskedCode = MaskedInputTextBox.Text;

        UnmaskButton.IsEnabled = false;
        RestoredCodeTextBox.Clear();

        SetStatus(
            "Kasa doğrulanıyor ve kod geri açılıyor...",
            StatusTone.Loading,
            isRestore: true);

        try
        {
            var encryptedVault =
                await ReadVaultFileSafelyAsync(
                    _selectedVaultFilePath);

            var unmaskingResult = await Task.Run(
                () =>
                {
                    var vault =
                        new EncryptedMappingVault();

                    var vaultContent = vault.Decrypt(
                        encryptedVault,
                        password,
                        maskedCode);

                    var restoredCode = UnmaskCode(
                        maskedCode,
                        vaultContent);

                    return (
                        RestoredCode: restoredCode,
                        vaultContent.SourceLanguage);
                });

            RestoredCodeTextBox.Text =
                unmaskingResult.RestoredCode;

            _restoredSourceLanguage =
                unmaskingResult.SourceLanguage;

            CopyRestoredButton.IsEnabled = true;
            SaveRestoredFileButton.IsEnabled = true;

            RestorePasswordSummaryIcon.Text = "✓";
            RestorePasswordSummaryIcon.Foreground =
                FindBrush("SuccessBrush");

            RestorePasswordSummaryTextBlock.Text =
                "Parola doğrulandı";

            RestorePasswordSummaryTextBlock.Foreground =
                FindBrush("TextPrimaryBrush");

            RestorePasswordValidationTextBlock.Text =
                "Parola doğrulandı ve kasa açıldı.";

            RestorePasswordValidationTextBlock.Foreground =
                FindBrush("SuccessBrush");

            CloseSettingsDrawer();

            SetStatus(
                "Kasa doğrulandı ve kod başarıyla geri açıldı.",
                StatusTone.Success,
                isRestore: true);

            PlayHeaderAnimation(
                HeaderAnimation.UnlockRestore,
                returnToDefaultAfterCurrentCycle: true);
        }
        catch (InvalidDataException exception)
        {
            ClearUnmaskingOutput();

            SetStatus(
                "Kod geri açılamadı: " +
                exception.Message,
                StatusTone.Error,
                isRestore: true);
        }
        catch (Exception exception)
        {
            ClearUnmaskingOutput();

            SetStatus(
                "Kod geri açılırken beklenmeyen bir hata oluştu: " +
                exception.Message,
                StatusTone.Error,
                isRestore: true);
        }
        finally
        {
            UpdateUnmaskButton();
        }
    }

    private void CopyRestoredButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(RestoredCodeTextBox.Text))
        {
            return;
        }

        Clipboard.SetText(RestoredCodeTextBox.Text);

        SetStatus(
            "Geri açılmış kod panoya kopyalandı.",
            StatusTone.Success,
            isRestore: true);
    }

    private async void SaveRestoredFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(RestoredCodeTextBox.Text))
        {
            return;
        }

        if (_restoredSourceLanguage is null)
        {
            SetStatus(
                "Geri açılan kodun kaynak dili belirlenemedi.",
                StatusTone.Error,
                isRestore: true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = _restoredSourceLanguage switch
            {
                SourceLanguage.Pl1 => "Geri açılmış PL/I dosyasını kaydedin",
                SourceLanguage.Egl => "Geri açılmış EGL dosyasını kaydedin",
                _ => "Geri açılmış kaynak dosyayı kaydedin"
            },

            Filter = _restoredSourceLanguage switch
            {
                SourceLanguage.Pl1 =>
                    "PL/I kaynak dosyaları (*.pli)|*.pli|" +
                    "PL/I kaynak dosyaları (*.pl1)|*.pl1",

                SourceLanguage.Egl =>
                    "EGL kaynak dosyaları (*.egl)|*.egl",

                _ =>
                    "Metin dosyaları (*.txt)|*.txt"
            },

            FileName = CreateRestoredFileName()
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, RestoredCodeTextBox.Text);

            SetStatus(
                $"Geri açılmış dosya kaydedildi: {dialog.FileName}",
                StatusTone.Success,
                isRestore: true);
        }
        catch (Exception exception)
        {
            SetStatus(
                "Geri açılmış dosya kaydedilemedi: " + exception.Message,
                StatusTone.Error,
                isRestore: true);
        }
    }

    private void VaultPasswordSource_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized ||
            ManualVaultPasswordPanel is null)
        {
            return;
        }

        UpdatePasswordSourceState();
        UpdateMaskButton();
    }

    private void RestorePasswordSource_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized ||
            ManualRestorePasswordPanel is null)
        {
            return;
        }

        UpdatePasswordSourceState();

        if (!IsLoaded)
        {
            return;
        }

        ClearUnmaskingOutput();
        UpdateUnmaskButton();
    }

    private void UpdatePasswordSourceState()
    {
        var useManualVaultPassword = ManualVaultPasswordRadioButton.IsChecked == true;
        ManualVaultPasswordPanel.Visibility =
            useManualVaultPassword
                ? Visibility.Visible
                : Visibility.Collapsed;
        VaultPasswordFilePanel.Visibility =
            useManualVaultPassword
                ? Visibility.Collapsed
                : Visibility.Visible;

        var useManualRestorePassword = ManualRestorePasswordRadioButton.IsChecked == true;
        ManualRestorePasswordPanel.Visibility =
            useManualRestorePassword
                ? Visibility.Visible
                : Visibility.Collapsed;
        RestorePasswordFilePanel.Visibility =
            useManualRestorePassword
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void VaultPasswordInput_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPasswordControls)
        {
            return;
        }

        UpdatePasswordPlaceholder(isRestorePassword: false);
        UpdatePasswordValidation(isRestorePassword: false);
        UpdateMaskButton();
    }

    private void VaultPasswordVisibilityButton_Click(object sender, RoutedEventArgs e)
    {
        SetPasswordVisibility(
            isRestorePassword: false,
            isVisible: !_isVaultPasswordVisible);
    }

    private void RestorePasswordVisibilityButton_Click(object sender, RoutedEventArgs e)
    {
        SetPasswordVisibility(
            isRestorePassword: true,
            isVisible: !_isRestorePasswordVisible);
    }

    private void SetPasswordVisibility(bool isRestorePassword, bool isVisible)
    {
        _isUpdatingPasswordControls = true;

        try
        {
            var passwordBox =
                isRestorePassword
                    ? RestoreVaultPasswordBox
                    : VaultPasswordBox;

            var textBox =
                isRestorePassword
                    ? RestoreVaultPasswordTextBox
                    : VaultPasswordTextBox;

            var button =
                isRestorePassword
                    ? RestorePasswordVisibilityButton
                    : VaultPasswordVisibilityButton;

            if (isVisible)
            {
                textBox.Text = passwordBox.Password;
                passwordBox.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;
                button.Content = "\uE7B3";
                button.ToolTip = "Parolayı gizle";
                textBox.Focus();
                textBox.CaretIndex = textBox.Text.Length;
            }
            else
            {
                passwordBox.Password = textBox.Text;
                textBox.Visibility = Visibility.Collapsed;
                passwordBox.Visibility = Visibility.Visible;
                button.Content = "\uE890";
                button.ToolTip = "Parolayı göster";
                passwordBox.Focus();
            }

            if (isRestorePassword)
            {
                _isRestorePasswordVisible = isVisible;
            }
            else
            {
                _isVaultPasswordVisible = isVisible;
            }
        }
        finally
        {
            _isUpdatingPasswordControls = false;
        }

        UpdatePasswordPlaceholder(isRestorePassword);
    }

    private string GetManualPassword(bool isRestorePassword)
    {
        if (isRestorePassword)
        {
            return _isRestorePasswordVisible
                ? RestoreVaultPasswordTextBox.Text
                : RestoreVaultPasswordBox.Password;
        }

        return _isVaultPasswordVisible
            ? VaultPasswordTextBox.Text
            : VaultPasswordBox.Password;
    }

    private async Task<string> ResolvePasswordAsync(bool isRestorePassword)
    {
        var usePasswordFile =
            isRestorePassword
                ? FileRestorePasswordRadioButton.IsChecked == true
                : FileVaultPasswordRadioButton.IsChecked == true;

        string password;

        if (usePasswordFile)
        {
            var filePath =
                isRestorePassword
                    ? _restorePasswordFilePath
                    : _vaultPasswordFilePath;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException("Parola dosyası seçilmedi.");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Seçilen parola dosyası bulunamadı.",
                    filePath);
            }

            password = (await File.ReadAllTextAsync(filePath)).TrimEnd('\r', '\n');
        }
        else
        {
            password = GetManualPassword(isRestorePassword);
        }

        if (password.Length < MinimumPasswordLength)
        {
            throw new InvalidOperationException(
                $"Kasa parolası en az {MinimumPasswordLength} karakter olmalıdır.");
        }

        return password;
    }

    private async void SelectVaultPasswordFileButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectPasswordFile();

        if (selectedPath is null)
        {
            return;
        }

        _vaultPasswordFilePath = selectedPath;
        VaultPasswordFileTextBlock.Text = Path.GetFileName(selectedPath);
        VaultPasswordFileTextBlock.ToolTip = selectedPath;

        try
        {
            await ResolvePasswordAsync(isRestorePassword: false);
            VaultPasswordFileTextBlock.Foreground = FindBrush("SuccessBrush");
        }
        catch (Exception exception)
        {
            VaultPasswordFileTextBlock.Foreground = FindBrush("ErrorBrush");
            SetStatus(
                "Parola dosyası doğrulanamadı: " + exception.Message,
                StatusTone.Error,
                isRestore: false);
        }

        UpdateMaskButton();
    }

    private async void SelectRestorePasswordFileButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectPasswordFile();

        if (selectedPath is null)
        {
            return;
        }

        _restorePasswordFilePath = selectedPath;
        RestorePasswordFileTextBlock.Text = Path.GetFileName(selectedPath);
        RestorePasswordFileTextBlock.ToolTip = selectedPath;

        try
        {
            await ResolvePasswordAsync(isRestorePassword: true);
            RestorePasswordFileTextBlock.Foreground = FindBrush("SuccessBrush");
        }
        catch (Exception exception)
        {
            RestorePasswordFileTextBlock.Foreground = FindBrush("ErrorBrush");
            SetStatus(
                "Parola dosyası doğrulanamadı: " + exception.Message,
                StatusTone.Error,
                isRestore: true);
        }

        ClearUnmaskingOutput();
        UpdateUnmaskButton();
    }

    private static string? SelectPasswordFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Kasa parola dosyasını seçin",
            Filter = "Metin dosyaları (*.txt)|*.txt|Tüm dosyalar (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    private bool HasVaultPasswordSource()
    {
        if (FileVaultPasswordRadioButton.IsChecked == true)
        {
            return !string.IsNullOrWhiteSpace(_vaultPasswordFilePath);
        }

        return GetManualPassword(isRestorePassword: false).Length >= MinimumPasswordLength;
    }

    private void UpdateMaskButton()
    {
        if (MaskButton is null)
        {
            return;
        }

        MaskButton.IsEnabled =
            !string.IsNullOrWhiteSpace(SourceCodeTextBox.Text) &&
            HasVaultPasswordSource();
    }

    private void UpdatePasswordPlaceholder(bool isRestorePassword)
    {
        var password = GetManualPassword(isRestorePassword);
        var placeholder =
            isRestorePassword
                ? RestorePasswordPlaceholderTextBlock
                : VaultPasswordPlaceholderTextBlock;

        placeholder.Visibility =
            password.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void UpdatePasswordValidation(bool isRestorePassword)
    {
        var password = GetManualPassword(isRestorePassword);
        var validation =
            isRestorePassword
                ? RestorePasswordValidationTextBlock
                : VaultPasswordValidationTextBlock;

        if (password.Length == 0)
        {
            validation.Text =
                isRestorePassword
                    ? "Kasa parolasını girin."
                    : "En az 12 karakterlik güçlü bir parola kullanın.";
            validation.Foreground = FindBrush("TextMutedBrush");
            return;
        }

        if (password.Length < MinimumPasswordLength)
        {
            validation.Text =
                $"Parola için {MinimumPasswordLength - password.Length} karakter daha gerekli.";
            validation.Foreground = FindBrush("WarningBrush");
            return;
        }

        validation.Text = "Kasa parolası biçim olarak hazır.";
        validation.Foreground = FindBrush("SuccessBrush");
    }

    private void ClearManualPassword(bool isRestorePassword)
    {
        _isUpdatingPasswordControls = true;

        try
        {
            if (isRestorePassword)
            {
                RestoreVaultPasswordBox.Clear();
                RestoreVaultPasswordTextBox.Clear();
                _isRestorePasswordVisible = false;
                RestoreVaultPasswordTextBox.Visibility = Visibility.Collapsed;
                RestoreVaultPasswordBox.Visibility = Visibility.Visible;
                RestorePasswordVisibilityButton.Content = "\uE890";
            }
            else
            {
                VaultPasswordBox.Clear();
                VaultPasswordTextBox.Clear();
                _isVaultPasswordVisible = false;
                VaultPasswordTextBox.Visibility = Visibility.Collapsed;
                VaultPasswordBox.Visibility = Visibility.Visible;
                VaultPasswordVisibilityButton.Content = "\uE890";
            }
        }
        finally
        {
            _isUpdatingPasswordControls = false;
        }

        UpdatePasswordPlaceholder(isRestorePassword);
        UpdatePasswordValidation(isRestorePassword);
    }

    private void ClearUnmaskingOutput()
    {
        _restoredSourceLanguage = null;
        RestoredCodeTextBox.Clear();
        CopyRestoredButton.IsEnabled = false;
        SaveRestoredFileButton.IsEnabled = false;
        RestorePasswordSummaryIcon.Text = "○";
        RestorePasswordSummaryIcon.Foreground = FindBrush("TextMutedBrush");
        RestorePasswordSummaryTextBlock.Text = "Parola bekleniyor";
        RestorePasswordSummaryTextBlock.Foreground = FindBrush("TextSecondaryBrush");
    }

    private void UpdateUnmaskButton()
    {
        if (UnmaskButton is null)
        {
            return;
        }

        var hasPassword =
            FileRestorePasswordRadioButton.IsChecked == true
                ? !string.IsNullOrWhiteSpace(_restorePasswordFilePath)
                : GetManualPassword(isRestorePassword: true).Length >= MinimumPasswordLength;

        UnmaskButton.IsEnabled =
            !string.IsNullOrWhiteSpace(MaskedInputTextBox.Text) &&
            !string.IsNullOrWhiteSpace(_selectedVaultFilePath) &&
            hasPassword;
    }

    private static async Task<byte[]> ReadVaultFileSafelyAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                "Seçilen şifreli kasa dosyası bulunamadı.",
                filePath);
        }

        if (fileInfo.Length > MaximumVaultFileSizeInBytes)
        {
            throw new InvalidDataException(
                "Şifreli kasa dosyası izin verilen azami boyutu aşıyor.");
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            options:
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        if (stream.Length > MaximumVaultFileSizeInBytes)
        {
            throw new InvalidDataException(
                "Şifreli kasa dosyası izin verilen azami boyutu aşıyor.");
        }

        var encryptedVault = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(encryptedVault);

        return encryptedVault;
    }

    private string CreateRestoredFileName()
    {
        var sourceLanguage = _restoredSourceLanguage ??
            throw new InvalidOperationException(
                "Geri açılan kodun kaynak dili belirlenmedi.");

        var defaultExtension = sourceLanguage switch
        {
            SourceLanguage.Pl1 => ".pli",
            SourceLanguage.Egl => ".egl",
            _ => throw new ArgumentOutOfRangeException(
                nameof(sourceLanguage),
                sourceLanguage,
                "Desteklenmeyen kaynak dili.")
        };

        if (string.IsNullOrWhiteSpace(_selectedMaskedFilePath))
        {
            return $"restored-code{defaultExtension}";
        }

        var fileName = Path.GetFileNameWithoutExtension(_selectedMaskedFilePath);
        var selectedExtension = Path.GetExtension(_selectedMaskedFilePath);

        if (fileName.EndsWith(".masked", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^".masked".Length];
        }

        var restoredExtension = sourceLanguage switch
        {
            SourceLanguage.Pl1
                when selectedExtension.Equals(".pli", StringComparison.OrdinalIgnoreCase) ||
                     selectedExtension.Equals(".pl1", StringComparison.OrdinalIgnoreCase) =>
                selectedExtension,

            SourceLanguage.Egl
                when selectedExtension.Equals(".egl", StringComparison.OrdinalIgnoreCase) =>
                selectedExtension,

            _ => defaultExtension
        };

        return $"{fileName}.restored{restoredExtension}";
    }

    private static string UnmaskCode(string maskedCode, MappingVaultContent vaultContent)
    {
        return vaultContent.SourceLanguage switch
        {
            SourceLanguage.Pl1 =>
                new Pl1CodeUnmasker().Unmask(maskedCode, vaultContent),

            SourceLanguage.Egl =>
                new EglCodeUnmasker().Unmask(maskedCode, vaultContent),

            _ => throw new InvalidDataException(
                "Kasa içindeki kaynak dili desteklenmiyor.")
        };
    }

    private static void UpdateLineNumbers(TextBox textBox, TextBlock lineNumbers)
    {
        var lineCount = Math.Max(1, textBox.LineCount);
        lineNumbers.Text = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, lineCount));
    }

    private void CodeEditor_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var editorParts = sender switch
        {
            TextBox textBox when ReferenceEquals(textBox, SourceCodeTextBox) =>
                (SourceLineNumbersTextBlock, SourceSyntaxRichTextBox),

            TextBox textBox when ReferenceEquals(textBox, MaskedCodeTextBox) =>
                (MaskedLineNumbersTextBlock, MaskedSyntaxRichTextBox),

            TextBox textBox when ReferenceEquals(textBox, MaskedInputTextBox) =>
                (MaskedInputLineNumbersTextBlock, MaskedInputSyntaxRichTextBox),

            TextBox textBox when ReferenceEquals(textBox, RestoredCodeTextBox) =>
                (RestoredLineNumbersTextBlock, RestoredSyntaxRichTextBox),

            _ => ((TextBlock?)null, (RichTextBox?)null)
        };

        if (editorParts.Item1 is null ||
            editorParts.Item2 is null)
        {
            return;
        }

        editorParts.Item1.RenderTransform = new TranslateTransform(
            0,
            -e.VerticalOffset);

        editorParts.Item2.ScrollToVerticalOffset(e.VerticalOffset);
        editorParts.Item2.ScrollToHorizontalOffset(e.HorizontalOffset);
    }

    private void UpdateSyntaxHighlighting(string code, RichTextBox richTextBox)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 22,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };

        var currentIndex = 0;

        foreach (Match match in SyntaxTokenRegex.Matches(code))
        {
            if (match.Index > currentIndex)
            {
                paragraph.Inlines.Add(
                    CreateSyntaxRun(
                        code[currentIndex..match.Index],
                        "TextPrimaryBrush"));
            }

            var brushKey = match.Groups["comment"].Success
                ? "SuccessBrush"
                : match.Groups["string"].Success
                    ? "WarningBrush"
                    : match.Groups["number"].Success
                        ? "PrimaryHoverBrush"
                        : "SyntaxKeywordBrush";

            paragraph.Inlines.Add(
                CreateSyntaxRun(
                    match.Value,
                    brushKey));

            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < code.Length)
        {
            paragraph.Inlines.Add(
                CreateSyntaxRun(
                    code[currentIndex..],
                    "TextPrimaryBrush"));
        }

        richTextBox.Document = new FlowDocument(paragraph)
        {
            PagePadding = new Thickness(0),
            PageWidth = 100000,
            FontFamily = new FontFamily(
                "Cascadia Code, Cascadia Mono, Consolas"),
            FontSize = 14,
            Foreground = FindBrush("TextPrimaryBrush")
        };
    }

    private Run CreateSyntaxRun(string text, string brushKey)
    {
        return new Run(text)
        {
            Foreground = FindBrush(brushKey)
        };
    }

    private static string CreateFileMetaText(string filePath, string content)
    {
        return $"{CreateFileSizeText(filePath)} • {CountLines(content)} satır";
    }

    private static string CreateTextMetaText(string content)
    {
        return $"{CountLines(content)} satır • editörden girildi";
    }

    private static string CreateFileSizeText(string filePath)
    {
        var length = new FileInfo(filePath).Length;

        return length < 1024
            ? $"{length} B"
            : $"{length / 1024d:0.##} KB";
    }

    private static int CountLines(string content)
    {
        return string.IsNullOrEmpty(content)
            ? 0
            : content.Count(character => character == '\n') + 1;
    }

    private void DismissStatusButton_Click(object sender, RoutedEventArgs e)
    {
        var isRestore = MainTabControl.SelectedIndex == 1;
        var border = isRestore ? RestoreStatusBorder : MaskStatusBorder;
        border.Visibility = Visibility.Collapsed;
    }

    private void SetStatus(string message, StatusTone tone, bool isRestore)
    {
        StatusTextBlock.Text = message;

        var statusText = isRestore
            ? RestoreStatusTextBlock
            : MaskStatusTextBlock;

        var statusBorder = isRestore
            ? RestoreStatusBorder
            : MaskStatusBorder;

        var statusIconBorder = isRestore
            ? RestoreStatusIconBorder
            : MaskStatusIconBorder;

        var statusIconText = isRestore
            ? RestoreStatusIconText
            : MaskStatusIconText;

        statusText.Text = message;
        statusBorder.Visibility = Visibility.Visible;

        switch (tone)
        {
            case StatusTone.Success:
                statusBorder.Background = FindBrush("SuccessSurfaceBrush");
                statusBorder.BorderBrush = new SolidColorBrush(
                    Color.FromRgb(32, 80, 68));
                statusIconBorder.BorderBrush = FindBrush("SuccessBrush");
                statusIconText.Foreground = FindBrush("SuccessBrush");
                statusIconText.Text = "✓";
                break;

            case StatusTone.Error:
                statusBorder.Background = FindBrush("ErrorSurfaceBrush");
                statusBorder.BorderBrush = FindBrush("ErrorBrush");
                statusIconBorder.BorderBrush = FindBrush("ErrorBrush");
                statusIconText.Foreground = FindBrush("ErrorBrush");
                statusIconText.Text = "!";
                break;

            case StatusTone.Loading:
                statusBorder.Background = FindBrush("PrimarySoftBrush");
                statusBorder.BorderBrush = FindBrush("PrimaryBrush");
                statusIconBorder.BorderBrush = FindBrush("PrimaryBrush");
                statusIconText.Foreground = FindBrush("PrimaryHoverBrush");
                statusIconText.Text = "…";
                break;

            default:
                statusBorder.Background = FindBrush("SurfaceElevatedBrush");
                statusBorder.BorderBrush = FindBrush("BorderStrongBrush");
                statusIconBorder.BorderBrush = FindBrush("TextMutedBrush");
                statusIconText.Foreground = FindBrush("TextSecondaryBrush");
                statusIconText.Text = "i";
                break;
        }
    }

    private void SetValidationIcon(TextBlock icon, bool isValid)
    {
        icon.Text = isValid ? "✓" : "○";
        icon.Foreground = FindBrush(isValid ? "SuccessBrush" : "TextMutedBrush");
    }

    private Brush FindBrush(string resourceKey)
    {
        return (Brush)FindResource(resourceKey);
    }

    private enum StatusTone
    {
        Neutral,
        Success,
        Error,
        Loading
    }

    private enum HeaderAnimation
    {
        EncryptedScan,
        VaultSeal,
        UnlockRestore
    }

    private void HeaderAnimationMediaElement_Loaded(object sender, RoutedEventArgs e)
    {
        PlayHeaderAnimation(HeaderAnimation.EncryptedScan);
    }

    private void HeaderAnimationMediaElement_MediaEnded(
    object sender,
    RoutedEventArgs e)
    {
        if (sender is not MediaElement endedMediaElement ||
            endedMediaElement != _activeHeaderAnimationMediaElement)
        {
            return;
        }

        if (_currentAnimationReturnsToDefault)
        {
            _currentAnimationReturnsToDefault = false;

            PlayHeaderAnimation(
                HeaderAnimation.EncryptedScan);

            return;
        }

        endedMediaElement.Position = TimeSpan.Zero;
        endedMediaElement.Play();
    }

    private void HeaderAnimationMediaElement_MediaFailed(
        object sender,
        ExceptionRoutedEventArgs e)
    {
        if (sender is MediaElement failedMediaElement)
        {
            failedMediaElement.Stop();
            failedMediaElement.Close();
            failedMediaElement.Opacity = 0;
        }

        _loadingHeaderAnimationMediaElement = null;
        _isHeaderAnimationTransitionRunning = false;

        SetStatus(
            "Başlık animasyonu açılamadı: " +
            e.ErrorException.Message,
            StatusTone.Error,
            isRestore: MainTabControl.SelectedIndex == 1);
    }

    private void PlayHeaderAnimation(
    HeaderAnimation animation,
    bool returnToDefaultAfterCurrentCycle = false)
    {
        var animationFileName = animation switch
        {
            HeaderAnimation.EncryptedScan => "encrypted-scan.mp4",
            HeaderAnimation.VaultSeal => "vault-seal.mp4",
            HeaderAnimation.UnlockRestore => "unlock-restore.mp4",

            _ => throw new ArgumentOutOfRangeException(
                nameof(animation),
                animation,
                "Desteklenmeyen başlık animasyonu.")
        };

        var absoluteAnimationPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Animations",
            animationFileName);

        if (!File.Exists(absoluteAnimationPath))
        {
            SetStatus(
                $"Animasyon dosyası bulunamadı: {animationFileName}",
                StatusTone.Error,
                isRestore: MainTabControl.SelectedIndex == 1);

            return;
        }

        if (_isHeaderAnimationTransitionRunning)
        {
            return;
        }

        var targetMediaElement =
            _activeHeaderAnimationMediaElement ==
            HeaderAnimationPrimaryMediaElement
                ? HeaderAnimationSecondaryMediaElement
                : HeaderAnimationPrimaryMediaElement;

        // Uygulama açılışındaki ilk yüklemede birincil katman kullanılır.
        if (_activeHeaderAnimationMediaElement is null)
        {
            targetMediaElement =
                HeaderAnimationPrimaryMediaElement;
        }

        _loadingHeaderAnimationMediaElement = targetMediaElement;
        _loadingHeaderAnimation = animation;
        _loadingAnimationReturnsToDefault =
            returnToDefaultAfterCurrentCycle;

        targetMediaElement.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        targetMediaElement.Opacity = 0;
        targetMediaElement.Stop();
        targetMediaElement.Close();

        targetMediaElement.Source = new Uri(
            absoluteAnimationPath,
            UriKind.Absolute);

        targetMediaElement.Position = TimeSpan.Zero;

        // Manual modda Play çağrısı dosyanın açılmasını ve ilk karenin
        // hazırlanmasını başlatır. Opacity sıfır olduğu için görünmez.
        targetMediaElement.Play();
    }

    private void HeaderAnimationMediaElement_MediaOpened(object sender,RoutedEventArgs e)
    {
        if (sender is not MediaElement openedMediaElement ||
            openedMediaElement != _loadingHeaderAnimationMediaElement)
        {
            return;
        }

        // İlk açılışta geçiş yapılacak eski bir video bulunmaz.
        if (_activeHeaderAnimationMediaElement is null)
        {
            openedMediaElement.Opacity = 1;

            _activeHeaderAnimationMediaElement =
                openedMediaElement;

            _currentHeaderAnimation =
                _loadingHeaderAnimation;

            _currentAnimationReturnsToDefault =
                _loadingAnimationReturnsToDefault;

            _loadingHeaderAnimationMediaElement = null;
            return;
        }

        StartHeaderAnimationCrossfade(
            _activeHeaderAnimationMediaElement,
            openedMediaElement);
    }

    private void StartHeaderAnimationCrossfade(MediaElement outgoingMediaElement,MediaElement incomingMediaElement)
    {
        _isHeaderAnimationTransitionRunning = true;

        var fadeOutAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = HeaderAnimationTransitionDuration,
            EasingFunction = new SineEase
            {
                EasingMode = EasingMode.EaseInOut
            }
        };

        var fadeInAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = HeaderAnimationTransitionDuration,
            EasingFunction = new SineEase
            {
                EasingMode = EasingMode.EaseInOut
            }
        };

        fadeInAnimation.Completed += (_, _) =>
        {
            CompleteHeaderAnimationTransition(
                outgoingMediaElement,
                incomingMediaElement);
        };

        outgoingMediaElement.BeginAnimation(
            UIElement.OpacityProperty,
            fadeOutAnimation);

        incomingMediaElement.BeginAnimation(
            UIElement.OpacityProperty,
            fadeInAnimation);
    }

    private void CompleteHeaderAnimationTransition(MediaElement outgoingMediaElement,MediaElement incomingMediaElement)
    {
        outgoingMediaElement.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        incomingMediaElement.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        outgoingMediaElement.Opacity = 0;
        incomingMediaElement.Opacity = 1;

        // Eski video ancak yeni video görünür olduktan sonra kapatılır.
        outgoingMediaElement.Stop();
        outgoingMediaElement.Close();
        outgoingMediaElement.Source = null;

        _activeHeaderAnimationMediaElement =
            incomingMediaElement;

        _currentHeaderAnimation =
            _loadingHeaderAnimation;

        _currentAnimationReturnsToDefault =
            _loadingAnimationReturnsToDefault;

        _loadingHeaderAnimationMediaElement = null;
        _isHeaderAnimationTransitionRunning = false;
    }
}