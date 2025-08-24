using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CLLM_Desktop.Models;
using CLLM_Desktop.Presenters;
using CLLM_Desktop.ViewModels;
using CLLM_Desktop.Views;
using System;
using System.IO;


namespace CLLM_Desktop
{
    /// <summary>
    /// Avalonia アプリケーションのエントリポイント。
    /// - リソース初期化
    /// - メインウィンドウの生成
    /// - 依存関係の組み立て（Composition Root）
    /// を担当する。
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// XAML リソースの初期化。
        /// （App.axaml に書かれているリソース定義を読み込む）
        /// </summary>
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// フレームワーク初期化後に呼ばれる処理。
        /// ここで依存オブジェクトを組み立てて、メインウィンドウに注入する。
        /// </summary>
        public override void OnFrameworkInitializationCompleted()
        {
            // デスクトップアプリとして起動している場合のみ処理する
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // ===============================
                // 依存性の組み立て (Composition Root)
                // ===============================

                // モデルファイル（GGUF）のパスを決定
                var modelPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "Llama",
                    "Llama-3.1-70B-Instruct-Q4_K_M.gguf");

                // Model を生成
                var model = new ChatModel(modelPath);

                // ViewModel を生成
                var vm = new ChatViewModel();

                // Presenter を生成して、ViewModel・Model を注入
                var presenter = new ChatPresenter(vm, model);

                // メインウィンドウに View を貼り付け
                desktop.MainWindow = new ChatView
                {
                    DataContext = vm,
                    Width = 900,
                    Height = 600,
                    Title = "CLLM_Desktop"
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
