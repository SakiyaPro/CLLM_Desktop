using System;
using System.Reactive;   // Unit

namespace CLLM_Desktop.Interfaces
{
    /// <summary>
    /// チャット用 ViewModel の公開インターフェイス。
    /// 
    /// 設計方針：
    /// - 入力（送信/中断）は <see cref="IObservable{T}"/> で公開し、Presenter はこれを購読する。
    /// - 表示（ユーザー文/アシスタント出力/完了/Busy）も <see cref="IObservable{T}"/> で公開し、View が購読する。
    /// - UI バインド用のコマンド（ReactiveCommand/ICommand）は **インターフェイスには載せない**。
    ///   → UI 技術詳細（ReactiveUI 依存）をアプリ境界に漏らさないため。
    /// </summary>
    public interface IChatViewModel : IDisposable
    {
        // ===== View → Presenter：入力トリガ（Presenter が購読） =====

        /// <summary>
        /// 「送信操作が発生した」ことを通知するストリーム。
        /// 要素は送信時点の入力テキスト（トリム済み）。
        /// </summary>
        IObservable<string> SendRequested { get; }

        /// <summary>
        /// 「中断操作が発生した」ことを通知するストリーム。
        /// </summary>
        IObservable<Unit> CancelRequested { get; }

        /// <summary>
        /// 応答生成中フラグ（true=生成中 / false=待機）。
        /// View はボタン活性やインジケータ表示の制御に用いる。
        /// </summary>
        IObservable<bool> BusyStream { get; }

        // ===== Presenter → VM：更新用の受け口（副作用） =====

        /// <summary>ユーザー文を View 側へ反映するよう通知する。</summary>
        void ShowUserMessage(string text);

        /// <summary>アシスタント出力の1トークンを View 側へ反映するよう通知する。</summary>
        void ShowAssistantToken(string token);

        /// <summary>Busy 状態の切替を通知する。</summary>
        void SetBusy(bool busy);

        /// <summary>内部状態のリセット（必要に応じて拡張）。</summary>
        void Reset();
    }
}
