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
    /// Avalonia �A�v���P�[�V�����̃G���g���|�C���g�B
    /// - ���\�[�X������
    /// - ���C���E�B���h�E�̐���
    /// - �ˑ��֌W�̑g�ݗ��āiComposition Root�j
    /// ��S������B
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// XAML ���\�[�X�̏������B
        /// �iApp.axaml �ɏ�����Ă��郊�\�[�X��`��ǂݍ��ށj
        /// </summary>
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// �t���[�����[�N��������ɌĂ΂�鏈���B
        /// �����ňˑ��I�u�W�F�N�g��g�ݗ��ĂāA���C���E�B���h�E�ɒ�������B
        /// </summary>
        public override void OnFrameworkInitializationCompleted()
        {
            // �f�X�N�g�b�v�A�v���Ƃ��ċN�����Ă���ꍇ�̂ݏ�������
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // ===============================
                // �ˑ����̑g�ݗ��� (Composition Root)
                // ===============================

                // ���f���t�@�C���iGGUF�j�̃p�X������
                var modelPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "Llama",
                    "Llama-3.1-70B-Instruct-Q4_K_M.gguf");

                // Model �𐶐�
                var model = new ChatModel(modelPath);

                // ViewModel �𐶐�
                var vm = new ChatViewModel();

                // Presenter �𐶐����āAViewModel�EModel �𒍓�
                var presenter = new ChatPresenter(vm, model);

                // ���C���E�B���h�E�� View ��\��t��
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
