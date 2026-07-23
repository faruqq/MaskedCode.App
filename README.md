# MaskedCode

MaskedCode, şirket kaynak kodunun harici yapay zekâ araçlarıyla
paylaşılmadan önce hassas içeriklerden arındırılmasına yardımcı olan
Windows masaüstü uygulamasıdır.

Uygulamanın amacı kaynak kodu anonimleştirirken kodun incelenebilir
yapısını mümkün olduğunca korumak ve gerektiğinde özgün kodu güvenli
biçimde geri açabilmektir.

> Maskelenmiş bir kodun şirket dışına çıkarılabilmesi, kurumun güvenlik
> politikalarına ve açık izinlerine bağlıdır. Bu uygulama tek başına
> paylaşım izni oluşturmaz.

## Mevcut Destek

Şu anda yalnızca PL/I kaynak kodu desteklenmektedir.

Maskelenebilen içerikler:

- Identifier’lar
- String değerleri
- Çalışma zamanı sayısal değerleri
- Yorumlar

Korunan yapılar:

- PL/I anahtar kelimeleri
- Declaration veri uzunlukları
- Dizi boyutları
- Sayısal veri tipi tanımları
- Bilimsel gösterimin exponent bölümü
- Satır yapısı ve sözdizimsel ayırıcılar

Arayüzde görünen EGL ve C# seçenekleri henüz üretim desteğine sahip
değildir.

## Maskeleme Modları

### Maksimum Gizlilik

Varsayılan ve önerilen seçenektir.

Identifier ve değerlerin uzunlukları ile biçimleri mümkün olduğunca
gizlenir.

### Biçim Korumalı

Aşağıdaki biçim özelliklerini korur:

- Değer uzunluğu
- Büyük-küçük harf düzeni
- Harf ve rakam konumları
- Ayırıcı karakterler

Bu mod kaynak kod hakkında sınırlı biçim bilgisi gösterebildiği için
yalnızca biçimin korunması gerçekten gerekli olduğunda kullanılmalıdır.

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

## Kod Maskeleme

1. **Kod Maskeleme** sekmesini aç.
2. PL/I kodunu metin alanına yapıştır veya bir `.pli` / `.pl1`
   dosyası seç.
3. **Maksimum Gizlilik** veya **Biçim Korumalı** yöntemini seç.
4. **Maskele** düğmesine bas.
5. Maskelenmiş kodu kontrol et.
6. En az 12 karakterlik güçlü bir kasa parolası gir.
7. Parolayı tekrar gir.
8. **Şifreli Kasayı Kaydet** düğmesiyle `.mcvault` dosyasını kaydet.
9. Maskelenmiş kodu kopyala veya ayrı bir dosyaya kaydet.

Şifreli kasa başarıyla kaydedilmeden maskelenmiş çıktı paylaşılmamalıdır.
Aksi durumda kodu geri açmak için gereken eşlemeler kaybolabilir.

## Kodu Geri Açma

1. **Kodu Geri Aç** sekmesini aç.
2. Maskelenmiş PL/I dosyasını seç veya kodu metin alanına yapıştır.
3. O koda ait `.mcvault` dosyasını seç.
4. Kasa parolasını gir.
5. **Kodu Geri Aç** düğmesine bas.
6. Sonucu kopyala veya dosya olarak kaydet.

Kasa başka bir maskelenmiş koda aitse işlem reddedilir.

## Güvenlik Kuralları

- `.mcvault` dosyasını maskelenmiş kodla birlikte paylaşma.
- Kasa dosyasını güvenli ve ayrı bir konumda sakla.
- Kasa parolasını dosyanın yanında saklama.
- Güçlü ve benzersiz bir parola kullan.
- Kasa dosyası veya parola kaybolursa kodun geri açılması mümkün olmaz.
- Maskelenmiş çıktıyı paylaşmadan önce manuel olarak incele.
- Uygulamayı kullanmak kurum güvenlik politikasının yerine geçmez.

## Şifreli Kasa

Maskeleme eşlemeleri düz metin olarak saklanmaz; parola ile şifrelenmiş
`.mcvault` dosyasına yazılır.

Kasanın teknik ve kriptografik tasarımı `MaskedCode.md` dosyasında
açıklanmaktadır.

## Teknik Dokümantasyon

- Güncel geliştirme durumu: `ProjectState.md`
- Maskeleme ve güvenlik tasarımı: `MaskedCode.md`