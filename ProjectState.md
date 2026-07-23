# Project State

## Çalışma Kuralları

### Unit Test Yaklaşımı

- Kritik üretim kodları için unit test yazılacaktır.
- Maskeleme, şifreli eşleme kasası ve kodu geri açma davranışları otomatik testlerle doğrulanacaktır.
- Gerçekçi PL/I ve EGL kodları test girdisi olarak kullanılacaktır.
- WPF görünümü, dosya seçme pencereleri ve kullanıcı deneyimi unit test kapsamına alınmayacaktır.
- Arayüz için yalnızca kısa manuel smoke test uygulanacaktır.
- Test kodunda over-engineering yapılmayacaktır.
- Somut ihtiyaç oluşmadan mock, interface, test base class, fixture veya özel test altyapısı eklenmeyecektir.
- Öncelik, güvenlik ve veri kaybı riski taşıyan davranışların test edilmesidir.
- İlgili testler başarılı olmadan tamamlanan yapı için commit önerilmeyecektir.

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