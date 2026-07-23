# Project State

## Çalışma Kuralları

### Unit Test Yaklaşımı

- Bu proje için unit test yazılmayacaktır.
- Yeni bir test projesi, test sınıfı veya test metodu oluşturulmayacaktır.
- Kullanıcı açıkça bu kararı değiştirmediği sürece unit test önerilmeyecektir.
- Test altyapısına ait dosyalar kullanıcıdan istenmeyecektir.
- Geliştirilen özellikler, kullanıcı tarafından sağlanan gerçekçi PL/I ve EGL örnekleriyle manuel olarak doğrulanacaktır.
- Manuel doğrulamada tespit edilen hatalar doğrudan mevcut üretim kodunda düzeltilecektir.

### Geliştirme Yaklaşımı

- Öncelik, projeyi güvenli ve kullanılabilir bir ürüne mümkün olan en kısa sürede ulaştırmaktır.
- Over-engineering’den kaçınılacaktır.
- Yalnızca mevcut ihtiyacı karşılayan en küçük ve anlaşılır çözüm geliştirilecektir.
- Gelecekte gerekebilir düşüncesiyle şimdiden sınıf, interface, abstraction, katman veya genişletme noktası eklenmeyecektir.
- Yeni bir sınıf veya metot ancak mevcut özellik başka türlü temiz ve güvenli biçimde tamamlanamıyorsa oluşturulacaktır.
- Çalışan kod, somut bir gereksinim veya hata bulunmadan yeniden yapılandırılmayacaktır.
- Mimari mükemmellik yerine güvenlik, doğruluk, sadelik ve projenin tamamlanması önceliklidir.
- Bir özellik yeterli seviyede çalıştığında kapsam büyütülmeden sıradaki zorunlu özelliğe geçilecektir.
- Kullanıcı açıkça istemedikçe ek özellik, genel amaçlı altyapı veya kapsamlı refactoring önerilmeyecektir.

## Kod Paylaşım Standardı

- Değiştirilecek bir metodun tamamı verilmelidir.
- Aynı dosyanın birçok bölümü değişiyorsa dosyanın tamamı verilmelidir.
- Eksik veya parçalı kod blokları verilmemelidir.
- Her değişiklik için dosya adı ve uygulanacağı yer açıkça belirtilmelidir.
- Kullanıcı açıkça istemedikçe repository üzerinde doğrudan değişiklik yapılmayacaktır.

## Güncel Öncelik

Projenin önceliği; PL/I ve EGL kaynak kodlarını şirket dışına çıkarılabilecek güvenli bir biçime dönüştüren temel maskeleme akışını tamamlamaktır.

Özellik geliştirme sırası:

1. Güvenlik açısından zorunlu maskeleme alanlarını tamamlamak.
2. Gerçek kod örnekleriyle manuel doğrulama yapmak.
3. Tespit edilen sözdizimi ve güvenlik hatalarını düzeltmek.
4. Şifreli eşleme kasasını tamamlamak.
5. Maskelenmiş kodun güvenli biçimde geri açılmasını sağlamak.
6. Kullanılabilir ilk sürümü tamamlamak.

Bu hedeflere doğrudan katkı sağlamayan çalışmalar ertelenecektir.