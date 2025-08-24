# 📘 CLLM_Desktop

ローカル環境で LLM (Large Language Model, GGUF形式) を動かし、  
**ChatGPT風のUI** で会話できる Avalonia 製デスクトップアプリケーションです。  

---

## ✨ 特徴
- Avalonia UI + MVP アーキテクチャ採用（View / ViewModel / Presenter / Model）
- LLamaSharp を利用して GGUF 形式の LLM をローカル実行
- CPU / GPU (CUDA) 両対応
- ReactiveUI を利用したリアクティブなデータバインディング
- シンプルなチャットUI（ユーザーとアシスタントの会話表示）
- `.env` に APIキーやモデルパスを分離管理

---

## 🏗️ アーキテクチャ設計****
```text
┌─────────────┐
│     View    │  ← 画面（Avalonia UI）
└───────▲─────┘
        │ Binding (ReactiveUI)
        │
┌───────┴─────┐
│  ViewModel  │  ← 状態保持 / コマンド公開
└───────▲─────┘
        │  インターフェース越しの参照
        │  (IViewModel)
┌───────┴─────┐
│  Presenter  │  ← 通知を受けて View/Model 呼び出し
└───────┬─────┘
        │  インターフェース越しの参照
        │  (IModel)
┌───────▼─────┐
│    Model    │  ← 業務ロジック / LLM操作など
└─────────────┘
```
