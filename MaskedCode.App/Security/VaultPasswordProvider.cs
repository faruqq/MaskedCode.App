using System.IO;
using System.Text;

namespace MaskedCode.App.Security;

/// <summary>
/// Kasa parolasının kullanıcıya özel sabit bir metin dosyasından
/// okunmasını sağlar.
///
/// Neden var?
/// Kullanıcının her kasa işleminde aynı parolayı yeniden yazmasını
/// önlemek için vardır.
///
/// Ne çözüyor?
/// Parolanın arayüzde tekrar tekrar girilmesi ihtiyacını ortadan
/// kaldırır ve parola dosyasının kullanılacağı konumu merkezileştirir.
///
/// Hangi örneği destekliyor?
/// %LOCALAPPDATA%\MaskedCode\vault-password.txt dosyasındaki tek
/// satırlık parolanın okunmasını destekler.
///
/// Nerede kullanılır?
/// Kasa kaydetme ve kodu geri açma ekranlarında dosyadan parola
/// seçeneği etkinleştirildiğinde kullanılır.
///
/// Gelecekte neye temel olur?
/// Parola kaynağının kullanıcı arayüzünden bağımsız ve güvenli biçimde
/// doğrulanmasına temel olur.
/// </summary>
public sealed class VaultPasswordProvider
{
    private const long MaximumPasswordFileSizeInBytes = 4 * 1024;

    public static string DefaultPasswordFilePath { get; } =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "MaskedCode",
            "vault-password.txt");

    public string PasswordFilePath { get; }

    public VaultPasswordProvider(string? passwordFilePath = null)
    {
        PasswordFilePath =
            string.IsNullOrWhiteSpace(passwordFilePath)
                ? DefaultPasswordFilePath
                : Path.GetFullPath(passwordFilePath);
    }

    public async Task<string> ReadPasswordAsync()
    {
        var fileInfo =
            new FileInfo(PasswordFilePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                "Kasa parola dosyası bulunamadı.",
                PasswordFilePath);
        }

        if (fileInfo.Length == 0)
        {
            throw new InvalidDataException(
                "Kasa parola dosyası boş.");
        }

        if (fileInfo.Length > MaximumPasswordFileSizeInBytes)
        {
            throw new InvalidDataException(
                "Kasa parola dosyası izin verilen boyutu aşıyor.");
        }

        var password =
            await File.ReadAllTextAsync(
                PasswordFilePath,
                Encoding.UTF8);

        password =
            password.TrimEnd(
                '\r',
                '\n');

        if (password.Length == 0)
        {
            throw new InvalidDataException(
                "Kasa parola dosyasında parola bulunamadı.");
        }

        if (password.Contains('\r') ||
            password.Contains('\n'))
        {
            throw new InvalidDataException(
                "Kasa parola dosyası yalnızca tek satır içermelidir.");
        }

        return password;
    }
}