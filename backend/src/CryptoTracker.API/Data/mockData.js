// mockData.js
// Sahte (mock) kripto para verisi - gercek bir API'ye bagli degildir.
// Gorev 1-4 icin kullanilacak statik veri kaynagi.
//
// Alanlar:
//   id                        -> coin'in benzersiz kimligi (route'larda /coin/:id icin kullanilir)
//   rank                      -> market cap siralamasi
//   name, symbol              -> coin adi ve kisaltmasi
//   image                     -> CoinGecko'nun herkese acik gorsel CDN linki (sadece logo gorseli, veri cekme yok)
//   currentPrice              -> USD fiyati
//   priceChangePercentage24h  -> son 24 saatteki yuzde degisim (pozitif/negatif)
//   marketCap, volume24h      -> piyasa degeri ve 24s islem hacmi
//   high24h, low24h           -> son 24 saatin en yuksek/en dusuk fiyati
//   circulatingSupply         -> dolasimdaki arz
//   sparkline7d               -> son 7 gun icin 7 sayilik rastgele seri (Gorev 4'teki
//                                 detay sayfasi grafigini gorsel olarak doldurmak icindir,
//                                 gercek fiyat gecmisi degildir)

const mockCoins = [
  {
    "id": "bitcoin",
    "rank": 1,
    "name": "Bitcoin",
    "symbol": "BTC",
    "image": "https://assets.coingecko.com/coins/images/1/large/bitcoin.png",
    "currentPrice": 66532.14,
    "priceChangePercentage24h": -2.99,
    "marketCap": 1292433808489.2,
    "volume24h": 109667322195.82,
    "high24h": 68164.86,
    "low24h": 65820.07,
    "circulatingSupply": 19425706.26,
    "sparkline7d": [
      66532.14,
      63003.27,
      63059.48,
      59559.65,
      59085.4,
      56035.57,
      53283.42
    ]
  },
  {
    "id": "ethereum",
    "rank": 2,
    "name": "Ethereum",
    "symbol": "ETH",
    "image": "https://assets.coingecko.com/coins/images/279/large/ethereum.png",
    "currentPrice": 3522.88,
    "priceChangePercentage24h": -1.28,
    "marketCap": 419065622450.72,
    "volume24h": 72144804354.45,
    "high24h": 3540.87,
    "low24h": 3501.3,
    "circulatingSupply": 118955406.5,
    "sparkline7d": [
      3522.88,
      3555.47,
      3511.39,
      3712.07,
      3510.1,
      3661.09,
      3568.66
    ]
  },
  {
    "id": "binancecoin",
    "rank": 3,
    "name": "BNB",
    "symbol": "BNB",
    "image": "https://assets.coingecko.com/coins/images/825/large/bnb-icon2_2x.png",
    "currentPrice": 575.57,
    "priceChangePercentage24h": -6.05,
    "marketCap": 81911691634.44,
    "volume24h": 9603328738.1,
    "high24h": 594.61,
    "low24h": 542.39,
    "circulatingSupply": 142314039.36,
    "sparkline7d": [
      575.57,
      585.16,
      576.2,
      579.51,
      549.1,
      520.08,
      501.73
    ]
  },
  {
    "id": "solana",
    "rank": 4,
    "name": "Solana",
    "symbol": "SOL",
    "image": "https://assets.coingecko.com/coins/images/4128/large/solana.png",
    "currentPrice": 144.79,
    "priceChangePercentage24h": 3.07,
    "marketCap": 66507710187.2,
    "volume24h": 4985753788.28,
    "high24h": 147.24,
    "low24h": 141.37,
    "circulatingSupply": 459339113.11,
    "sparkline7d": [
      144.79,
      149.9,
      153.48,
      148.77,
      150.1,
      150.55,
      157.33
    ]
  },
  {
    "id": "ripple",
    "rank": 5,
    "name": "XRP",
    "symbol": "XRP",
    "image": "https://assets.coingecko.com/coins/images/44/large/xrp-symbol-white-128.png",
    "currentPrice": 0.61737,
    "priceChangePercentage24h": 3.9,
    "marketCap": 32937999606.49,
    "volume24h": 4728946134.21,
    "high24h": 0.643473,
    "low24h": 0.607873,
    "circulatingSupply": 53352122076.69,
    "sparkline7d": [
      0.61737,
      0.591587,
      0.590803,
      0.558134,
      0.5694,
      0.587478,
      0.592626
    ]
  },
  {
    "id": "cardano",
    "rank": 6,
    "name": "Cardano",
    "symbol": "ADA",
    "image": "https://assets.coingecko.com/coins/images/975/large/cardano.png",
    "currentPrice": 0.448324,
    "priceChangePercentage24h": 6.38,
    "marketCap": 15670649267.69,
    "volume24h": 1542474539.16,
    "high24h": 0.472815,
    "low24h": 0.426142,
    "circulatingSupply": 34953848706.95,
    "sparkline7d": [
      0.448324,
      0.466614,
      0.491513,
      0.489985,
      0.499637,
      0.473296,
      0.48474
    ]
  },
  {
    "id": "dogecoin",
    "rank": 7,
    "name": "Dogecoin",
    "symbol": "DOGE",
    "image": "https://assets.coingecko.com/coins/images/5/large/dogecoin.png",
    "currentPrice": 0.121183,
    "priceChangePercentage24h": 2.5,
    "marketCap": 17366707435.94,
    "volume24h": 2262845637.03,
    "high24h": 0.124084,
    "low24h": 0.119584,
    "circulatingSupply": 143309766517.92,
    "sparkline7d": [
      0.121183,
      0.11424,
      0.113715,
      0.109185,
      0.104168,
      0.098655,
      0.101831
    ]
  },
  {
    "id": "avalanche-2",
    "rank": 8,
    "name": "Avalanche",
    "symbol": "AVAX",
    "image": "https://assets.coingecko.com/coins/images/12559/large/Avalanche_Circle_RedWhite_Trans.png",
    "currentPrice": 28.36,
    "priceChangePercentage24h": -6.3,
    "marketCap": 11306881014.0,
    "volume24h": 1101042704.75,
    "high24h": 29.45,
    "low24h": 26.58,
    "circulatingSupply": 398691150.0,
    "sparkline7d": [
      28.36,
      28.53,
      29.84,
      30.98,
      32.34,
      31.48,
      31.16
    ]
  },
  {
    "id": "polkadot",
    "rank": 9,
    "name": "Polkadot",
    "symbol": "DOT",
    "image": "https://assets.coingecko.com/coins/images/12171/large/polkadot.png",
    "currentPrice": 6.85,
    "priceChangePercentage24h": -2.4,
    "marketCap": 9620226350.21,
    "volume24h": 623328424.85,
    "high24h": 7.03,
    "low24h": 6.78,
    "circulatingSupply": 1404412605.87,
    "sparkline7d": [
      6.85,
      6.63,
      6.62,
      6.69,
      6.5,
      6.11,
      6.05
    ]
  },
  {
    "id": "chainlink",
    "rank": 10,
    "name": "Chainlink",
    "symbol": "LINK",
    "image": "https://assets.coingecko.com/coins/images/877/large/chainlink-new-logo.png",
    "currentPrice": 14.22,
    "priceChangePercentage24h": -2.22,
    "marketCap": 8772331211.94,
    "volume24h": 1075829159.14,
    "high24h": 14.56,
    "low24h": 13.95,
    "circulatingSupply": 616900929.11,
    "sparkline7d": [
      14.22,
      14.52,
      13.74,
      14.4,
      14.89,
      15.56,
      16.11
    ]
  },
  {
    "id": "polygon",
    "rank": 11,
    "name": "Polygon",
    "symbol": "MATIC",
    "image": "https://assets.coingecko.com/coins/images/4713/large/polygon.png",
    "currentPrice": 0.848283,
    "priceChangePercentage24h": -1.83,
    "marketCap": 8161744368.35,
    "volume24h": 327303434.7,
    "high24h": 0.854226,
    "low24h": 0.835749,
    "circulatingSupply": 9621487603.02,
    "sparkline7d": [
      0.848283,
      0.818637,
      0.785463,
      0.770387,
      0.729024,
      0.685303,
      0.656624
    ]
  },
  {
    "id": "litecoin",
    "rank": 12,
    "name": "Litecoin",
    "symbol": "LTC",
    "image": "https://assets.coingecko.com/coins/images/2/large/litecoin.png",
    "currentPrice": 78.09,
    "priceChangePercentage24h": -6.78,
    "marketCap": 5765785192.37,
    "volume24h": 301450084.07,
    "high24h": 79.79,
    "low24h": 72.8,
    "circulatingSupply": 73835128.6,
    "sparkline7d": [
      78.09,
      75.77,
      74.38,
      73.17,
      69.86,
      72.78,
      77.09
    ]
  },
  {
    "id": "tron",
    "rank": 13,
    "name": "TRON",
    "symbol": "TRX",
    "image": "https://assets.coingecko.com/coins/images/1094/large/tron-logo.png",
    "currentPrice": 0.109964,
    "priceChangePercentage24h": -0.58,
    "marketCap": 9539893627.43,
    "volume24h": 665059696.44,
    "high24h": 0.110199,
    "low24h": 0.109721,
    "circulatingSupply": 86754698150.54,
    "sparkline7d": [
      0.109964,
      0.114303,
      0.109659,
      0.103383,
      0.108978,
      0.109348,
      0.104711
    ]
  },
  {
    "id": "uniswap",
    "rank": 14,
    "name": "Uniswap",
    "symbol": "UNI",
    "image": "https://assets.coingecko.com/coins/images/12504/large/uniswap-uni.png",
    "currentPrice": 9.31,
    "priceChangePercentage24h": 0.73,
    "marketCap": 5563096008.62,
    "volume24h": 747844314.39,
    "high24h": 9.36,
    "low24h": 9.24,
    "circulatingSupply": 597539850.55,
    "sparkline7d": [
      9.31,
      9.04,
      8.9,
      8.54,
      8.82,
      8.86,
      9.15
    ]
  },
  {
    "id": "stellar",
    "rank": 15,
    "name": "Stellar",
    "symbol": "XLM",
    "image": "https://assets.coingecko.com/coins/images/100/large/Stellar_symbol_black_RGB.png",
    "currentPrice": 0.139225,
    "priceChangePercentage24h": -2.9,
    "marketCap": 4213727806.99,
    "volume24h": 635901196.31,
    "high24h": 0.143057,
    "low24h": 0.134832,
    "circulatingSupply": 30265597464.46,
    "sparkline7d": [
      0.139225,
      0.144543,
      0.148704,
      0.143828,
      0.144132,
      0.141634,
      0.133629
    ]
  },
  {
    "id": "monero",
    "rank": 16,
    "name": "Monero",
    "symbol": "XMR",
    "image": "https://assets.coingecko.com/coins/images/69/large/monero_logo.png",
    "currentPrice": 164.27,
    "priceChangePercentage24h": -8.03,
    "marketCap": 3018624915.49,
    "volume24h": 293060639.12,
    "high24h": 170.96,
    "low24h": 153.0,
    "circulatingSupply": 18375996.32,
    "sparkline7d": [
      164.27,
      172.88,
      183.01,
      193.0,
      189.87,
      183.5,
      177.48
    ]
  },
  {
    "id": "cosmos",
    "rank": 17,
    "name": "Cosmos",
    "symbol": "ATOM",
    "image": "https://assets.coingecko.com/coins/images/1481/large/cosmos_hub.png",
    "currentPrice": 8.85,
    "priceChangePercentage24h": -5.16,
    "marketCap": 3443785532.26,
    "volume24h": 350994113.24,
    "high24h": 9.21,
    "low24h": 8.38,
    "circulatingSupply": 389128308.73,
    "sparkline7d": [
      8.85,
      9.01,
      9.34,
      8.87,
      9.04,
      9.49,
      9.81
    ]
  },
  {
    "id": "ethereum-classic",
    "rank": 18,
    "name": "Ethereum Classic",
    "symbol": "ETC",
    "image": "https://assets.coingecko.com/coins/images/453/large/ethereum-classic-logo.png",
    "currentPrice": 26.69,
    "priceChangePercentage24h": 4.25,
    "marketCap": 3844865338.69,
    "volume24h": 577204777.53,
    "high24h": 27.19,
    "low24h": 25.63,
    "circulatingSupply": 144056400.85,
    "sparkline7d": [
      26.69,
      28.2,
      27.85,
      27.52,
      28.99,
      29.78,
      28.6
    ]
  },
  {
    "id": "filecoin",
    "rank": 19,
    "name": "Filecoin",
    "symbol": "FIL",
    "image": "https://assets.coingecko.com/coins/images/12817/large/filecoin.png",
    "currentPrice": 5.56,
    "priceChangePercentage24h": -6.34,
    "marketCap": 3250549124.83,
    "volume24h": 500508410.64,
    "high24h": 5.92,
    "low24h": 5.23,
    "circulatingSupply": 584631137.56,
    "sparkline7d": [
      5.56,
      5.88,
      5.99,
      5.88,
      5.92,
      5.66,
      5.33
    ]
  },
  {
    "id": "aptos",
    "rank": 20,
    "name": "Aptos",
    "symbol": "APT",
    "image": "https://assets.coingecko.com/coins/images/26455/large/aptos_round.png",
    "currentPrice": 11.33,
    "priceChangePercentage24h": 8.01,
    "marketCap": 5902902468.55,
    "volume24h": 948959096.29,
    "high24h": 11.98,
    "low24h": 10.38,
    "circulatingSupply": 520997570.04,
    "sparkline7d": [
      11.33,
      11.77,
      11.37,
      11.03,
      10.75,
      10.42,
      10.53
    ]
  },
  {
    "id": "near",
    "rank": 21,
    "name": "NEAR Protocol",
    "symbol": "NEAR",
    "image": "https://assets.coingecko.com/coins/images/10365/large/near.jpg",
    "currentPrice": 5.09,
    "priceChangePercentage24h": -4.09,
    "marketCap": 5889760928.15,
    "volume24h": 581461629.38,
    "high24h": 5.17,
    "low24h": 4.88,
    "circulatingSupply": 1157123954.45,
    "sparkline7d": [
      5.09,
      5.14,
      5.39,
      5.34,
      5.61,
      5.61,
      5.63
    ]
  },
  {
    "id": "internet-computer",
    "rank": 22,
    "name": "Internet Computer",
    "symbol": "ICP",
    "image": "https://assets.coingecko.com/coins/images/14495/large/Internet_Computer_logo.png",
    "currentPrice": 10.7,
    "priceChangePercentage24h": 0.4,
    "marketCap": 5190131163.36,
    "volume24h": 777873853.89,
    "high24h": 10.73,
    "low24h": 10.68,
    "circulatingSupply": 485058987.23,
    "sparkline7d": [
      10.7,
      10.28,
      10.25,
      10.52,
      10.59,
      10.37,
      10.4
    ]
  },
  {
    "id": "vechain",
    "rank": 23,
    "name": "VeChain",
    "symbol": "VET",
    "image": "https://assets.coingecko.com/coins/images/1167/large/VET.png",
    "currentPrice": 0.038216,
    "priceChangePercentage24h": 0.94,
    "marketCap": 3238627673.79,
    "volume24h": 231683523.38,
    "high24h": 0.038354,
    "low24h": 0.037947,
    "circulatingSupply": 84745333729.03,
    "sparkline7d": [
      0.038216,
      0.039465,
      0.039502,
      0.039795,
      0.041037,
      0.043068,
      0.042775
    ]
  },
  {
    "id": "hedera-hashgraph",
    "rank": 24,
    "name": "Hedera",
    "symbol": "HBAR",
    "image": "https://assets.coingecko.com/coins/images/3688/large/hedera-hashgraph.jpeg",
    "currentPrice": 0.072008,
    "priceChangePercentage24h": 1.91,
    "marketCap": 2740925002.85,
    "volume24h": 301482058.51,
    "high24h": 0.072984,
    "low24h": 0.070833,
    "circulatingSupply": 38064173464.82,
    "sparkline7d": [
      0.072008,
      0.071818,
      0.075623,
      0.077431,
      0.08093,
      0.085224,
      0.082765
    ]
  },
  {
    "id": "algorand",
    "rank": 25,
    "name": "Algorand",
    "symbol": "ALGO",
    "image": "https://assets.coingecko.com/coins/images/4380/large/download.png",
    "currentPrice": 0.191684,
    "priceChangePercentage24h": 1.01,
    "marketCap": 1549052816.69,
    "volume24h": 149201224.99,
    "high24h": 0.193566,
    "low24h": 0.190891,
    "circulatingSupply": 8081283866.64,
    "sparkline7d": [
      0.191684,
      0.181852,
      0.176192,
      0.167166,
      0.170566,
      0.176378,
      0.184781
    ]
  },
  {
    "id": "the-graph",
    "rank": 26,
    "name": "The Graph",
    "symbol": "GRT",
    "image": "https://assets.coingecko.com/coins/images/13397/large/Graph_Token.png",
    "currentPrice": 0.210908,
    "priceChangePercentage24h": -5.87,
    "marketCap": 1996583224.57,
    "volume24h": 349665049.05,
    "high24h": 0.221161,
    "low24h": 0.205778,
    "circulatingSupply": 9466607357.59,
    "sparkline7d": [
      0.210908,
      0.203811,
      0.214878,
      0.212255,
      0.211931,
      0.224389,
      0.233341
    ]
  },
  {
    "id": "aave",
    "rank": 27,
    "name": "Aave",
    "symbol": "AAVE",
    "image": "https://assets.coingecko.com/coins/images/12645/large/AAVE.png",
    "currentPrice": 164.77,
    "priceChangePercentage24h": -5.76,
    "marketCap": 2395837834.04,
    "volume24h": 186345476.18,
    "high24h": 171.53,
    "low24h": 159.35,
    "circulatingSupply": 14540497.87,
    "sparkline7d": [
      164.77,
      169.16,
      159.41,
      160.44,
      159.3,
      150.08,
      147.05
    ]
  },
  {
    "id": "maker",
    "rank": 28,
    "name": "Maker",
    "symbol": "MKR",
    "image": "https://assets.coingecko.com/coins/images/1364/large/Mark_Maker.png",
    "currentPrice": 1850.45,
    "priceChangePercentage24h": 2.11,
    "marketCap": 1691605188.47,
    "volume24h": 297307044.44,
    "high24h": 1864.17,
    "low24h": 1807.97,
    "circulatingSupply": 914158.82,
    "sparkline7d": [
      1850.45,
      1762.69,
      1713.1,
      1618.45,
      1672.64,
      1626.56,
      1554.26
    ]
  },
  {
    "id": "fantom",
    "rank": 29,
    "name": "Fantom",
    "symbol": "FTM",
    "image": "https://assets.coingecko.com/coins/images/4001/large/Fantom_round.png",
    "currentPrice": 0.584772,
    "priceChangePercentage24h": -1.32,
    "marketCap": 1595577832.24,
    "volume24h": 267858787.42,
    "high24h": 0.592145,
    "low24h": 0.580859,
    "circulatingSupply": 2728546907.58,
    "sparkline7d": [
      0.584772,
      0.589726,
      0.603909,
      0.574158,
      0.543672,
      0.555951,
      0.550969
    ]
  },
  {
    "id": "theta-token",
    "rank": 30,
    "name": "Theta Network",
    "symbol": "THETA",
    "image": "https://assets.coingecko.com/coins/images/2538/large/theta-token.png",
    "currentPrice": 1.46,
    "priceChangePercentage24h": -7.27,
    "marketCap": 1419867922.65,
    "volume24h": 224955773.97,
    "high24h": 1.55,
    "low24h": 1.36,
    "circulatingSupply": 972512275.79,
    "sparkline7d": [
      1.46,
      1.38,
      1.44,
      1.44,
      1.41,
      1.42,
      1.49
    ]
  }
];

export default mockCoins;
