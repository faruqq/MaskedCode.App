using MaskedCode.App.Animations;
using MaskedCode.App.Masking;
using MaskedCode.App.Masking.CSharp;
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
using System.Windows.Media.Imaging;

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
    private bool _isMaskEditorScrollLinked = true;
    private bool _isRestoreEditorScrollLinked =true;
    private bool _isSynchronizingEditorScroll;

    private static readonly Duration HeaderAnimationTransitionDuration =
    new(TimeSpan.FromMilliseconds(160));

    private readonly IHeaderAnimationProfile _headerAnimationProfile;
    private readonly Queue<HeaderAnimationStep> _headerAnimationSteps = new();

    private MediaElement? _activeHeaderAnimationMediaElement;
    private MediaElement? _loadingHeaderAnimationMediaElement;
    private FrameworkElement? _activeHeaderVisual;

    private HeaderAnimationStep? _currentHeaderAnimationStep;
    private HeaderAnimationStep? _loadingHeaderAnimationStep;
    private HeaderVisualState _currentHeaderVisualState;
    private HeaderVisualState _planFinalHeaderVisualState;

    private readonly Queue<HeaderAnimationEvent> _queuedHeaderAnimationEvents = new();

    private bool _isHeaderAnimationPlanRunning;
    private bool _isHeaderAnimationTransitionRunning;

    private readonly PngFrameSequencePlayer _pngFrameSequencePlayer = new();
    private CancellationTokenSource? _headerFrameSequenceCancellationTokenSource;

    public MainWindow()
    {
        _headerAnimationProfile = HeaderAnimationProfileSelector.Select();
        _currentHeaderVisualState = _headerAnimationProfile.InitialState;
        _planFinalHeaderVisualState = _headerAnimationProfile.InitialState;

        InitializeComponent();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateLineNumbers(
    SourceCodeTextBox,
    SourceLineNumbersItemsControl);

        UpdateLineNumbers(
            MaskedCodeTextBox,
            MaskedLineNumbersItemsControl);

        UpdateLineNumbers(
            MaskedInputTextBox,
            MaskedInputLineNumbersItemsControl);

        UpdateLineNumbers(
            RestoredCodeTextBox,
            RestoredLineNumbersItemsControl);
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
        if (!IsLoaded ||
            e.Source != MainTabControl)
        {
            return;
        }

        CloseSettingsDrawer();

        PlayHeaderAnimation(GetActiveTabHeaderAnimationEvent());
    }

    private HeaderAnimationEvent GetActiveTabHeaderAnimationEvent()
    {
        return MainTabControl.SelectedIndex == 1
            ? HeaderAnimationEvent.CodeRestoreTabActivated
            : HeaderAnimationEvent.CodeMaskingTabActivated;
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

    private void MaskButton_Click(object sender, RoutedEventArgs e)
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

        MaskButton.IsEnabled = false;

        PlayHeaderAnimation(
            HeaderAnimationEvent.MaskingStarted);

        SetStatus(
            "Kod güvenli biçimde maskeleniyor...",
            StatusTone.Loading,
            isRestore: false);

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

                "CSHARP" => new CSharpCodeMasker().Mask(
                    SourceCodeTextBox.Text,
                    selectedMode),

                _ => throw new NotSupportedException(
                    "Seçilen kaynak dili için maskeleme henüz desteklenmiyor.")
            };

            _lastMaskingResult = result;
            MaskedCodeTextBox.Text = result.MaskedCode;

            MaskModeSummaryTextBlock.Text =
                GetMaskingModeDisplayName(
                    result.Mode);

            UpdateOutputButtons();
            CloseSettingsDrawer();

            SetStatus(
                "Kod başarıyla maskelendi.",
                StatusTone.Success,
                isRestore: false);
        }
        catch (Exception exception)
        {
            ClearMaskingOutput(
                clearPassword: false);

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
        var extension =
            Path.GetExtension(
                filePath);

        LanguageComboBox.SelectedIndex =
            extension.ToLowerInvariant() switch
            {
                ".pli" or ".pl1" => 0,
                ".egl" => 1,
                ".cs" => 2,
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

            "CSHARP" =>
                "C# kaynak dosyaları (*.cs)|*.cs|" +
                "Tüm dosyalar (*.*)|*.*",

            _ =>
                "Tüm dosyalar (*.*)|*.*"
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

            "CSHARP" =>
                "C# kaynak dosyaları (*.cs)|*.cs",

            _ =>
                "Metin dosyaları (*.txt)|*.txt"
        };
    }

    private string CreateMaskedFileName()
    {
        if (!string.IsNullOrWhiteSpace(
                _selectedFilePath))
        {
            var fileName =
                Path.GetFileNameWithoutExtension(
                    _selectedFilePath);

            var extension =
                Path.GetExtension(
                    _selectedFilePath);

            return $"{fileName}.masked{extension}";
        }

        return GetSelectedLanguage() switch
        {
            "PL1" => "masked-code.pli",
            "EGL" => "masked-code.egl",
            "CSHARP" => "masked-code.cs",
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
                "Maskelenmiş kod dosyaları (*.pli;*.pl1;*.egl;*.cs;*.txt)|" +
                "*.pli;*.pl1;*.egl;*.cs;*.txt|" +
                "C# kaynak dosyaları (*.cs)|*.cs|" +
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

    private async void SaveRestoredFileButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(
                RestoredCodeTextBox.Text))
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

        var dialog =
            new SaveFileDialog
            {
                Title =
                    _restoredSourceLanguage switch
                    {
                        SourceLanguage.Pl1 =>
                            "Geri açılmış PL/I dosyasını kaydedin",

                        SourceLanguage.Egl =>
                            "Geri açılmış EGL dosyasını kaydedin",

                        SourceLanguage.CSharp =>
                            "Geri açılmış C# dosyasını kaydedin",

                        _ =>
                            "Geri açılmış kaynak dosyayı kaydedin"
                    },

                Filter =
                    _restoredSourceLanguage switch
                    {
                        SourceLanguage.Pl1 =>
                            "PL/I kaynak dosyaları (*.pli)|*.pli|" +
                            "PL/I kaynak dosyaları (*.pl1)|*.pl1",

                        SourceLanguage.Egl =>
                            "EGL kaynak dosyaları (*.egl)|*.egl",

                        SourceLanguage.CSharp =>
                            "C# kaynak dosyaları (*.cs)|*.cs",

                        _ =>
                            "Metin dosyaları (*.txt)|*.txt"
                    },

                FileName =
                    CreateRestoredFileName()
            };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(
                dialog.FileName,
                RestoredCodeTextBox.Text);

            SetStatus(
                $"Geri açılmış dosya kaydedildi: {dialog.FileName}",
                StatusTone.Success,
                isRestore: true);
        }
        catch (Exception exception)
        {
            SetStatus(
                "Geri açılmış dosya kaydedilemedi: " +
                exception.Message,
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
        var sourceLanguage =
            _restoredSourceLanguage ??
            throw new InvalidOperationException(
                "Geri açılan kodun kaynak dili belirlenmedi.");

        var defaultExtension =
            sourceLanguage switch
            {
                SourceLanguage.Pl1 => ".pli",
                SourceLanguage.Egl => ".egl",
                SourceLanguage.CSharp => ".cs",

                _ => throw new ArgumentOutOfRangeException(
                    nameof(sourceLanguage),
                    sourceLanguage,
                    "Desteklenmeyen kaynak dili.")
            };

        if (string.IsNullOrWhiteSpace(
                _selectedMaskedFilePath))
        {
            return $"restored-code{defaultExtension}";
        }

        var fileName =
            Path.GetFileNameWithoutExtension(
                _selectedMaskedFilePath);

        var selectedExtension =
            Path.GetExtension(
                _selectedMaskedFilePath);

        if (fileName.EndsWith(
                ".masked",
                StringComparison.OrdinalIgnoreCase))
        {
            fileName =
                fileName[..^".masked".Length];
        }

        var restoredExtension =
            sourceLanguage switch
            {
                SourceLanguage.Pl1
                    when selectedExtension.Equals(
                             ".pli",
                             StringComparison.OrdinalIgnoreCase) ||
                         selectedExtension.Equals(
                             ".pl1",
                             StringComparison.OrdinalIgnoreCase) =>
                    selectedExtension,

                SourceLanguage.Egl
                    when selectedExtension.Equals(
                        ".egl",
                        StringComparison.OrdinalIgnoreCase) =>
                    selectedExtension,

                SourceLanguage.CSharp
                    when selectedExtension.Equals(
                        ".cs",
                        StringComparison.OrdinalIgnoreCase) =>
                    selectedExtension,

                _ =>
                    defaultExtension
            };

        return $"{fileName}.restored{restoredExtension}";
    }

    private static string UnmaskCode(string maskedCode, MappingVaultContent vaultContent)
    {
        return vaultContent.SourceLanguage switch
        {
            SourceLanguage.Pl1 =>
                new Pl1CodeUnmasker().Unmask(
                    maskedCode,
                    vaultContent),

            SourceLanguage.Egl =>
                new EglCodeUnmasker().Unmask(
                    maskedCode,
                    vaultContent),

            SourceLanguage.CSharp =>
                new CSharpCodeUnmasker().Unmask(
                    maskedCode,
                    vaultContent),

            _ => throw new InvalidDataException(
                "Kasa içindeki kaynak dili desteklenmiyor.")
        };
    }

    private static void UpdateLineNumbers(TextBox textBox,ItemsControl lineNumbers)
    {
        var lineCount =
            Math.Max(
                1,
                textBox.LineCount);

        lineNumbers.ItemsSource =
            Enumerable.Range(
                    1,
                    lineCount)
                .ToArray();
    }

    private void CodeEditor_ScrollChanged(
    object sender,
    ScrollChangedEventArgs e)
    {
        if (sender is not TextBox sourceTextBox)
        {
            return;
        }

        var editorParts =
            GetEditorScrollParts(
                sourceTextBox);

        if (editorParts.LineNumbers is null ||
            editorParts.SyntaxEditor is null)
        {
            return;
        }

        editorParts.LineNumbers.RenderTransform =
            new TranslateTransform(
                0,
                -e.VerticalOffset);

        editorParts.SyntaxEditor.ScrollToVerticalOffset(
            e.VerticalOffset);

        editorParts.SyntaxEditor.ScrollToHorizontalOffset(
            e.HorizontalOffset);

        SynchronizeLinkedEditorScroll(
            sourceTextBox,
            e);
    }

    private void SynchronizeLinkedEditorScroll(
        TextBox sourceTextBox,
        ScrollChangedEventArgs e)
    {
        if (_isSynchronizingEditorScroll ||
            Math.Abs(e.VerticalChange) <
                double.Epsilon)
        {
            return;
        }

        var targetTextBox =
            GetLinkedEditor(
                sourceTextBox);

        if (targetTextBox is null)
        {
            return;
        }

        try
        {
            _isSynchronizingEditorScroll =
                true;

            targetTextBox.ScrollToVerticalOffset(
                e.VerticalOffset);
        }
        finally
        {
            _isSynchronizingEditorScroll =
                false;
        }
    }

    private static void SelectEditorLineText(
     TextBox textBox,
     int lineIndex,
     bool focusEditor)
    {
        if (lineIndex < 0 ||
            lineIndex >= textBox.LineCount)
        {
            return;
        }

        var lineStart =
            textBox.GetCharacterIndexFromLineIndex(
                lineIndex);

        if (lineStart < 0)
        {
            return;
        }

        var lineTextLength =
            textBox.GetLineLength(
                lineIndex);

        /*
         * GetLineLength bazı satırlarda CR ve LF karakterlerini
         * uzunluğa dahil edebilir. Kullanıcının fareyle yalnızca
         * satır metnini seçtiği davranışı korumak için bunlar çıkarılır.
         */
        while (lineTextLength > 0)
        {
            var lastCharacterIndex =
                lineStart +
                lineTextLength -
                1;

            if (lastCharacterIndex >= textBox.Text.Length)
            {
                lineTextLength--;
                continue;
            }

            var lastCharacter =
                textBox.Text[lastCharacterIndex];

            if (lastCharacter != '\r' &&
                lastCharacter != '\n')
            {
                break;
            }

            lineTextLength--;
        }

        var verticalOffset =
            textBox.VerticalOffset;

        var horizontalOffset =
            textBox.HorizontalOffset;

        textBox.Select(
            lineStart,
            lineTextLength);

        if (focusEditor)
        {
            textBox.Focus();
        }

        textBox.ScrollToVerticalOffset(
            verticalOffset);

        textBox.ScrollToHorizontalOffset(
            horizontalOffset);
    }

    private TextBox? GetLinkedEditor(
        TextBox sourceTextBox)
    {
        if (_isMaskEditorScrollLinked)
        {
            if (ReferenceEquals(
                    sourceTextBox,
                    SourceCodeTextBox))
            {
                return MaskedCodeTextBox;
            }

            if (ReferenceEquals(
                    sourceTextBox,
                    MaskedCodeTextBox))
            {
                return SourceCodeTextBox;
            }
        }

        if (_isRestoreEditorScrollLinked)
        {
            if (ReferenceEquals(
                    sourceTextBox,
                    MaskedInputTextBox))
            {
                return RestoredCodeTextBox;
            }

            if (ReferenceEquals(
                    sourceTextBox,
                    RestoredCodeTextBox))
            {
                return MaskedInputTextBox;
            }
        }

        return null;
    }

    private (ItemsControl? LineNumbers,RichTextBox? SyntaxEditor)GetEditorScrollParts(TextBox textBox)
    {
        if (ReferenceEquals(
                textBox,
                SourceCodeTextBox))
        {
            return (
                SourceLineNumbersItemsControl,
                SourceSyntaxRichTextBox);
        }

        if (ReferenceEquals(
                textBox,
                MaskedCodeTextBox))
        {
            return (
                MaskedLineNumbersItemsControl,
                MaskedSyntaxRichTextBox);
        }

        if (ReferenceEquals(
                textBox,
                MaskedInputTextBox))
        {
            return (
                MaskedInputLineNumbersItemsControl,
                MaskedInputSyntaxRichTextBox);
        }

        if (ReferenceEquals(
                textBox,
                RestoredCodeTextBox))
        {
            return (
                RestoredLineNumbersItemsControl,
                RestoredSyntaxRichTextBox);
        }

        return (
            null,
            null);
    }

    /*
 * Neden var?
 * Tıklanan satır numarasını doğrudan ilgili öğenin
 * DataContext değerinden alır.
 *
 * Ne çözüyor?
 * Font, DPI, padding, boş satır ve scroll koordinatlarından
 * kaynaklanan kademeli satır kaymasını tamamen ortadan kaldırır.
 *
 * Hangi örneği destekliyor?
 * 100 numaralı öğeye tıklandığında hesaplama yapmadan
 * doğrudan 100. kod satırını seçer.
 *
 * Nerede kullanılır?
 * Dört kod editörünün satır numarası öğelerinde kullanılır.
 *
 * Gelecekte neye temel olur?
 * Satır bazlı karşılaştırma ve eşlenik seçim davranışlarına
 * güvenilir bir satır kimliği sağlar.
 */
    private void LineNumber_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not TextBlock lineNumberTextBlock ||
            lineNumberTextBlock.DataContext is not int lineNumber ||
            e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var lineNumbersItemsControl =
            FindVisualParent<ItemsControl>(
                lineNumberTextBlock);

        if (lineNumbersItemsControl is null)
        {
            return;
        }

        var sourceTextBox =
            GetEditorFromLineNumbers(
                lineNumbersItemsControl);

        if (sourceTextBox is null)
        {
            return;
        }

        var lineIndex =
            lineNumber - 1;

        if (lineIndex < 0 ||
            lineIndex >= sourceTextBox.LineCount)
        {
            return;
        }

        var linkedTextBox =
            GetLinkedEditor(
                sourceTextBox);

        if (linkedTextBox is not null &&
            lineIndex < linkedTextBox.LineCount)
        {
            SelectEditorLineText(
                linkedTextBox,
                lineIndex,
                focusEditor: false);
        }

        SelectEditorLineText(
            sourceTextBox,
            lineIndex,
            focusEditor: true);

        e.Handled = true;
    }

    private TextBox? GetEditorFromLineNumbers(
        ItemsControl lineNumbers)
    {
        if (ReferenceEquals(
                lineNumbers,
                SourceLineNumbersItemsControl))
        {
            return SourceCodeTextBox;
        }

        if (ReferenceEquals(
                lineNumbers,
                MaskedLineNumbersItemsControl))
        {
            return MaskedCodeTextBox;
        }

        if (ReferenceEquals(
                lineNumbers,
                MaskedInputLineNumbersItemsControl))
        {
            return MaskedInputTextBox;
        }

        if (ReferenceEquals(
                lineNumbers,
                RestoredLineNumbersItemsControl))
        {
            return RestoredCodeTextBox;
        }

        return null;
    }

    private static TParent? FindVisualParent<TParent>(
        DependencyObject child)
        where TParent : DependencyObject
    {
        var current =
            VisualTreeHelper.GetParent(
                child);

        while (current is not null)
        {
            if (current is TParent parent)
            {
                return parent;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        return null;
    }

    private void EditorScrollLinkToggleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ReferenceEquals(
                sender,
                MaskScrollLinkToggleButton))
        {
            _isMaskEditorScrollLinked =
                MaskScrollLinkToggleButton.IsChecked ==
                true;

            MaskScrollLinkToggleButton.ToolTip =
                _isMaskEditorScrollLinked
                    ? "Dikey kaydırma bağlantısı açık"
                    : "Dikey kaydırma bağlantısı kapalı";

            return;
        }

        if (!ReferenceEquals(
                sender,
                RestoreScrollLinkToggleButton))
        {
            return;
        }

        _isRestoreEditorScrollLinked =
            RestoreScrollLinkToggleButton.IsChecked ==
            true;

        RestoreScrollLinkToggleButton.ToolTip =
            _isRestoreEditorScrollLinked
                ? "Dikey kaydırma bağlantısı açık"
                : "Dikey kaydırma bağlantısı kapalı";
    }

    private void UpdateSyntaxHighlighting(string code, RichTextBox richTextBox)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 19,
            LineStackingStrategy =
        LineStackingStrategy.BlockLineHeight
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

    private void SetStatus(string message, StatusTone tone, bool isRestore, bool playErrorAnimation = true)
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
                statusBorder.Background =
                    FindBrush("SuccessSurfaceBrush");
                statusBorder.BorderBrush = new SolidColorBrush(
                    Color.FromRgb(32, 80, 68));
                statusIconBorder.BorderBrush =
                    FindBrush("SuccessBrush");
                statusIconText.Foreground =
                    FindBrush("SuccessBrush");
                statusIconText.Text = "✓";
                break;

            case StatusTone.Error:
                statusBorder.Background =
                    FindBrush("ErrorSurfaceBrush");
                statusBorder.BorderBrush =
                    FindBrush("ErrorBrush");
                statusIconBorder.BorderBrush =
                    FindBrush("ErrorBrush");
                statusIconText.Foreground =
                    FindBrush("ErrorBrush");
                statusIconText.Text = "!";

                if (playErrorAnimation)
                {
                    PlayHeaderAnimation(
                        HeaderAnimationEvent.ErrorOccurred);
                }

                break;

            case StatusTone.Loading:
                statusBorder.Background =
                    FindBrush("PrimarySoftBrush");
                statusBorder.BorderBrush =
                    FindBrush("PrimaryBrush");
                statusIconBorder.BorderBrush =
                    FindBrush("PrimaryBrush");
                statusIconText.Foreground =
                    FindBrush("PrimaryHoverBrush");
                statusIconText.Text = "…";
                break;

            default:
                statusBorder.Background =
                    FindBrush("SurfaceElevatedBrush");
                statusBorder.BorderBrush =
                    FindBrush("BorderStrongBrush");
                statusIconBorder.BorderBrush =
                    FindBrush("TextMutedBrush");
                statusIconText.Foreground =
                    FindBrush("TextSecondaryBrush");
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

    private void HeaderAnimationMediaElement_Loaded(object sender, RoutedEventArgs e)
    {
        PlayHeaderAnimation(
            HeaderAnimationEvent.ApplicationStarted);
    }

    private void PlayHeaderAnimation(
    HeaderAnimationEvent animationEvent)
    {
        if (_isHeaderAnimationPlanRunning ||
            _isHeaderAnimationTransitionRunning)
        {
            QueueHeaderAnimationEvent(animationEvent);
            return;
        }

        if (ShouldIgnoreHeaderAnimationEvent(animationEvent))
        {
            return;
        }

        var plan = _headerAnimationProfile.CreatePlan(
            animationEvent,
            _currentHeaderVisualState);

        _headerAnimationSteps.Clear();

        foreach (var step in plan.Steps)
        {
            _headerAnimationSteps.Enqueue(step);
        }

        _planFinalHeaderVisualState = plan.FinalState;
        _isHeaderAnimationPlanRunning = true;

        PlayNextHeaderAnimationStep();
    }

    private void QueueHeaderAnimationEvent(
        HeaderAnimationEvent animationEvent)
    {
        if (_queuedHeaderAnimationEvents.TryPeek(
                out var queuedEvent) &&
            queuedEvent == animationEvent &&
            _queuedHeaderAnimationEvents.Count == 1)
        {
            return;
        }

        if (_queuedHeaderAnimationEvents.Count > 0 &&
            _queuedHeaderAnimationEvents.Last() == animationEvent)
        {
            return;
        }

        _queuedHeaderAnimationEvents.Enqueue(animationEvent);
    }

    private bool ShouldIgnoreHeaderAnimationEvent(
        HeaderAnimationEvent animationEvent)
    {
        if (_activeHeaderVisual is null)
        {
            return false;
        }

        return animationEvent switch
        {
            HeaderAnimationEvent.ApplicationStarted =>
                _currentHeaderVisualState ==
                HeaderVisualState.CodeMasking,

            HeaderAnimationEvent.CodeMaskingTabActivated =>
                _currentHeaderVisualState ==
                HeaderVisualState.CodeMasking,

            HeaderAnimationEvent.CodeRestoreTabActivated =>
                _currentHeaderVisualState ==
                HeaderVisualState.CodeRestore,

            HeaderAnimationEvent.ErrorOccurred =>
                _currentHeaderVisualState ==
                HeaderVisualState.Error,

            HeaderAnimationEvent.MaskingStarted =>
                false,

            _ => throw new ArgumentOutOfRangeException(
                nameof(animationEvent),
                animationEvent,
                "Desteklenmeyen başlık animasyonu olayı.")
        };
    }

    private void PlayNextHeaderAnimationStep()
    {
        if (_headerAnimationSteps.Count == 0)
        {
            CompleteHeaderAnimationPlan();
            return;
        }

        var step = _headerAnimationSteps.Dequeue();

        switch (step.AssetType)
        {
            case HeaderAnimationAssetType.Video:
                PlayHeaderVideoStep(step);
                break;

            case HeaderAnimationAssetType.Image:
                ShowHeaderImageStep(step);
                break;

            case HeaderAnimationAssetType.PngFrameSequence:
                PlayHeaderFrameSequenceStep(step);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(step.AssetType),
                    step.AssetType,
                    "Desteklenmeyen başlık animasyonu asset türü.");
        }
    }

    private async void PlayHeaderFrameSequenceStep(
    HeaderAnimationStep step)
    {
        var sequenceDirectory =
            GetHeaderAnimationAssetPath(step.AssetPath);

        CancelHeaderFrameSequence();

        var cancellationTokenSource =
            new CancellationTokenSource();

        _headerFrameSequenceCancellationTokenSource =
            cancellationTokenSource;

        _currentHeaderAnimationStep = step;

        try
        {
            await _pngFrameSequencePlayer.PlayAsync(
                sequenceDirectory,
                step.FrameRate,
                frame =>
                {
                    HeaderAnimationImage.Source = frame;

                    if (_activeHeaderVisual !=
                        HeaderAnimationImage)
                    {
                        HideHeaderMediaElements();

                        HeaderAnimationImage.BeginAnimation(
                            UIElement.OpacityProperty,
                            null);

                        HeaderAnimationImage.Opacity = 1;
                        _activeHeaderVisual =
                            HeaderAnimationImage;

                        _activeHeaderAnimationMediaElement =
                            null;
                    }
                },
                cancellationTokenSource.Token);

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            PlayNextHeaderAnimationStep();
        }
        catch (OperationCanceledException)
        {
            // Yeni bir oynatma veya pencerenin kapanması nedeniyle
            // yapılan kontrollü iptal hata olarak gösterilmez.
        }
        catch (Exception exception)
        {
            HandleHeaderAnimationFailure(
                $"PNG başlık animasyonu oynatılamadı: " +
                $"{exception.Message}");
        }
        finally
        {
            if (ReferenceEquals(
                    _headerFrameSequenceCancellationTokenSource,
                    cancellationTokenSource))
            {
                _headerFrameSequenceCancellationTokenSource.Dispose();
                _headerFrameSequenceCancellationTokenSource = null;
            }
        }
    }

    private void HideHeaderMediaElements()
    {
        foreach (var mediaElement in new[]
                 {
                 HeaderAnimationPrimaryMediaElement,
                 HeaderAnimationSecondaryMediaElement
             })
        {
            mediaElement.BeginAnimation(
                UIElement.OpacityProperty,
                null);

            mediaElement.Stop();
            mediaElement.Close();
            mediaElement.Source = null;
            mediaElement.Opacity = 0;
        }
    }

    private void PlayHeaderVideoStep(HeaderAnimationStep step)
    {
        var absoluteAssetPath = GetHeaderAnimationAssetPath(step.AssetPath);

        if (!File.Exists(absoluteAssetPath))
        {
            HandleMissingHeaderAnimationAsset(step.AssetPath);
            return;
        }

        var targetMediaElement =
            _activeHeaderAnimationMediaElement ==
            HeaderAnimationPrimaryMediaElement
                ? HeaderAnimationSecondaryMediaElement
                : HeaderAnimationPrimaryMediaElement;

        if (_activeHeaderAnimationMediaElement is null)
        {
            targetMediaElement =
                HeaderAnimationPrimaryMediaElement;
        }

        _loadingHeaderAnimationMediaElement = targetMediaElement;
        _loadingHeaderAnimationStep = step;

        targetMediaElement.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        targetMediaElement.Opacity = 0;
        targetMediaElement.Stop();
        targetMediaElement.Close();
        targetMediaElement.Source = new Uri(
            absoluteAssetPath,
            UriKind.Absolute);
        targetMediaElement.Position = TimeSpan.Zero;
        targetMediaElement.Play();
    }

    private void ShowHeaderImageStep(
    HeaderAnimationStep step)
    {
        var absoluteAssetPath =
            GetHeaderAnimationAssetPath(step.AssetPath);

        if (!File.Exists(absoluteAssetPath))
        {
            HandleMissingHeaderAnimationAsset(
                step.AssetPath);

            return;
        }

        try
        {
            var bitmapImage = new BitmapImage();

            bitmapImage.BeginInit();
            bitmapImage.CacheOption =
                BitmapCacheOption.OnLoad;
            bitmapImage.CreateOptions =
                BitmapCreateOptions.PreservePixelFormat;
            bitmapImage.UriSource = new Uri(
                absoluteAssetPath,
                UriKind.Absolute);
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            HeaderAnimationImage.Source = bitmapImage;
            _currentHeaderAnimationStep = step;

            if (_activeHeaderVisual ==
                HeaderAnimationImage)
            {
                HeaderAnimationImage.BeginAnimation(
                    UIElement.OpacityProperty,
                    null);

                HeaderAnimationImage.Opacity = 1;

                PlayNextHeaderAnimationStep();
                return;
            }

            HeaderAnimationImage.BeginAnimation(
                UIElement.OpacityProperty,
                null);

            HeaderAnimationImage.Opacity = 0;

            if (_activeHeaderVisual is null)
            {
                HeaderAnimationImage.Opacity = 1;
                _activeHeaderVisual =
                    HeaderAnimationImage;

                PlayNextHeaderAnimationStep();
                return;
            }

            StartHeaderAnimationCrossfade(
                _activeHeaderVisual,
                HeaderAnimationImage,
                completeCurrentStep: true);
        }
        catch (Exception exception)
        {
            HandleHeaderAnimationFailure(
                $"Başlık görseli açılamadı: " +
                $"{exception.Message}");
        }
    }

    private string GetHeaderAnimationAssetPath(string assetPath)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Animations",
            _headerAnimationProfile.AssetDirectoryName,
            assetPath);
    }

    private void HeaderAnimationMediaElement_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MediaElement openedMediaElement ||
            openedMediaElement != _loadingHeaderAnimationMediaElement ||
            _loadingHeaderAnimationStep is null)
        {
            return;
        }

        _currentHeaderAnimationStep = _loadingHeaderAnimationStep;

        var completesPlanAfterTransition =
            _currentHeaderAnimationStep.Playback ==
            HeaderAnimationPlayback.Loop;

        if (_activeHeaderVisual is null)
        {
            openedMediaElement.Opacity = 1;
            _activeHeaderAnimationMediaElement = openedMediaElement;
            _activeHeaderVisual = openedMediaElement;
            _loadingHeaderAnimationMediaElement = null;
            _loadingHeaderAnimationStep = null;

            if (completesPlanAfterTransition)
            {
                PlayNextHeaderAnimationStep();
            }

            return;
        }

        StartHeaderAnimationCrossfade(
            _activeHeaderVisual,
            openedMediaElement,
            completeCurrentStep: completesPlanAfterTransition);
    }

    private void HeaderAnimationMediaElement_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (sender is not MediaElement endedMediaElement ||
            endedMediaElement != _activeHeaderAnimationMediaElement ||
            _currentHeaderAnimationStep is null)
        {
            return;
        }

        if (_currentHeaderAnimationStep.Playback ==
            HeaderAnimationPlayback.Loop)
        {
            endedMediaElement.Position = TimeSpan.Zero;
            endedMediaElement.Play();
            return;
        }

        if (_currentHeaderAnimationStep.Playback ==
            HeaderAnimationPlayback.Once)
        {
            PlayNextHeaderAnimationStep();
        }
    }

    private void HeaderAnimationMediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is MediaElement failedMediaElement)
        {
            failedMediaElement.Stop();
            failedMediaElement.Close();
            failedMediaElement.Source = null;
            failedMediaElement.Opacity = 0;

            if (_activeHeaderVisual == failedMediaElement)
            {
                _activeHeaderVisual = null;
                _activeHeaderAnimationMediaElement = null;
            }
        }

        HandleHeaderAnimationFailure(
            "Başlık animasyonu açılamadı: " +
            e.ErrorException.Message);
    }

    private void HandleMissingHeaderAnimationAsset(string AssetPath)
    {
        HandleHeaderAnimationFailure(
            $"Animasyon dosyası bulunamadı: {AssetPath}");
    }

    private void HandleHeaderAnimationFailure(string message)
    {
        CancelHeaderFrameSequence();

        _headerAnimationSteps.Clear();
        _queuedHeaderAnimationEvents.Clear();

        _loadingHeaderAnimationMediaElement = null;
        _loadingHeaderAnimationStep = null;
        _currentHeaderAnimationStep = null;

        _isHeaderAnimationTransitionRunning = false;
        _isHeaderAnimationPlanRunning = false;

        SetStatus(
            message,
            StatusTone.Error,
            isRestore: MainTabControl.SelectedIndex == 1,
            playErrorAnimation: false);
    }

    private void StartHeaderAnimationCrossfade(FrameworkElement outgoingVisual, FrameworkElement incomingVisual, bool completeCurrentStep)
    {
        _isHeaderAnimationTransitionRunning = true;

        var fadeOutAnimation = new DoubleAnimation
        {
            From = outgoingVisual.Opacity,
            To = 0,
            Duration = HeaderAnimationTransitionDuration,
            EasingFunction = new SineEase
            {
                EasingMode = EasingMode.EaseInOut
            }
        };

        var fadeInAnimation = new DoubleAnimation
        {
            From = incomingVisual.Opacity,
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
                outgoingVisual,
                incomingVisual,
                completeCurrentStep);
        };

        outgoingVisual.BeginAnimation(
            UIElement.OpacityProperty,
            fadeOutAnimation);

        incomingVisual.BeginAnimation(
            UIElement.OpacityProperty,
            fadeInAnimation);
    }

    private void CompleteHeaderAnimationTransition(FrameworkElement outgoingVisual, FrameworkElement incomingVisual, bool completeCurrentStep)
    {
        outgoingVisual.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        incomingVisual.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        outgoingVisual.Opacity = 0;
        incomingVisual.Opacity = 1;

        if (outgoingVisual is MediaElement outgoingMediaElement)
        {
            outgoingMediaElement.Stop();
            outgoingMediaElement.Close();
            outgoingMediaElement.Source = null;
        }

        _activeHeaderVisual = incomingVisual;
        _activeHeaderAnimationMediaElement =
            incomingVisual as MediaElement;

        _loadingHeaderAnimationMediaElement = null;
        _loadingHeaderAnimationStep = null;
        _isHeaderAnimationTransitionRunning = false;

        if (completeCurrentStep)
        {
            PlayNextHeaderAnimationStep();
        }
    }

    private void CompleteHeaderAnimationPlan()
    {
        _currentHeaderVisualState =
            _planFinalHeaderVisualState;

        _isHeaderAnimationPlanRunning = false;

        PlayNextQueuedHeaderAnimationEvent();
    }

    private void PlayNextQueuedHeaderAnimationEvent()
    {
        while (_queuedHeaderAnimationEvents.Count > 0)
        {
            var queuedEvent =
                _queuedHeaderAnimationEvents.Dequeue();

            if (ShouldIgnoreHeaderAnimationEvent(queuedEvent))
            {
                continue;
            }

            PlayHeaderAnimation(queuedEvent);
            return;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelHeaderFrameSequence();

        base.OnClosed(e);
    }

    private void CancelHeaderFrameSequence()
    {
        if (_headerFrameSequenceCancellationTokenSource is null)
        {
            return;
        }

        _headerFrameSequenceCancellationTokenSource.Cancel();
        _headerFrameSequenceCancellationTokenSource.Dispose();
        _headerFrameSequenceCancellationTokenSource = null;
    }
}