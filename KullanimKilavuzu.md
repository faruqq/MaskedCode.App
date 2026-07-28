# MaskedCode Kullanım Kılavuzu

## MaskedCode Nedir?

MaskedCode; PL/I, EGL ve C# kaynak kodlarındaki hassas içerikleri
maskeleyerek kodun daha güvenli biçimde incelenmesine yardımcı olan
Windows masaüstü uygulamasıdır.

Uygulama temel olarak:

- Kullanıcı tanımlı isimleri
- Metin değerlerini
- Çalışma zamanı sayısal değerlerini
- Yorum içeriklerini

maskeler.

Dil anahtar kelimeleri, veri tipleri ve kaynak kodun çalışmasını
anlamak için gereken temel sözdizimi mümkün olduğunca korunur.

> MaskedCode kullanılması kaynak kodun paylaşılmasına tek başına izin
> vermez. Maskelenmiş kodun kullanılacağı ortam, kurumun güvenlik
> politikalarına uygun olmalıdır.

## Desteklenen Diller

- PL/I
- EGL
- C# / .NET

## Maskeleme Yöntemleri

### Maksimum Gizlilik

Varsayılan ve önerilen yöntemdir.

Maskelenen değerlerin özgün uzunluğunu ve yapısını mümkün olduğunca
gizler.

### Biçim Korumalı

Maskelenen değerlerde aşağıdaki özellikleri korur:

- Uzunluk
- Büyük ve küçük harf konumları
- Harf ve rakam konumları
- Ayırıcı karakterler

Bu yöntem kaynak kod hakkında sınırlı biçim bilgisi gösterebilir.
Yalnızca biçimin korunması gerektiğinde kullanılmalıdır.

## Kod Nasıl Maskelenir?

1. Sol menüden **Kod Maskeleme** bölümünü aç.
2. Kaynak kodu sol editöre yapıştır.
3. İstersen ayarlar bölümündeki **Dosya Seç** düğmesiyle kaynak
   dosyayı yükle.
4. **Maskeleme Ayarları** bölümünü aç.
5. Kaynak dili seç:
   - PL/I
   - EGL
   - C# / .NET
6. Maskeleme yöntemini seç:
   - Maksimum Gizlilik
   - Biçim Korumalı
7. Kasa parolası yöntemini seç:
   - Parolayı elle gir
   - Parolayı dosyadan kullan
8. En az 12 karakterlik bir parola gir veya yalnızca parolayı içeren
   güvenli metin dosyasını seç.
9. **Ayarları Uygula ve Maskele** düğmesine bas.
10. Oluşturulan maskelenmiş kodu sağ editörde kontrol et.
11. **Kopyala** düğmesiyle panoya kopyala veya **Kodu Kaydet**
    düğmesiyle dosyaya kaydet.
12. Maskelenmiş kodu ileride geri açacaksan **Kasa Dosyasını Kaydet**
    düğmesiyle `.mcvault` dosyasını kaydet.

Kaynak kod veya maskeleme yöntemi değiştirilirse önceki maskeleme
sonucu temizlenir. Yeni ayarlarla tekrar maskeleme yapılmalıdır.

## Şifreli Kasa Neden Gereklidir?

Şifreli kasa, özgün değerlerle maskelenmiş değerler arasındaki
eşlemeleri içerir.

Kasa yalnızca maskelenmiş kodun daha sonra özgün hâline döndürülmesi
gerekiyorsa kullanılır.

Maskelenmiş kodu sonradan geri açmak istiyorsan:

- Maskelenmiş kodu kaydet.
- Aynı işlemde oluşturulan `.mcvault` dosyasını kaydet.
- Kasa parolasını güvenli biçimde sakla.

Kasa kaydedilmezse aynı kaynak kod yeniden maskelenebilir ancak daha
önce oluşturulan maskelenmiş kod geri açılamaz.

Her maskeleme işleminde farklı eşlemeler üretilebileceği için
maskelenmiş kod ile kasa aynı işleme ait olmalıdır.

## Kod Nasıl Geri Açılır?

1. Sol menüden **Kodu Geri Aç** bölümünü aç.
2. Maskelenmiş kodu sol editöre yapıştır.
3. İstersen **Geri Açma Ayarları** bölümünden **Dosya Seç**
   düğmesiyle maskelenmiş dosyayı yükle.
4. Maskelenmiş kodla aynı işlemde oluşturulan `.mcvault` dosyasını
   seç.
5. Parola yöntemini seç:
   - Parolayı elle gir
   - Parolayı dosyadan kullan
6. Kasayı oluştururken kullanılan parolayı gir veya aynı parola
   dosyasını seç.
7. **Ayarları Uygula ve Kodu Geri Aç** düğmesine bas.
8. Geri açılan kodu sağ editörde kontrol et.
9. **Sonucu Kopyala** düğmesiyle panoya kopyala veya
   **Sonucu Kaydet** düğmesiyle dosyaya kaydet.

Kaynak dili ayrıca seçilmez. Uygulama doğru dili kasadaki bilgiden
belirler.

Aşağıdaki durumlarda kod geri açılmaz:

- Parola yanlışsa
- Kasa dosyası değiştirilmişse
- Kasa başka bir maskelenmiş koda aitse
- Maskelenmiş kod oluşturulduktan sonra değiştirilmişse

## Parola Dosyası Kullanımı

Parola elle yazılmak yerine güvenli bir metin dosyasından okunabilir.

Parola dosyası:

- Yalnızca parolayı içermelidir.
- En az 12 karakterlik bir parola içermelidir.
- Kasa ve maskelenmiş kodla aynı konumda saklanmamalıdır.
- Güvenilir olmayan bir ortama yüklenmemelidir.

## Editör Kullanımı

Her ekranda giriş ve sonuç editörleri yan yana bulunur.

- Bağlantılı kaydırma açıkken iki editör birlikte kaydırılır.
- Satır numaraları kaynak ve sonuç karşılaştırmasını kolaylaştırır.

## Güvenli Kullanım Kuralları

- Maskelenmiş çıktıyı paylaşmadan önce mutlaka kontrol et.
- Mümkün olduğunda **Maksimum Gizlilik** yöntemini kullan.
- `.mcvault` dosyasını maskelenmiş kodla birlikte paylaşma.
- Kasa parolasını kasa dosyasının yanında saklama.
- Kasa ve parola dosyalarını güvenli bir konumda tut.
- Kaynak kodun dosya adı ve klasör yolu uygulama tarafından
  maskelenmez.
- Maskelenmiş kodun iş mantığı hakkında bilgi gösterebileceğini
  unutma.
- Her zaman kurumun veri paylaşım ve yapay zekâ kullanım
  politikalarına uy.