# 会員制タスク管理アプリ(MBTM)

| ディレクトリ | 内容 |
| --- | --- |
| `MBTM.Api` | Azure Functions (.NET 8 isolated) / Table Storage / Application Insights |
| `MBTM.Web` | React + Vite + TypeScript。ログインは MSAL(Microsoft Entra External ID) |

## ローカル起動

### Web

```
cd MBTM.Web
copy .env.example .env.local   # 値を設定
npm install
npm run dev
```

### API

```
cd MBTM.Api
copy local.settings.sample.json local.settings.json   # 値を設定
func start
```

`func`(Azure Functions Core Tools)のインストールが必要です。

## Git

- リモート: `https://github.com/Iriun01-cell/test.git`
- ブランチ: `mbtm`(RMS が `main` を使用しているため分離)
