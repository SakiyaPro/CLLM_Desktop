using CLLM_Desktop.Interfaces;
using ReactiveUI;                      // ReactiveObject, ReactiveCommand
using System;
using System.Collections.ObjectModel;
using System.Reactive;                 // Unit
using System.Reactive.Linq;            // DistinctUntilChanged, Where, CombineLatest
using System.Reactive.Subjects;        // Subject, BehaviorSubject

namespace CLLM_Desktop.ViewModels
{
    /// <summary>
    /// チャット用 ViewModel（Presenter/Model を参照しない）。
    /// 
    /// 役割：
    /// 1) 入力トリガ（送信/中断）を IObservable として公開（Presenter が購読）
    /// 2) 表示用イベント（ユーザー文/アシスタント出力/完了/Busy）を IObservable として公開（View が購読）
    /// 3) Presenter から呼ばれる更新メソッド（Show*/SetBusy/Reset）を提供
    /// 
    /// 設計上のポイント：
    /// - ReactiveCommand は **View の XAML バインド用**に public だが、IChatViewModel には含めない。
    ///   → UI 技術への依存をアプリ境界に漏らさないため（疎結合）。
    /// </summary>
    public sealed class ChatViewModel : ReactiveObject, IChatViewModel
    {
        // ===== 表示用：View はこれに ItemsSource バインド =====
        private readonly ObservableCollection<MessageItem> _messages = new();
        public ReadOnlyObservableCollection<MessageItem> Messages { get; }

        // 表示アイテム（XAMLのItemTemplateが参照）
        public sealed record MessageItem(string Role, string Content);

        // ==========================
        // 入力欄（TwoWay bind 用プロパティ）
        // ==========================

        /// <summary>
        /// 入力欄テキスト。XAML から TwoWay バインディングされる想定。
        /// インターフェイスには載せない（Presenter には不要なため）。
        /// </summary>
        public string Input
        {
            get => _input;
            set => this.RaiseAndSetIfChanged(ref _input, value);
        }
        private string _input = string.Empty;

        // ==========================
        // UI バインド用コマンド（public）※インターフェイスには含めない
        // ==========================

        /// <summary>
        /// 送信コマンド（View がボタンにバインド）。
        /// 実行時、「トリム済み Input」を結果として発行する。
        /// </summary>
        public ReactiveCommand<Unit, string> SendCommand { get; }

        /// <summary>
        /// 中断コマンド（View がボタンにバインド）。
        /// 実行時、Unit.Default を発行する。
        /// </summary>
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        // ==========================
        // IChatViewModel：入力トリガの公開（Presenter が購読）
        // ==========================

        /// <summary>送信要求（送信時の文字列）。</summary>
        public IObservable<string> SendRequested => SendCommand;

        /// <summary>中断要求。</summary>
        public IObservable<Unit> CancelRequested => CancelCommand;

        // ==========================
        // 表示用ストリーム（Presenter → VM → View）
        // ==========================


        /// <summary>Busy 状態の公開（同一値の連続は抑止）。</summary>
        public IObservable<bool> BusyStream => _busySubject.DistinctUntilChanged();

        private readonly BehaviorSubject<bool> _busySubject = new(false);

        // ==========================
        // コンストラクタ
        // ==========================

        /// <summary>
        /// ReactiveCommand と公開ストリームの配線を行う。
        /// </summary>
        public ChatViewModel()
        {
            // ReadOnly ラッパーを公開（View からは編集不可）
            Messages = new ReadOnlyObservableCollection<MessageItem>(_messages);

            // 「送信可能？」= 入力が空白でなく、かつ Busy でない
            var canSend =
                this.WhenAnyValue(vm => vm.Input, s => !string.IsNullOrWhiteSpace(s))
                    .CombineLatest(_busySubject.StartWith(false), (hasText, busy) => hasText && !busy)
                    .ObserveOn(RxApp.MainThreadScheduler);

            // 送信コマンド：実行で Input をトリムして返す
            SendCommand = ReactiveCommand.Create(
                execute: () => (Input ?? string.Empty).Trim(),
                canExecute: canSend);

            // 実行後に入力欄をクリア（UI都合の副作用。Presenter へは SendRequested 経由で伝播）
            SendCommand.Where(text => text.Length > 0)
                       .Subscribe(_ => Input = string.Empty);

            // 中断コマンド：常に発火可能（ボタン活性は BusyStream とバインドで制御可能）
            CancelCommand = ReactiveCommand.Create(() => Unit.Default);
        }

        // ==========================
        // Presenter からの更新受け口（副作用）
        // ==========================

        /// <summary>ユーザー文を Messages に追加。</summary>
        public void ShowUserMessage(string text)
        {
            var t = (text ?? string.Empty).Trim();
            if (t.Length == 0) return;
            _messages.Add(new MessageItem("user", t));
        }

        /// <summary>アシスタント出力の部分トークンを、末尾assistantに追記（なければ新規）。</summary>
        public void ShowAssistantToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return;

            if (_messages.Count == 0 || _messages[^1].Role != "assistant")
                _messages.Add(new MessageItem("assistant", string.Empty));

            var last = _messages[^1];
            _messages[^1] = last with { Content = last.Content + token };
        }

        /// <summary>Busy 状態の変更を通知。</summary>
        public void SetBusy(bool busy)
        {
            _busySubject.OnNext(busy);
        }

        /// <summary>内部状態のリセット（必要に応じて拡張）。</summary>
        public void Reset()
        {
            Input = string.Empty;
            _messages.Clear();
        }

        // ==========================
        // リソース解放
        // ==========================

        /// <summary>
        /// 内部ストリームを完了・破棄。
        /// View / Presenter 側で購読解除されていることが前提。
        /// </summary>
        public void Dispose()
        {
            _busySubject.OnCompleted();
            _busySubject.Dispose();
        }
    }
}
