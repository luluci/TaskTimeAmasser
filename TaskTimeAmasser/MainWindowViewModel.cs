using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Prism.Ioc;
using Prism.Mvvm;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

using WinAPI = Microsoft.WindowsAPICodePack;

namespace TaskTimeAmasser
{
    class MainWindowViewModel : BindableBase, IDisposable
    {
        //
        public ReadOnlyReactivePropertySlim<bool> IsEnableDbCtrl { get; set; }
        public ReactivePropertySlim<string> DBFilePath { get; set; }
        public ReactiveCommand DBFilePathSelect { get; }
        public ReactivePropertySlim<string> DBFileConnectText { get; set; }
        public AsyncReactiveCommand DBFileConnect { get; set; }
        //
        public ReactivePropertySlim<bool> IsEnableRepoCtrl { get; set; }
        public ReadOnlyReactivePropertySlim<bool> IsEnableRepoLoad { get; set; }
        public ReactivePropertySlim<string> LogDirPath { get; set; }
        public ReactiveCommand LogDirPathSelect { get; }
        public ReactivePropertySlim<string> LogDirLoadText { get; set; }
        public AsyncReactiveCommand LogDirLoad { get; set; }

        public ReactiveProperty<DataTable> DB { get; }

        //private SQLite sqlite;
        private Config.IConfig config;
        private Repository.IRepository repository;


        public MainWindowViewModel(IContainerProvider diContainer, Config.IConfig config, Repository.IRepository repo)
        {
            this.config = config;
            this.repository = repo;

            
            // Configロード
            config.Load();
            config.AddTo(Disposable);
            // GUI初期化
            // DBFilePath設定
            // DB接続/切断ボタン表示テキスト
            DBFileConnectText = new ReactivePropertySlim<string>("DB接続");
            /*
            IsEnableDbCtrl = repository.IsConnect
                .ToReactivePropertySlimAsSynchronized(
                    x => x.Value
                    //(x) => { return !x; },
                    //(x) => { return x; },
                    //ReactivePropertyMode.DistinctUntilChanged
                )
                .AddTo(Disposable);
                */
            // DBファイル指定有効無効
            IsEnableDbCtrl = repository.IsConnect
                .Inverse()
                .ToReadOnlyReactivePropertySlim();
            IsEnableDbCtrl
                .Subscribe((x) =>
                {
                    if (x)
                    {
                        DBFileConnectText.Value = "DB接続";
                    }
                    else
                    {
                        DBFileConnectText.Value = "DB切断";
                    }
                })
                .AddTo(Disposable);
            // DBファイルパス
            DBFilePath = config.DBFilePath
                .ToReactivePropertySlimAsSynchronized(x => x.Value)
                .AddTo(Disposable);
            // DBファイル指定ダイアログボタンコマンド
            DBFilePathSelect = IsEnableDbCtrl.ToReactiveCommand();
            DBFilePathSelect
                .Subscribe(_ => {
                    var result = FileSelectDialog(DBFilePath.Value);
                    if (!(result is null))
                    {
                        DBFilePath.Value = result;
                    }
                })
                .AddTo(Disposable);
            // DB接続/切断ボタンコマンド
            //DBFileConnect = new AsyncReactiveCommand();
            DBFileConnect = repository.IsLoading.Inverse().ToAsyncReactiveCommand();
            DBFileConnect
                .WithSubscribe(async () => {
                    if (repository.IsConnect.Value)
                    {
                        await repository.Close();
                    }
                    else
                    {
                        //Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: DBFileConnect/WithSubscribe START");
                        await repository.Connect(config.DBFilePath.Value);
                        //Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: DBFileConnect/WithSubscribe END");
                    }
                })
                .AddTo(Disposable);
            // LogDir設定
            // Logファイルロードボタン表示テキスト
            LogDirLoadText = new ReactivePropertySlim<string>("ロード");
            // Logファイルロード有効無効
            IsEnableRepoLoad = repository.IsConnect
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposable);
            // Logファイル設定有効無効
            IsEnableRepoCtrl = new ReactivePropertySlim<bool>(true);
            // Logファイル保存ディレクトリパス
            LogDirPath = config.LogDirPath
                .ToReactivePropertySlimAsSynchronized(x => x.Value)
                .AddTo(Disposable);
            // Logファイル保存ディレクトリ選択ダイアログボタンコマンド
            LogDirPathSelect = IsEnableRepoCtrl.ToReactiveCommand();
            LogDirPathSelect
                .Subscribe(_ => {
                    var result = DirSelectDialog(LogDirPath.Value);
                    if (!(result is null))
                    {
                        LogDirPath.Value = result;
                    }
                })
                .AddTo(Disposable);
            // Logファイルロード開始ボタンコマンド
            LogDirLoad = IsEnableRepoLoad
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    repository.IsLoading.Value = true;
                    IsEnableRepoCtrl.Value = false;
                    await Task.Delay(1000);
                    IsEnableRepoCtrl.Value = true;
                    repository.IsLoading.Value = false;
                })
                .AddTo(Disposable);
            //DBFilePath = new ReactiveProperty<string>(Config.DBFilePath, mode: ReactivePropertyMode.DistinctUntilChanged);
            /*
            DBFilePath.PropertyChanged += (s, e) =>
            {
                Config.DBFilePath = DBFilePath.Value;
            };
            */
            /*
            DBFilePath.Subscribe(x =>
            {
                Config.DBFilePath = DBFilePath.Value;
            });
            Disposable.Add(DBFilePath);
            LogDirPath = new ReactivePropertySlim<string>(Config.LogDirPath, mode: ReactivePropertyMode.DistinctUntilChanged);
            LogDirPath.Subscribe(x =>
            {
                Config.LogDirPath = LogDirPath.Value;
            });
            Disposable.Add(LogDirPath);
            LogDirLoad = new ReactivePropertySlim<string>("ロード Logs", mode: ReactivePropertyMode.DistinctUntilChanged);
            Disposable.Add(LogDirLoad);
            */
            // DBロード
            /*
            sqlite = new SQLite();
            Disposable.Add(sqlite);
            sqlite.Open();

            sqlite.LoadLogFile("oreore", @"D:\home\csharp\TaskTimerPublish\TaskTimer_work\log\log.20211023.txt").Wait();
            */

            // DB作成
            var tbl = new DataTable();
            for (int i = 0; i<3; i++)
            {
                tbl.Columns.Add(i + "列目");
            }
            for (int i = 0; i < 10; i++)
            {
                var row = tbl.NewRow();
                foreach (DataColumn col in tbl.Columns)
                {
                    row[col] = col.ColumnName + "-" + i + "行目";
                }
                tbl.Rows.Add(row);
            }

            // Reactive設定
            DB = new ReactiveProperty<DataTable>(tbl);
            //DB.Value = tbl;
        }


        private string DirSelectDialog(string initDir)
        {
            string result = null;
            var dlg = new WinAPI::Dialogs.CommonOpenFileDialog
            {
                // フォルダ選択ダイアログ（falseにするとファイル選択ダイアログ）
                IsFolderPicker = true,
                // タイトル
                Title = "フォルダを選択してください",
                // 初期ディレクトリ
                InitialDirectory = initDir
            };

            if (dlg.ShowDialog() == WinAPI::Dialogs.CommonFileDialogResult.Ok)
            {
                result = dlg.FileName;
            }

            return result;
        }

        private string FileSelectDialog(string initDir)
        {
            string result = null;
            var dlg = new WinAPI::Dialogs.CommonOpenFileDialog
            {
                // フォルダ選択ダイアログ（falseにするとファイル選択ダイアログ）
                IsFolderPicker = false,
                // タイトル
                Title = "ファイルを選択してください",
                // 初期ディレクトリ
                InitialDirectory = initDir
            };
            
            if (dlg.ShowDialog() == WinAPI::Dialogs.CommonFileDialogResult.Ok)
            {
                result = dlg.FileName;
            }

            return result;
        }


        // Dispose
        private CompositeDisposable Disposable { get; } = new CompositeDisposable();

        private bool disposedValue = false; // 重複する呼び出しを検出する
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.Disposable.Dispose();
                }

                disposedValue = true;
            }
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
        }
    }
}
