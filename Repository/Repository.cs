using System;
using System.Collections.Generic;
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

        Task Connect(string repoPath);
        Task Close();
    }

    public class Repository : IRepository
    {
        private CompositeDisposable disposables = new CompositeDisposable();

        public ReactivePropertySlim<bool> IsConnect { get; set; }
        public ReactivePropertySlim<bool> IsLoading { get; set; }

        public Repository()
        {
            //
            IsConnect = new ReactivePropertySlim<bool>(false);
            IsConnect.AddTo(disposables);
            //
            IsLoading = new ReactivePropertySlim<bool>(false);
            IsLoading.AddTo(disposables);
        }


        public async Task Connect(string repoPath)
        {
            if (!IsConnect.Value)
            {
                IsConnect.Value = true;
                try
                {
                    /*
                    await Task.Run(() =>
                    {
                        // DB接続処理
                    });
                    */
                    await Task.Delay(100);
                }
                catch
                {
                    IsConnect.Value = false;
                }
            }
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
                    });
                    await Task.Delay(100);
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
