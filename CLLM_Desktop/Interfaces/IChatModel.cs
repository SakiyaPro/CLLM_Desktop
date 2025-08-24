using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CLLM_Desktop.Interfaces
{
    /// <summary>
    /// チャット応答生成を行うモデル層のインターフェイス。
    /// Presenter はこのインターフェイス越しにチャット応答を取得する。
    /// </summary>
    public interface IChatModel
    {
        /// <summary>
        /// ユーザー入力に基づいて応答を生成する。
        /// </summary>
        /// <param name="userInput">ユーザー入力のテキスト</param>
        /// <param name="ct">キャンセルトークン</param>
        /// <returns>アシスタントの応答をトークン列挙で返す</returns>
        Task<IEnumerable<string>> GenerateResponse(string userInput, CancellationToken ct);
    }
}
