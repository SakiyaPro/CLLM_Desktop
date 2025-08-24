using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CLLM_Desktop.Services
{
    /// <summary>
    /// Llama 系モデル（GGUF 形式）をラップして、
    /// チャット的に使えるようにするサービスクラス。
    /// 
    /// - モデルファイル読み込み
    /// - コンテキスト作成（スレッド数やコンテキストサイズの設定）
    /// - ChatSession を利用したストリーミング応答
    /// を担当する。
    /// 
    /// IDisposable を実装しているので、アプリ終了時に必ず Dispose() を呼び出すこと。
    /// </summary>
    public sealed class LlamaService : IDisposable
    {
        /// <summary>読み込んだモデルの重み（GGUF）。</summary>
        private readonly LLamaWeights _weights;

        /// <summary>推論用コンテキスト。</summary>
        private readonly LLamaContext _context;

        /// <summary>会話の履歴とチャット機能を提供するセッション。</summary>
        private readonly ChatSession _chat;

        /// <summary>毎回の推論に用いる既定のパラメータ。</summary>
        private readonly InferenceParams _inferenceParams;

        /// <summary>
        /// Llama モデルを初期化する。
        /// </summary>
        /// <param name="modelPath">GGUF モデルへのフル/相対パス。</param>
        /// <param name="ctxSize">
        /// コンテキスト長（トークン数）。4096 以上にすると長文に強いが、メモリ消費と速度に影響。
        /// </param>
        /// <param name="nThreads">
        /// 使用スレッド数。0 以下なら環境に合わせて (CPUコア数 - 1) を自動設定。
        /// </param>
        public LlamaService(string modelPath, int ctxSize = 4096, int nThreads = 0)
        {
            // --- 入力チェック ---
            var fullPath = Path.GetFullPath(modelPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Model file not found: {fullPath}", fullPath);

            if (nThreads <= 0)
                nThreads = Math.Max(1, Environment.ProcessorCount - 1);

            // --- 重要：旧 API の LLamaContextParams ではなく ModelParams を使う ---
            // ここでスレッド数やコンテキスト長、GPU層数などの「推論設定」をまとめて指定する。
            var modelParams = new ModelParams(fullPath)
            {
                ContextSize = (uint)ctxSize, // 旧: LLamaContextParams.ContextSize
                GpuLayerCount = 12,           // CPU 実行。GPU なら >0 に
                Threads = nThreads,          // 旧: LLamaContextParams.Threads

                // 必要なら他にも：
                // BatchSize = 512, UseMemorymap = true, UseMemoryLock = false, etc.
            };

            // --- モデル読み込みとコンテキスト作成（新 API の正しい流れ） ---
            _weights = LLamaWeights.LoadFromFile(modelParams);   // ← string ではなく ModelParams を渡す
            _context = _weights.CreateContext(modelParams);

            // 実行器（executor）は「インタラクティブ型」を採用
            // ※ 旧サンプルのような独自 IInference 実装は不要
            var executor = new InteractiveExecutor(_context);

            // ChatSession を作成。履歴はここが面倒みてくれる
            _chat = new ChatSession(executor);

            // 振る舞いを決めるシステムメッセージ
            _chat.History.AddMessage(AuthorRole.System,
                "You are a helpful assistant who answers concisely in Japanese.");

            // --- 推論ハイパラの既定値（必要最低限） ---
            // 旧: AntiPrompt -> 新: StopWords（環境によっては AntiPrompt でも残っているが、StopWords を推奨）
            // ★ サンプリング戦略（確率的な出力制御）を設定
            // InferenceParams.SamplingPipeline に代入することで、
            // モデルの出力多様性や再現性を調整できる。
            _inferenceParams = new InferenceParams
            {
                MaxTokens = 1024,              // 応答の最大生成トークン数
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    /// <summary>
                    /// 出力のランダム性を制御する温度パラメータ。
                    /// - 低い値 (例: 0.1〜0.5) → 出力がより決定的で安全
                    /// - 高い値 (例: 0.8〜1.2) → 出力が多様でクリエイティブ
                    /// </summary>
                    Temperature = 0.7f,

                    /// <summary>
                    /// nucleus サンプリング (Top-p) の閾値。
                    /// 確率分布の上位 p(%) の範囲だけから次のトークンを選ぶ。
                    /// - 0.9 → 上位90%から選択
                    /// - 値を下げると保守的に、上げると多様に
                    /// - 確率の「累積割合」カット
                    /// </summary>
                    TopP = 0.9f,

                    /// <summary>
                    /// Top-k サンプリングの閾値。
                    /// 出現確率が上位 K 個のトークンから候補を選ぶ。
                    /// - 40 → 上位40語からランダムに選択
                    /// - 小さいと決定的、大きいと多様
                    /// - 確率の「上位件数」カット
                    /// </summary>
                    TopK = 40,

                    /// <summary>
                    /// 乱数シード。
                    /// - 同じシード値を使えば同じ入力に対して同じ出力が再現される
                    /// - ランダム性を固定したいときに利用
                    /// - 0 または指定なしなら毎回ランダム
                    /// - 出力の再現性制御
                    /// </summary>
                    Seed = 1234,    // 乱数シード（固定したいときは任意の整数に）
                },
                AntiPrompts = new List<string>
                        {
                            "User:",          // ChatSessionのデフォ書式で最も効く
                            "System:",        // 念のため
                            "</s>",           // 一部モデルのEOS
                            "<|eot_id|>",     // Llama 3系テンプレを使っている場合
                            "<|start_header_id|>user" // 同上（あれば）
                        }
                // FrequencyPenalty = 0.0f, PresencePenalty = 0.0f なども必要に応じて
            };
        }

        /// <summary>
        /// ユーザーメッセージを送り、モデルの応答をトークン単位でストリーミング取得する。
        /// </summary>
        /// <param name="userMessage">ユーザー入力テキスト。</param>
        /// <param name="ct">キャンセルトークン。</param>
        /// <returns>生成文字列を逐次返す列挙。</returns>
        public async IAsyncEnumerable<string> StreamChatAsync(string userMessage, [EnumeratorCancellation] CancellationToken ct = default)
        {
            // ChatAsync の第2引数に InferenceParams を渡すのが新 API 流儀
            await foreach (var token in _chat.ChatAsync(
                new ChatHistory.Message(AuthorRole.User, userMessage),
                _inferenceParams,
                ct))
            {
                yield return token;
            }
        }

        /// <summary>
        /// 破棄。ネイティブリソースを持つため明示的に Dispose すること。
        /// </summary>
        public void Dispose()
        {
            _context?.Dispose();
            _weights?.Dispose();
        }
    }
}
