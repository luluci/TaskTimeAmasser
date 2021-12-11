using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Resources;
using MaterialDesignThemes.Wpf;
using Prism.Commands;
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
        // Filter全検索
        public ReactiveCollection<string> FilterTaskCode { get; }
        public ReactivePropertySlim<int> FilterTaskCodeSelectIndex { get; set; }
        public ReactivePropertySlim<string> FilterTaskCodeSelectItem { get; set; }
        public ReactivePropertySlim<string> FilterTaskName { get; set; }
        public ReactivePropertySlim<string> FilterTaskAlias { get; set; }
        public ReactivePropertySlim<string> FilterTaskAliasId { get; set; }
        private Dictionary<int,int> FilterTaskAliasIdDict { get; set; } = new Dictionary<int, int>();
        public ReactivePropertySlim<string> FilterToolTip { get; set; }
        public AsyncReactiveCommand QueryPresetGetTaskList { get; }
        public AsyncReactiveCommand QueryPresetGetSubTotal { get; }
        public AsyncReactiveCommand QueryPresetGetItemTotal { get; }
        public AsyncReactiveCommand QueryPresetGetPersonTotal { get; }
        public ReactivePropertySlim<string> ExcludeTaskCode { get; set; }
        // 期間集計
        public ReactivePropertySlim<bool> ReflectFilterQueryResultDate { get; set; }
        public ReactivePropertySlim<DateTime> FilterTermBegin { get; set; }
        public ReactivePropertySlim<DateTime> FilterTermEnd { get; set; }
        public ReactivePropertySlim<int> FilterTermUnitSelectIndex { get; set; }
        public AsyncReactiveCommand QueryPresetGetSubTotalTerm { get; }
        public AsyncReactiveCommand QueryPresetGetItemTotalTerm { get; }
        public AsyncReactiveCommand QueryPresetGetPersonTotalTerm { get; }
        public AsyncReactiveCommand QueryPresetGetPersonInfoTerm { get; }
        // Query Manual
        public ReactivePropertySlim<string> QueryText { get; set; }
        public ReactivePropertySlim<bool> EnablePresetUpdateQueryText { get; set; }
        public AsyncReactiveCommand QueryManualExecute { get; }
        // QueryResult領域
        public ReactiveProperty<DataTable> QueryResult { get; }
        public ReactiveCommand<MouseButtonEventArgs> QueryDoubleClick { get; }
        enum QueryResultMode
        {
            Notify,
            TaskList,
            Other,
        }
        QueryResultMode queryResultDisp = QueryResultMode.Notify;
        DataTable dbNotify;
        // ダイアログ操作
        public ReactivePropertySlim<string> DialogMessage { get; set; }

        private QueryResultResource queryResultResource;
        private Config.IConfig config;
        private Repository.IRepository repository;

        public StackPanel dialog { get; set; } = null;

        public MainWindowViewModel(IContainerProvider diContainer, Config.IConfig config, Repository.IRepository repo)
        {
            this.config = config;
            this.repository = repo;

            // ResourceDictionary
            queryResultResource = new QueryResultResource();

            // Configロード
            config.Load();
            config.AddTo(Disposables);
            // GUI初期化
            // DBFilePath設定
            // DB接続/切断ボタン表示テキスト
            DBFileConnectText = new ReactivePropertySlim<string>("DB接続");
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
                        UpdateQueryResultNotify("DB切断しました");
                    }
                    else
                    {
                        DialogMessage.Value = "Connecting DB ...";
                        var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                        {
                            //Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: DBFileConnect/WithSubscribe START");
                            var connResult = await repository.Connect(config.DBFilePath.Value);
                            // Message通知
                            if (repository.IsConnect.Value)
                            {
                                bool r;
                                // タスクコードリスト取得
                                r = await repository.UpdateTaskCodeList();
                                if (r)
                                {
                                    UpdateQueryInfo();
                                }
                                // レコード日時最小最大を取得
                                // ここでは必ず設定する
                                // ReflectFilterQueryResultDate.Value
                                await UpdateDateRange();
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
            LogDirLoadText = new ReactivePropertySlim<string>("ログファイル取り込み");
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
                            bool r;
                            // タスクコードリスト更新
                            r = await repository.UpdateTaskCodeList();
                            if (r)
                            {
                                UpdateQueryInfo();
                            }
                            // 日時範囲更新
                            // レコード内日時範囲反映設定が有効であれば更新する
                            if (ReflectFilterQueryResultDate.Value)
                            {
                                await UpdateDateRange();
                            }
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
            FilterTaskCode = new ReactiveCollection<string>
            {
                "<指定なし>"
            };
            FilterTaskCode
                .AddTo(Disposables);
            FilterTaskCodeSelectItem = new ReactivePropertySlim<string>("");
            FilterTaskCodeSelectItem
                .AddTo(Disposables);
            FilterTaskCodeSelectIndex = new ReactivePropertySlim<int>(0);
            FilterTaskCodeSelectIndex
                .AddTo(Disposables);
            FilterTaskName = new ReactivePropertySlim<string>("");
            FilterTaskName
                .AddTo(Disposables);
            FilterTaskAlias = new ReactivePropertySlim<string>("");
            FilterTaskAlias
                .AddTo(Disposables);
            FilterTaskAliasId = new ReactivePropertySlim<string>("");
            FilterTaskAliasId
                .Subscribe(x =>
                {
                    MakeTaskAliasIdFilter(x);
                })
                .AddTo(Disposables);
            FilterToolTip = new ReactivePropertySlim<string>("*       任意の0文字以上の文字列\r\n?       任意の1文字\r\n[abc]  a or b or cのいずれかに一致\r\n[a-d]  aからdまでにいずれかに一致");
            FilterToolTip
                .AddTo(Disposables);
            // DBファイルパス
            ExcludeTaskCode = config.QueryExcludeTaskCode
                .ToReactivePropertySlimAsSynchronized(x => x.Value)
                .AddTo(Disposables);
            //QueryPresetGetTaskList = new AsyncReactiveCommand();
            QueryPresetGetTaskList = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            return await GetTaskList();
                        });
                        UpdateDbView(r, QueryResultMode.TaskList);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetSubTotal = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            var q = MakeQuerySelectSubTotal();
                            var qr = await ExecuteQuery(q);
                            if (qr)
                            {
                                // レコード内日時範囲反映設定が有効であれば更新する
                                if (ReflectFilterQueryResultDate.Value)
                                {
                                    await UpdateDateRange();
                                }
                            }
                            return qr;
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetItemTotal = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            var q = MakeQuerySelectItemTotal();
                            var qr = await ExecuteQuery(q);
                            if (qr)
                            {
                                // レコード内日時範囲反映設定が有効であれば更新する
                                if (ReflectFilterQueryResultDate.Value)
                                {
                                    await UpdateDateRange();
                                }
                            }
                            return qr;
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetPersonTotal = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            var q = MakeQuerySelectPersonTotal();
                            var qr = await ExecuteQuery(q);
                            if (qr)
                            {
                                // レコード内日時範囲反映設定が有効であれば更新する
                                if (ReflectFilterQueryResultDate.Value)
                                {
                                    await UpdateDateRange();
                                }
                            }
                            return qr;
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            ReflectFilterQueryResultDate = new ReactivePropertySlim<bool>(true);
            ReflectFilterQueryResultDate
                .AddTo(Disposables);
            FilterTermBegin = new ReactivePropertySlim<DateTime>(DateTime.Now);
            FilterTermBegin
                .AddTo(Disposables);
            FilterTermEnd = new ReactivePropertySlim<DateTime>(DateTime.Now);
            FilterTermEnd
                .AddTo(Disposables);
            FilterTermUnitSelectIndex = new ReactivePropertySlim<int>(1);
            FilterTermUnitSelectIndex
                .AddTo(Disposables);
            QueryPresetGetSubTotalTerm = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            var q = MakeQuerySelectSubTotalTerm();
                            return await ExecuteQuery(q);
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetItemTotalTerm = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            var q = MakeQuerySelectItemTotalTerm();
                            return await ExecuteQuery(q);
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetPersonTotalTerm = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            var q = MakeQuerySelectPersonTotalTerm();
                            return await ExecuteQuery(q);
                        });
                        UpdateDbView(r, QueryResultMode.Other);
                        args.Session.Close(false);
                    });
                })
                .AddTo(Disposables);
            QueryPresetGetPersonInfoTerm = repository.IsConnect
                .ToAsyncReactiveCommand()
                .WithSubscribe(async () =>
                {
                    DialogMessage.Value = "Query Executing ...";
                    var result = await DialogHost.Show(this.dialog, async delegate (object sender, DialogOpenedEventArgs args)
                    {
                        var r = await Task.Run(async () =>
                        {
                            var q = MakeQuerySelectPersonInfoTerm();
                            return await ExecuteQuery(q);
                        });
                        UpdateDbView(r, QueryResultMode.Other);
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
                            UpdateDbView(r, QueryResultMode.Other);
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
            QueryDoubleClick = new ReactiveCommand<MouseButtonEventArgs>();
            QueryDoubleClick
                .WithSubscribe(OnDoubleClickQueryResult)
                .AddTo(Disposables);
        }

        private void OnDoubleClickQueryResult(MouseButtonEventArgs e)
        {
            switch (queryResultDisp)
            {
                case QueryResultMode.TaskList:
                    OnDoubleClickQueryResultTaskList(e);
                    break;
            }
        }
        private void OnDoubleClickQueryResultTaskList(MouseButtonEventArgs e)
        {
            var elem = e.MouseDevice.DirectlyOver as FrameworkElement;
            if (elem != null)
            {
                DataGridCell cell = elem.Parent as DataGridCell;
                if (cell == null)
                {
                    // ParentでDataGridCellが拾えなかった時はTemplatedParentを参照
                    // （Borderをダブルクリックした時）
                    cell = elem.TemplatedParent as DataGridCell;
                }
                if (cell != null)
                {
                    // ここでcellの内容を処理
                    // （cell.DataContextにバインドされたものが入っているかと思います）
                    var row = ((DataRowView)cell.DataContext).Row;
                    //var rowidx = QueryResult.Value.Rows.IndexOf(row);
                    var colidx = cell.Column.DisplayIndex;
                    //MessageBox.Show($"({rowidx}, {cell.Column.DisplayIndex})");
                    var data = row.Field<string>(colidx);
                    // 選択した内容を転送
                    switch (colidx)
                    {
                        case 0:
                            // TaskCode
                            FilterTaskCodeSelectItem.Value = data;
                            break;
                        case 1:
                            // TaskName
                            FilterTaskName.Value = data;
                            break;
                        case 2:
                            // TaskAlias
                            FilterTaskAlias.Value = data;
                            break;
                        case 3:
                            // TaskAliasId
                            if (int.TryParse(data, out int val))
                            {
                                AddTaskAliasIdFilter(val);
                            }
                            break;
                    }
                }
            }
        }

        private void UpdateQueryResultNotify(string msg)
        {
            dbNotify.Rows[0][0] = msg;
            queryResultDisp = QueryResultMode.Notify;
            QueryResult.Value = dbNotify;
        }

        private void UpdateQueryInfo()
        {
            // フィルタ用タスクコードリスト更新
            FilterTaskCode.Clear();
            FilterTaskCode.Add("<指定なし>");
            foreach (var item in repository.TaskCodeList)
            {
                FilterTaskCode.Add(item.code);
            }
            FilterTaskCodeSelectIndex.Value = 0;
        }

        private async Task UpdateDateRange()
        {
            var filter = MakeQueryFilterTask();
            var query = SqlQuery.MakeQuerySelectDateRange(queryResultResource, filter);
            var dateResult = await repository.UpdateDateRange(query);
            if (dateResult)
            {
                // 集計用日時更新
                if (repository.DateRange.begin != 0 && repository.DateRange.end != 0)
                {
                    FilterTermBegin.Value = DateTime.FromBinary(repository.DateRange.begin);
                    FilterTermEnd.Value = DateTime.FromBinary(repository.DateRange.end);
                }
            }
        }

        private async Task<bool> GetTaskList()
        {
            var filter = MakeQueryFilterTask();
            var q = SqlQuery.MakeQuerySelectTaskList(queryResultResource, filter);
            return await ExecuteQuery(q);
        }

        static readonly Regex reTaskAliasIdList = new Regex(@"(\d+),?", RegexOptions.Compiled);
        private bool inAddTaskAliasIdFilter = false;
        private void MakeTaskAliasIdFilter(string text)
        {
            if (!inAddTaskAliasIdFilter)
            {
                FilterTaskAliasIdDict.Clear();
                // テキストチェック
                var comma = "";
                var str = new StringBuilder();
                var matches = reTaskAliasIdList.Matches(text);
                for (var i = 0; i < matches.Count; i++)
                {
                    var m = matches[i];
                    str.Append($"{comma}{m.Groups[1]}");
                    comma = ",";
                    if (int.TryParse(m.Groups[1].ToString(), out int val))
                    {
                        if (!FilterTaskAliasIdDict.ContainsKey(val))
                        {
                            FilterTaskAliasIdDict.Add(val, 1);
                        }
                    }
                }
                // GUI更新
                inAddTaskAliasIdFilter = true;
                FilterTaskAliasId.Value = str.ToString();
                inAddTaskAliasIdFilter = false;
            }
        }
        private void AddTaskAliasIdFilter(int id)
        {
            if (!FilterTaskAliasIdDict.ContainsKey(id))
            {
                // 新しいIDが指定された場合
                // 辞書に登録
                FilterTaskAliasIdDict.Add(id, 1);
                // フィルタテキスト更新
                var text = FilterTaskAliasId.Value;
                if (text.Length == 0)
                {
                    text = $"{id}";
                }
                else
                {
                    text = $"{text},{id}";
                }
                // GUI更新
                inAddTaskAliasIdFilter = true;
                FilterTaskAliasId.Value = text;
                inAddTaskAliasIdFilter = false;
            }
        }

        private QueryFilterTask MakeQueryFilterTask()
        {
            // タスクフィルタ作成
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                TaskAliasId = FilterTaskAliasId.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
                //ExcludeSubTaskCode = new List<string> { "CodeB", "CodeF" },
            };
            filter.Init();
            return filter;
        }

        private QueryFilterTerm MakeQueryFilterTerm()
        {
            // 期間フィルタ作成
            var term = new QueryFilterTerm
            {
                Begin = FilterTermBegin.Value,
                End = FilterTermEnd.Value,
                Unit = FilterTermUnitSelectIndex.Value
            };
            term.Init();
            return term;
        }



        private string MakeQuerySelectSubTotal()
        {
            // フィルタオブジェクト作成
            var filter = MakeQueryFilterTask();
            // ダミー期間定義
            var term = new QueryFilterTerm();
            return MakeQuerySelectSubTotalImpl(filter, term);
        }

        private string MakeQuerySelectSubTotalTerm()
        {
            var filter = MakeQueryFilterTask();
            var term = MakeQueryFilterTerm();
            return MakeQuerySelectSubTotalImpl(filter, term);
        }
        
        private string MakeQuerySelectSubTotalImpl(QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.EnableTasks)
            {
                query.AppendLine($@"  time_tbl.task_code AS '{queryResultResource.TaskCode}',");
                query.AppendLine($@"  time_tbl.task_name AS '{queryResultResource.TaskName}',");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine($@"  time_tbl.task_alias_name AS '{queryResultResource.TaskAlias}',");
            }
            // サブタスクコードカラム表示
            query.AppendLine($@"  time_tbl.subtask_code AS '{queryResultResource.SubTaskCode}',");
            // 期間集計指定ありのときはカラム定義を追加する
            if (term.IsActive)
            {
                // 高速化のためにStringBuilderを渡して追加する
                MakeQuerySelectSubTotalImpl_InsertTermColumns(query, term);
            }
            else
            {
                query.AppendLine(@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 ELSE time_tbl.time END) AS '工数(合計)'");
            }
            query.AppendLine(@"FROM (");
            query.AppendLine(@"  (");
            query.AppendLine(@"   subtasks");
            if (filter.EnableTasks)
            {
                query.AppendLine(@"   LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine(@"   LEFT OUTER JOIN task_aliases");
            }
            query.AppendLine(@"  )");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN work_times");
            query.AppendLine(@") AS time_tbl");
            // WHERE: 条件設定
            if (filter.IsActive)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeSubTaskCode)
                {
                    foreach (var subcode in filter.ExcludeSubTaskCode)
                    {
                        query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{subcode}'");
                        and = "AND ";
                    }
                }
                if (filter.EnableSubTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{filter.SubTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.task_code GLOB '{filter.ExcludeTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"  {and}time_tbl.task_code GLOB '{filter.TaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"  {and}time_tbl.task_name GLOB '{filter.TaskName}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_name GLOB '{filter.TaskAlias}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAliasId)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_id IN ({filter.TaskAliasId})");
                    and = "AND ";
                }
            }
            // GROUP BY: グループ定義
            query.AppendLine(@"GROUP BY");
            // エイリアス＞タスク名＞タスクコード　の順でGroup化する
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.subtask_code");
            query.AppendLine(@"ORDER BY");
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.subtask_id");
            query.Append(@";");
            return query.ToString();
        }

        private void MakeQuerySelectSubTotalImpl_InsertTermColumns(StringBuilder query, QueryFilterTerm term)
        {
            // カラム作成
            // 指定期間
            for (int i = 0; i < term.Terms.Count; i++)
            {
                var thre = term.Terms[i];
                query.AppendLine($@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 WHEN {thre.boundLo} <= time_tbl.date AND time_tbl.date < {thre.boundHi} THEN time_tbl.time ELSE 0 END) AS '工数({thre.date})',");
            }
            // 期間全体
            var boundLo = term.Terms.First().boundLo;
            var boundHi = term.Terms.Last().boundHi;
            query.AppendLine($@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 WHEN {boundLo} <= time_tbl.date AND time_tbl.date < {boundHi} THEN time_tbl.time ELSE 0 END) AS '工数(合計)'");
        }
        


        private string MakeQuerySelectItemTotal()
        {
            var filter = MakeQueryFilterTask();
            // ダミー期間定義
            var term = new QueryFilterTerm();
            return MakeQuerySelectItemTotalImpl(filter, term);
        }

        private string MakeQuerySelectItemTotalTerm()
        {
            var filter = MakeQueryFilterTask();
            var term = MakeQueryFilterTerm();
            return MakeQuerySelectItemTotalImpl(filter, term);
        }

        private string MakeQuerySelectItemTotalImpl(QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.EnableTasks)
            {
                query.AppendLine($@"  time_tbl.task_code AS '{queryResultResource.TaskCode}',");
                query.AppendLine($@"  time_tbl.task_name AS '{queryResultResource.TaskName}',");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine($@"  time_tbl.task_alias_name AS '{queryResultResource.TaskAlias}',");
            }
            // サブタスクコードカラム表示
            query.AppendLine($@"  time_tbl.subtask_code AS '{queryResultResource.SubTaskCode}',");
            query.AppendLine($@"  time_tbl.item_name AS '{queryResultResource.ItemName}',");
            // 期間集計指定ありのときはカラム定義を追加する
            if (term.IsActive)
            {
                // 高速化のためにStringBuilderを渡して追加する
                MakeQuerySelectSubTotalImpl_InsertTermColumns(query, term);
            }
            else
            {
                query.AppendLine(@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 ELSE time_tbl.time END) AS '工数(合計)'");
            }
            query.AppendLine(@"FROM (");
            query.AppendLine(@"  (");
            query.AppendLine(@"   SELECT task_id, task_alias_id, subtask_id, item_id, date, time FROM work_times");
            query.AppendLine(@"     UNION ALL");
            query.AppendLine(@"   SELECT DISTINCT task_id, task_alias_id, s.subtask_id, s.item_id, 0, 0");
            query.AppendLine(@"   FROM");
            query.AppendLine(@"     subtask_item_rel s");
            query.AppendLine(@"     LEFT OUTER JOIN ( SELECT subtask_id, subtask_alias_id, item_id, item_alias_id, task_id, task_alias_id FROM work_times ) AS w");
            query.AppendLine(@"  ) AS item_tbl");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN items NATURAL LEFT OUTER JOIN subtasks");
            if (filter.EnableTasks)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN task_aliases");
            }
            query.AppendLine(@"  ) AS time_tbl");
            // WHERE: 条件設定
            if (filter.IsActive)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeSubTaskCode)
                {
                    foreach (var subcode in filter.ExcludeSubTaskCode)
                    {
                        query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{subcode}'");
                        and = "AND ";
                    }
                }
                if (filter.EnableSubTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{filter.SubTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.task_code GLOB '{filter.ExcludeTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"  {and}time_tbl.task_code GLOB '{filter.TaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"  {and}time_tbl.task_name GLOB '{filter.TaskName}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_name GLOB '{filter.TaskAlias}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAliasId)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_id IN ({filter.TaskAliasId})");
                    and = "AND ";
                }
            }
            // GROUP BY: グループ定義
            query.AppendLine(@"GROUP BY");
            // エイリアス＞タスク名＞タスクコード　の順でGroup化する
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.subtask_code,");
            query.AppendLine(@"  time_tbl.item_name");
            /*
            query.AppendLine(@"ORDER BY");
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.subtask_id");
            */
            query.Append(@";");
            return query.ToString();
        }


        private string MakeQuerySelectPersonInfoTerm()
        {
            var filter = MakeQueryFilterTask();
            var term = MakeQueryFilterTerm();
            return MakeQuerySelectPersonInfoTermImpl(filter, term);
        }

        private string MakeQuerySelectPersonInfoTermImpl(QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            query.AppendLine($@"  person_name AS '{queryResultResource.Person}',");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.EnableTasks)
            {
                query.AppendLine($@"  task_code AS '{queryResultResource.TaskCode}',");
                query.AppendLine($@"  task_name AS '{queryResultResource.TaskName}',");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine($@"  task_alias_name AS '{queryResultResource.TaskAlias}',");
            }
            // ログ数情報
            // 基本的に期間指定ありき
            if (!term.IsActive)
            {
                // throw;
            }
            for (int i = 0; i < term.Terms.Count; i++)
            {
                var thre = term.Terms[i];
                var comma = i == term.Terms.Count - 1 ? string.Empty : ",";
                query.AppendLine($@"  Sum(CASE WHEN {thre.boundLo} <= date AND date < {thre.boundHi} THEN 1 ELSE 0 END) AS 'ログ数({thre.date})'{comma}");
            }
            query.AppendLine(@"FROM");
            query.AppendLine(@"  work_times");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN persons");
            if (filter.EnableSubTasks)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN subtasks");
            }
            if (filter.EnableTasks)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN task_aliases");
            }
            // WHERE: 条件設定
            if (filter.IsActive)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeSubTaskCode)
                {
                    foreach (var subcode in filter.ExcludeSubTaskCode)
                    {
                        query.AppendLine($@"  {and}NOT subtask_code GLOB '{subcode}'");
                        and = "AND ";
                    }
                }
                if (filter.EnableSubTaskCode)
                {
                    query.AppendLine($@"  {and}NOT subtask_code GLOB '{filter.SubTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  {and}NOT task_code GLOB '{filter.ExcludeTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"  {and}task_code GLOB '{filter.TaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"  {and}task_name GLOB '{filter.TaskName}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"  {and}task_alias_name GLOB '{filter.TaskAlias}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAliasId)
                {
                    query.AppendLine($@"  {and}task_alias_id IN ({filter.TaskAliasId})");
                    and = "AND ";
                }
            }
            // GROUP BY: グループ定義
            query.AppendLine(@"GROUP BY");
            // エイリアス＞タスク名＞タスクコード　の順でGroup化する
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  task_code,");
            }
            query.AppendLine(@"  person_id");
            query.AppendLine(@"ORDER BY");
            /*
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            */
            query.AppendLine(@"  person_id");
            query.Append(@";");
            return query.ToString();
        }

        private string MakeQuerySelectPersonTotal()
        {
            // フィルタオブジェクト作成
            var filter = MakeQueryFilterTask();
            // ダミー期間定義
            var term = new QueryFilterTerm();
            return MakeQuerySelectPersonTotalImpl(filter, term);
        }

        private string MakeQuerySelectPersonTotalTerm()
        {
            var filter = MakeQueryFilterTask();
            var term = MakeQueryFilterTerm();
            return MakeQuerySelectPersonTotalImpl(filter, term);
        }

        private string MakeQuerySelectPersonTotalImpl(QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.EnableTasks)
            {
                query.AppendLine($@"  time_tbl.task_code AS '{queryResultResource.TaskCode}',");
                query.AppendLine($@"  time_tbl.task_name AS '{queryResultResource.TaskName}',");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine($@"  time_tbl.task_alias_name AS '{queryResultResource.TaskAlias}',");
            }
            // サブタスクコードカラム表示
            query.AppendLine($@"  time_tbl.person_name AS '{queryResultResource.Person}',");
            // 期間集計指定ありのときはカラム定義を追加する
            if (term.IsActive)
            {
                // 高速化のためにStringBuilderを渡して追加する
                MakeQuerySelectSubTotalImpl_InsertTermColumns(query, term);
            }
            else
            {
                query.AppendLine(@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 ELSE time_tbl.time END) AS '工数(合計)'");
            }
            query.AppendLine(@"FROM (");
            query.AppendLine(@"  (");
            query.AppendLine(@"   persons");
            if (filter.EnableSubTasks)
            {
                query.AppendLine(@"   LEFT OUTER JOIN subtasks");
            }
            if (filter.EnableTasks)
            {
                query.AppendLine(@"   LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias || filter.EnableTaskAliasId)
            {
                query.AppendLine(@"   LEFT OUTER JOIN task_aliases");
            }
            query.AppendLine(@"  )");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN work_times");
            query.AppendLine(@") AS time_tbl");
            // WHERE: 条件設定
            if (filter.IsActive)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeSubTaskCode)
                {
                    foreach (var subcode in filter.ExcludeSubTaskCode)
                    {
                        query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{subcode}'");
                        and = "AND ";
                    }
                }
                if (filter.EnableSubTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.subtask_code GLOB '{filter.SubTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  {and}NOT time_tbl.task_code GLOB '{filter.ExcludeTaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"  {and}time_tbl.task_code GLOB '{filter.TaskCode}'");
                    and = "AND ";
                }
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"  {and}time_tbl.task_name GLOB '{filter.TaskName}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAlias)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_name GLOB '{filter.TaskAlias}'");
                    and = "AND ";
                }
                if (filter.EnableTaskAliasId)
                {
                    query.AppendLine($@"  {and}time_tbl.task_alias_id IN ({filter.TaskAliasId})");
                    and = "AND ";
                }
            }
            // GROUP BY: グループ定義
            query.AppendLine(@"GROUP BY");
            // エイリアス＞タスク名＞タスクコード　の順でGroup化する
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.person_name");
            query.AppendLine(@"ORDER BY");
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  time_tbl.task_alias_name,");
            }
            else if (filter.EnableTaskName)
            {
                query.AppendLine(@"  time_tbl.task_name,");
            }
            else if (filter.EnableTaskCode)
            {
                query.AppendLine(@"  time_tbl.task_code,");
            }
            query.AppendLine(@"  time_tbl.person_id");
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
        private void UpdateDbView(bool executeResult, QueryResultMode mode)
        {
            if (executeResult)
            {
                // 結果反映
                queryResultDisp = mode;
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
