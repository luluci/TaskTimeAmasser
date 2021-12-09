using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
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
            {
                ResourceDictionary dic = new ResourceDictionary
                {
                    Source = new Uri("/TaskTimeAmasser;component/Resources/GUIDictionary.xaml", UriKind.Relative)
                };
                queryResultResource = new QueryResultResource
                {
                    TaskCode = dic["GuiDispQueryResultTaskCode"].ToString(),
                    TaskName = dic["GuiDispQueryResultTaskName"].ToString(),
                    TaskAlias = dic["GuiDispQueryResultTaskAlias"].ToString(),
                    SubTaskCode = dic["GuiDispQueryResultSubTaskCode"].ToString(),
                    ItemName = dic["GuiDispQueryResultItemName"].ToString(),
                    Person = dic["GuiDispQueryResultPerson"].ToString(),
                };
            }

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
                                r = await repository.UpdateDateRange(MakeQuerySelectDateRange());
                                if (r)
                                {
                                    UpdateQueryDateRange();
                                }
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
                                r = await repository.UpdateDateRange(MakeQuerySelectDateRange());
                                if (r)
                                {
                                    UpdateQueryDateRange();
                                }
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
                            var q = MakeQuerySelectTaskList();
                            return await ExecuteQuery(q);
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
                                    var dateResult = await repository.UpdateDateRange(MakeQuerySelectDateRange());
                                    if (dateResult)
                                    {
                                        UpdateQueryDateRange();
                                    }
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
                                    var dateResult = await repository.UpdateDateRange(MakeQuerySelectDateRange());
                                    if (dateResult)
                                    {
                                        UpdateQueryDateRange();
                                    }
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
                                    var dateResult = await repository.UpdateDateRange(MakeQuerySelectDateRange());
                                    if (dateResult)
                                    {
                                        UpdateQueryDateRange();
                                    }
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

        private void UpdateQueryDateRange()
        {
            // 集計用日時更新
            if (repository.DateRange.begin != 0 && repository.DateRange.end != 0)
            {
                FilterTermBegin.Value = DateTime.FromBinary(repository.DateRange.begin);
                FilterTermEnd.Value = DateTime.FromBinary(repository.DateRange.end);
            }
        }

        private string MakeQuerySelectTaskList()
        {
            // フィルタ作成
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
            };
            filter.Init();
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT DISTINCT");
            query.AppendLine($@"  task_code AS '{queryResultResource.TaskCode}',");
            query.AppendLine($@"  task_name AS '{queryResultResource.TaskName}',");
            query.AppendLine($@"  task_alias_name AS '{queryResultResource.TaskAlias}'");
            query.AppendLine(@"FROM work_times");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN tasks");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN task_aliases");
            // WHERE: 条件設定
            if (filter.IsActive || filter.EnableExcludeTaskCode)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  NOT task_code GLOB '{filter.ExcludeTaskCode}'");
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
            }
            // ORDER BY: ソート
            query.AppendLine(@"ORDER BY");
            query.AppendLine(@"  task_code, task_name, task_alias_name");
            query.Append(@";");
            return query.ToString();
        }

        private string MakeQuerySelectDateRange()
        {
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
            };
            filter.Init();
            // クエリ作成
            if (!filter.IsActive)
            {
                return "SELECT max(w.date) AS MAX, min(w.date) AS MIN FROM work_times w;";
            }
            else
            {
                return MakeQuerySelectDateRangeFilter(filter);
            }
        }
        private string MakeQuerySelectDateRangeFilter(QueryFilterTask filter)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT max(time_tbl.date) AS MAX, min(time_tbl.date) AS MIN");
            query.AppendLine(@"FROM");
            query.AppendLine(@"  (SELECT w.date");
            query.AppendLine(@"   FROM");
            query.AppendLine(@"     work_times w");
            // タスクフィルターサブテーブル
            if (filter.EnableTasks)
            {
                // タスク名とタスクIDの両方をフィルターするか？
                var partAnd = "";
                if (filter.EnableTaskCode && filter.EnableTaskName)
                {
                    partAnd = " AND ";
                }
                query.AppendLine(@"     ,");
                query.AppendLine($@"     (SELECT t.task_id AS task_id FROM tasks t");
                query.AppendLine($@"      WHERE");
                if (filter.EnableTaskName)
                {
                    query.AppendLine($@"        t.task_name GLOB '{filter.TaskName}' {partAnd}");
                }
                if (filter.EnableTaskCode)
                {
                    query.AppendLine($@"        t.task_code GLOB '{filter.TaskCode}'");
                }
                query.AppendLine($@"     ) AS filter_task");
            }
            // タスクエイリアスフィルターサブテーブル
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"     ,");
                query.AppendLine($@"     (SELECT a.task_alias_id AS alias_id FROM task_aliases a WHERE a.task_alias_name GLOB '{filter.TaskAlias}') AS filter_alias");
            }
            query.AppendLine(@"   WHERE");
            if (filter.EnableTasks)
            {
                var partAnd = "";
                if (filter.EnableTaskAlias)
                {
                    partAnd = "AND";
                }
                query.AppendLine($@"     w.task_id = filter_task.task_id {partAnd}");
            }
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"     w.task_alias_id = filter_alias.alias_id");
            }
            query.AppendLine(@"  ) AS time_tbl");
            query.Append(@";");
            return query.ToString();
        }



        private string MakeQuerySelectSubTotal()
        {
            // フィルタオブジェクト作成
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
            };
            filter.Init();
            // ダミー期間定義
            var term = new QueryFilterTerm();
            return MakeQuerySelectSubTotalImpl(filter, term);
        }

        private string MakeQuerySelectSubTotalTerm()
        {
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
            };
            filter.Init();
            var term = new QueryFilterTerm
            {
                Begin = FilterTermBegin.Value,
                End = FilterTermEnd.Value,
                Unit = FilterTermUnitSelectIndex.Value
            };
            term.Init();
            return MakeQuerySelectSubTotalImpl(filter, term);
        }
        
        private string MakeQuerySelectSubTotalImpl(QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.IsActive)
            {
                query.AppendLine($@"  time_tbl.task_code AS '{queryResultResource.TaskCode}',");
                query.AppendLine($@"  time_tbl.task_name AS '{queryResultResource.TaskName}',");
            }
            if (filter.EnableTaskAlias)
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
            query.AppendLine(@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 ELSE time_tbl.time END) AS '工数(合計)'");
            query.AppendLine(@"FROM (");
            query.AppendLine(@"  (");
            query.AppendLine(@"   subtasks");
            if (filter.IsActive || filter.EnableExcludeTaskCode)
            {
                query.AppendLine(@"   LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"   LEFT OUTER JOIN task_aliases");
            }
            query.AppendLine(@"  )");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN work_times");
            query.AppendLine(@") AS time_tbl");
            // WHERE: 条件設定
            if (filter.IsActive || filter.EnableExcludeTaskCode)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  NOT time_tbl.task_code GLOB '{filter.ExcludeTaskCode}'");
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
            for (int i = 0; i < term.Terms.Count; i++)
            {
                var thre = term.Terms[i];
                query.AppendLine($@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 WHEN {thre.boundLo} <= time_tbl.date AND time_tbl.date < {thre.boundHi} THEN time_tbl.time ELSE 0 END) AS '工数({thre.date})',");
            }
        }
        


        private string MakeQuerySelectItemTotal()
        {
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
            };
            filter.Init();
            // ダミー期間定義
            var term = new QueryFilterTerm();
            return MakeQuerySelectItemTotalImpl(filter, term);
        }

        private string MakeQuerySelectItemTotalTerm()
        {
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
            };
            filter.Init();
            var term = new QueryFilterTerm
            {
                Begin = FilterTermBegin.Value,
                End = FilterTermEnd.Value,
                Unit = FilterTermUnitSelectIndex.Value
            };
            term.Init();
            return MakeQuerySelectItemTotalImpl(filter, term);
        }

        private string MakeQuerySelectItemTotalImpl(QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.IsActive)
            {
                query.AppendLine($@"  time_tbl.task_code AS '{queryResultResource.TaskCode}',");
                query.AppendLine($@"  time_tbl.task_name AS '{queryResultResource.TaskName}',");
            }
            if (filter.EnableTaskAlias)
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
            query.AppendLine(@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 ELSE time_tbl.time END) AS '工数(合計)'");
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
            if (filter.IsActive || filter.EnableExcludeTaskCode)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN task_aliases");
            }
            query.AppendLine(@"  ) AS time_tbl");
            // WHERE: 条件設定
            if (filter.IsActive || filter.EnableExcludeTaskCode)
            {
                query.AppendLine(@"WHERE");
                var and = "";
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  NOT time_tbl.task_code GLOB '{filter.ExcludeTaskCode}'");
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
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
            };
            filter.Init();
            var term = new QueryFilterTerm
            {
                Begin = FilterTermBegin.Value,
                End = FilterTermEnd.Value,
                Unit = FilterTermUnitSelectIndex.Value
            };
            term.Init();
            return MakeQuerySelectPersonInfoTermImpl(filter, term);
        }

        private string MakeQuerySelectPersonInfoTermImpl(QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            query.AppendLine($@"  person_name AS '{queryResultResource.Person}',");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.IsActive)
            {
                query.AppendLine($@"  task_code AS '{queryResultResource.TaskCode}',");
                query.AppendLine($@"  task_name AS '{queryResultResource.TaskName}',");
            }
            if (filter.EnableTaskAlias)
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
            if (filter.IsActive || filter.EnableExcludeTaskCode)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"  NATURAL LEFT OUTER JOIN task_aliases");
            }
            // WHERE: 条件設定
            if (filter.IsActive || filter.EnableExcludeTaskCode)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  NOT task_code GLOB '{filter.ExcludeTaskCode}'");
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
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
            };
            filter.Init();
            // ダミー期間定義
            var term = new QueryFilterTerm();
            return MakeQuerySelectPersonTotalImpl(filter, term);
        }

        private string MakeQuerySelectPersonTotalTerm()
        {
            var filter = new QueryFilterTask
            {
                TaskCode = FilterTaskCodeSelectItem.Value,
                TaskName = FilterTaskName.Value,
                TaskAlias = FilterTaskAlias.Value,
                ExcludeTaskCode = config.QueryExcludeTaskCode.Value,
            };
            filter.Init();
            var term = new QueryFilterTerm
            {
                Begin = FilterTermBegin.Value,
                End = FilterTermEnd.Value,
                Unit = FilterTermUnitSelectIndex.Value
            };
            term.Init();
            return MakeQuerySelectPersonTotalImpl(filter, term);
        }

        private string MakeQuerySelectPersonTotalImpl(QueryFilterTask filter, QueryFilterTerm term)
        {
            // クエリ作成
            var query = new StringBuilder();
            query.AppendLine(@"SELECT");
            // フィルタをかけるときはタスク情報も表示する
            if (filter.IsActive)
            {
                query.AppendLine($@"  time_tbl.task_code AS '{queryResultResource.TaskCode}',");
                query.AppendLine($@"  time_tbl.task_name AS '{queryResultResource.TaskName}',");
            }
            if (filter.EnableTaskAlias)
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
            query.AppendLine(@"  Sum(CASE WHEN time_tbl.time IS NULL THEN 0 ELSE time_tbl.time END) AS '工数(合計)'");
            query.AppendLine(@"FROM (");
            query.AppendLine(@"  (");
            query.AppendLine(@"   persons");
            if (filter.IsActive || filter.EnableExcludeTaskCode)
            {
                query.AppendLine(@"   LEFT OUTER JOIN tasks");
            }
            if (filter.EnableTaskAlias)
            {
                query.AppendLine(@"   LEFT OUTER JOIN task_aliases");
            }
            query.AppendLine(@"  )");
            query.AppendLine(@"  NATURAL LEFT OUTER JOIN work_times");
            query.AppendLine(@") AS time_tbl");
            // WHERE: 条件設定
            if (filter.IsActive || filter.EnableExcludeTaskCode)
            {
                var and = "";
                query.AppendLine(@"WHERE");
                if (filter.EnableExcludeTaskCode)
                {
                    query.AppendLine($@"  NOT time_tbl.task_code GLOB '{filter.ExcludeTaskCode}'");
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

    class QueryResultResource
    {
        public string TaskCode { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public string TaskAlias { get; set; } = string.Empty;
        public string SubTaskCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Person { get; set; } = string.Empty;
    }
    
    class QueryFilterTask
    {
        public string TaskCode { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public string TaskAlias { get; set; } = string.Empty;
        public string ExcludeTaskCode { get; set; } = string.Empty;

        public bool IsActive { get; set; } = false;
        public bool EnableTasks { get; set; } = false;
        public bool EnableTaskCode { get; set; } = false;
        public bool EnableTaskName { get; set; } = false;
        public bool EnableTaskAlias { get; set; } = false;
        public bool EnableExcludeTaskCode { get; set; } = false;

        public QueryFilterTask() { }

        public void Init()
        {
            EnableTaskCode = TaskCode != "<指定なし>";
            EnableTaskName = TaskName.Length != 0;
            EnableTaskAlias = TaskAlias.Length != 0;
            EnableTasks = (EnableTaskCode || EnableTaskName);
            //
            EnableExcludeTaskCode = ExcludeTaskCode.Length != 0;
            //
            IsActive = (EnableTaskCode || EnableTaskName || EnableTaskAlias);
        }

    }

    class QueryFilterTerm
    {
        public DateTime Begin { get; set; }
        public DateTime End { get; set; }
        public int Unit { get; set; }

        public List<(long boundLo, long boundHi, string date)> Terms { get; set; } = new List<(long boundLo, long boundHi, string date)>();

        public QueryFilterTerm()
        {
            IsActive = false;
        }

        public bool IsActive { get; set; } = false;

        public void Init()
        {
            // 前後関係チェック
            if (Begin > End)
            {
                var temp = Begin;
                Begin = End;
                End = temp;
            }
            MakeTerm();
            // 有効化
            IsActive = true;
        }

        private void MakeTerm()
        {
            switch (Unit)
            {
                case 0:
                    // 日単位
                    MakeTermDay();
                    break;
                case 1:
                    // 月単位
                    MakeTermMonth();
                    break;
                case 2:
                    // 年単位
                    MakeTermYear();
                    break;
                default:
                    Terms.Clear();
                    break;
            }
        }

        private void MakeTermDay()
        {
            // (Lo, Hi]の判定とする
            // Beginの日付(Beginの0時0分になってるはず)を初期境界値下限にセット
            var boundCurr = Begin.Date;
            // Endの日付まで含めるものとする
            // 日時の差分をバッファサイズとする
            var span = End.Date - boundCurr;
            Terms.Clear();
            Terms.Capacity = span.Days + 1;
            // 1日分の差分を境界値として登録
            for (int i = 0; i < Terms.Capacity; i++)
            {
                var boundNext = boundCurr.AddDays(1);
                Terms.Add((boundCurr.ToBinary(), boundNext.ToBinary(), boundCurr.ToString("yyyy/MM/dd")));
                boundCurr = boundNext;
            }
        }

        private void MakeTermMonth()
        {
            // (Lo, Hi]の判定とする
            // Beginの日付(Beginの0時0分になってるはず)を初期境界値下限にセット
            var boundCurr = new DateTime(Begin.Year, Begin.Month, 1);
            // Endの日付まで含めるものとする
            var boundEnd = new DateTime(End.Year, End.Month, 1);
            // 日時の差分をバッファサイズとする
            var span = (boundEnd.Month - boundCurr.Month) + (12 * (boundEnd.Year - boundCurr.Year));
            Terms.Clear();
            Terms.Capacity = span + 1;
            // 1日分の差分を境界値として登録
            for (int i = 0; i < Terms.Capacity; i++)
            {
                var boundNext = boundCurr.AddMonths(1);
                Terms.Add((boundCurr.ToBinary(), boundNext.ToBinary(), boundCurr.ToString("yyyy/MM")));
                boundCurr = boundNext;
            }
        }

        private void MakeTermYear()
        {
            // (Lo, Hi]の判定とする
            // Beginの日付(Beginの0時0分になってるはず)を初期境界値下限にセット
            var boundCurr = new DateTime(Begin.Year, 1, 1);
            // Endの日付まで含めるものとする
            var boundEnd = new DateTime(End.Year, 1, 1);
            // 日時の差分をバッファサイズとする
            var span = boundEnd.Year - boundCurr.Year;
            Terms.Clear();
            Terms.Capacity = span + 1;
            // 1日分の差分を境界値として登録
            for (int i = 0; i < Terms.Capacity; i++)
            {
                var boundNext = boundCurr.AddYears(1);
                Terms.Add((boundCurr.ToBinary(), boundNext.ToBinary(), boundCurr.ToString("yyyy")));
                boundCurr = boundNext;
            }
        }
    }
}
