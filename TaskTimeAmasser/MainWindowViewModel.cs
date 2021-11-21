using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
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
        // Query Preset
        public ReactiveCollection<string> TaskCodeList { get; }
        public ReactivePropertySlim<int> TaskCodeListSelectIndex { get; set; }
        public AsyncReactiveCommand QueryPresetGetTaskList { get; }
        // Query Manual
        public ReactivePropertySlim<string> QueryText { get; set; }
        public ReactivePropertySlim<bool> EnablePresetUpdateQueryText { get; set; }
        // QueryResult領域
        public ReactiveProperty<DataTable> QueryResult { get; }
        DataTable dbNotify;
        // ダイアログ操作
        public ReactivePropertySlim<string> DialogMessage { get; set; }

        private Config.IConfig config;
        private Repository.IRepository repository;

        public StackPanel dialog { get; set; } = null;

        public MainWindowViewModel(IContainerProvider diContainer, Config.IConfig config, Repository.IRepository repo)
        {
            this.config = config;
            this.repository = repo;

            
            // Configロード
            config.Load();
            config.AddTo(Disposables);
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
                .AddTo(Disposables);
            // DBファイルパス
            DBFilePath = config.DBFilePath
                .ToReactivePropertySlimAsSynchronized(x => x.Value)
                .AddTo(Disposables);
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
                .AddTo(Disposables);
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
                        DialogMessage.Value = "Connecting DB ...";
                        var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                        {
                            //Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: DBFileConnect/WithSubscribe START");
                            await repository.Connect(config.DBFilePath.Value);
                            await repository.Update();
                            UpdateQueryInfo();
                            // Message通知
                            if (repository.IsConnect.Value)
                            {
                                UpdateQueryResultNotify("DB接続しました");
                            }
                            else
                            {
                                UpdateQueryResultNotify($"DB接続失敗しました:\r\n{repository.ErrorMessage}");
                            }
                            //Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: DBFileConnect/WithSubscribe END");
                            args.Session.Close(false);
                        });
                    }
                })
                .AddTo(Disposables);
            // LogDir設定
            // Logファイルロードボタン表示テキスト
            LogDirLoadText = new ReactivePropertySlim<string>("ロード");
            // Logファイルロード有効無効
            IsEnableRepoLoad = repository.IsConnect
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);
            // Logファイル設定有効無効
            IsEnableRepoCtrl = new ReactivePropertySlim<bool>(true);
            // Logファイル保存ディレクトリパス
            LogDirPath = config.LogDirPath
                .ToReactivePropertySlimAsSynchronized(x => x.Value)
                .AddTo(Disposables);
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
                .AddTo(Disposables);
            // Logファイルロード開始ボタンコマンド
            LogDirLoad = IsEnableRepoLoad
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    IsEnableRepoCtrl.Value = false;
                    DialogMessage.Value = "Loading LogFiles ...";
                    var dlgresult = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var result = await repository.Load(LogDirPath.Value);
                        if (result)
                        {
                            UpdateQueryResultNotify("Logファイル取り込み正常終了");
                        }
                        else
                        {
                            UpdateQueryResultNotify($"Logファイル取り込み失敗:\r\n{repository.ErrorMessage}");
                        }
                        args.Session.Close(false);
                    });
                    IsEnableRepoCtrl.Value = true;
                })
                .AddTo(Disposables);
            // Query Preset
            TaskCodeList = new ReactiveCollection<string>();
            TaskCodeList.Add("<指定なし>");
            TaskCodeList
                .AddTo(Disposables);
            TaskCodeListSelectIndex = new ReactivePropertySlim<int>(0);
            TaskCodeListSelectIndex
                .AddTo(Disposables);
            //QueryPresetGetTaskList = new AsyncReactiveCommand();
            QueryPresetGetTaskList = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        await UpdateDbView();
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            // Query Manual
            QueryText = new ReactivePropertySlim<string>("");
            EnablePresetUpdateQueryText = new ReactivePropertySlim<bool>(true);
            //
            DialogMessage = new ReactivePropertySlim<string>("");
            DialogMessage.AddTo(Disposables);

            // QueryResult領域
            // メッセージ表示用DataTable作成
            dbNotify = new DataTable();
            dbNotify.Columns.Add("Message");
            {
                var row = dbNotify.NewRow();
                row[0] = "<DB未接続>";
                dbNotify.Rows.Add(row);
            }
            // Reactive設定
            QueryResult = new ReactiveProperty<DataTable>(dbNotify);
        }

        private void UpdateQueryResultNotify(string msg)
        {
            dbNotify.Rows[0][0] = msg;
            QueryResult.Value = dbNotify;
        }

        private void UpdateQueryInfo()
        {
            TaskCodeList.Clear();
            TaskCodeList.Add("<指定なし>");
            foreach (var code in repository.TaskCodeList)
            {
                TaskCodeList.Add(code);
            }
            TaskCodeListSelectIndex.Value = 0;
        }

        private async Task UpdateDbView()
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT DISTINCT t.task_code, t.task_name, a.task_alias_name");
            query.AppendLine(@"  FROM work_times w, tasks t, task_aliases a");
            query.AppendLine(@"  WHERE w.task_id = t.task_id AND w.task_alias_id = a.task_alias_id");
            query.Append(@";");
            var qstr = query.ToString();
            //
            if (EnablePresetUpdateQueryText.Value)
            {
                QueryText.Value = qstr;
            }
            // クエリ実行
            await repository.QueryExecute(qstr);
            // 結果反映
            QueryResult.Value = repository.QueryResult;
            /*
            DB.Value.Clear();
            // Column作成
            foreach (DataColumn col in repository.QueryResult.Columns)
            {
                DB.Value.Columns.Add(col.ColumnName);
            }
            // Row作成
            foreach (DataRow row in repository.QueryResult.Rows)
            {
                var new_row = DB.Value.NewRow();
                foreach (DataColumn col in repository.QueryResult.Columns)
                {
                }
            }
            */
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
        private CompositeDisposable Disposables { get; } = new CompositeDisposable();

        private bool disposedValue = false; // 重複する呼び出しを検出する
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.Disposables.Dispose();
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
