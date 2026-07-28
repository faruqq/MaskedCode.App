# Project State

## Son Doğrulanan Commit

`8dc2840ba71e69a0674e84c35418eca8bdcdbd22`

Bu commit itibarıyla PL/I, EGL ve C# kaynak kodlarının maskelenmesi,
şifreli eşleme kasasının oluşturulması, kaynak diline göre geri açma
işlemi, parola dosyası desteği, WPF kullanıcı arayüzü ve ilgili
otomatik testler tamamlanmıştır.

Uygulamanın mevcut sürümü kullanılabilir durumdadır.

## Tamamlanan Özellikler

### Desteklenen Kaynak Dilleri

- PL/I
- EGL
- C# / .NET

Her üç dil için de:

- Maksimum Gizlilik maskelemesi
- Biçim Korumalı maskeleme
- Şifreli kasa oluşturma
- Kasa ve parola ile geri açma
- Kaynak diline uygun sonuç dosyası kaydetme

desteklenmektedir.

### WPF Arayüzü

- Kaynak kod doğrudan editöre yapıştırılabilir.
- Kaynak kod dosyadan yüklenebilir.
- PL/I, EGL veya C# kaynak dili seçilebilir.
- Maskeleme yöntemi seçilebilir.
- Parola elle girilebilir.
- Parola güvenli bir metin dosyasından okunabilir.
- Maskelenmiş kod ekranda görüntülenebilir.
- Maskelenmiş kod panoya kopyalanabilir.
- Maskelenmiş kod dosyaya kaydedilebilir.
- Şifreli eşleme kasası `.mcvault` olarak kaydedilebilir.
- Maskelenmiş dosya ve şifreli kasa seçilerek kod geri açılabilir.
- Geri açılan kod panoya kopyalanabilir.
- Geri açılan kod dosyaya kaydedilebilir.
- Geri açılan dosyanın uzantısı kasadaki kaynak diline göre belirlenir.
- İşlem, başarı ve hata durumları kullanıcıya gösterilir.

### Editör ve Görünüm Özellikleri

- Giriş ve sonuç editörleri yan yana gösterilir.
- Satır numaraları görüntülenir.
- Editörler bağlantılı olarak kaydırılabilir.
- Ortadaki ayırıcıyla editör genişlikleri değiştirilebilir.
- Ayırıcıya çift tıklanarak dengeli görünüme dönülebilir.
- Tek bir editör geçici olarak büyütülebilir.
- Ayarlar, içerik üzerinde açılan drawer yapısıyla yönetilir.
- Ayarlar açıkken arka editörlerle etkileşim engellenir.
- Karartılmış alana tıklanarak ayarlar kapatılabilir.
- Maskeleme işlemi sırasında loader animasyonu gösterilir.

Uygulama açılışında header animasyonu bir defa seçilir:

- Yüzde 10 olasılıkla MaskState
- Yüzde 90 olasılıkla H harfli ClassicVideo

### PL/I Maskeleme

Maskelenen içerikler:

- Identifier’lar
- String değerleri
- Çalışma zamanı sayısal değerleri
- Yorum içerikleri

Korunan yapılar:

- PL/I anahtar kelimeleri
- Veri tipi uzunlukları
- Precision ve scale değerleri
- Dizi boyutları
- Level number değerleri
- Declaration yapıları
- Bilimsel gösterimin sözdizimsel bölümleri
- Gömülü SQL anahtar kelimeleri
- Noktalama, boşluk ve satır sonları

`INIT(...)` içindeki çalışma zamanı başlangıç değerleri maskelenir.

PL/I identifier eşlemeleri büyük-küçük harf duyarsızdır.

### EGL Maskeleme

Maskelenen içerikler:

- Kullanıcı tanımlı identifier’lar
- String değerleri
- Çalışma zamanı sayısal değerleri
- Satır ve blok yorumları
- `#doc` bloklarının hassas içerikleri
- `#sql` içindeki kullanıcı tanımlı identifier’lar
- SQL string değerleri ve yorumları

Korunan yapılar:

- EGL anahtar kelimeleri
- Yerleşik EGL veri tipleri
- Desteklenen metadata property adları
- Desteklenen sistem kökleri ve üyeleri
- `main` giriş noktası
- Yapısal sayılar
- `#doc` ve `#sql` directive yapıları
- DB2 SQL anahtar kelimeleri
- SQL isolation level değerleri
- Noktalama, boşluk ve satır sonları

EGL identifier eşlemeleri büyük-küçük harfe duyarlıdır.

Güvenli biçimde ayrıştırılamayan hassas EGL bağlamlarında kısmi sonuç
üretilmez. İşlem açık bir hatayla durdurulur.

### C# Maskeleme

C# kaynak kodu Roslyn tabanlı sözdizimsel ve semantik analizle
işlenmektedir.

Maskelenen içerikler:

- Namespace bölümleri
- Kullanıcı tanımlı tip adları
- Metot ve constructor adları
- Property, field ve event adları
- Parametreler ve yerel değişkenler
- Generic type parameter adları
- Kullanıcı tanımlı attribute ve üye adları
- String ve character literal değerleri
- Çalışma zamanı sayısal literal değerleri
- Satır, blok ve XML dokümantasyon yorumları
- Preprocessor directive içindeki maskelenebilir içerikler

Desteklenen literal yapıları:

- Normal string
- Verbatim string
- Raw string
- Interpolated string
- Character literal
- C# sayısal literal biçimleri

Korunan yapılar:

- C# anahtar kelimeleri
- Bağlamsal anahtar kelimeler
- Yerleşik tipler
- .NET runtime ve BCL sembolleri
- Desteklenen framework sembolleri
- Desteklenen xUnit sembolleri
- Noktalama, boşluk ve satır yapısı

Runtime metadata reference’ları kullanılarak framework sembolleri
kaynak sembollerinden ayrılır.

Yaygın .NET implicit using’leri ile xUnit global using’i semantik
analiz sırasında yardımcı syntax tree olarak sağlanır. Bu yardımcı
dosya kullanıcının koduna veya maskelenmiş çıktıya eklenmez.

Bu sayede kaynak dosyada açık `using` bulunmadığında da aşağıdaki
gibi semboller korunabilir:

- `Environment.NewLine`
- `Task`
- `InvalidOperationException`
- `Fact`
- `Theory`
- `InlineData`
- `Assert.Equal`
- `Assert.ThrowsAsync`

Kaynak kodun kendi tanımladığı semboller framework sembolleriyle aynı
ada sahip olsa bile maskelenir.

### Maskeleme Modları

İki maskeleme modu desteklenmektedir:

1. `MaximumPrivacy`
   - Arayüzde `Maksimum Gizlilik` olarak gösterilir.
   - Varsayılan ve önerilen moddur.
   - Değerlerin uzunluğunu ve yapısını mümkün olduğunca gizler.

2. `FormatPreserving`
   - Arayüzde `Biçim Korumalı` olarak gösterilir.
   - Uzunluğu korur.
   - Büyük-küçük harf düzenini korur.
   - Harf ve rakam konumlarını korur.
   - Ayırıcı karakterleri korur.
   - Kaynak hakkında sınırlı biçim bilgisi gösterebilir.

Güvenlik öncelikli kullanımlarda varsayılan tercih
`MaximumPrivacy` olmalıdır.

### Şifreli Eşleme Kasası

Maskeleme eşlemeleri düz metin olarak saklanmaz.

Kasa güvenliği:

- AES-256-GCM
- PBKDF2-HMAC-SHA256
- 600.000 PBKDF2 iterasyonu
- Her kasa için rastgele salt
- Her kasa için rastgele nonce
- Authentication tag doğrulaması
- En az 12 karakterlik parola
- Maskelenmiş kodun SHA-256 özeti
- Kasa-kod eşleşmesi doğrulaması
- En fazla 64 MB kasa dosyası

üzerine kuruludur.

Kasa içeriğinde aşağıdaki bilgiler korunur:

- Maskeleme modu
- Kaynak dili
- Maskelenmiş kod özeti
- Maskeleme eşlemeleri

Desteklenen kaynak dili değerleri:

- `Pl1`
- `Egl`
- `CSharp`

Kaynak dili alanı bulunmayan eski kasalar geriye dönük uyumluluk için
PL/I kasası olarak değerlendirilir.

### Parola Kullanımı

Kasa parolası:

- Kullanıcı tarafından elle girilebilir.
- Yalnızca parolayı içeren güvenli bir metin dosyasından okunabilir.
- En az 12 karakter olmalıdır.

Parola dosyasının kasa ve maskelenmiş kodla aynı konumda tutulmaması
önerilir.

### Kodu Geri Açma

Geri açma işlemi:

- Maskelenmiş kodu
- Aynı işleme ait şifreli kasayı
- Doğru kasa parolasını

birlikte gerektirir.

Kasa açıldıktan sonra doğru geri açıcı kasadaki kaynak diline göre
seçilir:

- `SourceLanguage.Pl1` için PL/I
- `SourceLanguage.Egl` için EGL
- `SourceLanguage.CSharp` için C#

Dosyanın fiziksel uzantısı kaynak dilini belirlemek için kullanılmaz.

Geri açılan dosya için önerilen uzantılar:

- PL/I için `.pli`
- EGL için `.egl`
- C# için `.cs`

Aşağıdaki durumlarda işlem reddedilir:

- Yanlış parola
- Boş veya geçersiz kasa
- Değiştirilmiş şifreli içerik
- Başka maskelenmiş koda ait kasa
- Değiştirilmiş maskelenmiş kod
- Maskelenmiş kodda bulunmayan kasa eşlemesi
- Çakışan eşlemeler
- Eksik veya geçersiz geri açma verisi
- Geçersiz veya desteklenmeyen kaynak dili

Başarılı işlem sonunda kaynak kod özgün hâline döndürülür.

## Otomatik Test Durumu

`MaskedCode.App.Tests` projesi bulunmaktadır.

### PL/I Test Kapsamı

- İki maskeleme modunda maskeleme ve geri açma
- Yanlış parolanın reddedilmesi
- Değiştirilmiş kasanın reddedilmesi
- Değiştirilmiş maskelenmiş kodun reddedilmesi
- Başka koda ait kasanın reddedilmesi
- Declaration içindeki yapısal sayılar
- `INIT` değerleri
- Yorumlar
- Escaped quote içeren stringler
- Procedure identifier’ları
- Bilimsel gösterim
- PL/I ve gömülü SQL anahtar kelimeleri

### EGL Test Kapsamı

- Kullanıcı tanımlı identifier’ların maskelenmesi
- Büyük-küçük harf bakımından farklı identifier’ların ayrılması
- İki maskeleme modu
- Tekrarlanan string eşlemeleri
- Escaped quote içeren stringler
- Satır ve blok yorumları
- String içindeki yorum işaretleri
- Çalışma zamanı ve yapısal sayıların ayrılması
- `#doc` blokları
- `#sql` blokları
- SQL stringleri ve yorumları
- DB2 SQL anahtar kelimeleri
- SQL isolation level değerleri
- Sonlandırılmamış blok ve stringlerin reddedilmesi
- Karakter karakter geri açma
- Fazladan ve çakışan eşlemelerin reddedilmesi

### C# Test Kapsamı

- İki maskeleme modu
- Kullanıcı tanımlı identifier’ların maskelenmesi
- Tanım ve referansların tutarlı değiştirilmesi
- Namespace, tip, üye, parametre ve yerel değişkenler
- Normal, verbatim, raw ve interpolated stringler
- Character literal değerleri
- Sayısal literal değerleri
- Satır, blok ve XML dokümantasyon yorumları
- Preprocessor directive içerikleri
- Framework ve BCL sembollerinin korunması
- xUnit sembollerinin korunması
- Implicit ve project global using senaryoları
- Kaynakta tanımlanan aynı adlı sembollerin maskelenmesi
- Maskelenmiş çıktının geçerli C# olarak ayrıştırılması
- C# kasasıyla karakter karakter geri açma

### Ortak Kasa Test Kapsamı

- PL/I, EGL ve C# kaynak dilinin kasada korunması
- Yanlış parola
- Değiştirilmiş kasa
- Değiştirilmiş maskelenmiş kod
- Yanlış kasa-kod çifti
- Eşlemesi bulunmayan sonuç
- Çakışan eşlemeler
- Geçersiz kaynak dili
- Eski kasaların PL/I olarak okunması
- Kaynak diline uygun geri açıcı seçimi

Tamamlanan geliştirme paketlerinde solution build’i ve ilgili
otomatik testler başarıyla doğrulanmıştır.

## Mevcut Kullanılabilir Sürüm Kapsamı

Mevcut sürüm aşağıdaki kapsamı desteklemektedir:

- Windows WPF masaüstü uygulaması
- PL/I kaynak kodu desteği
- EGL kaynak kodu desteği
- C# / .NET kaynak kodu desteği
- Maksimum Gizlilik maskelemesi
- Biçim Korumalı maskeleme
- Identifier, string, sayı ve yorum maskelemesi
- PL/I gömülü SQL desteği
- EGL `#doc` ve `#sql` desteği
- C# Roslyn tabanlı semantik analiz
- Framework, BCL ve xUnit sembollerinin korunması
- Parolayla şifrelenmiş `.mcvault` kasası
- Manuel parola ve parola dosyası seçenekleri
- Kaynak diline göre doğru geri açma
- Kasa bütünlüğü ve kasa-kod eşleşmesi kontrolleri
- Eski PL/I kasalarıyla geriye dönük uyumluluk
- Dosya yükleme, kaydetme ve panoya kopyalama
- Geliştirilmiş çift editör kullanıcı arayüzü
- Ayarlar drawer’ları ve işlem animasyonları

## Bilinen Sınırlar

- Maskelenmiş çıktı paylaşılmadan önce kullanıcı tarafından
  incelenmelidir.
- Uygulama dosya ve klasör adlarını maskelemez.
- Maskelenmiş kod iş mantığı hakkında sınırlı bilgi gösterebilir.
- Harici kütüphanelerdeki bütün sembollerin framework sembolü olarak
  otomatik korunması garanti edilmez.
- Kasa kaydedilmezse daha önce üretilen maskelenmiş kod geri açılamaz.
- Uygulama şu anda kaydedilmemiş kasa için çıkış veya yeni işlem
  uyarısı göstermez.
- Toplu dosya maskeleme desteklenmez.
- Kurulum paketi ve otomatik güncelleme mekanizması bulunmaz.

Kaydedilmemiş kasa koruması mevcut sürüm için zorunlu iş olarak
değerlendirilmemiştir. Kullanıcıya kasanın hangi durumda saklanması
gerektiği kullanım kılavuzunda açıklanmaktadır.

## Dokümantasyon Durumu

Aşağıdaki dokümanlar güncel uygulama durumuna göre hazırlanmıştır:

- `README.md`
- `KullanimKilavuzu.md`
- `MaskedCode.md`
- `ProjectState.md`

Dokümanlar:

- Desteklenen üç kaynak dilini
- Maskeleme ve geri açma akışlarını
- Şifreli kasanın kullanımını
- Parola dosyası desteğini
- Güncel WPF arayüzünü
- Teknik ve operasyonel güvenlik sınırlarını

kapsamaktadır.

## Sıradaki Aşama

PL/I, EGL ve C# için planlanan temel uygulama kapsamı tamamlanmıştır.

Yeni bir zorunlu geliştirme aşaması bulunmamaktadır.

İleride ihtiyaç oluşursa değerlendirilebilecek isteğe bağlı özellikler:

- Kaydedilmemiş kasa uyarısı
- Panonun belirli süre sonra temizlenmesi
- Sürükle-bırakla dosya açma
- Son kullanılan ayarları hatırlama
- Toplu dosya maskeleme
- Kurulum paketi
- Otomatik güncelleme

Bu maddeler mevcut sürümün kullanılmasına engel değildir.

## Çalışma Kuralları

### Kod Paylaşım Standardı

- Değiştirilecek metodun tamamı verilmelidir.
- Metot adı ve bütün parametreleri aynı satırda yazılmalıdır.
- Metot imzasındaki parametreler alt satırlara bölünmemelidir.
- Aynı dosyadaki birden fazla mevcut metot ayrı snippet’lerde
  verilmelidir.
- Yeni ve aynı konuma birlikte eklenecek metotlar gerektiğinde aynı
  snippet içinde verilebilir.
- Büyük ölçüde değişen dosyalar parçalı olarak değil, tamamen
  verilmelidir.
- Eksik veya parçalı metot gövdesi verilmemelidir.
- Her değişiklik için dosya adı ve uygulanacağı yer belirtilmelidir.
- Her paket en fazla 4–5 kod veya doküman snippet’i içermelidir.
- Mantıksal paket sonunda commit adı verilmelidir.
- Kullanıcı yeni commit hash’ini paylaşmadan sonraki pakete
  geçilmemelidir.
- Kullanıcı `devam edelim` dediğinde son işlemin sorunsuz tamamlandığı
  kabul edilmelidir.
- İşlemler Visual Studio üzerinden anlatılmalıdır.
- Gereksiz over-engineering yapılmamalıdır.

### Test Yaklaşımı

- Güvenlik veya veri kaybı riski taşıyan üretim davranışları
  otomatik test edilmelidir.
- Test kodunda gereksiz mock, interface, base class, fixture veya
  özel test altyapısı oluşturulmamalıdır.
- WPF görünümü ve dosya seçme pencereleri unit test kapsamına
  alınmamalıdır.
- İlgili otomatik testler başarılı olmadan üretim kodu paketi
  tamamlanmış kabul edilmemelidir.