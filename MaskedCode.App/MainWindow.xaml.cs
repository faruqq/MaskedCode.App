using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MaskedCode.App.Masking;

namespace MaskedCode.App;

public partial class MainWindow : Window
{
    private string? _selectedFilePath;

    public MainWindow()
    {
        InitializeComponent();
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
            SelectedFileTextBlock.Text = dialog.FileName;

            SelectLanguageFromFileExtension(dialog.FileName);

            StatusTextBlock.Text =
                $"Dosya yüklendi: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Dosya okunamadı.{Environment.NewLine}{exception.Message}",
                "Dosya okuma hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusTextBlock.Text = "Dosya okunurken hata oluştu.";
        }
    }

    private void MaskButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SourceCodeTextBox.Text))
        {
            StatusTextBlock.Text =
                "Maskelenecek kaynak kod bulunamadı.";

            return;
        }

        if (GetSelectedLanguage() != "PL1")
        {
            MaskedCodeTextBox.Clear();
            UpdateOutputButtons();

            StatusTextBlock.Text =
                "Bu aşamada yalnızca PL/I maskelemesi destekleniyor.";

            return;
        }

        try
        {
            var selectedMode = GetSelectedMaskingMode();
            var masker = new Pl1CodeMasker();

            var result = masker.Mask(
                SourceCodeTextBox.Text,
                selectedMode);

            MaskedCodeTextBox.Text = result.MaskedCode;
            UpdateOutputButtons();

            var modeDisplayName =
                GetMaskingModeDisplayName(result.Mode);

            StatusTextBlock.Text =
                $"{result.IdentifierCount} benzersiz identifier, " +
                $"{result.StringLiteralCount} benzersiz string değer ve " +
                $"{result.NumericLiteralCount} benzersiz sayısal değer " +
                $"{modeDisplayName} moduyla maskelendi. " +
                "Yorumlar ve şifreli eşleme kasası henüz " +
                "tamamlanmadığı için bu çıktıyı şirket dışına göndermeyin.";
        }
        catch (Exception exception)
        {
            MaskedCodeTextBox.Clear();
            UpdateOutputButtons();

            StatusTextBlock.Text =
                $"Maskeleme işlemi tamamlanamadı: " +
                exception.Message;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(MaskedCodeTextBox.Text))
        {
            return;
        }

        Clipboard.SetText(MaskedCodeTextBox.Text);
        StatusTextBlock.Text = "Maskelenmiş kod panoya kopyalandı.";
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
            await File.WriteAllTextAsync(
                dialog.FileName,
                MaskedCodeTextBox.Text);

            StatusTextBlock.Text =
                $"Maskelenmiş dosya kaydedildi: {dialog.FileName}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Dosya kaydedilemedi.{Environment.NewLine}{exception.Message}",
                "Dosya kaydetme hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusTextBlock.Text = "Dosya kaydedilirken hata oluştu.";
        }
    }

    private void SourceCodeTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        MaskButton.IsEnabled =
            !string.IsNullOrWhiteSpace(SourceCodeTextBox.Text);

        if (!IsLoaded)
        {
            return;
        }

        MaskedCodeTextBox.Clear();
        UpdateOutputButtons();

        if (_selectedFilePath is not null &&
            SourceCodeTextBox.IsKeyboardFocusWithin)
        {
            _selectedFilePath = null;
            SelectedFileTextBlock.Text = "Ekrana yapıştırılan kod";
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

        MaskedCodeTextBox.Clear();
        UpdateOutputButtons();

        StatusTextBlock.Text =
            $"Kaynak dil değiştirildi: {GetSelectedLanguageDisplayName()}";
    }

    private void SelectLanguageFromFileExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        LanguageComboBox.SelectedIndex = extension.ToLowerInvariant() switch
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

            "EGL" => "EGL kaynak dosyaları (*.egl)|*.egl",

            "CSharp" => "C# kaynak dosyaları (*.cs)|*.cs",

            _ => "Metin dosyaları (*.txt)|*.txt"
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
            "CSharp" => "masked-code.cs",
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

    private void UpdateOutputButtons()
    {
        var hasMaskedCode =
            !string.IsNullOrEmpty(MaskedCodeTextBox.Text);

        CopyButton.IsEnabled = hasMaskedCode;
        SaveFileButton.IsEnabled = hasMaskedCode;
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
            string.IsNullOrEmpty(MaskedCodeTextBox.Text))
        {
            return;
        }

        MaskedCodeTextBox.Clear();
        UpdateOutputButtons();

        StatusTextBlock.Text =
            "Maskeleme yöntemi değiştirildiği için " +
            "önceki maskelenmiş sonuç temizlendi.";
    }
}