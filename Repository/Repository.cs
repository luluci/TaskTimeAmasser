using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

using Reactive.Bindings;
using Reactive.Bindings.Extensions;

namespace Repository
{

    public interface IRepository : IDisposable
    {
        ReactivePropertySlim<bool> IsConnect { get; set; }
        ReactivePropertySlim<bool> IsLoading { get; set; }
        List<(string code, string name)> TaskCodeList { get; set; }
        (long begin, long end) DateRange { get; set; }
        DataTable QueryResult { get; set; }
        string ErrorMessage { get; set; }

        Task<bool> Connect(string repoPath);
        Task Close();
        Task<bool> Load(string logPath);
        Task<bool> UpdateTaskCodeList();
        Task<bool> UpdateDateRange(string query);
        Task<bool> QueryExecute(string query);
    }

    public class Repository : IRepository
    {
        private CompositeDisposable disposables = new CompositeDisposable();
        private SQLite sqlite;

        public ReactivePropertySlim<bool> IsConnect { get; set; }
        public ReactivePropertySlim<bool> IsLoading { get; set; }

        public List<(string code, string name)> TaskCodeList { get; set; } = new List<(string code, string name)>();
        public (long begin, long end) DateRange { get; set; } = (0, 0);

        public DataTable QueryResult { get; set; }
        public string ErrorMessage { get; set; }


        public Repository()
        {
            //
            IsConnect = new ReactivePropertySlim<bool>(false);
            IsConnect.AddTo(disposables);
            //
            IsLoading = new ReactivePropertySlim<bool>(false);
            IsLoading.AddTo(disposables);
            //
            QueryResult = new DataTable();
            // DB
            sqlite = new SQLite();
            disposables.Add(sqlite);
        }


        public async Task<bool> Connect(string repoPath)
        {
            if (!IsConnect.Value)
            {
                IsConnect.Value = true;
                try
                {
                    var result = await Task.Run(() =>
                    {
                        // DB接続処理
                        return sqlite.Open(repoPath);
                    });
                    if (!result)
                    {
                        IsConnect.Value = false;
                    }
                    ErrorMessage = sqlite.LastErrorMessage;
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                    IsConnect.Value = false;
                }
            }
            return IsConnect.Value;
        }

        public async Task Close()
        {
            if (IsConnect.Value)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        // DB切断処理
                        sqlite.Close();
                    });
                }
                catch
                {
                    //
                }
                finally
                {
                    IsConnect.Value = false;
                }
            }
        }

        public async Task<bool> Load(string logPath)
        {
            bool result = false;
            if (IsConnect.Value && !IsLoading.Value)
            {
                IsLoading.Value = true;
                try
                {
                    result = await Task.Run(async () =>
                    {
                        // logフォルダを起点にファイル走査
                        return await LoadLogDirRoot(logPath);
                    });
                }
                catch
                {
                    result = false;
                }
                finally
                {
                    IsLoading.Value = false;
                }
            }
            ErrorMessage = sqlite.LastErrorMessage;
            return result;
        }

        private async Task<bool> LoadLogDirRoot(string logPath)
        {
            bool existLog = false;
            // 存在チェック
            if (!Directory.Exists(logPath))
            {
                return false;
            }
            // 直下フォルダチェック
            foreach (var child in Directory.EnumerateDirectories(logPath))
            {
                // 直下のフォルダ名をperson_idとする
                var personId = Path.GetFileName(child);
                // フォルダ内のファイルをログとして取得する
                existLog = await LoadLogDirChild(child, personId);
            }

            return existLog;
        }

        private async Task<bool> LoadLogDirChild(string logDirPath, string personId)
        {
            bool existLog = false;
            foreach (var file in Directory.EnumerateFiles(logDirPath, "*.txt", SearchOption.AllDirectories))
            {
                var result = await sqlite.LoadLogFile(personId, logDirPath, file);
                if (result) existLog = true;
            }
            return existLog;
        }

        public async Task<bool> UpdateTaskCodeList()
        {
            // Taskコードリスト更新
            TaskCodeList.Clear();
            var result = await sqlite.QueryGetTaskCode<string>(TaskCodeList);

            // エラーメッセージ更新
            ErrorMessage = sqlite.LastErrorMessage;
            return result;
        }

        public async Task<bool> UpdateDateRange(string query)
        {
            // ログ日時最小最大を更新
            var result2 = await sqlite.QueryGetDateRange(query);
            if (result2.Item1)
            {
                DateRange = (result2.Item2, result2.Item3);
            }

            // エラーメッセージ更新
            ErrorMessage = sqlite.LastErrorMessage;
            return result2.Item1;
        }

        public async Task<bool> QueryExecute(string query)
        {
            //QueryResult.Clear();
            QueryResult = new DataTable();
            var result = await sqlite.QueryGetSelectResult(QueryResult, query);
            ErrorMessage = sqlite.LastErrorMessage;
            return result;
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)。
                    disposables.Dispose();
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        // ~DB() {
        //   // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
        //   Dispose(false);
        // }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        void IDisposable.Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
