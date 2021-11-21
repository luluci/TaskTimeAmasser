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
        public ReactivePropertySlim<string> FilterTaskAlias { get; set; }
        public ReactiveCollection<string> FilterTaskCode { get; }
        public ReactivePropertySlim<int> FilterTaskCodeSelectIndex { get; set; }
        public AsyncReactiveCommand QueryPresetGetTaskList { get; }
        public AsyncReactiveCommand QueryPresetGetCodeSum { get; }
        // Query Manual
        public ReactivePropertySlim<string> QueryText { get; set; }
        public ReactivePropertySlim<bool> EnablePresetUpdateQueryText { get; set; }
        public AsyncReactiveCommand QueryManualExecute { get; }
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
            FilterTaskAlias = new ReactivePropertySlim<string>("");
            FilterTaskCode = new ReactiveCollection<string>();
            FilterTaskCode.Add("<指定なし>");
            FilterTaskCode
                .AddTo(Disposables);
            FilterTaskCodeSelectIndex = new ReactivePropertySlim<int>(0);
            FilterTaskCodeSelectIndex
                .AddTo(Disposables);
            //QueryPresetGetTaskList = new AsyncReactiveCommand();
            QueryPresetGetTaskList = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var q = MakeQuerySelectTaskList();
                        var r = await ExecuteQuery(q);
                        UpdateDbView(r);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetCodeSum = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var q = MakeQuerySelectCodeSum();
                        var r = await ExecuteQuery(q);
                        UpdateDbView(r);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            // Query Manual
            QueryText = new ReactivePropertySlim<string>("");
            EnablePresetUpdateQueryText = new ReactivePropertySlim<bool>(true);
            QueryManualExecute = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        if (QueryText.Value.Length > 0)
                        {
                            var r = await ExecuteQuery(QueryText.Value);
                            UpdateDbView(r);
                        }
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
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
            FilterTaskCode.Clear();
            FilterTaskCode.Add("<指定なし>");
            foreach (var code in repository.TaskCodeList)
            {
                FilterTaskCode.Add(code);
            }
            FilterTaskCodeSelectIndex.Value = 0;
        }

        private string MakeQuerySelectTaskList()
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT DISTINCT t.task_code, t.task_name, a.task_alias_name");
            query.AppendLine(@"  FROM work_times w, tasks t, task_aliases a");
            query.AppendLine(@"  WHERE w.task_id = t.task_id AND w.task_alias_id = a.task_alias_id");
            query.Append(@";");
            return query.ToString();
        }

        private string MakeQuerySelectCodeSum()
        {
            // クエリ作成
            if (FilterTaskAlias.Value.Length == 0)
            {
                return MakeQuerySelectCodeSumNoFilter();
            }
            else
            {
                return MakeQuerySelectCodeSumFilter(FilterTaskAlias.Value);
            }
        }

        private string MakeQuerySelectCodeSumNoFilter()
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT s.subtask_code AS コード, Sum(unitbl.time) AS 工数");
            query.AppendLine(@"FROM subtasks s,");
            query.AppendLine(@"  (SELECT w.subtask_id AS id, s.subtask_code, w.time AS time FROM subtasks s, work_times w WHERE w.subtask_id = s.subtask_id");
            query.AppendLine(@"   UNION ALL");
            query.AppendLine(@"   SELECT s.subtask_id, s.subtask_code, 0 FROM subtasks s) AS unitbl");
            query.AppendLine(@"WHERE s.subtask_id = unitbl.id");
            query.AppendLine(@"GROUP BY s.subtask_id");
            query.Append(@";");
            return query.ToString();
        }

        private string MakeQuerySelectCodeSumFilter(string filterTaskAlias)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT s.subtask_code AS コード, Sum(unitbl.time) AS 工数");
            query.AppendLine(@"FROM subtasks s,");
            query.AppendLine(@"  (SELECT intbl.id AS id, intbl.code AS code, intbl.time AS time");
            query.AppendLine(@"   FROM");
            query.AppendLine(@"     (SELECT w.subtask_id AS id, s.subtask_code AS code, w.time AS time, w.task_alias_id AS alias_id FROM subtasks s, work_times w WHERE w.subtask_id = s.subtask_id");
            query.AppendLine(@"      UNION ALL");
            query.AppendLine(@"      SELECT s.subtask_id, s.subtask_code, 0, -1 FROM subtasks s) AS intbl,");
            query.AppendLine($@"     (SELECT a.task_alias_id AS alias_id FROM task_aliases a WHERE a.task_alias_name LIKE '{filterTaskAlias}') AS tasktbl");
            query.AppendLine(@"   WHERE intbl.alias_id IN (tasktbl.alias_id, -1)");
            query.AppendLine(@"  ) AS unitbl");
            query.AppendLine(@"WHERE s.subtask_id = unitbl.id");
            query.AppendLine(@"GROUP BY s.subtask_id");
            query.Append(@";");
            return query.ToString();
        }

        private async Task<bool> ExecuteQuery(string query)
        {
            // クエリ実行前処理
            if (EnablePresetUpdateQueryText.Value)
            {
                QueryText.Value = query;
            }
            // クエリ実行
            return await repository.QueryExecute(query);
        }
        private void UpdateDbView(bool executeResult)
        {
            if (executeResult)
            {
                // 結果反映
                QueryResult.Value = repository.QueryResult;
            }
            else
            {
                UpdateQueryResultNotify(repository.ErrorMessage);
            }
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
