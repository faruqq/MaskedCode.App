# MaskedCode

MaskedCode, şirket kaynak kodunun harici yapay zekâ araçlarıyla
paylaşılmadan önce hassas içeriklerden arındırılmasına yardımcı olan
Windows masaüstü uygulamasıdır.

Uygulama kaynak kodu anonimleştirirken incelenebilir yapıyı mümkün
olduğunca korur ve gerektiğinde özgün kodun şifreli eşleme kasasıyla
geri açılmasını sağlar.

> Maskelenmiş bir kodun şirket dışına çıkarılabilmesi kurumun
> güvenlik politikalarına ve açık izinlerine bağlıdır. Bu uygulama
> tek başına paylaşım izni oluşturmaz.

## Mevcut Dil Desteği

Üretim desteğine sahip diller:

- PL/I
- EGL

C# / .NET seçeneği arayüzde görünmektedir ancak henüz
desteklenmemektedir.

## Maskelenebilen İçerikler

PL/I ve EGL için temel olarak:

- Identifier’lar
- String değerleri
- Çalışma zamanı sayısal değerleri
- Yorumlar

maskelenir.

Dil anahtar kelimeleri, veri tipi tanımları, yapısal sayılar,
noktalama işaretleri ve satır yapısı korunur.

PL/I gömülü SQL yapıları desteklenir.

EGL tarafında ayrıca:

- `#doc` blokları
- `#sql` blokları
- DB2 SQL anahtar kelimeleri
- SQL stringleri ve yorumları

desteklenir.

## Maskeleme Modları

### Maksimum Gizlilik

Varsayılan ve önerilen seçenektir.

Identifier ve değerlerin özgün uzunlukları ile biçimleri mümkün
olduğunca gizlenir.

### Biçim Korumalı

Aşağıdaki biçim özelliklerini korur:

- Değer uzunluğu
- Büyük-küçük harf düzeni
- Harf ve rakam konumları
- Ayırıcı karakterler

Bu mod kaynak hakkında sınırlı biçim bilgisi gösterebildiği için
yalnızca biçimin korunması gerçekten gerekli olduğunda
kullanılmalıdır.

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
2. Kaynak dili olarak **PL/I** veya **EGL** seç.
3. Kaynak kodu metin alanına yapıştır veya desteklenen bir dosya seç:
   - PL/I için `.pli` veya `.pl1`
   - EGL için `.egl`
4. **Maksimum Gizlilik** veya **Biçim Korumalı** yöntemini seç.
5. **Maskele** düğmesine bas.
6. Maskelenmiş kodu manuel olarak kontrol et.
7. En az 12 karakterlik güçlü bir kasa parolası gir.
8. Parolayı tekrar gir.
9. **Şifreli Kasayı Kaydet** düğmesiyle `.mcvault` dosyasını kaydet.
10. Maskelenmiş kodu kopyala veya ayrı bir dosyaya kaydet.

Şifreli kasa başarıyla kaydedilmeden maskelenmiş çıktı
paylaşılmamalıdır. Aksi durumda kodu geri açmak için gereken
eşlemeler kaybolabilir.

Aynı kaynak kod yeniden maskelendiğinde yeni rastgele eşlemeler
üretilebilir. Maskelenmiş kod ile kasanın aynı maskeleme işlemine
ait olması gerekir.

## Kodu Geri Açma

1. **Kodu Geri Aç** sekmesini aç.
2. Maskelenmiş dosyayı seç veya maskelenmiş kodu metin alanına
   yapıştır.
3. Aynı maskeleme işlemine ait `.mcvault` dosyasını seç.
4. Kasa parolasını gir.
5. **Kodu Geri Aç** düğmesine bas.
6. Geri açılan kodu kontrol et.
7. Sonucu kopyala veya dosya olarak kaydet.

Geri açma sırasında kaynak dili dosya uzantısından değil, kasadaki
dil bilgisinden belirlenir.

Sonuç dosyası için:

- PL/I kasasında `.pli`
- EGL kasasında `.egl`

uzantısı önerilir.

Kasa başka bir maskelenmiş koda aitse veya maskelenmiş kod
değiştirilmişse işlem reddedilir.

## Güvenlik Kuralları

- `.mcvault` dosyasını maskelenmiş kodla birlikte paylaşma.
- Kasa dosyasını güvenli ve ayrı bir konumda sakla.
- Kasa parolasını dosyanın yanında saklama.
- Güçlü ve benzersiz bir parola kullan.
- Kasa dosyası veya parola kaybolursa kodun geri açılması mümkün
  olmayabilir.
- Maskelenmiş çıktıyı paylaşmadan önce manuel olarak incele.
- Mümkün olan durumlarda **Maksimum Gizlilik** modunu kullan.
- Uygulamayı kullanmak kurum güvenlik politikasının yerine geçmez.

## Şifreli Kasa

Maskeleme eşlemeleri düz metin olarak saklanmaz. Eşlemeler, kaynak
dili ve maskelenmiş kodun özeti parola ile şifrelenmiş `.mcvault`
dosyasına yazılır.

Kasanın teknik ve kriptografik tasarımı `MaskedCode.md` dosyasında
açıklanmaktadır.

## Teknik Dokümantasyon

- Güncel geliştirme durumu: `ProjectState.md`
- Maskeleme ve güvenlik tasarımı: `MaskedCode.md`