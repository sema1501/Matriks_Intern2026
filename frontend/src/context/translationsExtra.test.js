import extraTranslations from './translationsExtra';

// Bir dile eklenen her metnin diğer dilde de karşılığı olmalı, yoksa ekranda anahtar adı görünür.
test('TR ve EN çevirilerinde aynı anahtarlar var', () => {
  const trKeys = Object.keys(extraTranslations.tr).sort();
  const enKeys = Object.keys(extraTranslations.en).sort();
  expect(enKeys).toEqual(trKeys);
});

test('{ad} yer tutucuları iki dilde aynı', () => {
  const placeholders = (text) => (text.match(/\{\w+\}/g) || []).sort();
  Object.keys(extraTranslations.tr).forEach((key) => {
    expect([key, placeholders(extraTranslations.en[key])]).toEqual([key, placeholders(extraTranslations.tr[key])]);
  });
});
