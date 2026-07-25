using System.IO;
using MaskedCode.App.Security;
using Xunit;

namespace MaskedCode.App.Tests.Security;

public sealed class VaultPasswordProviderTests
{
    [Fact]
    public async Task ReadPasswordAsync_WithSingleLinePassword_ShouldReturnPassword()
    {
        var testDirectory =
            Path.Combine(
                Path.GetTempPath(),
                $"MaskedCode-{Guid.NewGuid():N}");

        var passwordFilePath =
            Path.Combine(
                testDirectory,
                "vault-password.txt");

        Directory.CreateDirectory(
            testDirectory);

        try
        {
            await File.WriteAllTextAsync(
                passwordFilePath,
                "short-password\r\n");

            var provider =
                new VaultPasswordProvider(
                    passwordFilePath);

            var password =
                await provider.ReadPasswordAsync();

            Assert.Equal(
                "short-password",
                password);
        }
        finally
        {
            Directory.Delete(
                testDirectory,
                recursive: true);
        }
    }

    [Fact]
    public async Task ReadPasswordAsync_WithMissingFile_ShouldRejectPasswordSource()
    {
        var passwordFilePath =
            Path.Combine(
                Path.GetTempPath(),
                $"missing-{Guid.NewGuid():N}.txt");

        var provider =
            new VaultPasswordProvider(
                passwordFilePath);

        var exception =
            await Assert.ThrowsAsync<FileNotFoundException>(
                provider.ReadPasswordAsync);

        Assert.Contains(
            "Kasa parola dosyası bulunamadı.",
            exception.Message);
    }

    [Fact]
    public async Task ReadPasswordAsync_WithMultipleLines_ShouldRejectPasswordSource()
    {
        var testDirectory =
            Path.Combine(
                Path.GetTempPath(),
                $"MaskedCode-{Guid.NewGuid():N}");

        var passwordFilePath =
            Path.Combine(
                testDirectory,
                "vault-password.txt");

        Directory.CreateDirectory(
            testDirectory);

        try
        {
            await File.WriteAllTextAsync(
                passwordFilePath,
                $"first-line{Environment.NewLine}second-line");

            var provider =
                new VaultPasswordProvider(
                    passwordFilePath);

            var exception =
                await Assert.ThrowsAsync<InvalidDataException>(
                    provider.ReadPasswordAsync);

            Assert.Contains(
                "yalnızca tek satır",
                exception.Message);
        }
        finally
        {
            Directory.Delete(
                testDirectory,
                recursive: true);
        }
    }
}