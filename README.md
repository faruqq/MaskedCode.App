# MaskedCode

MaskedCode; PL/I, EGL ve C# kaynak kodlarındaki hassas içeriklerin,
kod harici bir ortamda incelenmeden önce maskelenmesine yardımcı
olan Windows masaüstü uygulamasıdır.

Uygulama kaynak kodu yerel olarak işler, maskelenmiş çıktıyı üretir
ve gerektiğinde kodun şifreli eşleme kasası kullanılarak özgün hâline
döndürülmesini sağlar.

> Maskelenmiş kodun şirket dışındaki bir ortamda kullanılabilmesi
> kurumun güvenlik politikalarına ve açık izinlerine bağlıdır.
> MaskedCode tek başına paylaşım izni oluşturmaz.

## Desteklenen Diller

- PL/I
- EGL
- C# / .NET

## Temel Özellikler

- Kaynak kodu doğrudan editöre yapıştırma
- Kaynak kodu dosyadan yükleme
- PL/I, EGL ve C# için dile duyarlı maskeleme
- Maksimum Gizlilik ve Biçim Korumalı maskeleme yöntemleri
- Maskelenmiş kodu panoya kopyalama
- Maskelenmiş kodu dosyaya kaydetme
- Parolayla şifrelenmiş `.mcvault` kasası oluşturma
- Parolayı elle girme veya güvenli bir dosyadan kullanma
- Maskelenmiş kodu doğru kasa ve parolayla geri açma
- Geri açılan kodu kopyalama veya dosyaya kaydetme
- Satır numaraları ve bağlantılı editör kaydırma
- Editör genişliklerini değiştirme ve tek editörü büyütme
- İşlem, başarı ve hata durumlarını gösteren kullanıcı arayüzü

## Maskelenen İçerikler

Dile ve kullanıldığı bağlama göre aşağıdaki içerikler maskelenir:

- Kullanıcı tanımlı identifier’lar
- String değerleri
- Çalışma zamanı sayısal değerleri
- Yorum içerikleri

Dil anahtar kelimeleri, yerleşik tipler, yapısal sayılar, noktalama
işaretleri ve kaynak kodun temel sözdizimi korunur.

PL/I gömülü SQL yapıları desteklenir.

EGL tarafında ayrıca:

- `#doc` blokları
- `#sql` blokları
- DB2 SQL anahtar kelimeleri
- SQL stringleri ve yorumları

desteklenir.

C# tarafında Roslyn tabanlı sözdizimsel ve semantik analiz
kullanılır. Framework, BCL ve desteklenen xUnit sembolleri korunurken
kaynak koda ait kullanıcı tanımlı semboller maskelenir.

C# desteği aşağıdaki yapıları kapsar:

- Normal, verbatim, raw ve interpolated stringler
- Character literal değerleri
- Sayısal literal değerleri
- Satır ve blok yorumları
- XML dokümantasyon yorumları
- Preprocessor directive içerikleri
- Namespace, tip, üye, parametre ve yerel değişken isimleri
- Kullanıcı tanımlı attribute ve metot isimleri

## Maskeleme Yöntemleri

### Maksimum Gizlilik

Varsayılan ve önerilen yöntemdir.

Identifier ve değerlerin özgün uzunluğunu ve yapısını mümkün
olduğunca gizler.

### Biçim Korumalı

Aşağıdaki biçim özelliklerini korur:

- Değer uzunluğu
- Büyük ve küçük harf düzeni
- Harf ve rakam konumları
- Ayırıcı karakterler

Bu yöntem kaynak hakkında sınırlı biçim bilgisi gösterebildiği için
yalnızca biçimin korunması gerektiğinde kullanılmalıdır.

## Şifreli Eşleme Kasası

Özgün ve maskelenmiş değerlerin eşlemeleri düz metin olarak
saklanmaz. Eşlemeler parola ile şifrelenmiş `.mcvault` dosyasına
yazılır.

Kasa:

- Maskeleme yöntemini
- Kaynak dilini
- Maskelenmiş kodun SHA-256 özetini
- Maskeleme eşlemelerini

şifreli biçimde içerir.

Maskelenmiş kodu daha sonra geri açmak istiyorsan aynı maskeleme
işleminde oluşturulan kasa dosyasını kaydetmelisin.

Kasa kaydedilmezse kaynak kod yeniden maskelenebilir ancak daha önce
üretilmiş maskelenmiş kod özgün hâline döndürülemez.

## Kısa Kullanım

### Kod Maskeleme

1. **Kod Maskeleme** bölümünü aç.
2. Kaynak kodu yapıştır veya dosyadan yükle.
3. Kaynak dili ve maskeleme yöntemini seç.
4. En az 12 karakterlik kasa parolası gir veya parola dosyası seç.
5. **Ayarları Uygula ve Maskele** düğmesine bas.
6. Maskelenmiş çıktıyı kontrol et.
7. Çıktıyı kopyala veya dosyaya kaydet.
8. Kod daha sonra geri açılacaksa `.mcvault` dosyasını kaydet.

### Kodu Geri Açma

1. **Kodu Geri Aç** bölümünü aç.
2. Maskelenmiş kodu yapıştır veya dosyadan yükle.
3. Aynı işleme ait `.mcvault` dosyasını seç.
4. Doğru parolayı gir veya parola dosyasını seç.
5. **Ayarları Uygula ve Kodu Geri Aç** düğmesine bas.
6. Sonucu kontrol ederek kopyala veya dosyaya kaydet.

Ayrıntılı kullanım için `KullanimKilavuzu.md` dosyasına bak.

## Güvenlik Kuralları

- Maskelenmiş çıktıyı paylaşmadan önce manuel olarak incele.
- Mümkün olduğunda **Maksimum Gizlilik** yöntemini kullan.
- `.mcvault` dosyasını maskelenmiş kodla birlikte paylaşma.
- Kasa parolasını kasa dosyasının yanında saklama.
- Parola dosyasını kasa ve maskelenmiş koddan ayrı tut.
- Kasa veya parola kaybolursa kod geri açılamayabilir.
- MaskedCode’un dosya adı, klasör adı veya uygulama dışındaki
  metadata’yı maskelemediğini unutma.
- Uygulamayı kurumun güvenlik politikalarına uygun kullan.

## Gereksinimler

- Windows
- .NET 8
- Visual Studio 2022
- `.NET desktop development` workload’u

## Uygulamayı Çalıştırma

1. `MaskedCode.App.slnx` dosyasını Visual Studio ile aç.
2. `MaskedCode.App` projesini başlangıç projesi olarak seç.
3. **Build > Rebuild Solution** işlemini çalıştır.
4. Uygulamayı başlat.

## Dokümantasyon

- Kullanım kılavuzu: `KullanimKilavuzu.md`
- Teknik ve güvenlik tasarımı: `MaskedCode.md`
- Güncel geliştirme durumu: `ProjectState.md`