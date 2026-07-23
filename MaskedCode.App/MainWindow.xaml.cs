using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MaskedCode.App.Masking;

namespace MaskedCode.App;

public partial class MainWindow : Window
{
    private const long MaximumVaultFileSizeInBytes = 64L * 1024L * 1024L;

    private string? _selectedFilePath;
    private string? _selectedMaskedFilePath;
    private string? _selectedVaultFilePath;

    private Pl1MaskingResult? _lastMaskingResult;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void SelectFileButton_Click(
        object sender,
        RoutedEventArgs e)
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
            var sourceCode =
                await File.ReadAllTextAsync(
                    dialog.FileName);

            _selectedFilePath = dialog.FileName;
            SourceCodeTextBox.Text = sourceCode;
            SelectedFileTextBlock.Text = dialog.FileName;

            SelectLanguageFromFileExtension(
                dialog.FileName);

            StatusTextBlock.Text =
                $"Dosya yüklendi: " +
                $"{Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Dosya okunamadı.{Environment.NewLine}" +
                exception.Message,
                "Dosya okuma hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusTextBlock.Text =
                "Dosya okunurken hata oluştu.";
        }
    }

    private void MaskButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(
                SourceCodeTextBox.Text))
        {
            StatusTextBlock.Text =
                "Maskelenecek kaynak kod bulunamadı.";

            return;
        }

        if (GetSelectedLanguage() != "PL1")
        {
            ClearMaskingOutput();

            StatusTextBlock.Text =
                "Bu aşamada yalnızca PL/I " +
                "maskelemesi destekleniyor.";

            return;
        }

        try
        {
            var selectedMode =
                GetSelectedMaskingMode();

            var masker =
                new Pl1CodeMasker();

            var result = masker.Mask(
                SourceCodeTextBox.Text,
                selectedMode);

            VaultPasswordBox.Clear();
            VaultPasswordConfirmationBox.Clear();

            _lastMaskingResult = result;
            MaskedCodeTextBox.Text = result.MaskedCode;

            UpdateOutputButtons();

            var modeDisplayName =
                GetMaskingModeDisplayName(
                    result.Mode);

            StatusTextBlock.Text =
                $"{result.IdentifierCount} benzersiz identifier, " +
                $"{result.StringLiteralCount} benzersiz string değer, " +
                $"{result.NumericLiteralCount} benzersiz sayısal değer ve " +
                $"{result.CommentCount} benzersiz yorum " +
                $"{modeDisplayName} moduyla maskelendi. " +
                "Şifreli eşleme kasası henüz kaydedilmediği için " +
                "bu çıktıyı şirket dışına göndermeyin.";
        }
        catch (Exception exception)
        {
            ClearMaskingOutput();

            StatusTextBlock.Text =
                "Maskeleme işlemi tamamlanamadı: " +
                exception.Message;
        }
    }

    private void CopyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(
                MaskedCodeTextBox.Text))
        {
            return;
        }

        Clipboard.SetText(
            MaskedCodeTextBox.Text);

        StatusTextBlock.Text =
            "Maskelenmiş kod panoya kopyalandı.";
    }

    private async void SaveFileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(
                MaskedCodeTextBox.Text))
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
            await File.WriteAllTextAsync(
                dialog.FileName,
                MaskedCodeTextBox.Text);

            StatusTextBlock.Text =
                $"Maskelenmiş dosya kaydedildi: " +
                dialog.FileName;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Dosya kaydedilemedi.{Environment.NewLine}" +
                exception.Message,
                "Dosya kaydetme hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusTextBlock.Text =
                "Dosya kaydedilirken hata oluştu.";
        }
    }

    private async void SaveVaultButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_lastMaskingResult is null ||
            _lastMaskingResult.Mappings.Count == 0)
        {
            StatusTextBlock.Text =
                "Şifrelenecek maskeleme eşlemesi bulunamadı.";

            return;
        }

        var password =
            VaultPasswordBox.Password;

        var passwordConfirmation =
            VaultPasswordConfirmationBox.Password;

        if (password.Length < 12)
        {
            StatusTextBlock.Text =
                "Kasa parolası en az 12 karakter olmalıdır.";

            VaultPasswordBox.Focus();
            return;
        }

        if (!string.Equals(
                password,
                passwordConfirmation,
                StringComparison.Ordinal))
        {
            StatusTextBlock.Text =
                "Kasa parolası ile parola tekrarı aynı değil.";

            VaultPasswordConfirmationBox.Focus();
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Şifreli eşleme kasasını kaydedin",
            Filter =
                "MaskedCode şifreli kasa dosyası (*.mcvault)|" +
                "*.mcvault",
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
            var vault =
                new EncryptedMappingVault();

            var encryptedVault =
                vault.Encrypt(
                    _lastMaskingResult,
                    password);

            await File.WriteAllBytesAsync(
                dialog.FileName,
                encryptedVault);

            VaultPasswordBox.Clear();
            VaultPasswordConfirmationBox.Clear();

            StatusTextBlock.Text =
                "Şifreli eşleme kasası kaydedildi. " +
                "Kasa dosyasını maskelenmiş koddan ayrı ve " +
                "güvenli bir konumda saklayın.";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text =
                "Şifreli eşleme kasası kaydedilemedi: " +
                exception.Message;
        }
    }

    private void SourceCodeTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        MaskButton.IsEnabled =
            !string.IsNullOrWhiteSpace(
                SourceCodeTextBox.Text);

        if (!IsLoaded)
        {
            return;
        }

        ClearMaskingOutput();

        if (_selectedFilePath is not null &&
            SourceCodeTextBox.IsKeyboardFocusWithin)
        {
            _selectedFilePath = null;
            SelectedFileTextBlock.Text =
                "Ekrana yapıştırılan kod";
        }
    }

    private void LanguageComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ClearMaskingOutput();

        StatusTextBlock.Text =
            $"Kaynak dil değiştirildi: " +
            GetSelectedLanguageDisplayName();
    }

    private void SelectLanguageFromFileExtension(
        string filePath)
    {
        var extension =
            Path.GetExtension(filePath);

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

            "CSharp" =>
                "C# kaynak dosyaları (*.cs)|*.cs|" +
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

            "CSharp" =>
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
            "CSharp" => "masked-code.cs",
            _ => "masked-code.txt"
        };
    }

    private string GetSelectedLanguage()
    {
        return LanguageComboBox.SelectedItem
            is ComboBoxItem item
                ? item.Tag?.ToString() ?? string.Empty
                : string.Empty;
    }

    private string GetSelectedLanguageDisplayName()
    {
        return LanguageComboBox.SelectedItem
            is ComboBoxItem item
                ? item.Content?.ToString() ?? string.Empty
                : string.Empty;
    }

    private void ClearMaskingOutput()
    {
        _lastMaskingResult = null;

        MaskedCodeTextBox.Clear();
        VaultPasswordBox.Clear();
        VaultPasswordConfirmationBox.Clear();

        UpdateOutputButtons();
    }

    private void UpdateOutputButtons()
    {
        var hasMaskedCode =
            !string.IsNullOrEmpty(
                MaskedCodeTextBox.Text);

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

    private static string GetMaskingModeDisplayName(
        MaskingMode mode)
    {
        return mode switch
        {
            MaskingMode.MaximumPrivacy =>
                "Maksimum Gizlilik",

            MaskingMode.FormatPreserving =>
                "Biçim Korumalı",

            _ => throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Desteklenmeyen maskeleme modu.")
        };
    }

    private void MaskingModeRadioButton_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (!IsInitialized ||
            MaskedCodeTextBox is null ||
            string.IsNullOrEmpty(
                MaskedCodeTextBox.Text))
        {
            return;
        }

        ClearMaskingOutput();

        StatusTextBlock.Text =
            "Maskeleme yöntemi değiştirildiği için " +
            "önceki maskelenmiş sonuç temizlendi.";
    }

    private async void SelectMaskedFileButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Geri açılacak maskelenmiş PL/I dosyasını seçin",
            Filter =
                "PL/I kaynak dosyaları (*.pli;*.pl1)|*.pli;*.pl1|" +
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
            var maskedCode =
                await File.ReadAllTextAsync(
                    dialog.FileName);

            _selectedMaskedFilePath =
                dialog.FileName;

            MaskedInputTextBox.Text =
                maskedCode;

            SelectedMaskedFileTextBlock.Text =
                dialog.FileName;

            ClearUnmaskingOutput();
            UpdateUnmaskButton();

            StatusTextBlock.Text =
                $"Maskelenmiş dosya yüklendi: " +
                $"{Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Maskelenmiş dosya okunamadı.{Environment.NewLine}" +
                exception.Message,
                "Dosya okuma hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusTextBlock.Text =
                "Maskelenmiş dosya okunurken hata oluştu.";
        }
    }

    private void SelectVaultFileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Şifreli eşleme kasasını seçin",
            Filter =
                "MaskedCode şifreli kasa dosyası (*.mcvault)|" +
                "*.mcvault",
            DefaultExt = ".mcvault",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _selectedVaultFilePath =
            dialog.FileName;

        SelectedVaultFileTextBlock.Text =
            dialog.FileName;

        RestoreVaultPasswordBox.Clear();
        ClearUnmaskingOutput();
        UpdateUnmaskButton();

        StatusTextBlock.Text =
            $"Şifreli kasa seçildi: " +
            $"{Path.GetFileName(dialog.FileName)}";
    }

    private void MaskedInputTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ClearUnmaskingOutput();

        if (_selectedMaskedFilePath is not null &&
            MaskedInputTextBox.IsKeyboardFocusWithin)
        {
            _selectedMaskedFilePath = null;

            SelectedMaskedFileTextBlock.Text =
                "Ekrana yapıştırılan maskelenmiş kod";
        }

        UpdateUnmaskButton();
    }

    private void RestoreInput_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ClearUnmaskingOutput();
        UpdateUnmaskButton();
    }

    private async void UnmaskButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(
                MaskedInputTextBox.Text))
        {
            StatusTextBlock.Text =
                "Geri açılacak maskelenmiş kod bulunamadı.";

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _selectedVaultFilePath))
        {
            StatusTextBlock.Text =
                "Şifreli eşleme kasası seçilmedi.";

            return;
        }

        var password =
            RestoreVaultPasswordBox.Password;

        if (password.Length < 12)
        {
            StatusTextBlock.Text =
                "Kasa parolası en az 12 karakter olmalıdır.";

            RestoreVaultPasswordBox.Focus();
            return;
        }

        var maskedCode =
            MaskedInputTextBox.Text;

        UnmaskButton.IsEnabled = false;
        RestoredCodeTextBox.Clear();

        StatusTextBlock.Text =
            "Kasa doğrulanıyor ve kod geri açılıyor...";

        try
        {
            var encryptedVault =
                await ReadVaultFileSafelyAsync(
                    _selectedVaultFilePath);

            var restoredCode =
                await Task.Run(
                    () =>
                    {
                        var vault =
                            new EncryptedMappingVault();

                        var vaultContent =
                            vault.Decrypt(
                                encryptedVault,
                                password,
                                maskedCode);

                        var unmasker =
                            new Pl1CodeUnmasker();

                        return unmasker.Unmask(
                            maskedCode,
                            vaultContent);
                    });

            RestoredCodeTextBox.Text =
                restoredCode;

            CopyRestoredButton.IsEnabled = true;
            SaveRestoredFileButton.IsEnabled = true;

            StatusTextBlock.Text =
                "Kasa doğrulandı ve kod başarıyla geri açıldı.";
        }
        catch (InvalidDataException exception)
        {
            ClearUnmaskingOutput();

            MessageBox.Show(
                exception.Message,
                "Kasa doğrulama hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            StatusTextBlock.Text =
                "Kod geri açılamadı: " +
                exception.Message;
        }
        catch (Exception exception)
        {
            ClearUnmaskingOutput();

            MessageBox.Show(
                $"Kod geri açılamadı.{Environment.NewLine}" +
                exception.Message,
                "Geri açma hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusTextBlock.Text =
                "Kod geri açılırken beklenmeyen bir hata oluştu.";
        }
        finally
        {
            RestoreVaultPasswordBox.PasswordChanged -=
                RestoreInput_Changed;

            RestoreVaultPasswordBox.Clear();

            RestoreVaultPasswordBox.PasswordChanged +=
                RestoreInput_Changed;

            UpdateUnmaskButton();
        }
    }

    private void CopyRestoredButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(
                RestoredCodeTextBox.Text))
        {
            return;
        }

        Clipboard.SetText(
            RestoredCodeTextBox.Text);

        StatusTextBlock.Text =
            "Geri açılmış kod panoya kopyalandı.";
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

        var dialog = new SaveFileDialog
        {
            Title = "Geri açılmış PL/I dosyasını kaydedin",
            Filter =
                "PL/I kaynak dosyaları (*.pli)|*.pli|" +
                "PL/I kaynak dosyaları (*.pl1)|*.pl1",
            FileName = CreateRestoredFileName()
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

            StatusTextBlock.Text =
                $"Geri açılmış dosya kaydedildi: " +
                dialog.FileName;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Dosya kaydedilemedi.{Environment.NewLine}" +
                exception.Message,
                "Dosya kaydetme hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusTextBlock.Text =
                "Geri açılmış dosya kaydedilirken hata oluştu.";
        }
    }

    private static async Task<byte[]>
        ReadVaultFileSafelyAsync(
            string filePath)
    {
        var fileInfo =
            new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                "Seçilen şifreli kasa dosyası bulunamadı.",
                filePath);
        }

        if (fileInfo.Length >
            MaximumVaultFileSizeInBytes)
        {
            throw new InvalidDataException(
                "Şifreli kasa dosyası izin verilen " +
                "azami boyutu aşıyor.");
        }

        await using var stream =
            new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                options:
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

        if (stream.Length >
            MaximumVaultFileSizeInBytes)
        {
            throw new InvalidDataException(
                "Şifreli kasa dosyası izin verilen " +
                "azami boyutu aşıyor.");
        }

        var encryptedVault =
            new byte[checked((int)stream.Length)];

        await stream.ReadExactlyAsync(
            encryptedVault);

        return encryptedVault;
    }

    private string CreateRestoredFileName()
    {
        if (string.IsNullOrWhiteSpace(
                _selectedMaskedFilePath))
        {
            return "restored-code.pli";
        }

        var fileName =
            Path.GetFileNameWithoutExtension(
                _selectedMaskedFilePath);

        var extension =
            Path.GetExtension(
                _selectedMaskedFilePath);

        if (fileName.EndsWith(
                ".masked",
                StringComparison.OrdinalIgnoreCase))
        {
            fileName =
                fileName[..^".masked".Length];
        }

        return $"{fileName}.restored{extension}";
    }

    private void ClearUnmaskingOutput()
    {
        RestoredCodeTextBox.Clear();

        CopyRestoredButton.IsEnabled = false;
        SaveRestoredFileButton.IsEnabled = false;
    }

    private void UpdateUnmaskButton()
    {
        UnmaskButton.IsEnabled =
            !string.IsNullOrWhiteSpace(
                MaskedInputTextBox.Text) &&
            !string.IsNullOrWhiteSpace(
                _selectedVaultFilePath) &&
            !string.IsNullOrEmpty(
                RestoreVaultPasswordBox.Password);
    }
}