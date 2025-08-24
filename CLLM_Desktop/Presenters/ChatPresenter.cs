using CLLM_Desktop.Interfaces;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CLLM_Desktop.Presenters
{
    /// <summary>
    /// チャット機能のユースケースを司るプレゼンター。
    /// 
    /// 設計方針：
    /// - ViewModel は Presenter / Model を知らない（疎結合）。
    /// - Presenter は IChatViewModel が公開する IObservable（SendRequested / CancelRequested）を購読して開始/中断を制御。
    /// - 実際の応答生成は IChatModel に委譲。
    /// - 生成結果（トークン列挙）は ViewModel の受け口（Show*/SetBusy/Complete）で UI へ通知。
    /// 
    /// 役割の境界：
    /// - UI（View）はコマンドを押すだけ、購読して描画するだけ。
    /// - ViewModel はストリームと受け口を提供するだけ。
    /// - Presenter は「順序」と「中断」を取り仕切る。
    /// - Model は LLM 叩きなどの処理本体を持つ。
    /// </summary>
    public sealed class ChatPresenter : IDisposable
    {
        /// <summary>UI 側との橋渡し（受け口とストリーム公開）。</summary>
        private readonly IChatViewModel _vm;

        /// <summary>応答生成の実処理を担うモデル層。</summary>
        private readonly IChatModel _model;

        /// <summary>Presenter 内で管理する購読の束。</summary>
        private readonly CompositeDisposable _disposables = new();

        /// <summary>現在進行中の推論を中断するためのトークンソース。</summary>
        private CancellationTokenSource? _cts;

        /// <summary>重複実行を避けるための軽量ガード。</summary>
        private int _running = 0;

        /// <summary>
        /// コンストラクタ。
        /// ストリーム購読をセットアップして、送信/中断の操作を受け付ける。
        /// </summary>
        /// <param name="vm">IChatViewModel（ストリーム公開＋受け口）</param>
        /// <param name="model">IChatModel（応答生成の実体）</param>
        public ChatPresenter(IChatViewModel vm, IChatModel model)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _model = model ?? throw new ArgumentNullException(nameof(model));

            // ---- 送信要求の購読 ----
            // ・空文字は無視（VM 側でも canSend を持たせているが、二重の安全策）
            // ・逐次実行：走っている最中は新リクエストで「前回をキャンセル」→ すぐ新規開始
            _vm.SendRequested
               .Where(text => !string.IsNullOrWhiteSpace(text))
               .Subscribe(async text => await HandleSendAsync(text).ConfigureAwait(false))
               .DisposeWith(_disposables);

            // ---- 中断要求の購読 ----
            _vm.CancelRequested
               .Subscribe(_ => CancelRunning())
               .DisposeWith(_disposables);
        }

        /// <summary>
        /// 送信要求が来たときの処理本体。
        /// - 進行中があれば中断
        /// - Busy ON
        /// - ユーザー文を表示
        /// - モデルに応答生成を依頼し、トークンを逐次 VM へ通知
        /// - 完了時に Busy OFF
        /// </summary>
        private async Task HandleSendAsync(string userText)
        {
            // 多重実行防止：0→1 へ遷移できたスレッドだけが処理を回す
            if (Interlocked.Exchange(ref _running, 1) == 1)
            {
                // すでに走行中 → 一旦キャンセルしてから受け直す
                CancelRunning();
                // 直ちに新規実行へ進む（処理中断にかかる遅延はモデル側のキャンセル応答速度に依存）
            }

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                _vm.SetBusy(true);                // Busy ON（UI: ボタン無効化/スピナー表示など）
                _vm.ShowUserMessage(userText);    // ユーザー文を先に表示

                // ---- 応答生成（モデル委譲）----
                // 現在の IChatModel は Task<IEnumerable<string>> を返す設計。
                // 逐次性を保ちたい場合は IAsyncEnumerable<string> にするのが理想だが、
                // 現状でも返ってきた列挙を順に流すことで UI には連続追加が可能。
                IEnumerable<string> tokens = await _model.GenerateResponse(userText, ct).ConfigureAwait(false);

                // モデルから受け取ったトークンを逐次 UI へ反映
                foreach (var token in tokens)
                {
                    if (ct.IsCancellationRequested) break;
                    _vm.ShowAssistantToken(token);
                }
            }
            catch (OperationCanceledException)
            {
                // 中断は想定内：静かに握りつぶす（必要なら「中断しました」を表示させる API を追加）
            }
            catch (Exception ex)
            {
                // 予期せぬエラー。最低限 Busy は戻す。
                // ログ基盤があればここで送る。UI 表示が必要なら VM にエラー表示用メソッドを追加。
                System.Diagnostics.Debug.WriteLine($"Presenter error: {ex}");
            }
            finally
            {
                _vm.SetBusy(false);       // Busy OFF
                Interlocked.Exchange(ref _running, 0);
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// 現在進行中の推論があればキャンセルを要求する。
        /// </summary>
        private void CancelRunning()
        {
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 競合で既に Dispose 済みの場合は無視
            }
        }

        /// <summary>
        /// Presenter が保持する購読/トークンを破棄。
        /// </summary>
        public void Dispose()
        {
            CancelRunning();
            _cts?.Dispose();
            _disposables.Dispose();
        }
    }
}
